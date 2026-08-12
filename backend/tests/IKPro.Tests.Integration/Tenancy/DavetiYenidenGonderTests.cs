using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// <c>POST /api/tenants/{id}/resend-invite</c> — operatörün kurtarma aracı.
///
/// Neden gerekli: provizyon altı adımlıdır ve davet e-postası 5. adımdadır. O adım
/// başarısız olduğunda (SMTP arızası, spam kutusu, yanlış adres) geriye kullanıcısı
/// oluşmuş ama girişi imkânsız bir kiracı kalıyordu. Bu uçtan önce operatörün tek
/// çaresi kiracıyı silip baştan kurmaktı — bir e-posta gitmedi diye veri silmek.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DavetiYenidenGonderTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task SifresizHesap_YeniDavetleGirisYapabilir()
    {
        var eposta = $"kayip-{Guid.NewGuid():N}@davet.local";
        var kiraci = await ProvisionTenantAsync("Kayıp Davet A.Ş.", eposta);

        // İlk davet gitti ama kullanıcıya ulaşmadı diyelim — operatör yeniden gönderiyor.
        var yanit = await PlatformClient().PostAsJsonAsync(
            $"/api/tenants/{kiraci.TenantId}/resend-invite", new { email = eposta });
        yanit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Asıl kanıt: YENİ token gerçekten çalışıyor ve hesap kullanılabilir hale geliyor.
        // (AcceptInviteAsync outbox'taki EN YENİ daveti okur.)
        await AcceptInviteAsync(eposta);
        var client = await AuthedClientAsync(eposta);
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Şifresi olan hesaba davet gönderilmez. Davet, hesabı İLK KEZ kurmak içindir;
    /// etkin bir hesabın parolasını bu uçtan yenilemek, operatöre kullanıcı parolası
    /// sıfırlatma yetkisi vermek olurdu — ayrı ve bilinçli olarak tasarlanması
    /// gereken bir akış.
    /// </summary>
    [Fact]
    public async Task EtkinHesap_DavetiReddeder()
    {
        var eposta = $"etkin-{Guid.NewGuid():N}@davet.local";
        var kiraci = await ProvisionAndActivateAsync("Etkin Hesap A.Ş.", eposta);

        var yanit = await PlatformClient().PostAsJsonAsync(
            $"/api/tenants/{kiraci.TenantId}/resend-invite", new { email = eposta });

        yanit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PlatformAnahtariYoksa_401Doner()
    {
        var eposta = $"anahtarsiz-{Guid.NewGuid():N}@davet.local";
        var kiraci = await ProvisionTenantAsync("Anahtarsız Davet A.Ş.", eposta);

        var yanit = await Factory.CreateClient().PostAsJsonAsync(
            $"/api/tenants/{kiraci.TenantId}/resend-invite", new { email = eposta });

        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BaskaKiracininEpostasi_404Doner()
    {
        var kiraci = await ProvisionTenantAsync("Yabancı A.Ş.", $"yabanci-{Guid.NewGuid():N}@davet.local");

        var yanit = await PlatformClient().PostAsJsonAsync(
            $"/api/tenants/{kiraci.TenantId}/resend-invite",
            new { email = $"hicyok-{Guid.NewGuid():N}@davet.local" });

        yanit.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
