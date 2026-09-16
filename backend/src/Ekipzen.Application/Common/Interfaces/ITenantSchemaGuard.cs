namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracının veritabanı şemasının GÜNCEL olup olmadığını söyler.
///
/// Neden gerekli: kiracı veritabanları açılışta migrate EDİLMEZ (açılış süresi
/// müşteri sayısıyla büyümemeli ve tek bir kiracının arızası tüm uygulamayı
/// offline yapmamalı). Dolayısıyla yeni bir sürüm yayınlandığında kiracılar bir
/// süre eski şemayla kalır. O aralıkta istekleri kabul etmek, var olmayan
/// sütunları sorgulamak demektir.
///
/// <b>Önbelleklidir:</b> aksi halde her istek <c>__EFMigrationsHistory</c>'ye
/// gider. Migration koşucusu bitince <see cref="Invalidate"/> çağırır, yani
/// kiracı TTL beklemeden hizmete döner.
/// </summary>
public interface ITenantSchemaGuard
{
    Task<bool> IsUpToDateAsync(int tenantId, CancellationToken cancellationToken);

    void Invalidate(int tenantId);
}
