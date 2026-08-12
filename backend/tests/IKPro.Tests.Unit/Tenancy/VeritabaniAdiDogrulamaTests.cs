using FluentAssertions;
using IKPro.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IKPro.Tests.Unit.Tenancy;

/// <summary>
/// Veritabanı adı doğrulaması, enjeksiyona karşı TEK savunmadır.
///
/// <c>CREATE DATABASE</c> ve <c>DROP DATABASE</c> parametre kabul etmez; ad SQL
/// metnine gömülmek zorundadır. Ad ise slug'dan türetilir ve slug istemciden gelir.
/// Bu yüzden doğrulama bir "temizlik" değil, bir güvenlik sınırıdır.
///
/// Testler sunucuya HİÇ bağlanmaz: doğrulama bağlantı açılmadan ÖNCE çalışmalı.
/// Kötü girdinin sunucuya kadar gitmesi, ancak orada reddedilmesi bir kere fazla.
/// </summary>
public class VeritabaniAdiDogrulamaTests
{
    private static SqlServerTenantDatabaseProvisioner Saglayici() =>
        new(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Geçersiz adlar bu dizeye HİÇ ulaşmaz (doğrulama önce çalışır).
                    // Geçerli ad testi ise ulaşır ve bağlanmayı dener — bu yüzden dize
                    // HIZLI başarısız olacak şekilde seçildi: kapalı bir port + 1 sn
                    // zaman aşımı. Aksi halde çözülemeyen bir sunucu adı, birim test
                    // takımına on saniyelerce DNS beklemesi eklerdi.
                    ["ConnectionStrings:DefaultConnection"] =
                        "Data Source=127.0.0.1,1;Initial Catalog=yok;Integrated Security=True;Connect Timeout=1",
                })
                .Build(),
            NullLogger<SqlServerTenantDatabaseProvisioner>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("IKPro acme")]           // boşluk
    [InlineData("IKPro-acme")]           // tire
    [InlineData("IKPro.acme")]           // nokta
    [InlineData("[IKProAcme]")]          // köşeli parantez kaçışını kırma denemesi
    [InlineData("acme]; DROP DATABASE [master")]  // enjeksiyon denemesi
    [InlineData("acme;--")]
    [InlineData("kiracı")]               // ASCII dışı (Türkçe karakter)
    public async Task GecersizAd_SunucuyaHicGitmedenReddedilir(string ad)
    {
        var saglayici = Saglayici();

        // Üç metot da aynı kapıdan geçmeli; biri unutulursa açık orada olurdu.
        await FluentActions.Awaiting(() => saglayici.ExistsAsync(ad, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => saglayici.CreateAsync(ad, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => saglayici.DropAsync(ad, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CokUzunAd_Reddedilir()
    {
        // SQL Server tanımlayıcı sınırı 128; 129 karakter sunucuda değil burada durmalı.
        var uzun = new string('a', 129);

        await FluentActions.Awaiting(() => Saglayici().ExistsAsync(uzun, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GecerliAd_DogrulamayiGecer()
    {
        // Geçerli ad doğrulamadan geçer ve bağlantı denemesine ULAŞIR. Bağlantı
        // hedefi kapalı bir port olduğu için burada bir SQL hatası bekliyoruz —
        // ArgumentException BEKLEMİYORUZ. Testin söylediği tam olarak şu:
        // "IKPro_acme_1" doğrulamada takılmıyor.
        await FluentActions.Awaiting(() => Saglayici().ExistsAsync("IKPro_acme_1", CancellationToken.None))
            .Should().NotThrowAsync<ArgumentException>();
    }
}
