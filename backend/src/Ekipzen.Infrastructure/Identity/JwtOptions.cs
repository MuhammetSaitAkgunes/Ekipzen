namespace Ekipzen.Infrastructure.Identity;

/// <summary>JWT üretim/doğrulama ayarları (appsettings "Jwt" bölümü).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "Ekipzen";
    public string Audience { get; init; } = "EkipzenFrontend";

    /// <summary>HMAC-SHA256 imza anahtarı; en az 32 karakter olmalı.</summary>
    public string Secret { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 60;
    public int RefreshTokenDays { get; init; } = 7;
}
