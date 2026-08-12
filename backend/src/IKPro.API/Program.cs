using IKPro.API.Middleware;
using IKPro.API.Services;
using IKPro.Application;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Common.Security;
using IKPro.Domain.Constants;
using IKPro.Infrastructure;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Threading.RateLimiting;

// Bootstrap logger — captures failures during startup before the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog reads its full configuration from appsettings.json.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Application (MediatR + FluentValidation + Mapster) ve
    // Infrastructure (EF Core + MSSQL + audit interceptor + Identity/JWT).
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Güvenlik sertleştirme: üretimde dev placeholder sır kabul edilmez — güçlü bir
    // Jwt:Secret ortam değişkeninden gelmelidir. (DI ayrıca uzunluk kontrolü yapar.)
    if (!builder.Environment.IsDevelopment())
    {
        var jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
        if (jwtSecret.Contains("do-not-use-in-prod", StringComparison.OrdinalIgnoreCase) || jwtSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "Üretimde Jwt:Secret güçlü bir değerle (ortam değişkeni/secret store) sağlanmalı; dev placeholder reddedilir.");
        }
    }

    // Rate limiting: kimlik/provizyon uçlarında brute-force/kötüye kullanımı sınırlar.
    // Limit yapılandırmadan gelir; testler yüksek bir değerle bunu etkisiz kılar.
    var authPermitPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 30);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        // "signup" politikası KALDIRILDI: tek kullanıcısı olan self-servis kayıt ucu
        // kaldırıldı. Kiracı açan tek uç (POST /api/tenants) platform anahtarı istediği
        // için anonim kötüye kullanıma açık değil; onu "auth" politikası kapsar.
        // RateLimiting:SignupPermitPerHour ayarı artık okunmuyor.
    });

    // İstek bağlamındaki kullanıcı (audit + yetki kapsamı).
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();

    // Rol policy'leri — frontend routes.js roles[] matrisiyle birebir.
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(Policies.HrAdminOnly, policy => policy.RequireRole(Roles.HrAdmin));
        options.AddPolicy(Policies.Management, policy => policy.RequireRole(Roles.HrAdmin, Roles.Manager));
        options.AddPolicy(Policies.PayrollAccess, policy => policy.RequireRole(Roles.HrAdmin, Roles.Employee));
    });

    // MVC controllers + JSON.
    builder.Services.AddControllers();

    // RFC 7807 ProblemDetails for consistent error envelopes.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // OpenAPI / Swagger with a JWT bearer scheme so later phases can authorize from the UI.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "İK Pro API",
            Version = "v1",
            Description =
                "İK Pro insan kaynakları platformu backend API'si. " +
                "Roller: hr-admin / manager / employee (policy'ler routes.js matrisiyle birebir). " +
                "Demo giriş: ik@hrmaster.local / demo123.",
        });

        // Controller XML yorumları uç açıklaması olarak gösterilir (Faz 12 cilası).
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "IKPro.API.xml");
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        var jwtScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT token'ını 'Bearer {token}' formatında girin.",
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        };
        options.AddSecurityDefinition("Bearer", jwtScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [jwtScheme] = Array.Empty<string>()
        });
    });

    // Health checks (self + iki veritabanı bağlantısı). Yalnız AppDbContext kontrol edilirse
    // platform DB (IKProPlatform — Tenants/TenantDirectoryEntries) düşükken /health yine de
    // yeşil kalır ama hiç kimse giriş yapamaz (LoginAsync kiracı durumunu platform DB'sinden
    // okur). İkisi de kontrol edilmeli.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<IKPro.Infrastructure.Persistence.AppDbContext>("database")
        .AddDbContextCheck<IKPro.Infrastructure.Persistence.PlatformDbContext>("platform");

    // Doğrulanmamış kiracıların uygulama-içi periyodik temizliği (varsayılan kapalı; opt-in).
    builder.Services.Configure<UnverifiedTenantCleanupOptions>(
        builder.Configuration.GetSection(UnverifiedTenantCleanupOptions.SectionName));
    builder.Services.AddHostedService<UnverifiedTenantCleanupService>();

    // Açılışta kiracı şemalarını KONTROL eder (migrate etmez) ve geride kalanı
    // yüksek sesle loglar. Migration ayrı bir dağıtım adımıdır:
    // POST /api/tenants/migrate. Bkz. TenantSchemaCheckService xmldoc.
    builder.Services.AddHostedService<TenantSchemaCheckService>();

    // CORS — allow the local frontend origin during development.
    const string frontendCors = "frontend";
    builder.Services.AddCors(options => options.AddPolicy(frontendCors, policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();

    // Development'ta migration + demo seed otomatik uygulanır (docker-compose MSSQL).
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();

        // 1) Platform veritabanı: kiracı kimliğinin yaşadığı yer, her şeyin önkoşulu.
        var platformInitializer = scope.ServiceProvider
            .GetRequiredService<IKPro.Infrastructure.Persistence.PlatformDbInitializer>();
        await platformInitializer.InitialiseAsync();

        // 2) PAYLAŞILAN IKProDb'yi migrate et. Kiracılar artık kendi kataloglarında
        //    yaşıyor, ama bu katalog hâlâ gerekli: kiracı bağlamı OLMAYAN her şey
        //    (sağlık kontrolü, henüz ayrılmamış kiracılar) buraya bağlanıyor.
        //    İçi boş kalabilir — var olması ve şemasının güncel olması yeterli.
        var paylasilan = scope.ServiceProvider
            .GetRequiredService<IKPro.Infrastructure.Persistence.AppDbContextInitializer>();
        await paylasilan.InitialiseAsync();

        // 3) Demo kiracıları GERÇEK PROVİZYON YOLUNDAN kur (kendi veritabanlarıyla)
        //    ve içlerini doldur. Seed'in kendi kestirmesi yok: kayıt akışı böylece
        //    her geliştirme kurulumunda baştan sona çalıştırılmış olur.
        var demoSeeder = scope.ServiceProvider
            .GetRequiredService<IKPro.Infrastructure.Persistence.DemoTenantSeeder>();
        await demoSeeder.SeedAsync();
    }

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "İK Pro API v1");
            options.DocumentTitle = "İK Pro API";
        });
    }

    app.UseHttpsRedirection();
    app.UseCors(frontendCors);
    app.UseRateLimiter();
    app.UseAuthentication();
    // Erişim kapısının HTTP ayağı — kimlik doğrulamadan SONRA (claim okunabilir
    // olmalı), yetkilendirmeden ÖNCE (dondurulmuş kiracıyı rol kontrolünden önce
    // kesmek gereksiz iş yapmaz). Bkz. TenantAccessMiddleware xmldoc.
    app.UseMiddleware<TenantAccessMiddleware>();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "İK Pro API başlatılırken kritik hata oluştu.");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
