using FluentAssertions;
using Ekipzen.API.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Ekipzen.Tests.Integration.Hosting;

// Gerçek SQL/SMTP'ye bağlanmaz. Üretim yapılandırması ve HTTP hattı ayrı test host'unda denenir.
public sealed class ProductionHostingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ekipzen-hosting-tests", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> config;
    private string Site => Path.Combine(root, "site");

    public ProductionHostingTests()
    {
        foreach (var name in new[] { "site/wwwroot", "keys", "storage", "logs" }) Directory.CreateDirectory(Path.Combine(root, name));
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Ekipzen test only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3));
        var certificatePath = Path.Combine(root, "test.pfx");
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, "test-only-password"));
        File.WriteAllText(Path.Combine(Site, "wwwroot", "index.html"), "<html>Pilot test shell</html>");
        config = new()
        {
            ["Jwt:Secret"] = "test-only-jwt-secret-" + Guid.NewGuid().ToString("N"),
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Platform:ProvisioningKey"] = "test-only-platform-" + Guid.NewGuid().ToString("N"),
            ["AllowedHosts"] = "pilot.example.test",
            ["App:BaseUrl"] = "https://pilot.example.test",
            ["ConnectionStrings:DefaultConnection"] = "Server=sql.example.test;Database=Ekipzen;Integrated Security=True;Encrypt=True;TrustServerCertificate=False",
            ["ConnectionStrings:PlatformConnection"] = "Server=sql.example.test;Database=Platform;Integrated Security=True;Encrypt=True;TrustServerCertificate=False",
            ["Email:Mode"] = "smtp", ["Smtp:Host"] = "smtp.example.test", ["Smtp:From"] = "ekipzen@example.test", ["Smtp:Port"] = "587",
            ["DemoRequests:NotifyEmail"] = "satis@example.test",
            ["Storage:Root"] = Path.Combine(root, "storage"), ["DataProtection:KeysPath"] = Path.Combine(root, "keys"),
            ["DataProtection:CertificatePath"] = certificatePath, ["DataProtection:CertificatePassword"] = "test-only-password",
            ["Logging:FilePath"] = Path.Combine(root, "logs", "ekipzen-.log"),
        };
    }

    [Fact]
    public void ExplicitProductionConfiguration_PassesWithoutConnectingToExternalServices()
        => ProductionConfiguration.GetErrors(new ConfigurationBuilder().AddInMemoryCollection(config).Build(), Site).Should().BeEmpty();

    [Fact]
    public void DeploymentExampleConfiguration_DoesNotMissDemoRequestsNotifyEmail()
    {
        // Örnek dosyada yer tutucu sırlar var; yalnız DemoRequests:NotifyEmail eksikliğini denetler.
        var exampleJson = Path.Combine(RepoRoot(), "deployment", "windows", "configuration.example.json");
        File.Exists(exampleJson).Should().BeTrue($"örnek dosya bulunamadı: {exampleJson}");
        var config = new ConfigurationBuilder().AddJsonFile(exampleJson).Build();
        var errors = ProductionConfiguration.GetErrors(config, Site);
        errors.Should().NotContain(e => e.Contains("DemoRequests:NotifyEmail"));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "deployment")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo kökü bulunamadı (deployment/ üzerinden aranıyor).");
    }

    [Theory]
    [InlineData("Jwt:Secret", "short")]
    [InlineData("Jwt:Secret", "a-long-do-not-use-in-prod-secret-placeholder")]
    [InlineData("Platform:ProvisioningKey", "dev-platform-key-change-in-prod")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("AllowedHosts", "https://pilot.example.test")]
    [InlineData("Jwt:AccessTokenMinutes", "60")]
    [InlineData("Seed:Enabled", "true")]
    [InlineData("Email:Mode", "outbox")]
    [InlineData("Smtp:Port", "0")]
    [InlineData("Smtp:From", "not-an-email")]
    [InlineData("Logging:FilePath", "logs/app.log")]
    [InlineData("App:BaseUrl", "http://localhost:5173")]
    [InlineData("App:BaseUrl", "https://unknown.example.test")]
    [InlineData("Smtp:Host", "")]
    [InlineData("DemoRequests:NotifyEmail", "")]
    [InlineData("DemoRequests:NotifyEmail", "not-an-email")]
    [InlineData("Storage:Root", "App_Data/storage")]
    [InlineData("DataProtection:KeysPath", "keys")]
    [InlineData("DataProtection:CertificatePassword", "")]
    [InlineData("Cors:AllowedOrigins:0", "http://pilot.example.test")]
    [InlineData("Cors:AllowedOrigins:0", "https://pilot.example.test/path")]
    [InlineData("Cors:AllowedOrigins:0", "https://user:secret@pilot.example.test")]
    [InlineData("ConnectionStrings:DefaultConnection", "Server=sql;Database=Ekipzen;Encrypt=False")]
    [InlineData("ConnectionStrings:DefaultConnection", "Server=sql;Database=Ekipzen;TrustServerCertificate=True")]
    [InlineData("ConnectionStrings:DefaultConnection", "Server=sql;Database=Ekipzen;User ID=sa")]
    public void UnsafeConfiguration_IsRejectedWithoutPrintingValues(string key, string value)
    {
        config[key] = value;
        var errors = ProductionConfiguration.GetErrors(new ConfigurationBuilder().AddInMemoryCollection(config).Build(), Site);
        errors.Should().NotBeEmpty();
        string.Join(" ", errors).Should().NotContain(config["Platform:ProvisioningKey"]!);
    }

    [Fact]
    public void PersistentData_CannotBeInReleaseOrShareKeyDirectory()
    {
        config["Storage:Root"] = Path.Combine(Site, "App_Data");
        ProductionConfiguration.GetErrors(new ConfigurationBuilder().AddInMemoryCollection(config).Build(), Site).Should().NotBeEmpty();
        config["Storage:Root"] = config["DataProtection:KeysPath"];
        ProductionConfiguration.GetErrors(new ConfigurationBuilder().AddInMemoryCollection(config).Build(), Site).Should().NotBeEmpty();
        ProductionConfiguration.IsWithin(Path.Combine(root, "site-other"), Site).Should().BeFalse();
    }

    [Fact]
    public async Task HttpRedirect_DoesNotTrustSpoofedForwardedHeaders()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://pilot.example.test");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "attacker.example.test");
        var response = await client.GetAsync("/api/probe");
        response.StatusCode.Should().Be(HttpStatusCode.PermanentRedirect);
        response.Headers.Location!.Host.Should().Be("pilot.example.test");
        response.Headers.Location.Scheme.Should().Be("https");
    }

    [Fact]
    public async Task UnknownHost_IsRejected()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://attacker.example.test");
        (await client.GetAsync("/api/probe")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HttpsSecurityHeaders_StaticShell_AndUnknownApiAreCorrect()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://pilot.example.test");
        var response = await client.GetAsync("/api/probe");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle();
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("script-src 'self'");
        response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await client.GetStringAsync("/")).Should().Contain("Pilot test shell");
        (await client.GetAsync("/api/not-an-endpoint")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("https://admin.example.test", true)]
    [InlineData("https://attacker.example.test", false)]
    public async Task Cors_OnlyAllowsExplicitOrigins(string origin, bool allowed)
    {
        config["Cors:AllowedOrigins:0"] = "https://admin.example.test";
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://pilot.example.test");
        client.DefaultRequestHeaders.Add("Origin", origin);
        var response = await client.GetAsync("/api/probe");
        response.Headers.Contains("Access-Control-Allow-Origin").Should().Be(allowed);
        if (allowed) response.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be(origin);
    }

    [Fact]
    public async Task EncryptedKeys_SurviveReleaseDirectoryChange_AndProductionPasswordPolicyIsStronger()
    {
        string protectedValue;
        await using (var app = await StartAsync())
        {
            var policy = app.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.Password;
            policy.RequiredLength.Should().Be(12);
            policy.RequireUppercase.Should().BeTrue();
            protectedValue = app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("pilot-test").Protect("test-payload");
        }
        var xml = string.Join("", Directory.GetFiles(config["DataProtection:KeysPath"]!, "*.xml").Select(File.ReadAllText));
        xml.Should().Contain("encryptedSecret");
        var nextSite = Path.Combine(root, "site-v2");
        Directory.CreateDirectory(nextSite);
        await using var restarted = await StartAsync(nextSite);
        restarted.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("pilot-test")
            .Unprotect(protectedValue).Should().Be("test-payload");
    }

    [Fact]
    public async Task SpaFallback_ServesShellForPageRoutes_ButNotForApiOrFiles()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://pilot.example.test");

        var page = await client.GetAsync("/ozellikler");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).Should().Contain("Pilot test shell");
        page.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        page.Headers.CacheControl!.NoCache.Should().BeTrue();

        (await client.GetStringAsync("/app/dashboard")).Should().Contain("Pilot test shell");
        (await client.GetAsync("/api/not-an-endpoint")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/assets/missing.js")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WrongCertificatePassword_PreventsStartup()
    {
        config["DataProtection:CertificatePassword"] = "wrong-password";
        var start = () => StartAsync();
        await start.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task NonRsaCertificate_IsRejectedBeforeFirstProtectedToken()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=unsupported test", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3));
        File.WriteAllBytes(config["DataProtection:CertificatePath"]!, certificate.Export(X509ContentType.Pfx, "test-only-password"));
        var start = () => StartAsync();
        await start.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RSA*");
    }

    [Fact]
    public async Task MissingPersistentDirectory_IsNotSilentlyRecreated()
    {
        Directory.Delete(config["DataProtection:KeysPath"]!);
        var start = () => StartAsync();
        await start.Should().ThrowAsync<InvalidOperationException>().WithMessage("*dizinlerini*");
    }

    private async Task<WebApplication> StartAsync(string? site = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production, ContentRootPath = site ?? Site,
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(config);
        builder.WebHost.UseTestServer();
        PilotHosting.Configure(builder);
        var app = builder.Build();
        PilotHosting.UseWebSecurity(app);
        app.MapGet("/api/probe", () => "ok");
        PilotHosting.MapSpaFallback(app);
        await app.StartAsync();
        return app;
    }

    public void Dispose()
    {
        // Yalnız bu testin oluşturduğu GUID'li geçici kök; gerçek sunucu verisi içermez.
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
