using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.Extensions.Logging;

namespace Ekipzen.Infrastructure.Tenancy;

/// <summary>
/// Erişim kapısının tek uygulaması: kütükten (<see cref="ITenantRegistry"/>) durumu
/// okur, yalnız <see cref="TenantStatus.Active"/> geçer.
///
/// Kiracı bulunamazsa (kütük <c>null</c> döner) da reddeder — var olmayan
/// kiracıya erişim verilmez. Bilinçli bir sınır: kütük "yok" sonucunu da
/// önbelleğe alıyor (bkz. <see cref="TenantRegistry"/>), yani teorik olarak bir
/// kiracı sağlanmadan hemen önce sorgulanmışsa "yok" cevabı TTL dolana kadar
/// (5 dk) yapışık kalabilir. Gerçekçi bulunmadı (provizyon anında henüz hiçbir
/// kullanıcı token isteyemez) — ek kod (ör. negatif sonuç için ayrı/kısa TTL)
/// bilinçli olarak yazılmadı.
/// </summary>
public sealed class TenantAccessGuard(
    ITenantRegistry registry,
    ITenantSchemaGuard schemaGuard,
    ILogger<TenantAccessGuard> logger) : ITenantAccessGuard
{
    private const string KullaniciMesaji = "Şirket hesabınız şu anda kullanıma kapalı. Yöneticinizle iletişime geçin.";

    private const string SemaMesaji =
        "Sistem güncelleniyor; şirket hesabınız birkaç dakika içinde tekrar erişilebilir olacak.";

    public async Task EnsureAccessibleAsync(int tenantId, CancellationToken cancellationToken)
    {
        var durum = await registry.GetStatusAsync(tenantId, cancellationToken);

        if (durum == TenantStatus.Active)
        {
            // Kiracı açık — ama veritabanı şeması uygulamanın gerisindeyse istekleri
            // ALMAK, var olmayan sütunları sorgulamak demektir. Kontrol BURADA duruyor
            // çünkü bu metot iki darboğazın da ortak yoludur: token üretimi (login +
            // refresh) ve elinde token'la gelen istek. Ayrı bir mekanizma kurmak, o
            // iki yoldan birini kaçırma riski taşırdı.
            if (!await schemaGuard.IsUpToDateAsync(tenantId, cancellationToken))
            {
                throw new TenantSchemaOutdatedException(SemaMesaji);
            }

            return;
        }

        // Gerçek durum yalnız loga yazılır — kullanıcıya iç durum adı (Frozen/Purging)
        // sızdırılmaz, "kullanıma kapalı" yeterli ve doğru düzeyde bilgi verir.
        logger.LogWarning(
            "Kiracı erişimi reddedildi: TenantId={TenantId}, Durum={Durum}",
            tenantId, durum?.ToString() ?? "bulunamadı");

        throw new TenantInaccessibleException(KullaniciMesaji);
    }
}
