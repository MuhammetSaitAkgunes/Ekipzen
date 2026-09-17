using Ekipzen.API.Hosting;
using Ekipzen.API.Middleware;
using Ekipzen.API.Services;
using Ekipzen.Application;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Common.Security;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Enums;
using Ekipzen.Infrastructure;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

// Bootstrap logger — captures failures during startup before the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    PilotHosting.LoadExternalConfiguration(builder, args);
    PilotHosting.Configure(builder);
    if (builder.Configuration.GetValue("verify-production", false))
    {
        if (builder.Environment.IsDevelopment()) throw new InvalidOperationException("Kontrol Production ortamında çalıştırılmalıdır.");
        Console.WriteLine("Üretim yapılandırması ve DataProtection sertifikası doğrulandı. SQL/SMTP/IIS bağlantısı veya migration yapılmadı.");
        return;
    }

    // Serilog reads its full configuration from appsettings.json.
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration.ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services).Enrich.FromLogContext();
        if (!context.HostingEnvironment.IsDevelopment())
            configuration.WriteTo.File(context.Configuration["Logging:FilePath"]!,
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true);
    });

    // Application (MediatR + FluentValidation + Mapster) ve
    // Infrastructure (EF Core + MSSQL + audit interceptor + Identity/JWT).
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Rate limiting: kimlik/provizyon uçlarında brute-force/kötüye kullanımı sınırlar.
    // Limit yapılandırmadan gelir; testler yüksek bir değerle bunu etkisiz kılar.
    var authPermitPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 30);
    var publicFormPermitPerHour = builder.Configuration.GetValue("RateLimiting:PublicFormPermitPerHour", 5);
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

        // Tanıtım sitesi formları: anonim, e-posta üretir → IP başına saatlik sınır.
        options.AddPolicy("public-form", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = publicFormPermitPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }));

        // "signup" politikası KALDIRILDI: tek kullanıcısı olan self-servis kayıt ucu
        // kaldırıldı. Kiracı açan tek uç (POST /api/tenants) platform anahtarı istediği
        // için anonim kötüye kullanıma açık değil; onu "auth" politikası kapsar.
        // RateLimiting:SignupPermitPerHour ayarı artık okunmuyor.
    });

    // Landing sayfasındaki demo talep formunun bildirim adresi. Application katmanında
    // yapılandırma soyutlaması olmadığı için singleton olarak burada kaydedilir.
    builder.Services.AddSingleton(new Ekipzen.Application.Features.DemoRequests.DemoRequestSettings(
        builder.Configuration["DemoRequests:NotifyEmail"] ?? ""));

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
    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            // Bordro sözleşmesi enumları okunabilir, camelCase değerlerle taşınır.
            // Mevcut modüllerin sayısal enum sözleşmesini değiştirmemek için kapsam dar tutulur.
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter<PayrollStatutoryProfile>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter<PayrollMealTreatment>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter<EmployerSgkDiscountType>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter<PayrollDisabilityReliefType>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter<PayrollSettingsStatus>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        });

    // RFC 7807 ProblemDetails for consistent error envelopes.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // OpenAPI / Swagger with a JWT bearer scheme so later phases can authorize from the UI.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Ekipzen API",
            Version = "v1",
            Description =
                "Ekipzen insan kaynakları platformu backend API'si. " +
                "Roller: hr-admin / manager / employee (policy'ler routes.js matrisiyle birebir). " +
                "Demo giriş: ik@hrmaster.local / demo123.",
        });

        // Controller XML yorumları uç açıklaması olarak gösterilir (Faz 12 cilası).
        var xmlPath = Path.Combine(AppContext.BaseDirectory, "Ekipzen.API.xml");
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
    // platform DB (EkipzenPlatform — Tenants/TenantDirectoryEntries) düşükken /health yine de
    // yeşil kalır ama hiç kimse giriş yapamaz (LoginAsync kiracı durumunu platform DB'sinden
    // okur). İkisi de kontrol edilmeli.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<Ekipzen.Infrastructure.Persistence.AppDbContext>("database")
        .AddDbContextCheck<Ekipzen.Infrastructure.Persistence.PlatformDbContext>("platform");

    // Doğrulanmamış kiracıların uygulama-içi periyodik temizliği (varsayılan kapalı; opt-in).
    builder.Services.Configure<UnverifiedTenantCleanupOptions>(
        builder.Configuration.GetSection(UnverifiedTenantCleanupOptions.SectionName));
    builder.Services.AddHostedService<UnverifiedTenantCleanupService>();

    // Tenant iş verisiyle atomik kaydedilen login/rol provizyonlarını dış servis
    // hatalarından sonra otomatik ve tenant-izole biçimde yeniden dener.
    builder.Services.Configure<IdentityProvisioningRetryOptions>(
        builder.Configuration.GetSection(IdentityProvisioningRetryOptions.SectionName));
    builder.Services.AddSingleton<IdentityProvisioningRetryService>();
    builder.Services.AddHostedService(provider =>
        provider.GetRequiredService<IdentityProvisioningRetryService>());

    // Açılışta kiracı şemalarını KONTROL eder (migrate etmez) ve geride kalanı
    // yüksek sesle loglar. Migration ayrı bir dağıtım adımıdır:
    // POST /api/tenants/migrate. Bkz. TenantSchemaCheckService xmldoc.
    builder.Services.AddHostedService<TenantSchemaCheckService>();

    var app = builder.Build();

    // Development'ta migration + demo seed otomatik uygulanır (docker-compose MSSQL).
    if (app.Environment.IsDevelopment()
        && builder.Configuration.GetValue("Seed:Enabled", true))
    {
        using var scope = app.Services.CreateScope();

        // 1) Platform veritabanı: kiracı kimliğinin yaşadığı yer, her şeyin önkoşulu.
        var platformInitializer = scope.ServiceProvider
            .GetRequiredService<Ekipzen.Infrastructure.Persistence.PlatformDbInitializer>();
        await platformInitializer.InitialiseAsync();

        // 2) PAYLAŞILAN EkipzenDb'yi migrate et. Kiracılar artık kendi kataloglarında
        //    yaşıyor, ama bu katalog hâlâ gerekli: kiracı bağlamı OLMAYAN her şey
        //    (sağlık kontrolü, henüz ayrılmamış kiracılar) buraya bağlanıyor.
        //    İçi boş kalabilir — var olması ve şemasının güncel olması yeterli.
        var paylasilan = scope.ServiceProvider
            .GetRequiredService<Ekipzen.Infrastructure.Persistence.AppDbContextInitializer>();
        await paylasilan.InitialiseAsync();

        // 3) Demo kiracıları GERÇEK PROVİZYON YOLUNDAN kur (kendi veritabanlarıyla)
        //    ve içlerini doldur. Seed'in kendi kestirmesi yok: kayıt akışı böylece
        //    her geliştirme kurulumunda baştan sona çalıştırılmış olur.
        var demoSeeder = scope.ServiceProvider
            .GetRequiredService<Ekipzen.Infrastructure.Persistence.DemoTenantSeeder>();
        await demoSeeder.SeedAsync();
    }

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Ekipzen API v1");
            options.DocumentTitle = "Ekipzen API";
        });
    }

    PilotHosting.UseWebSecurity(app);
    app.UseRateLimiter();
    app.UseAuthentication();
    // Erişim kapısının HTTP ayağı — kimlik doğrulamadan SONRA (claim okunabilir
    // olmalı), yetkilendirmeden ÖNCE (dondurulmuş kiracıyı rol kontrolünden önce
    // kesmek gereksiz iş yapmaz). Bkz. TenantAccessMiddleware xmldoc.
    app.UseMiddleware<TenantAccessMiddleware>();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");
    PilotHosting.MapSpaFallback(app);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Ekipzen API başlatılırken kritik hata oluştu.");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
