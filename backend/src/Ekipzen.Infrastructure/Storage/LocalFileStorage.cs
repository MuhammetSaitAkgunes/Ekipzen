using Ekipzen.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Ekipzen.Infrastructure.Storage;

/// <summary>
/// Yerel disk dosya deposu. Kök dizin "Storage:Root" ayarından gelir.
/// Dosyalar {kök}/tenant-{id}/{kategori}/{guid}{uzantı} olarak saklanır;
/// orijinal ad DB'de meta olarak tutulur.
///
/// Kiracı ön ekini ÇAĞIRAN DEĞİL BU SINIF uygular. Böylece yeni bir yükleme ucu
/// eklendiğinde ön eki koymayı unutmak imkânsızdır — veritabanı kiracı
/// filtresindeki reflection yaklaşımıyla aynı ilke.
///
/// Dönen/alınan yollar kiracıdan BAĞIMSIZ görelidir (veritabanı bunları saklar).
/// Ön ek her işlemde AKTİF kiracıdan yeniden uygulandığı için, kayıttaki yol
/// kurcalansa bile başka kiracının alanına erişilemez.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly ICurrentTenant _currentTenant;

    public LocalFileStorage(IConfiguration configuration, ICurrentTenant currentTenant)
    {
        var configured = configuration["Storage:Root"] ?? Path.Combine("App_Data", "storage");
        _root = Path.GetFullPath(configured);
        Directory.CreateDirectory(_root);
        _currentTenant = currentTenant;
    }

    /// <summary>Kiracı klasör adı — yedekleme ve migrasyon script'leri de bu şemayı kullanır.</summary>
    public static string TenantFolder(int tenantId) => $"tenant-{tenantId}";

    public async Task<StoredFile> SaveAsync(
        Stream content, string fileName, string category, CancellationToken cancellationToken)
    {
        var tenantRoot = AktifKiraciKoku();
        var safeCategory = SanitizeSegment(category);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var relativePath = Path.Combine(safeCategory, $"{Guid.NewGuid():N}{extension}");

        var fullPath = ResolveSafe(tenantRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken);

        // Yol ayracı platformdan bağımsız saklanır; kiracı ön eki DB'ye YAZILMAZ.
        return new StoredFile(relativePath.Replace('\\', '/'), fileName, target.Length);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafe(AktifKiraciKoku(), path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Depodaki dosya bulunamadı.", path);
        }

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafe(AktifKiraciKoku(), path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task DeleteTenantSpaceAsync(int tenantId, CancellationToken cancellationToken)
    {
        var tenantRoot = Path.Combine(_root, TenantFolder(tenantId));
        if (Directory.Exists(tenantRoot))
        {
            Directory.Delete(tenantRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>Aktif kiracının kök dizini; kiracı yoksa işlem reddedilir.</summary>
    private string AktifKiraciKoku()
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException(
                "Dosya işlemi için aktif kiracı gerekli. Kiracısız yazma, kiracılar arası sızıntının tohumudur.");

        return Path.Combine(_root, TenantFolder(tenantId));
    }

    /// <summary>
    /// Kiracı kökü dışına çıkan yolları (../ vb.) reddeder. Karşılaştırma kök +
    /// dizin ayracı ile yapılır: ayraçsız bir StartsWith, kök adının ön ekini
    /// paylaşan KARDEŞ dizinleri ("tenant-1" kökü için "tenant-11") kök içinde sanır.
    /// </summary>
    private static string ResolveSafe(string tenantRoot, string relativePath)
    {
        var boundary = tenantRoot.EndsWith(Path.DirectorySeparatorChar)
            ? tenantRoot
            : tenantRoot + Path.DirectorySeparatorChar;

        var fullPath = Path.GetFullPath(Path.Combine(tenantRoot, relativePath));
        if (!fullPath.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Geçersiz dosya yolu.");
        }

        return fullPath;
    }

    private static string SanitizeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(segment.Where(c => !invalid.Contains(c) && c != '.').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "misc" : cleaned;
    }
}
