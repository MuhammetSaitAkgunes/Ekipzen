using IKPro.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IKPro.Application.Features.Tenancy.Commands;

/// <summary>
/// TÜM kiracı veritabanlarını sırayla migrate eder. <b>Dağıtımın bir adımıdır.</b>
///
/// Neden açılışta değil de ayrı bir adım: açılışta migrate etmek iki şeyi bozardı.
/// Birincisi açılış süresi müşteri sayısıyla büyür — 200 müşteride uygulama
/// dakikalarca kapalı kalır. İkincisi ve daha kötüsü, tek bir müşterinin
/// veritabanı erişilemez olduğunda uygulama HİÇ açılmaz; yani bir müşterinin
/// sorunu bütün müşterileri offline yapar. Ayrılmanın amacı tam olarak bunun
/// tersiydi.
///
/// Unutulma riski şöyle kapatıldı: şeması geride kalan kiracı istekleri 503 ile
/// reddeder (<see cref="ITenantSchemaGuard"/>) ve durum
/// <c>GET /api/tenants/schema-status</c> ile görünür. Yani adımı atlamak sessiz
/// kalmaz.
/// </summary>
public sealed record MigrateTenantsCommand : IRequest<MigrateTenantsResult>;

/// <summary>Tek bir kiracının sonucu.</summary>
public sealed record TenantMigrationOutcome(
    int TenantId,
    string Slug,
    bool Basarili,
    int UygulananMigrationSayisi,
    string? Hata);

public sealed record MigrateTenantsResult(
    int Toplam,
    int Basarili,
    int Basarisiz,
    IReadOnlyList<TenantMigrationOutcome> Sonuclar);

public sealed class MigrateTenantsCommandHandler(
    IPlatformDbContext platform,
    ITenantSchemaInitializer schemaInitializer,
    ITenantSchemaGuard schemaGuard,
    ILogger<MigrateTenantsCommandHandler> logger)
    : IRequestHandler<MigrateTenantsCommand, MigrateTenantsResult>
{
    public async Task<MigrateTenantsResult> Handle(
        MigrateTenantsCommand request, CancellationToken cancellationToken)
    {
        var kiracilar = await platform.Tenants
            .Where(t => t.DatabaseName != null)
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Slug })
            .ToListAsync(cancellationToken);

        List<TenantMigrationOutcome> sonuclar = [];

        foreach (var kiraci in kiracilar)
        {
            try
            {
                var bekleyen = await schemaInitializer.PendingMigrationsAsync(kiraci.Id, cancellationToken);
                if (bekleyen.Count > 0)
                {
                    await schemaInitializer.PrepareAsync(kiraci.Id, cancellationToken);
                    logger.LogInformation(
                        "Kiracı {TenantId} ({Slug}) migrate edildi: {Sayi} migration.",
                        kiraci.Id, kiraci.Slug, bekleyen.Count);
                }

                // Başarılı olsun ya da zaten güncel olsun, kapıyı ANINDA aç: TTL
                // beklemek, migration bittikten sonra kiracıyı boş yere 503'te tutardı.
                schemaGuard.Invalidate(kiraci.Id);
                sonuclar.Add(new TenantMigrationOutcome(kiraci.Id, kiraci.Slug, true, bekleyen.Count, null));
            }
            catch (Exception ex)
            {
                // Bir kiracının başarısızlığı DİĞERLERİNİ durdurmaz. Durdursaydı,
                // sıradaki tek bir arıza geri kalan bütün müşterileri eski şemada
                // bırakırdı — üstelik hangilerinin güncellendiği belirsiz olurdu.
                logger.LogError(ex,
                    "Kiracı {TenantId} ({Slug}) migrate EDİLEMEDİ.", kiraci.Id, kiraci.Slug);
                sonuclar.Add(new TenantMigrationOutcome(
                    kiraci.Id, kiraci.Slug, false, 0, ex.Message));
            }
        }

        var basarili = sonuclar.Count(s => s.Basarili);
        return new MigrateTenantsResult(sonuclar.Count, basarili, sonuclar.Count - basarili, sonuclar);
    }
}
