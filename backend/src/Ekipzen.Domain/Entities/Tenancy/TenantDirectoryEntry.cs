namespace Ekipzen.Domain.Entities.Tenancy;

/// <summary>
/// E-posta → kiracı yönlendirmesi. Login kiracıyı SORMADAN yapıldığı için,
/// hangi kiracının veritabanına bakılacağı buradan çözülür.
///
/// TÜRETİLMİŞ kayıttır, asıl kaynak değildir: gerçek doğruluk kiracı
/// veritabanındaki Users tablosudur. Bu tablo bozulur ya da bir geri
/// yüklemeden sonra saparsa, kiracı veritabanı taranarak yeniden kurulur.
///
/// Birincil anahtarın e-posta olması "tek e-posta = tek kiracı" kuralını
/// veritabanı seviyesinde zorlar.
/// </summary>
public class TenantDirectoryEntry
{
    public string NormalizedEmail { get; set; } = string.Empty;

    public int TenantId { get; set; }

    /// <summary>
    /// E-postayı arama biçimine indirger. Identity'nin NormalizedEmail'i ile
    /// aynı kural: büyük harfe çevir. Türkçe'ye özgü i/İ sorununu doğurmamak
    /// için INVARIANT kültür kullanılır — e-posta adresleri ASCII'dir ve
    /// tr-TR'de "i".ToUpper() = "İ" olurdu, bu da Identity ile uyuşmazlık
    /// yaratırdı.
    /// </summary>
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
