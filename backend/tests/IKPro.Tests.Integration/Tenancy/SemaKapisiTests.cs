using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Tenancy.Commands;
using IKPro.Application.Features.Tenancy.Queries;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Kiracı veritabanları açılışta migrate EDİLMEZ (migration ayrı bir dağıtım
/// adımıdır). Bunun bedeli, adımın atlanabilmesidir; bu dosya o bedelin nasıl
/// karşılandığını sabitler: <b>şeması geride kalan kiracı istekleri 503 ile
/// reddeder.</b>
///
/// Neden reddetmek doğru: uygulama kodu artık var olmayan sütunları sorguluyor.
/// En iyi ihtimalle her istek anlaşılmaz bir SQL hatasıyla düşer; daha kötüsü,
/// kısmen çalışan bir yazma yolu veriyi tutarsız bırakır. Açıkça reddetmek,
/// sessizce bozmaktan iyidir.
///
/// Geride kalmış şema, kiracının <c>__EFMigrationsHistory</c> tablosundan en son
/// satır silinerek TAKLİT ediliyor: EF o migration'ı "bekliyor" sayar. Gerçek bir
/// dağıtımda aynı durum, yeni kod eski veritabanıyla buluşunca oluşur.
///
/// <b>Testler tahribattan sonra kapı önbelleğini AÇIKÇA düşürüyor.</b> Üretimde bu
/// gerekmez: şema ancak bir dağıtımla eskir, dağıtım da süreci yeniden başlatır ve
/// önbellek zaten boş doğar. Test aynı süreç içinde koştuğu için o yeniden
/// başlamayı taklit etmek zorunda. Yani bu dosya kapının DAVRANIŞINI kanıtlıyor;
/// önbelleğin süre dolmasıyla kendiliğinden tazelenmesini kanıtlamıyor.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SemaKapisiTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task SemasiGerideKalanKiraci_IsteklerdeVeGiristе_503Alir()
    {
        var eposta = $"sema-{Guid.NewGuid():N}@sema.local";
        var kiraci = await ProvisionAndActivateAsync("Şema Geride A.Ş.", eposta);
        var katalog = Namer.For(kiraci.Slug);

        // Önce sağlıklı hâli doğrula — sonraki 503'ün sebebi "zaten çalışmıyordu"
        // olmasın.
        var client = await AuthedClientAsync(eposta);
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var silinen = await SonMigrationKaydiniSilAsync(katalog);
        KapiOnbelleginiDusur(kiraci.TenantId);

        try
        {
            // 1) Elinde geçerli token'la gelen istek.
            (await client.GetAsync("/api/me")).StatusCode
                .Should().Be(HttpStatusCode.ServiceUnavailable,
                    "şeması geride kalan kiracıya istek kabul edilmemeli");

            // 2) Token üretimi (login). İki darboğaz da kapalı olmalı; biri açık
            // kalsaydı kullanıcı giriş yapar ve ilk sayfada SQL hatası görürdü.
            var giris = await Factory.CreateClient().PostAsJsonAsync(
                "/api/auth/login", new { email = eposta, password = DefaultPassword });
            giris.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            await MigrationKaydiniGeriYazAsync(katalog, silinen);
        }
    }

    [Fact]
    public async Task SchemaStatus_GerideKalaniVeBekleyenMigrationiGosterir()
    {
        var eposta = $"durum-{Guid.NewGuid():N}@sema.local";
        var kiraci = await ProvisionAndActivateAsync("Durum Raporu A.Ş.", eposta);
        var katalog = Namer.For(kiraci.Slug);

        var silinen = await SonMigrationKaydiniSilAsync(katalog);
        try
        {
            // schema-status kapının önbelleğini kullanmaz; doğrudan veritabanına
            // bakar. Operatörün "gerçekte ne var?" sorusuna önbellekten cevap vermek
            // yanıltıcı olurdu.
            var durumlar = await GetAsync<List<TenantSchemaStatusDto>>(
                PlatformClient(), "/api/tenants/schema-status");

            var bizim = durumlar.Single(d => d.TenantId == kiraci.TenantId);
            bizim.Guncel.Should().BeFalse();
            bizim.BekleyenMigrationlar.Should().Contain(silinen.MigrationId,
                "operatör yalnız 'geride' değil, NEYİN gerisinde olduğunu da görmeli");
        }
        finally
        {
            await MigrationKaydiniGeriYazAsync(katalog, silinen);
        }
    }

    /// <summary>
    /// Migration adımı çalıştıktan sonra kiracı ANINDA hizmete dönmeli — önbellek
    /// süresinin dolmasını beklememeli. Beklenseydi, dağıtımı doğru yapan bir
    /// operatör bile müşterilerinin yarım dakika daha 503 aldığını görürdü.
    /// </summary>
    [Fact]
    public async Task MigrateUcu_CalistiktanSonra_KiraciAnindaGeriDoner()
    {
        var eposta = $"geri-{Guid.NewGuid():N}@sema.local";
        var kiraci = await ProvisionAndActivateAsync("Geri Dönen A.Ş.", eposta);
        var katalog = Namer.For(kiraci.Slug);
        var client = await AuthedClientAsync(eposta);

        var silinen = await SonMigrationKaydiniSilAsync(katalog);
        KapiOnbelleginiDusur(kiraci.TenantId);
        (await client.GetAsync("/api/me")).StatusCode
            .Should().Be(HttpStatusCode.ServiceUnavailable);

        // Dağıtım adımının yaptığı şeyi yap: şemayı tamamla, sonra koşucuyu çağır.
        // (Bu testte migration SQL'i yeniden uygulanamaz — nesneler zaten var — bu
        // yüzden geçmiş kaydı geri yazılıp koşucunun "bekleyen yok" yolundan geçmesi
        // sağlanıyor. Sınanan şey koşucunun ŞEMAYI değil, KAPIYI açması.)
        await MigrationKaydiniGeriYazAsync(katalog, silinen);

        var yanit = await PlatformClient().PostAsync("/api/tenants/migrate", null);
        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
        var sonuc = (await yanit.Content.ReadFromJsonAsync<MigrateTenantsResult>())!;
        sonuc.Basarisiz.Should().Be(0);

        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK,
            "koşucu bitince kiracı beklemeden hizmete dönmeli");
    }

    [Fact]
    public async Task MigrateVeSchemaStatus_PlatformAnahtariIster()
    {
        (await Factory.CreateClient().PostAsync("/api/tenants/migrate", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Factory.CreateClient().GetAsync("/api/tenants/schema-status"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Kapı önbelleğini düşürür — üretimde bunu dağıtımın kendisi (süreç yeniden
    /// başlaması) yapar; test aynı süreçte koştuğu için elle taklit ediyor.
    /// </summary>
    private void KapiOnbelleginiDusur(int tenantId) =>
        Factory.Services.GetRequiredService<ITenantSchemaGuard>().Invalidate(tenantId);

    private sealed record MigrationKaydi(string MigrationId, string ProductVersion);

    private static async Task<MigrationKaydi> SonMigrationKaydiniSilAsync(string katalog)
    {
        await using var baglanti = await AcAsync(katalog);

        await using var oku = baglanti.CreateCommand();
        oku.CommandText =
            "SELECT TOP 1 MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId DESC;";
        MigrationKaydi kayit;
        await using (var reader = await oku.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue("kiracı veritabanında migration geçmişi olmalı");
            kayit = new MigrationKaydi(reader.GetString(0), reader.GetString(1));
        }

        await using var sil = baglanti.CreateCommand();
        sil.CommandText = "DELETE FROM __EFMigrationsHistory WHERE MigrationId = @id;";
        sil.Parameters.AddWithValue("@id", kayit.MigrationId);
        await sil.ExecuteNonQueryAsync();

        return kayit;
    }

    private static async Task MigrationKaydiniGeriYazAsync(string katalog, MigrationKaydi kayit)
    {
        await using var baglanti = await AcAsync(katalog);
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = @id)
                INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (@id, @ver);
            """;
        komut.Parameters.AddWithValue("@id", kayit.MigrationId);
        komut.Parameters.AddWithValue("@ver", kayit.ProductVersion);
        await komut.ExecuteNonQueryAsync();
    }

    private static async Task<SqlConnection> AcAsync(string katalog)
    {
        var kurucu = new SqlConnectionStringBuilder(IKProApiFactory.TestConnectionString)
        {
            InitialCatalog = katalog,
        };
        var baglanti = new SqlConnection(kurucu.ConnectionString);
        await baglanti.OpenAsync();
        return baglanti;
    }
}
