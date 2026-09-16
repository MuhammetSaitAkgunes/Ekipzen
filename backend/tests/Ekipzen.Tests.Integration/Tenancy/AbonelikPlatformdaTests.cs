using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Settings;
using Ekipzen.Domain.Entities.Settings;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Abonelik PLATFORM veritabanındadır, kiracının kendi kataloğunda değil.
///
/// Bu taşımanın bedeli var ve testler tam olarak onu koruyor: platform
/// context'inde <b>global kiracı filtresi YOKTUR</b>. Kiracı veritabanında
/// "filtreyi yazmayı unutmak" yapısal olarak imkânsızdı; burada mümkün ve
/// unutulursa bir müşteri diğerinin planını görür. Sorgunun kiracıyı gerçekten
/// filtrelediğini bu yüzden ayrı bir testle sabitliyoruz.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AbonelikPlatformdaTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Abonelik_KiracininKendiKatalogundaDEGIL_Platformda()
    {
        var eposta = $"abn-{Guid.NewGuid():N}@abonelik.local";
        var kiraci = await ProvisionAndActivateAsync("Abonelik Yeri A.Ş.", eposta);

        await AbonelikYazAsync(kiraci.TenantId, "PRO", "Yer Testi");

        (await TabloVarMiAsync(Namer.For(kiraci.Slug), "Subscriptions")).Should().BeFalse(
            "abonelik tablosu kiracı veritabanında hiç bulunmamalı");

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platform.Subscriptions.AnyAsync(s => s.TenantId == kiraci.TenantId)).Should().BeTrue();
    }

    /// <summary>
    /// İki kiracı, iki farklı plan. Filtre unutulsaydı ikisi de aynı (ilk bulunan)
    /// planı görürdü — testin iki tarafı da bu yüzden ayrı ayrı kontrol ediliyor.
    /// </summary>
    [Fact]
    public async Task IkiKiraci_KendiPlanlariniGorur()
    {
        var epostaA = $"abn-a-{Guid.NewGuid():N}@abonelik.local";
        var epostaB = $"abn-b-{Guid.NewGuid():N}@abonelik.local";
        var a = await ProvisionAndActivateAsync("Plan A A.Ş.", epostaA);
        var b = await ProvisionAndActivateAsync("Plan B A.Ş.", epostaB);

        await AbonelikYazAsync(a.TenantId, "PRO", "A Planı");
        await AbonelikYazAsync(b.TenantId, "STANDART", "B Planı");

        var ayarlarA = await GetAsync<SettingsDto>(await AuthedClientAsync(epostaA), "/api/settings");
        var ayarlarB = await GetAsync<SettingsDto>(await AuthedClientAsync(epostaB), "/api/settings");

        ayarlarA.Subscription.PlanName.Should().Be("A Planı");
        ayarlarB.Subscription.PlanName.Should().Be("B Planı");
    }

    /// <summary>
    /// Kiracı tamamen silinince aboneliği de silinmeli. "Geri yükleme aboneliği
    /// geri sarmaz" ilkesiyle çelişmez: orada kiracı YAŞAMAYA DEVAM eder, burada
    /// yok edilir. Kalsaydı sahipsiz bir faturalama satırı olur ve aynı slug
    /// yeniden açıldığında yanlış müşteriye bağlanırdı.
    /// </summary>
    [Fact]
    public async Task Purge_AbonelikSatiriniDaSiler()
    {
        var eposta = $"abn-purge-{Guid.NewGuid():N}@abonelik.local";
        var kiraci = await ProvisionAndActivateAsync("Abonelik Silinir A.Ş.", eposta);
        await AbonelikYazAsync(kiraci.TenantId, "PRO", "Silinecek Plan");

        (await PlatformClient().DeleteAsync($"/api/tenants/{kiraci.TenantId}?confirmSlug={kiraci.Slug}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platform.Subscriptions.AnyAsync(s => s.TenantId == kiraci.TenantId)).Should().BeFalse();
    }

    private async Task AbonelikYazAsync(int tenantId, string plan, string planAdi)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

        var mevcut = await platform.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId);
        if (mevcut is null)
        {
            platform.Subscriptions.Add(new Subscription
            {
                TenantId = tenantId, Plan = plan, PlanName = planAdi,
                BillingCycle = "Yıllık", Price = 1000m, CreatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            mevcut.Plan = plan;
            mevcut.PlanName = planAdi;
        }

        await platform.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<bool> TabloVarMiAsync(string katalog, string tablo)
    {
        var kurucu = new SqlConnectionStringBuilder(EkipzenApiFactory.TestConnectionString)
        {
            InitialCatalog = katalog,
        };
        await using var baglanti = new SqlConnection(kurucu.ConnectionString);
        await baglanti.OpenAsync();
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = @ad;";
        komut.Parameters.AddWithValue("@ad", tablo);
        return (int)(await komut.ExecuteScalarAsync())! > 0;
    }
}
