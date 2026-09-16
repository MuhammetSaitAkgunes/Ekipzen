using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Tenancy.Commands;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Dondurma/çözme uçları: operatörün SQL'e inmeden kiracıyı Frozen ↔ Active
/// arasında geçirebilmesi ve bunun kütüğü ANINDA düşürmesi.
///
/// Kritik fark: <see cref="ErisimKapisiTests"/> kütüğü elle
/// <c>ITenantRegistry.Invalidate</c> çağırarak düşürür — kapının davranışını
/// test eder, kütüğün düşmesini değil. Buradaki testler durum değişikliğini
/// SADECE uç üzerinden yapar; elle Invalidate çağrılmaz. Böylece ucun kendisinin
/// kütüğü düşürdüğü kanıtlanır — aksi halde 5 dakikalık TTL dolana kadar test
/// yanlışlıkla eski (önbellekli) durumu görüp yeşile çıkabilirdi.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TenantFreezeTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Freeze_UcUzerinden_UcYoluDaAninda_Kapatir()
    {
        var (eposta, tenantId) = await AktifKiraciAsync("DondurUc");
        var client = await AuthedClientAsync(eposta);

        // Token dondurmadan ÖNCE alındı ve hâlâ geçerli.
        (await client.GetAsync("/api/departments")).StatusCode.Should().Be(HttpStatusCode.OK);

        var girisYaniti = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });
        var auth = (await girisYaniti.Content.ReadFromJsonAsync<AuthResponse>())!;

        // Durum değişikliği YALNIZ uç üzerinden — elle Invalidate çağrılmıyor.
        var dondurYaniti = await PlatformClient().PostAsync($"/api/tenants/{tenantId}/freeze", null);
        dondurYaniti.StatusCode.Should().Be(HttpStatusCode.OK);
        var dondurSonuc = await dondurYaniti.Content.ReadFromJsonAsync<TenantStatusResult>();
        dondurSonuc!.Status.Should().Be(TenantStatus.Frozen);

        // 1) Elindeki geçerli token'la istek — 403.
        (await client.GetAsync("/api/departments")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "dondurma ucu kütüğü düşürmeliydi; düşürmediyse bu istek hâlâ önbellekten OK dönerdi");

        // 2) Refresh ile oturum uzatma — reddedilmeli.
        (await Factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3) Yeni giriş — reddedilmeli.
        (await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unfreeze_UcUzerinden_UcYoluDaAninda_Acar()
    {
        var (eposta, tenantId) = await AktifKiraciAsync("CozUc");

        (await PlatformClient().PostAsync($"/api/tenants/{tenantId}/freeze", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Dondurulmuşken üçü de reddedilmeli (ön koşulun doğrulaması).
        (await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Durum değişikliği YALNIZ uç üzerinden.
        var cozYaniti = await PlatformClient().PostAsync($"/api/tenants/{tenantId}/unfreeze", null);
        cozYaniti.StatusCode.Should().Be(HttpStatusCode.OK);
        var cozSonuc = await cozYaniti.Content.ReadFromJsonAsync<TenantStatusResult>();
        cozSonuc!.Status.Should().Be(TenantStatus.Active);

        // 1) Yeni giriş tekrar çalışmalı.
        var girisYaniti = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });
        girisYaniti.StatusCode.Should().Be(HttpStatusCode.OK,
            "çözme ucu kütüğü düşürmeliydi; düşürmediyse bu istek hâlâ önbellekten 403 dönerdi");
        var auth = (await girisYaniti.Content.ReadFromJsonAsync<AuthResponse>())!;

        // 2) Aynı access token'la yetkili istek çalışmalı.
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
        (await client.GetAsync("/api/departments")).StatusCode.Should().Be(HttpStatusCode.OK);

        // 3) Refresh tekrar çalışmalı.
        (await Factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Freeze_ZatenDonuk_Idempotent_NoOpDoner()
    {
        var (_, tenantId) = await AktifKiraciAsync("DondurTekrar");
        (await PlatformClient().PostAsync($"/api/tenants/{tenantId}/freeze", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var ikinci = await PlatformClient().PostAsync($"/api/tenants/{tenantId}/freeze", null);
        ikinci.StatusCode.Should().Be(HttpStatusCode.OK, "zaten dondurulmuş kiracıyı tekrar dondurmak hataya çarpmamalı (retry güvenliği)");
        var sonuc = await ikinci.Content.ReadFromJsonAsync<TenantStatusResult>();
        sonuc!.AlreadyInTargetState.Should().BeTrue();
        sonuc.Status.Should().Be(TenantStatus.Frozen);
    }

    [Fact]
    public async Task Unfreeze_ZatenAktif_Idempotent_NoOpDoner()
    {
        var (_, tenantId) = await AktifKiraciAsync("CozTekrar");

        var yanit = await PlatformClient().PostAsync($"/api/tenants/{tenantId}/unfreeze", null);
        yanit.StatusCode.Should().Be(HttpStatusCode.OK, "zaten aktif kiracıyı çözmeye çalışmak hataya çarpmamalı");
        var sonuc = await yanit.Content.ReadFromJsonAsync<TenantStatusResult>();
        sonuc!.AlreadyInTargetState.Should().BeTrue();
        sonuc.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task Freeze_ProvisioningDurumundaKiraci_409Doner()
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var yariKurulu = new Tenant
        {
            Name = "Yari Kurulu A.S.",
            Slug = $"yari-{Guid.NewGuid():N}",
            Status = TenantStatus.Provisioning,
            CreatedAtUtc = DateTime.UtcNow,
        };
        platform.Tenants.Add(yariKurulu);
        await platform.SaveChangesAsync(default);

        var yanit = await PlatformClient().PostAsync($"/api/tenants/{yariKurulu.Id}/freeze", null);
        yanit.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Provisioning durumundaki kiracı yaşam döngüsünün kendi akışına aittir, elle dondurulamaz");
    }

    [Fact]
    public async Task Unfreeze_PurgingDurumundaKiraci_409Doner()
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var siliniyor = new Tenant
        {
            Name = "Siliniyor A.S.",
            Slug = $"siliniyor-{Guid.NewGuid():N}",
            Status = TenantStatus.Purging,
            CreatedAtUtc = DateTime.UtcNow,
        };
        platform.Tenants.Add(siliniyor);
        await platform.SaveChangesAsync(default);

        var yanit = await PlatformClient().PostAsync($"/api/tenants/{siliniyor.Id}/unfreeze", null);
        yanit.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Purging durumundaki kiracı yaşam döngüsünün kendi akışına aittir, elle çözülemez");
    }

    [Fact]
    public async Task Freeze_OlmayanKiraci_404Doner()
    {
        var yanit = await PlatformClient().PostAsync("/api/tenants/999999/freeze", null);
        yanit.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unfreeze_OlmayanKiraci_404Doner()
    {
        var yanit = await PlatformClient().PostAsync("/api/tenants/999999/unfreeze", null);
        yanit.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Freeze_PlatformAnahtariYoksa_401Doner()
    {
        var (_, tenantId) = await AktifKiraciAsync("DondurAnahtarsiz");
        var yanit = await Factory.CreateClient().PostAsync($"/api/tenants/{tenantId}/freeze", null);
        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unfreeze_PlatformAnahtariYoksa_401Doner()
    {
        var (_, tenantId) = await AktifKiraciAsync("CozAnahtarsiz");
        var yanit = await Factory.CreateClient().PostAsync($"/api/tenants/{tenantId}/unfreeze", null);
        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(string Eposta, int TenantId)> AktifKiraciAsync(string ad)
    {
        var eposta = $"{ad.ToLowerInvariant()}-{Guid.NewGuid():N}@ornek.local";
        var kiraci = await ProvisionAndActivateAsync(ad, eposta);
        return (eposta, kiraci.TenantId);
    }

}
