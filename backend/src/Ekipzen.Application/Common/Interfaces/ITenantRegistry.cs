using Ekipzen.Domain.Entities.Tenancy;

namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracı kütüğü: kiracının durumunu bellekte önbellekler.
///
/// Neden var: erişim kapısı her istekte kiracının durumunu soruyor. Bunu her
/// seferinde platform veritabanından okumak, ayırdığımız katmanı her istekte
/// yeniden birleştirmek olurdu.
///
/// Kütük artık katalog adını da taşıyor: bağlantı çözücü her <c>AppDbContext</c>
/// kurulumunda hangi veritabanına gidileceğini soruyor ve bunu her seferinde
/// platform veritabanından okumak, ayırdığımız katmanı her istekte yeniden
/// birleştirmek olurdu. İkisi TEK kayıtta önbelleklenir — tek okuma iki soruyu
/// da cevaplar.
/// </summary>
public interface ITenantRegistry
{
    /// <summary>Kiracının durumu; kiracı yoksa <c>null</c>.</summary>
    Task<TenantStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Kiracının katalog adı; kiracı yoksa ya da henüz ayrılmamışsa (paylaşılan
    /// veritabanında duruyorsa) <c>null</c>.
    ///
    /// <b>Neden senkron:</b> tek çağıranı bağlantı çözücüdür, o da
    /// <c>AddDbContext</c> yapılandırma geri çağrısının içinden çalışır — orası
    /// senkron bir bağlamdır ve async'e çevrilemez. Sync-over-async yapmak yerine
    /// (bir thread'i bloke edip kuyruk açlığı riski almaktansa) önbellek ıskası
    /// GERÇEKTEN senkron bir sorguyla doldurulur. Iska kiracı başına beş dakikada
    /// bir yaşanır; sıcak yolda hiç veritabanına gidilmez.
    /// </summary>
    string? GetDatabaseName(int tenantId);

    /// <summary>
    /// Önbellek kaydını düşürür. Durum değiştiren her yol bunu çağırmalı —
    /// dondurmanın bir sonraki istekte etkili olması buna bağlıdır, süre
    /// dolmasını beklemeyiz.
    /// </summary>
    void Invalidate(int tenantId);
}
