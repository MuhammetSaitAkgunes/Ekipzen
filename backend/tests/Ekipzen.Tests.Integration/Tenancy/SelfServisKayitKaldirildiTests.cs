using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Güvenlik regresyon testi: anonim self-servis şirket kaydı
/// (<c>POST /api/tenants/signup</c>) KALDIRILDI ve geri gelmemeli.
///
/// Gerekçe: kiracı başına ayrı veritabanına geçildikten sonra "kaydol" bir satır
/// değil bir VERİTABANI oluşturmak demektir. Ucu anonim bırakmak, internetten
/// herhangi birinin bir script'le SQL Server'a istediği kadar veritabanı
/// kurdurabilmesi anlamına gelirdi — disk, yedekleme penceresi ve lisans
/// maliyeti saldırganın elinde olurdu. Veritabanı oluşturmak otomatik bir yan
/// etki değil, operatörün bilinçli eylemidir.
///
/// Bu dosya, aynı yerdeki eski <c>SelfServiceSignupTests</c>'in yerini alır:
/// orada ucun ÇALIŞTIĞI doğrulanıyordu, burada OLMADIĞI doğrulanıyor.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SelfServisKayitKaldirildiTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    /// <summary>
    /// Uca yapılan istek başarısız olmalı ve —asıl önemlisi— ARKASINDA KİRACI
    /// OLUŞMAMALI.
    ///
    /// Durum kodu bilerek sabitlenmiyor: ASP.NET bu yola bugün 405 döndürüyor
    /// (404 değil), çünkü iki segmentli yol var olan bir rota şablonuna başka bir
    /// fiille denk düşüyor. Kodu teste çakmak bizim sözleşmemizi değil ASP.NET'in
    /// yönlendiricisini test etmek olurdu; sözleşme "böyle bir uç yok"tur ve
    /// bunun tek ölçülebilir karşılığı "istek başarısız + yan etki sıfır"dır.
    /// </summary>
    [Fact]
    public async Task Signup_Kaldirildi_KiraciOlusturmaz()
    {
        const string sirket = "Davetsiz A.Ş.";
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/tenants/signup",
            new { companyName = sirket, adminName = "X", adminEmail = $"x-{Guid.NewGuid():N}@test.local" });

        response.IsSuccessStatusCode.Should().BeFalse(
            "POST /api/tenants/signup artık mevcut olmamalı — kiracı açmak yalnız platform anahtarıyla yapılır");

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platform.Tenants.AnyAsync(t => t.Name == sirket)).Should().BeFalse(
            "anonim istek hiçbir koşulda kiracı satırı (ve Faz 2b'den sonra veritabanı) oluşturmamalı");
    }

    /// <summary>
    /// Kiracı açmanın TEK yolunun platform anahtarı olduğunu doğrular: anahtarsız
    /// istek 401 döner (404 değil — uç vardır, çağıran yetkisizdir), anahtarlı
    /// istek 201. İki taraf birlikte olmadan test bir şey söylemez: yalnız 401
    /// görmek ucun bozuk olmasıyla da açıklanabilirdi.
    /// </summary>
    [Fact]
    public async Task Provizyon_YalnizPlatformAnahtariylaCalisir()
    {
        var anahtarsiz = await ProvisionRawAsync(new
        {
            companyName = "Anahtarsız A.Ş.",
            slug = TestSlug("anahtarsiz"),
            adminName = "X",
            adminEmail = $"anahtarsiz-{Guid.NewGuid():N}@test.local",
        }, withPlatformKey: false);
        anahtarsiz.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "platform anahtarı olmadan kiracı açılamamalı");

        await ProvisionTenantAsync("Anahtarlı A.Ş.", $"anahtarli-{Guid.NewGuid():N}@test.local");
    }
}
