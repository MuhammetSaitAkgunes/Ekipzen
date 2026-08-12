using IKPro.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IKPro.Infrastructure.Tenancy;

/// <inheritdoc cref="ITenantSchemaGuard"/>
public sealed class TenantSchemaGuard(
    ITenantSchemaInitializer schemaInitializer,
    IMemoryCache cache,
    ILogger<TenantSchemaGuard> logger) : ITenantSchemaGuard
{
    /// <summary>
    /// "Güncel" cevabı uzun yaşar: şema kendiliğinden eskimez, ancak bir dağıtımla
    /// eskir ve o dağıtım zaten süreci yeniden başlatır (önbellek sıfırlanır).
    /// </summary>
    private static readonly TimeSpan GuncelOmru = TimeSpan.FromMinutes(30);

    /// <summary>
    /// "Geride" cevabı KISA yaşar. Migration koşucusu bitince
    /// <see cref="Invalidate"/> çağırıyor, ama biri veritabanını elle migrate
    /// ederse (ör. dağıtım script'i doğrudan <c>dotnet ef database update</c>
    /// çalıştırırsa) kimse haber vermez. Kısa TTL, o durumda kiracının en geç
    /// yarım dakikada hizmete dönmesini sağlar — aksi halde çalışan bir sistem
    /// yarım saat boyunca 503 döndürürdü.
    /// </summary>
    private static readonly TimeSpan GerideOmru = TimeSpan.FromSeconds(30);

    private static string Anahtar(int tenantId) => $"kiraci-sema-{tenantId}";

    public async Task<bool> IsUpToDateAsync(int tenantId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Anahtar(tenantId), out bool guncel))
        {
            return guncel;
        }

        List<string> bekleyen;
        try
        {
            bekleyen = [.. await schemaInitializer.PendingMigrationsAsync(tenantId, cancellationToken)];
        }
        catch (Exception ex)
        {
            // Katalog yok ya da erişilemiyor. "Güncel" diyemeyiz — diyeydik istekler
            // içeri alınır ve anlaşılmaz SQL hatalarıyla düşerdi. Geride sayıp
            // reddediyoruz; sebep loga yazılıyor. Sonucu ÖNBELLEKLEMİYORUZ: geçici
            // bir bağlantı arızası, kiracıyı yarım dakika boyunca kapatmasın.
            logger.LogError(ex,
                "Kiracı {TenantId} şema durumu okunamadı; erişim kapatıldı.", tenantId);
            return false;
        }

        guncel = bekleyen.Count == 0;
        cache.Set(Anahtar(tenantId), guncel, guncel ? GuncelOmru : GerideOmru);

        if (!guncel)
        {
            logger.LogWarning(
                "Kiracı {TenantId} şeması GERİDE — bekleyen migration: {Bekleyen}. " +
                "POST /api/tenants/migrate ile güncelleyin.",
                tenantId, string.Join(", ", bekleyen));
        }

        return guncel;
    }

    public void Invalidate(int tenantId) => cache.Remove(Anahtar(tenantId));
}
