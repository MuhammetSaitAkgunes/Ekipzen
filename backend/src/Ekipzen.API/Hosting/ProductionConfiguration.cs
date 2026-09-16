using Microsoft.Data.SqlClient;

namespace Ekipzen.API.Hosting;

/// <summary>Üretim açılış kapısı. Hatalara ayar adı yazılır, sır değeri yazılmaz.</summary>
public static class ProductionConfiguration
{
    public static IReadOnlyList<string> GetErrors(IConfiguration config, string contentRoot)
    {
        var errors = new List<string>();
        if (config.GetValue("Seed:Enabled", false)) errors.Add("Seed:Enabled üretimde false olmalıdır.");
        RequireSecret("Jwt:Secret");
        RequireSecret("Platform:ProvisioningKey");
        var minutes = config.GetValue("Jwt:AccessTokenMinutes", 0);
        if (minutes is < 1 or > 30) errors.Add("Jwt:AccessTokenMinutes 1–30 aralığında olmalıdır.");

        var hosts = (config["AllowedHosts"] ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (hosts.Length == 0 || hosts.Any(host => host.Contains('*') || Uri.CheckHostName(host) == UriHostNameType.Unknown))
            errors.Add("AllowedHosts açık host adları içermelidir; wildcard kabul edilmez.");
        var appUrl = config["App:BaseUrl"] ?? "";
        if (!IsHttpsOrigin(appUrl) || !hosts.Contains(new Uri(appUrl).Host, StringComparer.OrdinalIgnoreCase))
            errors.Add("App:BaseUrl AllowedHosts içindeki bir host için tam HTTPS origin olmalıdır.");
        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(origin => !IsHttpsOrigin(origin)))
            errors.Add("Cors:AllowedOrigins yalnız tam HTTPS origin içermelidir (yol/wildcard/kimlik bilgisi yok).");

        SqlConnectionStringBuilder? shared = null;
        SqlConnectionStringBuilder? platform = null;
        CheckSql("DefaultConnection", ref shared);
        CheckSql("PlatformConnection", ref platform);
        if (shared is not null && platform is not null
            && string.Equals(shared.DataSource, platform.DataSource, StringComparison.OrdinalIgnoreCase)
            && string.Equals(shared.InitialCatalog, platform.InitialCatalog, StringComparison.OrdinalIgnoreCase))
            errors.Add("PlatformConnection ve DefaultConnection ayrı kataloglara yönelmelidir.");

        if (!string.Equals(config["Email:Mode"], "smtp", StringComparison.OrdinalIgnoreCase))
            errors.Add("Email:Mode üretimde smtp olmalıdır; outbox teslimat değildir.");
        if (string.IsNullOrWhiteSpace(config["Smtp:Host"]) || !System.Net.Mail.MailAddress.TryCreate(config["Smtp:From"], out _))
            errors.Add("Smtp:Host ve Smtp:From zorunludur.");
        if (config.GetValue("Smtp:Port", 0) is < 1 or > 65535) errors.Add("Smtp:Port geçersiz.");
        if (!string.IsNullOrWhiteSpace(config["Smtp:User"]) && string.IsNullOrWhiteSpace(config["Smtp:Password"]))
            errors.Add("Smtp:User tanımlıysa Smtp:Password zorunludur.");

        CheckDataDirectory("Storage:Root");
        CheckDataDirectory("DataProtection:KeysPath");
        if (IsAbsoluteNonRoot(config["Storage:Root"]) && IsAbsoluteNonRoot(config["DataProtection:KeysPath"])
            && PathsOverlap(config["Storage:Root"]!, config["DataProtection:KeysPath"]!))
            errors.Add("Storage:Root ve DataProtection:KeysPath ayrı, iç içe olmayan dizinler olmalıdır.");
        var logPath = config["Logging:FilePath"];
        if (!IsAbsoluteNonRoot(logPath) || IsWithin(logPath!, contentRoot))
            errors.Add("Logging:FilePath yayın dizini dışında mutlak dosya yolu olmalıdır.");
        var certificate = config["DataProtection:CertificatePath"];
        if (!IsAbsoluteNonRoot(certificate) || IsWithin(certificate!, contentRoot))
            errors.Add("DataProtection:CertificatePath yayın dizini dışında mutlak bir dosya yolu olmalıdır.");
        if (string.IsNullOrWhiteSpace(config["DataProtection:CertificatePassword"]))
            errors.Add("DataProtection:CertificatePassword zorunludur.");
        return errors;

        void RequireSecret(string key)
        {
            var value = config[key] ?? "";
            if (value.Length < 32 || value.Contains("do-not-use-in-prod", StringComparison.OrdinalIgnoreCase)
                || value.Contains("change-in-prod", StringComparison.OrdinalIgnoreCase)
                || value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{key} en az 32 karakterli, örnek olmayan bir sır olmalıdır.");
        }

        void CheckDataDirectory(string key)
        {
            var value = config[key];
            if (!IsAbsoluteNonRoot(value) || PathsOverlap(value!, contentRoot))
                errors.Add($"{key} yayın dizininden bağımsız mutlak bir dizin olmalıdır.");
        }

        void CheckSql(string name, ref SqlConnectionStringBuilder? sql)
        {
            try
            {
                sql = new SqlConnectionStringBuilder(config.GetConnectionString(name) ?? "");
                if (string.IsNullOrWhiteSpace(sql.DataSource) || string.IsNullOrWhiteSpace(sql.InitialCatalog)
                    || sql.Encrypt.Equals(SqlConnectionEncryptOption.Optional) || sql.TrustServerCertificate
                    || string.Equals(sql.UserID, "sa", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"ConnectionStrings:{name} katalog, doğrulanmış TLS ve sa dışı kimlik gerektirir.");
            }
            catch (ArgumentException)
            {
                errors.Add($"ConnectionStrings:{name} geçersiz; bağlantı dizesi loglanmadı.");
            }
        }
    }

    public static bool IsHttpsOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && !value.Contains('*') && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && value == uri.GetLeftPart(UriPartial.Authority);

    public static bool IsAbsoluteNonRoot(string? value) => !string.IsNullOrWhiteSpace(value)
        && Path.IsPathFullyQualified(value)
        && Path.TrimEndingDirectorySeparator(Path.GetFullPath(value)) != Path.GetPathRoot(value);

    public static bool IsWithin(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return fullPath.Equals(fullRoot, comparison)
            || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static bool PathsOverlap(string first, string second) => IsWithin(first, second) || IsWithin(second, first);
}
