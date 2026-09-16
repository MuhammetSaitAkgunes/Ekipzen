using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Login, kiracıyı yönlendirme dizininden çözer. Faz 2'de kullanıcı tablosu
/// kiracı veritabanında olacağı için, hangi veritabanına bakılacağını bilmeden
/// kullanıcı aranamaz — dizin bu yüzden login'in ÖN adımıdır.
/// </summary>
[Collection(ApiCollection.Name)]
public class LoginDizinTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task DizindeOlmayanEposta_GirisYapamaz()
    {
        var eposta = $"dizinsiz-{Guid.NewGuid():N}@ornek.local";
        var kiraci = await ProvisionAndActivateAsync("LoginDizin", eposta);

        // Dizin kaydını sil: kullanıcı Identity'de duruyor ama yönlendirme yok.
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var anahtar = TenantDirectoryEntry.Normalize(eposta);
            var kayit = await platform.Directory.FirstAsync(d => d.NormalizedEmail == anahtar);
            platform.Directory.Remove(kayit);
            await platform.SaveChangesAsync(default);
        }

        var yanit = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "dizin yönlendirmesi olmadan hangi kiracıya bakılacağı bilinemez");

        // Kurtarma yolu çalışmalı: dizin yeniden kurulunca giriş geri gelmeli.
        var platformClient = Factory.CreateClient();
        platformClient.DefaultRequestHeaders.Add("X-Platform-Key", EkipzenApiFactory.PlatformKey);
        (await platformClient.PostAsync($"/api/tenants/{kiraci.TenantId}/rebuild-directory", null))
            .EnsureSuccessStatusCode();

        var ikinciDeneme = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        ikinciDeneme.StatusCode.Should().Be(HttpStatusCode.OK,
            "dizin yeniden kurulduktan sonra giriş çalışmalı — kurtarma prosedürünün kanıtı");
    }

    [Fact]
    public async Task DizinBaskaKiraciyaIsaretEderse_GirisReddedilir()
    {
        var eposta = $"tutarsiz-{Guid.NewGuid():N}@ornek.local";
        await ProvisionAndActivateAsync("LoginDizinTutarsiz-A", eposta);

        // İkinci (ilgisiz) bir kiracı daha provizyonla — dizin kaydını buna
        // çevirmek için gerçek, var olan ama YANLIŞ bir TenantId lazım.
        var digerKiraci = await ProvisionTenantAsync(
            "LoginDizinTutarsiz-B", $"diger-{Guid.NewGuid():N}@ornek.local");

        // Dizin kaydını diğer kiracıya çevir: kullanıcı Identity'de kendi
        // (doğru) TenantId'siyle duruyor ama dizin başka bir kiracıyı gösteriyor.
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var anahtar = TenantDirectoryEntry.Normalize(eposta);
            var kayit = await platform.Directory.FirstAsync(d => d.NormalizedEmail == anahtar);
            kayit.TenantId = digerKiraci.TenantId;
            await platform.SaveChangesAsync(default);
        }

        var yanit = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "dizin ile Identity'nin kendi kaydı uyuşmuyorsa (bütünlük hatası) giriş reddedilmeli — " +
            "Faz 2'de bu yanlış kiracının veritabanına bakmak demek olurdu");

        var govde = await yanit.Content.ReadAsStringAsync();
        govde.Should().NotContain(eposta,
            "genel hata mesajı dönmeli — hesabın var olduğu ya da iç tutarsızlığın niteliği sızmamalı");
    }
}
