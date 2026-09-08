using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography.X509Certificates;

namespace IKPro.API.Hosting;

public static class PilotHosting
{
    public const string CorsPolicy = "frontend";

    public static void LoadExternalConfiguration(WebApplicationBuilder builder, string[] args)
    {
        var path = Environment.GetEnvironmentVariable("IKPRO_CONFIG_FILE");
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!ProductionConfiguration.IsAbsoluteNonRoot(path)
            || ProductionConfiguration.IsWithin(path, builder.Environment.ContentRootPath))
            throw new InvalidOperationException("IKPRO_CONFIG_FILE yayın dizini dışında mutlak dosya yolu olmalıdır.");
        // Sır dosyası publish paketine girmez. Ortam/CLI override önceliği korunur.
        builder.Configuration.AddJsonFile(path, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables().AddCommandLine(args);
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        if (!builder.Environment.IsDevelopment())
        {
            var errors = ProductionConfiguration.GetErrors(config, builder.Environment.ContentRootPath);
            if (errors.Count > 0) throw new InvalidOperationException("Üretim yapılandırması reddedildi: " + string.Join(" ", errors));

            var keys = new DirectoryInfo(config["DataProtection:KeysPath"]!);
            if (!keys.Exists || !Directory.Exists(config["Storage:Root"])
                || !Directory.Exists(Path.GetDirectoryName(config["Logging:FilePath"]!)))
                throw new InvalidOperationException("Kalıcı storage, log ve DataProtection dizinlerini kısıtlı ACL ile önceden oluşturun.");
            // Dosya tabanlı, şifreli PFX; makineye bağlı DPAPI yerine ayrı ortamda restore edilebilir.
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                config["DataProtection:CertificatePath"]!, config["DataProtection:CertificatePassword"],
                X509KeyStorageFlags.EphemeralKeySet);
            using var publicKey = certificate.GetRSAPublicKey();
            if (!certificate.HasPrivateKey || publicKey is null || publicKey.KeySize < 2048 || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
                || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
            {
                certificate.Dispose();
                throw new InvalidOperationException("DataProtection sertifikası geçerli süreli, özel anahtarlı ve en az 2048 bit RSA olmalıdır.");
            }
            builder.Services.AddSingleton(_ => certificate);
            builder.Services.AddDataProtection().SetApplicationName("IKPro")
                .PersistKeysToFileSystem(keys).ProtectKeysWithCertificate(certificate);
            builder.Services.PostConfigure<IdentityOptions>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;
            });
        }
        AddWebSecurity(builder.Services, config, builder.Environment.IsDevelopment());
    }

    public static void AddWebSecurity(IServiceCollection services, IConfiguration config, bool development)
    {
        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? (development ? ["http://localhost:5173", "http://127.0.0.1:5173"] : []);
        services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
        {
            if (origins.Length > 0) policy.WithOrigins(origins);
            policy.AllowAnyHeader().AllowAnyMethod();
        }));
        services.AddHttpsRedirection(options =>
        {
            if (!development) options.HttpsPort = 443;
            options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        });
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(30);
            options.IncludeSubDomains = false;
            options.Preload = false;
        });
    }

    public static void UseWebSecurity(WebApplication app)
    {
        var production = !app.Environment.IsDevelopment();
        if (production) app.UseHsts();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (production)
                context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; "
                    + "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; "
                    + "font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com data:; "
                    + "img-src 'self' blob: data:; connect-src 'self'; frame-ancestors 'none'; "
                    + "object-src 'none'; base-uri 'self'; form-action 'self'";
            if (context.Request.Path.StartsWithSegments("/api"))
                context.Response.Headers.CacheControl = "no-store";
            await next();
        });
        app.UseHttpsRedirection();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseCors(CorsPolicy);
        // IIS in-process şema/IP'yi doğrudan sağlar. Keyfî X-Forwarded-* başlıklarına güvenilmez.
    }
}
