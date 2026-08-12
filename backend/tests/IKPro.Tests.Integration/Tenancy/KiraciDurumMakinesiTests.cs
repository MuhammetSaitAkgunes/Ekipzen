using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Tenancy;
using IKPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Kiracının durumunu DEĞİŞTİRMEYE kimin yetkili olduğunu sabitler.
///
/// Kural: durum geçişleri PROVİZYON yolunun işidir. Bir son kullanıcının yaptığı
/// hiçbir şey — özellikle davet kabul edip şifre belirlemek — kiracıyı
/// erişilebilir hale getiremez.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class KiraciDurumMakinesiTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    /// <summary>
    /// Davet kabulü kiracıyı Provisioning'den Active'e ÇEKMEZ.
    ///
    /// Eskiden çekiyordu: self-servis kayıt kiracıyı Provisioning oluşturuyordu ve
    /// ilk admin'in şifre belirlemesi "e-posta doğrulandı" anlamına geldiği için
    /// kiracı orada etkinleşiyordu. Self-servis kayıt kalktı; geriye yalnız
    /// tehlikeli tarafı kalırdı: kiracı başına veritabanında Provisioning'de takılı
    /// kalmış bir kiracı, VERİTABANI HENÜZ KURULMAMIŞ demektir. Onu Active yapmak
    /// kullanıcıyı var olmayan bir kataloğa sokar — hata mesajı da veritabanı
    /// katmanından gelir, yani sebebi anlaşılmaz.
    /// </summary>
    [Fact]
    public async Task DavetKabulu_ProvisioningKiraciyiAktiflestirmez()
    {
        var eposta = $"yarim-{Guid.NewGuid():N}@durum.local";
        var kiraci = await ProvisionTenantAsync("Yarım Provizyon A.Ş.", eposta);

        // Provizyonun ortasında kesilmiş hali: kiracı satırı ve davet var, durum Provisioning.
        await DurumuAyarlaAsync(kiraci.TenantId, TenantStatus.Provisioning);

        await AcceptInviteAsync(eposta);

        (await DurumuOkuAsync(kiraci.TenantId)).Should().Be(TenantStatus.Provisioning,
            "şifre belirlemek bir kiracıyı kullanılabilir yapmaz — bunu yalnız provizyon yolu yapar");
    }

    /// <summary>
    /// Şifre gerçekten belirlenmiş olmalı: yukarıdaki test "hiçbir şey olmadı"
    /// diye de geçebilirdi. Kiracı Active'e alınınca aynı şifreyle giriş çalışıyorsa
    /// davet kabulü işini yapmış, sadece duruma dokunmamış demektir.
    /// </summary>
    [Fact]
    public async Task DavetKabulu_SifreyiYineDeBelirler()
    {
        var eposta = $"sifre-{Guid.NewGuid():N}@durum.local";
        var kiraci = await ProvisionTenantAsync("Şifre Belirlendi A.Ş.", eposta);
        await DurumuAyarlaAsync(kiraci.TenantId, TenantStatus.Provisioning);

        await AcceptInviteAsync(eposta);

        await DurumuAyarlaAsync(kiraci.TenantId, TenantStatus.Active);
        var client = await AuthedClientAsync(eposta);
        client.Should().NotBeNull();
    }

    private async Task DurumuAyarlaAsync(int tenantId, TenantStatus durum)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var tenant = await platform.Tenants.FirstAsync(t => t.Id == tenantId);
        tenant.Status = durum;
        await platform.SaveChangesAsync(CancellationToken.None);
        // Kütük bellekte önbellekliyor; elle değiştirdiğimiz durumu görmesi için düşür.
        scope.ServiceProvider.GetRequiredService<ITenantRegistry>().Invalidate(tenantId);
    }

    private async Task<TenantStatus> DurumuOkuAsync(int tenantId)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        return (await platform.Tenants.FirstAsync(t => t.Id == tenantId)).Status;
    }
}

/// <summary>
/// <see cref="AppDbContextInitializer"/> KİRACIYA SABİTLENMİŞ bir kapsamdan
/// çözülmelidir. Eskiden kendi içinde <c>ICurrentTenant.Impersonate</c> çağırıyordu;
/// bu, "önce impersone et, sonra servisi kullanmaya devam et" sırasına güvenmek
/// demekti ve o sıra bozulduğunda hata FIRLAMADAN yanlış kiracıya yazılırdı.
///
/// Artık kiracı kapsamla birlikte gelir ve gelmezse seed AÇIKÇA reddeder. Bu test
/// o tel tuzağı sabitler: kiracısız kapsamdan çözülüp çalıştırılırsa patlamalı.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SeedKapsamiTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Seed_KiracisizKapsamda_AcikcaReddeder()
    {
        using var scope = Factory.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<AppDbContextInitializer>();

        var calistir = async () => await initializer.SeedAsync();

        await calistir.Should().ThrowAsync<InvalidOperationException>(
            "kiracısı belli olmayan bir kapsamda seed çalışırsa veriyi sessizce yanlış yere yazar");
    }
}
