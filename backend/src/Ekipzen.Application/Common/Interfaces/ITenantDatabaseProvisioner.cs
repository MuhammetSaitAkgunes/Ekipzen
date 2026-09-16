namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracının fiziksel veritabanını oluşturur/düşürür.
///
/// Neden ayrı bir soyutlama: bu işler EF migration'ının İÇİNDE yapılamaz —
/// <c>CREATE DATABASE</c> kendi veritabanına bağlıyken çalıştırılamaz, <c>master</c>'a
/// ham SQL gerekir. Yani bu, EF'in modelleyebildiği dünyanın dışında kalan bir
/// yetenektir ve kendi sözleşmesini hak eder.
///
/// <b>Veritabanı adı SQL'e enterpole edilir</b> — <c>CREATE DATABASE</c> ve
/// <c>DROP DATABASE</c> parametre kabul etmez. Ad ise slug'dan türetilir, slug da
/// istemciden gelir. Bu yüzden ad doğrulaması (<c>[A-Za-z0-9_]</c>, 1–128 karakter)
/// bu katmanın TEK savunmasıdır ve her metotta uygulanır.
/// </summary>
public interface ITenantDatabaseProvisioner
{
    /// <summary>Verilen katalog sunucuda var mı?</summary>
    Task<bool> ExistsAsync(string databaseName, CancellationToken cancellationToken);

    /// <summary>
    /// Katalogu oluşturur. Zaten varsa hiçbir şey yapmaz (idempotent — yarıda
    /// kalmış bir provizyonun yeniden denenmesi ilk adımda patlamamalı).
    /// </summary>
    Task CreateAsync(string databaseName, CancellationToken cancellationToken);

    /// <summary>
    /// Katalogu düşürür. Yoksa hiçbir şey yapmaz. Aktif bağlantılar koparılır —
    /// aksi halde tek bir açık oturum silmeyi süresiz engellerdi.
    /// </summary>
    Task DropAsync(string databaseName, CancellationToken cancellationToken);
}
