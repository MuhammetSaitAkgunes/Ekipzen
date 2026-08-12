using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Auth;
using IKPro.Tests.Integration.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IKPro.Tests.Integration.Auth;

/// <summary>
/// Kimlik katmanı, kiracı veritabanları ayrıldığında (Faz 2b) çalışacak şekilde
/// kurgulanır. Bu fazda veri BÖLÜNMEZ — testler yapının doğruluğunu sınar,
/// davranış değişikliğini değil.
/// </summary>
[Collection(ApiCollection.Name)]
public class KimlikKiraciKapsamiTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
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
