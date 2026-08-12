using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Kiracı kütüğü: durum bellekte önbelleklenir ki her istek platform
/// veritabanına gitmesin. Ama durum değiştiğinde ANINDA düşmeli — dondurma
/// işleminin bir sonraki istekte etkili olması buna bağlı.
/// </summary>
[Collection(ApiCollection.Name)]
public class KiraciKutuguTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Kutuk_KiracininDurumunuDoner()
    {
        var kiraci = await ProvisionAndActivateAsync("Kutuk", $"kut-{Guid.NewGuid():N}@ornek.local");

        using var scope = Factory.Services.CreateScope();
        var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();

        (await kutuk.GetStatusAsync(kiraci.TenantId, default)).Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task Kutuk_VarOlmayanKiraciIcinNullDoner()
    {
        using var scope = Factory.Services.CreateScope();
        var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();

        (await kutuk.GetStatusAsync(-999, default)).Should().BeNull();
    }

    [Fact]
    public async Task Kutuk_DurumDegisipDusurulunce_YeniDurumuDoner()
    {
        var kiraci = await ProvisionAndActivateAsync("KutukDusur", $"kdz-{Guid.NewGuid():N}@ornek.local");

        // Önce önbelleğe girsin.
        using (var scope = Factory.Services.CreateScope())
        {
            var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            (await kutuk.GetStatusAsync(kiraci.TenantId, default)).Should().Be(TenantStatus.Active);
        }

        // Veritabanında değiştir ve kütüğü düşür.
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var satir = await platform.Tenants.FirstAsync(t => t.Id == kiraci.TenantId);
            satir.Status = TenantStatus.Frozen;
            await platform.SaveChangesAsync(default);

            scope.ServiceProvider.GetRequiredService<ITenantRegistry>().Invalidate(kiraci.TenantId);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            (await kutuk.GetStatusAsync(kiraci.TenantId, default)).Should().Be(TenantStatus.Frozen,
                "düşürülen kayıt bir sonraki okumada veritabanından tazelenmeli");
        }
    }

    /// <summary>
    /// Kütük katalog adını da taşır — bağlantı çözücünün tek kaynağı burasıdır.
    ///
    /// Provizyonlanan kiracıda ad HENÜZ BOŞTUR (fiziksel ayrılma Görev 4/5'te
    /// başlıyor) ve bu bir eksiklik değil, geçişin tasarımıdır: boş ad
    /// "paylaşılan veritabanında" demek. Test iki tarafı da sabitliyor —
    /// boşken null, doldurulup kütük düşürülünce yeni değer.
    /// </summary>
    [Fact]
    public async Task Kutuk_KatalogAdiniDoner_VeDusurulunceTazelenir()
    {
        var kiraci = await ProvisionAndActivateAsync("Katalog", $"kat-{Guid.NewGuid():N}@ornek.local");

        using var scope = Factory.Services.CreateScope();
        var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();

        kutuk.GetDatabaseName(kiraci.TenantId).Should().BeNull(
            "yeni kiracı henüz ayrılmadı; boş ad paylaşılan veritabanı demektir");

        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var tenant = await platform.Tenants.FirstAsync(t => t.Id == kiraci.TenantId);
        tenant.DatabaseName = "IKPro_katalog_testi";
        await platform.SaveChangesAsync(CancellationToken.None);

        kutuk.GetDatabaseName(kiraci.TenantId).Should().BeNull(
            "önbellek düşürülmeden eski cevap geçerli kalmalı — kütüğün varlık sebebi bu");

        kutuk.Invalidate(kiraci.TenantId);
        kutuk.GetDatabaseName(kiraci.TenantId).Should().Be("IKPro_katalog_testi");
    }

    /// <summary>
    /// Durum ve katalog adı TEK önbellek kaydından gelir. Ayrı kayıtlar olsaydı
    /// biri düşüp diğeri kalabilir ve kütük kendiyle çelişen bir cevap verirdi.
    /// </summary>
    [Fact]
    public async Task Kutuk_DurumVeKatalogAdi_AyniKayittanGelir()
    {
        var kiraci = await ProvisionAndActivateAsync("Tek Kayıt", $"tek-{Guid.NewGuid():N}@ornek.local");

        using var scope = Factory.Services.CreateScope();
        var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

        var tenant = await platform.Tenants.FirstAsync(t => t.Id == kiraci.TenantId);
        tenant.DatabaseName = "IKPro_tek_kayit";
        tenant.Status = TenantStatus.Frozen;
        await platform.SaveChangesAsync(CancellationToken.None);
        kutuk.Invalidate(kiraci.TenantId);

        // Önce katalog adı sorulur (önbelleği senkron yol doldurur), sonra durum:
        // durum artık VERİTABANINA GİTMEDEN aynı kayıttan gelmeli ve güncel olmalı.
        kutuk.GetDatabaseName(kiraci.TenantId).Should().Be("IKPro_tek_kayit");
        (await kutuk.GetStatusAsync(kiraci.TenantId, default)).Should().Be(TenantStatus.Frozen);
    }
}
