namespace Ekipzen.Infrastructure.Email;

/// <summary>
/// SMTP gönderim ayarları (yapılandırma bölümü: <c>Smtp</c>). Yalnız
/// <c>Email:Mode=smtp</c> iken kullanılır ve startup'ta <see cref="Validate"/>
/// ile doğrulanır (fail-fast). Şifre asla commit edilmez; env'den gelir.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    /// <summary>587 = submission (StartTls); 465 için <see cref="UseStartTls"/>=false yapın.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Boşsa kimlik doğrulaması yapılmaz (ör. yerel relay).</summary>
    public string User { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Gönderen adresi (zorunlu), ör. noreply@sirketiniz.com.</summary>
    public string From { get; set; } = string.Empty;

    public string FromName { get; set; } = "Ekipzen";

    /// <summary>true → StartTls (587); false → bağlantıda TLS (465, SslOnConnect).</summary>
    public bool UseStartTls { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException(
                "Email:Mode=smtp seçildi ama Smtp:Host boş. SMTP sunucu adresini yapılandırın.");
        }

        if (string.IsNullOrWhiteSpace(From))
        {
            throw new InvalidOperationException(
                "Email:Mode=smtp seçildi ama Smtp:From boş. Gönderen adresini yapılandırın.");
        }
    }
}
