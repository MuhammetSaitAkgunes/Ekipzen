using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Tenancy;
using IKPro.Infrastructure.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IKPro.Tests.Unit.Tenancy;

/// <summary>
/// Bağlantı çözücü, kiracıdan veritabanına giden ayrılma noktasıdır.
///
/// Üç yolu da sabitler: kiracı bağlamı yok → paylaşılan; kiracının katalog adı
/// boş (henüz ayrılmamış) → paylaşılan; katalog adı dolu → O katalog. Ortadaki
/// durum geçiş boyunca NORMAL: ayrılma kiracı kiracı yapılıyor, dolayısıyla bir
/// süre iki tür kiracı yan yana yaşıyor.
///
/// Gerçek veritabanı gerekmez — çözücünün işi dize üretmek.
/// </summary>
public class BaglantiCozucuTests
{
    private const string Paylasilan = "Data Source=localhost;Initial Catalog=IKProDb;Integrated Security=True;Trust Server Certificate=True";

    /// <summary>Kütük yerine geçen basit sözlük: kiracı → katalog adı.</summary>
    private sealed class SahteKutuk(Dictionary<int, string?> kataloglar) : ITenantRegistry
    {
        public string? GetDatabaseName(int tenantId) =>
            kataloglar.TryGetValue(tenantId, out var ad) ? ad : null;

        public Task<TenantStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken)
            => Task.FromResult<TenantStatus?>(TenantStatus.Active);
        public void Invalidate(int tenantId) { }
    }

    private static TenantConnectionResolver Cozucu(
        Dictionary<int, string?>? kataloglar = null, string? paylasilan = Paylasilan)
    {
        var ayarlar = new Dictionary<string, string?>();
        if (paylasilan is not null)
        {
            ayarlar["ConnectionStrings:DefaultConnection"] = paylasilan;
        }

        return new TenantConnectionResolver(
            new ConfigurationBuilder().AddInMemoryCollection(ayarlar).Build(),
            new SahteKutuk(kataloglar ?? []));
    }

    [Fact]
    public void KiraciBaglamiYoksa_PaylasilanDizeyiDoner()
    {
        // Migration, platform işlemleri ve login'in dizin sorgusundan ÖNCEKİ anı
        // bu yoldan geçer; kiracı bilinmeden de bir veritabanı gerekir.
        Cozucu().ResolveFor(null).Should().Be(Paylasilan);
    }

    [Fact]
    public void KatalogAdiBosKiraci_PaylasilanDizeyiDoner()
    {
        var cozucu = Cozucu(new Dictionary<int, string?> { [1] = null, [2] = "  " });

        cozucu.ResolveFor(1).Should().Be(Paylasilan, "henüz ayrılmamış kiracı paylaşılan veritabanında");
        cozucu.ResolveFor(2).Should().Be(Paylasilan, "boşluktan ibaret ad da 'yok' sayılmalı");
        cozucu.ResolveFor(99).Should().Be(Paylasilan, "kütüğün tanımadığı kiracı da paylaşılana düşmeli");
    }

    [Fact]
    public void KatalogAdiDoluKiraci_KendiKataloguyla_AyniSunucuyaBaglanir()
    {
        var cozucu = Cozucu(new Dictionary<int, string?> { [7] = "IKPro_acme" });

        var dize = new SqlConnectionStringBuilder(cozucu.ResolveFor(7));
        var paylasilanKurucu = new SqlConnectionStringBuilder(Paylasilan);

        dize.InitialCatalog.Should().Be("IKPro_acme");
        // Katalog DIŞINDA hiçbir şey değişmemeli: sunucu ve kimlik ayarları
        // paylaşılan dizeden gelir, kiracı başına ayrı dize tutulmaz.
        dize.DataSource.Should().Be(paylasilanKurucu.DataSource);
        dize.IntegratedSecurity.Should().Be(paylasilanKurucu.IntegratedSecurity);
        dize.TrustServerCertificate.Should().Be(paylasilanKurucu.TrustServerCertificate);
    }

    [Fact]
    public void IkiFarkliKiraci_FarkliKataloglaraGider()
    {
        var cozucu = Cozucu(new Dictionary<int, string?> { [1] = "IKPro_a", [2] = "IKPro_b" });

        new SqlConnectionStringBuilder(cozucu.ResolveFor(1)).InitialCatalog.Should().Be("IKPro_a");
        new SqlConnectionStringBuilder(cozucu.ResolveFor(2)).InitialCatalog.Should().Be("IKPro_b");
    }

    [Fact]
    public void BaglantiDizesiYoksa_AnlamliHataVerir()
    {
        FluentActions.Invoking(() => Cozucu(paylasilan: null).ResolveFor(1))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*DefaultConnection*");
    }
}
