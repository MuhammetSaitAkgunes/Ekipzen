using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Entities.Tenancy;
using IKPro.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Provizyon artık GERÇEK bir veritabanı kuruyor. Bu dosya o iddianın kanıtı:
/// katalog var mı, şema uygulandı mı, veri gerçekten oraya mı yazılıyor.
///
/// Buradaki testler pahalıdır (her biri bir CREATE DATABASE + tüm migration'lar).
/// Sayıları bilinçli olarak az tutulmuştur; ucuz varyasyonlar (farklı isim, farklı
/// rol) hiçbir şey kanıtlamaz, yalnız koşum süresini şişirirdi.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProvizyonVeritabaniKurarTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Provizyon_KiraciyaAitKatalogOlusturur_VeSatirinaYazar()
    {
        var kiraci = await ProvisionTenantAsync("Katalog Kuran A.Ş.", $"kk-{Guid.NewGuid():N}@test.local");
        var beklenenAd = Namer.For(kiraci.Slug);

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var tenant = await platform.Tenants.FirstAsync(t => t.Id == kiraci.TenantId);

        tenant.DatabaseName.Should().Be(beklenenAd);
        tenant.Status.Should().Be(TenantStatus.Active, "tüm adımlar bitince kiracı kullanılabilir olmalı");

        var saglayici = Factory.Services.GetRequiredService<ITenantDatabaseProvisioner>();
        (await saglayici.ExistsAsync(beklenenAd, CancellationToken.None)).Should().BeTrue(
            "katalog fiziksel olarak var olmalı");
    }

    /// <summary>
    /// Asıl iddia: kiracının verisi PAYLAŞILAN veritabanında DEĞİL, kendi
    /// katalogunda. Bunu sorgu filtresiyle değil, iki ayrı bağlantıyla ölçüyoruz —
    /// filtre doğru çalışsa bile veri yanlış yerde olabilirdi.
    /// </summary>
    [Fact]
    public async Task Kiracinin_Verisi_KendiKatalogunda_PaylasilandaDegil()
    {
        var eposta = $"veri-{Guid.NewGuid():N}@test.local";
        var kiraci = await ProvisionAndActivateAsync("Veri Yeri A.Ş.", eposta);
        var katalog = Namer.For(kiraci.Slug);

        var bolumAdi = $"Bolum-{Guid.NewGuid():N}";
        await SeedInTenantAsync(kiraci.TenantId, db =>
        {
            db.Departments.Add(new Department { Name = bolumAdi });
            return Task.CompletedTask;
        });

        (await BolumSayisiAsync(katalog, bolumAdi)).Should().Be(1,
            "veri kiracının kendi katalogunda olmalı");
        (await BolumSayisiAsync("IKProDb_Test", bolumAdi)).Should().Be(0,
            "veri paylaşılan veritabanında OLMAMALI — ayrılmanın bütün anlamı bu");
    }

    /// <summary>
    /// Şema gerçekten uygulanmış olmalı: admin giriş yapabiliyor ve ROLÜ var.
    /// Rol tohumlaması atlanırsa AddToRoleAsync sessizce düşer, kullanıcı oluşur,
    /// giriş yapar ve hiçbir şey göremez — en sinsi arıza budur, bu yüzden ayrı
    /// bir test.
    /// </summary>
    [Fact]
    public async Task YeniKiracinin_AdminiGirisYapar_VeRolununYetkisiniKullanir()
    {
        var eposta = $"rol-{Guid.NewGuid():N}@test.local";
        await ProvisionAndActivateAsync("Rollü A.Ş.", eposta);

        var client = await AuthedClientAsync(eposta);

        // /api/departments hr-admin yetkisi ister; rol atanmamış olsaydı 403 gelirdi.
        (await client.GetAsync("/api/departments")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Yarıda kalmış bir kurulumdan artakalan katalog SESSİZCE yeniden
    /// kullanılmamalı: iki müşterinin verisinin aynı yerde toplanması, bu ürünün
    /// yapabileceği en pahalı hatadır.
    /// </summary>
    [Fact]
    public async Task Katalog_ZatenVarsa_ProvizyonReddedilir()
    {
        var slug = TestSlug("artik");
        var katalog = Namer.For(slug);
        var saglayici = Factory.Services.GetRequiredService<ITenantDatabaseProvisioner>();

        try
        {
            // Yarıda kalmış kurulumun bıraktığı artığı taklit et.
            await saglayici.CreateAsync(katalog, CancellationToken.None);

            var yanit = await ProvisionRawAsync(new
            {
                companyName = "Artık A.Ş.",
                slug,
                adminName = "X",
                adminEmail = $"artik-{Guid.NewGuid():N}@test.local",
            });

            yanit.StatusCode.Should().Be(HttpStatusCode.Conflict,
                "var olan katalog sessizce yeniden kullanılmamalı");
        }
        finally
        {
            await saglayici.DropAsync(katalog, CancellationToken.None);
        }
    }

    /// <summary>
    /// Başarısız provizyon geriye ARTIK BIRAKMAMALI — ne kiracı satırı ne katalog.
    ///
    /// <b>Ne kanıtlar, ne kanıtlamaz:</b> buradaki çakışma <c>EmailExistsAsync</c>
    /// ÖN KONTROLÜNDE yakalanır, yani hiçbir şey yazılmadan. Bu testin ölçtüğü şey
    /// budur: ön kontrol gerçekten en başta duruyor. Adım 1'in transaction'ını
    /// (kiracı satırı yazıldıktan SONRA rezervasyonun çakışması — TOCTOU yolu)
    /// bu test tetiklemez; onu <c>PlatformKatmaniTests</c> içindeki dizin-yarışı
    /// testi tetikler ve orada da satırın geri alındığı doğrulanır.
    /// </summary>
    [Fact]
    public async Task EpostaCakismasi_KiraciSatiriBirakmaz()
    {
        var eposta = $"cakisan-{Guid.NewGuid():N}@test.local";
        await ProvisionAndActivateAsync("İlk Sahip A.Ş.", eposta);

        var ikinciSlug = TestSlug("cakisan");
        var yanit = await ProvisionRawAsync(new
        {
            companyName = "İkinci Talip A.Ş.",
            slug = ikinciSlug,
            adminName = "X",
            adminEmail = eposta,
        });
        yanit.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platform.Tenants.AnyAsync(t => t.Slug == ikinciSlug)).Should().BeFalse(
            "başarısız provizyon geriye kiracı satırı bırakmamalı");

        var saglayici = Factory.Services.GetRequiredService<ITenantDatabaseProvisioner>();
        (await saglayici.ExistsAsync(Namer.For(ikinciSlug), CancellationToken.None))
            .Should().BeFalse("başarısız provizyon geriye katalog da bırakmamalı");
    }

    private static async Task<int> BolumSayisiAsync(string katalog, string bolumAdi)
    {
        var kurucu = new SqlConnectionStringBuilder(IKProApiFactory.TestConnectionString)
        {
            InitialCatalog = katalog,
        };
        await using var baglanti = new SqlConnection(kurucu.ConnectionString);
        await baglanti.OpenAsync();
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = "SELECT COUNT(*) FROM Departments WHERE Name = @ad;";
        komut.Parameters.AddWithValue("@ad", bolumAdi);
        return (int)(await komut.ExecuteScalarAsync())!;
    }
}
