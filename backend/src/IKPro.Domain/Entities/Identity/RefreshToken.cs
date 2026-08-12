namespace IKPro.Domain.Entities.Identity;

/// <summary>
/// JWT yenileme token'ı. Erişim token'ı süresi dolunca yeni token almak için kullanılır.
///
/// PLATFORM veritabanında saklanır (bkz. <c>PlatformDbContext</c>), uygulama
/// veritabanında DEĞİL: elinde yalnız token dizesi olan sunucu, kiracı DB'leri
/// ayrıldığında hangi katalogda arayacağını başka türlü bilemez. Satırdaki
/// <see cref="TenantId"/> yönlendirmeyi doğrudan verir.
///
/// Domain katmanında yaşar (Infrastructure'daki ApplicationUser'ın aksine): bu
/// tip ASP.NET Core Identity framework'üne bağımlı değildir (düz POCO) ve hem
/// Application katmanındaki <c>IPlatformDbContext</c> hem de Infrastructure
/// katmanındaki <c>PlatformDbContext</c> tarafından görülmesi gerekir —
/// Application projesi Infrastructure'a referans VERMEZ, bu yüzden tip
/// Infrastructure.Identity'de kalamaz (bkz. Tenant/TenantDirectoryEntry'nin
/// aynı gerekçeyle burada olması).
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    /// <summary>Sahip kiracı (token'ın ait olduğu kullanıcının kiracısı).</summary>
    public int TenantId { get; set; }

    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// Sahip kullanıcının Identity kimliği. Navigasyon YOKTUR: token platform
    /// veritabanında, kullanıcı kiracı veritabanındadır ve SQL Server çapraz-veritabanı
    /// yabancı anahtarı desteklemez. Kullanıcı, <see cref="TenantId"/> ile açılan
    /// kiracı kapsamından bu kimlikle çözülür.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
