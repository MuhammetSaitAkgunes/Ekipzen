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

    // CORS — allow the local frontend origin during development.
    const string frontendCors = "frontend";
    builder.Services.AddCors(options => options.AddPolicy(frontendCors, policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();

    // Development'ta migration + demo seed otomatik uygulanır (docker-compose MSSQL).
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();

        // Önce platform: migrate et ve varsayılan kiracıyı garanti et. Kiracı kimliği
        // bir sonraki adımın ÖNKOŞULU — seed'in hangi kiracıya yazacağı, seed
        // başlamadan bilinmeli.
        var platformInitializer = scope.ServiceProvider
            .GetRequiredService<IKPro.Infrastructure.Persistence.PlatformDbInitializer>();
        await platformInitializer.InitialiseAsync();
        var varsayilanKiraciId = await platformInitializer.EnsureDefaultTenantAsync();

        // Uygulama initializer'ı KİRACIYA SABİTLENMİŞ bir kapsamdan çözülür. Kök
        // kapsamdan çözüp içeride Impersonate çağırmak işe yaramaz: AppDbContext'in
        // bağlantısı, initializer DI tarafından kurulurken çoktan belirlenmiş olur —
        // yani kiracı başına veritabanına geçildiğinde seed sessizce YANLIŞ
        // veritabanına yazardı. Kapsam fabrikası kiracıyı kapsam dönmeden önce
        // sabitler; bu sırayı yanlış yapmak yapısal olarak imkânsızdır.
        using var kiraciKapsami = scope.ServiceProvider
            .GetRequiredService<IKPro.Application.Common.Interfaces.ITenantScopeFactory>()
            .Create(varsayilanKiraciId);
        var initializer = kiraciKapsami.Services
            .GetRequiredService<IKPro.Infrastructure.Persistence.AppDbContextInitializer>();
        await initializer.InitialiseAsync();
        await initializer.SeedAsync();
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
