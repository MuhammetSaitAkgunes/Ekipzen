using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Tests.Integration.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Ekipzen.Tests.Integration.Auth;

/// <summary>
/// Kimlik katmanı, kiracı veritabanları ayrıldığında (Faz 2b) çalışacak şekilde
/// kurgulanır. Bu fazda veri BÖLÜNMEZ — testler yapının doğruluğunu sınar,
/// davranış değişikliğini değil.
/// </summary>
[Collection(ApiCollection.Name)]
public class KimlikKiraciKapsamiTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    /// <summary>
    /// Login, ambient kapsamda kiracı OLMADAN çalışabilmeli: anonim bir istekte
    /// JWT yok, dolayısıyla <c>ICurrentTenant</c> boş. Kiracı yalnız dizinden
    /// çözülüp kapsam ORADAN açıldığı için giriş çalışır. Faz 2b'de ambient kapsam
    /// yanlış (ya da hiçbir) katalogda olacağından bu koruma değerlidir.
    /// </summary>
    [Fact]
    public async Task Login_AmbientKapsamdaKiraciYokkenDeCalisir()
    {
        var eposta = $"ak-{Guid.NewGuid():N}@ornek.local";
        await ProvisionAndActivateAsync("AmbientKapsam", eposta);

        var yanit = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Davet kabulü de anonim bir istektir ve kullanıcı kiracı veritabanındadır:
    /// token doğrulaması onu bulabilmek için kiracıya sabitlenmiş kapsamda
    /// çalışmalı. Kiracı yine dizinden çözülür.
    /// </summary>
    [Fact]
    public async Task DavetKabulu_KiraciKapsamindaCalisir()
    {
        var eposta = $"dk-{Guid.NewGuid():N}@ornek.local";
        await ProvisionTenantAsync("DavetKapsam", eposta);

        await AcceptInviteAsync(eposta);

        var giris = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        giris.StatusCode.Should().Be(HttpStatusCode.OK, "davet kabulü sonrası giriş çalışmalı");
    }

    /// <summary>
    /// Kilitlemenin uçtan uca çalıştığını doğrular: beş hatalı denemeden sonra
    /// DOĞRU şifre bile reddedilmeli ve reddin sebebi "şifre hatalı" değil
    /// KİLİTLENME olmalı. Sayaç kalıcı yazılmazsa bu test kırılır.
    ///
    /// Politika: 5 başarısız denemede 5 dakika kilit (bkz. DependencyInjection).
    ///
    /// KAPSAM İDDİASI DEĞİLDİR: bu testin <c>UserManager</c> ile
    /// <c>SignInManager</c>'ın aynı kapsamdan çözüldüğünü kanıtladığını
    /// varsaymayın. Faz 2a'da tüm kiracılar aynı fiziksel veritabanını paylaşıyor
    /// (<c>TenantConnectionResolver</c> <c>tenantId</c>'yi yok sayıyor), dolayısıyla
    /// ayrı bir kapsam da sayacı AYNI tabloya yazardı ve test yeşil kalırdı.
    /// Aynı-kapsam garantisi testte değil, <c>IdentityService.KimlikServisleri</c>'nin
    /// ikisini tek ifadede tek kapsamdan döndürmesinde — yapısal olarak sağlanır.
    /// </summary>
    [Fact]
    public async Task Kilitleme_BesBasarisizDenemedenSonraDogruSifreyiDeReddeder()
    {
        var eposta = $"kilit-{Guid.NewGuid():N}@ornek.local";
        await ProvisionAndActivateAsync("KilitKapsam", eposta);

        var client = Factory.CreateClient();

        for (var i = 1; i <= 5; i++)
        {
            var yanlis = await client.PostAsJsonAsync(
                "/api/auth/login", new { email = eposta, password = "kesinlikle-yanlis" });
            yanlis.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{i}. hatalı deneme reddedilmeli");
        }

        // DOĞRU şifreyle dene: sayaç kalıcı yazıldıysa hesap artık kilitli olmalı.
        var dogruSifre = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = eposta, password = DefaultPassword });

        dogruSifre.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "beş hatalı denemeden sonra doğru şifre bile reddedilmeli");

        var govde = await dogruSifre.Content.ReadAsStringAsync();
        govde.Should().Contain("kilitlendi",
            "reddin sebebi 'şifre hatalı' değil KİLİTLENME olmalı — aksi halde sayaç " +
            "hiç yazılmamış demektir ve UserManager ile SignInManager ayrı kapsamlardan geliyordur");
    }

    /// <summary>
    /// Refresh token PLATFORM veritabanında durmalı: elinde yalnız token dizesi
    /// olan sunucu, kiracı DB'leri ayrıldığında hangi katalogda arayacağını
    /// başka türlü bilemez. Satırdaki TenantId yönlendirmeyi verir.
    /// </summary>
    [Fact]
    public async Task RefreshToken_PlatformVeritabaninaYazilir()
    {
        var eposta = $"rt-{Guid.NewGuid():N}@ornek.local";
        var kiraci = await ProvisionAndActivateAsync("RefreshPlatform", eposta);

        var girisYaniti = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });
        girisYaniti.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

        var kayit = await platform.RefreshTokens
            .Where(t => t.TenantId == kiraci.TenantId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync();

        kayit.Should().NotBeNull("refresh token platform veritabanında olmalı");
        kayit!.TenantId.Should().Be(kiraci.TenantId, "satır kendi kiracısını taşımalı — yönlendirme buna dayanır");
    }

    /// <summary>Taşıma sonrası refresh akışının bozulmadığını uçtan uca doğrular.</summary>
    [Fact]
    public async Task Refresh_TasimaSonrasiCalismayaDevamEder()
    {
        var eposta = $"rt2-{Guid.NewGuid():N}@ornek.local";
        await ProvisionAndActivateAsync("RefreshCalisir", eposta);

        var giris = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });
        var auth = (await giris.Content.ReadFromJsonAsync<AuthResponse>())!;

        var yenile = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });

        yenile.StatusCode.Should().Be(HttpStatusCode.OK);
        var yeni = (await yenile.Content.ReadFromJsonAsync<AuthResponse>())!;
        yeni.Token.Should().NotBeNullOrWhiteSpace();
        yeni.RefreshToken.Should().NotBe(auth.RefreshToken, "rotasyon: eski token tek kullanımlıktır");
    }

    /// <summary>Rotasyon sonrası eski token'ın reddedildiğini doğrular (taşıma bunu bozmamalı).</summary>
    [Fact]
    public async Task Refresh_EskiTokenRotasyondanSonraReddedilir()
    {
        var eposta = $"rt3-{Guid.NewGuid():N}@ornek.local";
        await ProvisionAndActivateAsync("RefreshRotasyon", eposta);

        var giris = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });
        var auth = (await giris.Content.ReadFromJsonAsync<AuthResponse>())!;

        (await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken }))
            .EnsureSuccessStatusCode();

        var ikinciDeneme = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });

        ikinciDeneme.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
