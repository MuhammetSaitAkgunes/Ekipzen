using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Veritabanı sağlayıcısının GERÇEK SQL Server'a karşı uçtan uca doğrulaması:
/// oluştur → var mı → harmanlama doğru mu → düşür → yok mu.
///
/// Bu görevde sağlayıcıyı üretim kodunda kimse ÇAĞIRMIYOR (provizyona Görev 4'te
/// bağlanacak). Yine de burada sınanıyor: yeteneği kullanıldığı anda değil,
/// tek başına doğrulanabildiği anda kanıtlamak, Görev 4'te bir hata çıktığında
/// "sağlayıcı mı, provizyon akışı mı" sorusunu ortadan kaldırır.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class VeritabaniSaglayiciTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    private ITenantDatabaseProvisioner Saglayici() =>
        Factory.Services.GetRequiredService<ITenantDatabaseProvisioner>();

    // Testin kendi katalogu; adı çakışmasın diye rastgele. Alt çizgi dışında
    // ayraç yok — doğrulama zaten sadece [A-Za-z0-9_] kabul ediyor.
    private static string TazeAd() => $"IKPro_Test_Saglayici_{Guid.NewGuid():N}";

    [Fact]
    public async Task Olustur_VarOl_Dusur_YokOl()
    {
        var saglayici = Saglayici();
        var ad = TazeAd();

        try
        {
            (await saglayici.ExistsAsync(ad, CancellationToken.None)).Should().BeFalse(
                "test taze bir adla başlıyor");

            await saglayici.CreateAsync(ad, CancellationToken.None);
            (await saglayici.ExistsAsync(ad, CancellationToken.None)).Should().BeTrue();

            // Harmanlama AÇIKÇA verilmeli — sunucu varsayılanı yerel (Turkish) ile
            // CI (Latin1) arasında ayrışıyor ve bu, Türkçe sıralama/eşitlik
            // davranışını ortamdan ortama değiştirirdi.
            (await HarmanlamaOkuAsync(ad)).Should().Be("Turkish_CI_AS");

            // Kurtarma modeli de açıkça verilmeli. SIMPLE'da işlem günlüğü yedeği
            // ALINAMAZ; yani RESTORE ... STOPAT imkânsız hale gelir ve RPO taahhüdü
            // (1 saat) sessizce çöker. Model sunucunun `model` veritabanından miras
            // alınsaydı bu, sunucudan sunucuya değişirdi — üstelik hiçbir belirti
            // vermeden, ta ki felaket anına kadar.
            (await KurtarmaModeliOkuAsync(ad)).Should().Be("FULL",
                "log yedeği alınamayan bir kiracı veritabanı, saniye hassasiyetinde " +
                "geri yükleme vaadini sessizce geçersiz kılar");

            await saglayici.DropAsync(ad, CancellationToken.None);
            (await saglayici.ExistsAsync(ad, CancellationToken.None)).Should().BeFalse();
        }
        finally
        {
            // Test ortada patlarsa sunucuda artık katalog bırakma.
            await saglayici.DropAsync(ad, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Olustur_IkinciKezCagrilirsaPatlamaz()
    {
        // Yarıda kalmış bir provizyonun yeniden denenmesi ilk adımda patlamamalı.
        var saglayici = Saglayici();
        var ad = TazeAd();

        try
        {
            await saglayici.CreateAsync(ad, CancellationToken.None);
            await FluentActions.Awaiting(() => saglayici.CreateAsync(ad, CancellationToken.None))
                .Should().NotThrowAsync("oluşturma idempotent olmalı");
        }
        finally
        {
            await saglayici.DropAsync(ad, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Dusur_OlmayanKatalogdaPatlamaz()
    {
        // Temizlik yolları (purge, yarım provizyon toplama) "zaten yok" durumunu
        // hata saymamalı; aksi halde tek bir eksik katalog toplu temizliği keser.
        await FluentActions.Awaiting(() => Saglayici().DropAsync(TazeAd(), CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task Dusur_AcikBaglantiVarkenDeCalisir()
    {
        // Gerçek engel bu: kataloğa bir oturum açıksa DROP başarısız olur.
        // SINGLE_USER + ClearAllPools tam olarak bunu çözmek için var.
        var saglayici = Saglayici();
        var ad = TazeAd();
        await saglayici.CreateAsync(ad, CancellationToken.None);

        var kurucu = new SqlConnectionStringBuilder(IKProApiFactory.TestConnectionString)
        {
            InitialCatalog = ad,
        };
        var acikBaglanti = new SqlConnection(kurucu.ConnectionString);
        await acikBaglanti.OpenAsync();

        try
        {
            await FluentActions.Awaiting(() => saglayici.DropAsync(ad, CancellationToken.None))
                .Should().NotThrowAsync("açık oturum silmeyi süresiz engellememeli");
            (await saglayici.ExistsAsync(ad, CancellationToken.None)).Should().BeFalse();
        }
        finally
        {
            await acikBaglanti.DisposeAsync();
            await saglayici.DropAsync(ad, CancellationToken.None);
        }
    }

    private static Task<string> KurtarmaModeliOkuAsync(string ad) =>
        SistemDegeriOkuAsync("SELECT recovery_model_desc FROM sys.databases WHERE name = @ad;", ad);

    private static Task<string> HarmanlamaOkuAsync(string ad) =>
        SistemDegeriOkuAsync("SELECT collation_name FROM sys.databases WHERE name = @ad;", ad);

    private static async Task<string> SistemDegeriOkuAsync(string sorgu, string ad)
    {
        var kurucu = new SqlConnectionStringBuilder(IKProApiFactory.TestConnectionString)
        {
            InitialCatalog = "master",
        };
        await using var baglanti = new SqlConnection(kurucu.ConnectionString);
        await baglanti.OpenAsync();
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = sorgu;
        komut.Parameters.AddWithValue("@ad", ad);
        return (string)(await komut.ExecuteScalarAsync())!;
    }
}
