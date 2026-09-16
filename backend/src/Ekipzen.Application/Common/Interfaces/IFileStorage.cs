namespace Ekipzen.Application.Common.Interfaces;

/// <summary>Depolanan dosyanın göreli yolu ve meta bilgisi.</summary>
public sealed record StoredFile(string Path, string FileName, long SizeBytes);

/// <summary>
/// Dosya depolama soyutlaması (Faz 3: yerel disk). Yollar depo köküne göre görelidir;
/// mutlak yol/dizin kaçışı implementasyonda engellenir.
/// </summary>
public interface IFileStorage
{
    /// <summary>Dosyayı verilen kategori klasörüne benzersiz adla kaydeder.</summary>
    Task<StoredFile> SaveAsync(Stream content, string fileName, string category, CancellationToken cancellationToken);

    /// <summary>Okuma akışı açar; dosya yoksa <c>FileNotFoundException</c>.</summary>
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>Dosyayı siler; yoksa sessizce geçer.</summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Kiracının tüm dosya alanını siler (purge). Dizin yoksa sessizce geçer.
    /// Kiracı AÇIKÇA parametredir: purge sırasında silinen kiracı ile aktif
    /// bağlam ayrışabilir, örtük çözümleme burada tehlikelidir.
    /// </summary>
    Task DeleteTenantSpaceAsync(int tenantId, CancellationToken cancellationToken);
}
