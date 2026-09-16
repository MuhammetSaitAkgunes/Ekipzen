using Ekipzen.Application.Features.Tenancy.Queries;
using MediatR;

namespace Ekipzen.API.Services;

/// <summary>
/// Açılıştan kısa süre sonra her kiracının şema durumunu KONTROL EDER — migrate
/// ETMEZ.
///
/// Bu ayrım fazın en önemli işletim kararı: migration'ı açılışa bağlamak, açılış
/// süresini müşteri sayısıyla büyütür ve tek bir kiracının arızasında uygulamanın
/// HİÇ açılmamasına yol açar. Ayrılmanın amacı tam tersiydi — bir müşterinin
/// sorunu diğerlerini etkilememeli.
///
/// Geriye kalan risk "dağıtım adımını unutmak"tı. Üç yerden birden görünür kılındı:
/// bu açılış logu, <c>GET /api/tenants/schema-status</c> ve şeması geride kalan
/// kiracının 503 alması. Sessiz kalabilecek hiçbir yol bırakılmadı.
///
/// <b>Açılışı BLOKLAMAZ:</b> arka planda çalışır. Kontrolün kendisi ucuz ama
/// N kiracı için N bağlantı demek; uygulamanın hazır olmasını buna bağlamak,
/// kaçındığımız sorunun küçük bir kopyasını geri getirirdi.
/// </summary>
public sealed class TenantSchemaCheckService(
    IServiceScopeFactory scopeFactory,
    ILogger<TenantSchemaCheckService> logger) : BackgroundService
{
    /// <summary>
    /// Kısa gecikme: uygulama isteklere cevap vermeye başlasın, kontrol ondan
    /// sonra koşsun. Amaç hazır olma süresini uzatmamak.
    /// </summary>
    private static readonly TimeSpan Gecikme = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Gecikme, stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var durumlar = await sender.Send(new GetSchemaStatusQuery(), stoppingToken);

            var sorunlular = durumlar.Where(d => !d.Guncel).ToList();
            if (sorunlular.Count == 0)
            {
                logger.LogInformation(
                    "Şema kontrolü: {Sayi} kiracının tamamı güncel.", durumlar.Count);
                return;
            }

            // YÜKSEK SESLE: bu satırı görmeyen bir operatör, müşterilerinin 503
            // aldığını ancak müşteri arayınca öğrenir.
            logger.LogError(
                "ŞEMA GERİDE — {Sorunlu}/{Toplam} kiracı eski şemada ve istekleri 503 " +
                "ile reddedecek. Geride kalanlar: {Kiracilar}. Düzeltmek için: " +
                "POST /api/tenants/migrate",
                sorunlular.Count, durumlar.Count,
                string.Join(", ", sorunlular.Select(s => $"{s.Slug}#{s.TenantId}")));
        }
        catch (OperationCanceledException)
        {
            // Uygulama kapanıyor — kontrol yarıda kalabilir, sorun değil.
        }
        catch (Exception ex)
        {
            // Kontrolün kendisi patlarsa uygulama ÇALIŞMAYA DEVAM ETMELİ: burası bir
            // teşhis aracı, hizmetin önkoşulu değil.
            logger.LogError(ex, "Şema kontrolü çalıştırılamadı.");
        }
    }
}
