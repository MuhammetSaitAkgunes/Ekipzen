using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Tenancy.Commands;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Multi-tenant testleri için ortak yardımcılar: kiracı provizyonu, kiracı-kapsamlı
/// doğrudan tohumlama ve authed istemci. Provizyonlanan admin ŞİFRESİZ oluşturulur;
/// davet e-postasındaki token ile <see cref="AcceptInviteAsync"/> çağrılarak
/// (veya kısayol <see cref="ProvisionAndActivateAsync"/>) şifre belirlenir.
/// </summary>
public abstract class TenancyTestBase(EkipzenApiFactory factory) : IAsyncLifetime
{
    protected EkipzenApiFactory Factory { get; } = factory;

    /// <summary>Katalog adı üretici — testlerde önek yapılandırmadan gelir.</summary>
    protected ITenantDatabaseNamer Namer => Factory.Services.GetRequiredService<ITenantDatabaseNamer>();

    // Bu test sırasında provizyonlanan kiracıların katalog adları. xUnit her test
    // metodu için sınıfın YENİ bir örneğini kurduğundan liste test başına temizdir.
    private readonly List<string> _acilanKataloglar = [];

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Her testten sonra o testin açtığı kiracı veritabanını ve platform kaydını temizler.
    ///
    /// Ürün kararı (kullanıcı onaylı): temizlik testten sonra AGRESİF yapılır.
    /// Alternatif — koşum sonunda toplu düşürme — daha hızlıydı ama koşum ortasında
    /// bir çökme sunucuda artık katalog bırakırdı. Burada maliyet koşum süresidir
    /// ve bilinçli ödeniyor: geliştiricinin makinesinde biriken artık veritabanı,
    /// bir süre sonra "neden diskim doldu" sorusuna dönüşür.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_acilanKataloglar.Count == 0)
        {
            return;
        }

        var saglayici = Factory.Services.GetRequiredService<ITenantDatabaseProvisioner>();
        foreach (var katalog in _acilanKataloglar)
        {
            if (!katalog.StartsWith(EkipzenApiFactory.KiraciKatalogOneki, StringComparison.Ordinal))
                throw new InvalidOperationException("Test temizliği yalnız test kataloglarına uygulanabilir.");
            using var cleanupScope = Factory.Services.CreateScope();
            var platform = cleanupScope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var tenant = await platform.Tenants.SingleOrDefaultAsync(x => x.DatabaseName == katalog);
            if (tenant is not null)
            {
                // Yalnız DB'yi düşürmek platformda artık kiracı bırakır; sonraki
                // toplu migration silinmiş kataloğa bağlanıp zaman aşımına uğrar.
                await cleanupScope.ServiceProvider.GetRequiredService<ITenantPurger>()
                    .PurgeAsync(tenant.Id, CancellationToken.None);
            }
            else
            {
                // Testin kendisi kiracıyı purge etmiş olabilir.
                await saglayici.DropAsync(katalog, CancellationToken.None);
            }
        }
    }

    /// <summary>Davet akışında belirlenen varsayılan şifre (login için de kullanılır).</summary>
    protected const string DefaultPassword = "demo123";

    /// <summary>
    /// Platform anahtarını taşıyan istemci. Üç test sınıfında birebir aynı üç satır
    /// olarak tekrarlanıyordu; anahtarın nasıl geçirildiği tek yerde dursun.
    /// </summary>
    protected HttpClient PlatformClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", EkipzenApiFactory.PlatformKey);
        return client;
    }

    /// <summary>Platform anahtarıyla yeni kiracı + ilk hr-admin provizyonlar.</summary>
    protected async Task<ProvisionTenantResult> ProvisionTenantAsync(string companyName, string adminEmail)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", EkipzenApiFactory.PlatformKey);
        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            companyName,
            slug = TestSlug(companyName),
            adminName = $"{companyName} Yöneticisi",
            adminEmail,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"provizyon başarısız: {companyName}");

        var sonuc = (await response.Content.ReadFromJsonAsync<ProvisionTenantResult>())!;
        _acilanKataloglar.Add(Namer.For(sonuc.Slug));
        return sonuc;
    }

    /// <summary>
    /// Ham provizyon isteği (hata senaryoları için). İstek BAŞARILI olursa açılan
    /// katalog izlenir — bu yol da gerçek bir veritabanı kurar ve testin sonunda
    /// temizlenmesi gerekir. İzlemeyi çağıranın sorumluluğuna bırakmak, "bu test
    /// 409 bekliyordu ama 201 aldı" gibi bir durumda sunucuda sessizce artık
    /// bırakırdı.
    /// </summary>
    protected async Task<HttpResponseMessage> ProvisionRawAsync(object body, bool withPlatformKey = true)
    {
        var client = Factory.CreateClient();
        if (withPlatformKey)
        {
            client.DefaultRequestHeaders.Add("X-Platform-Key", EkipzenApiFactory.PlatformKey);
        }

        var response = await client.PostAsJsonAsync("/api/tenants", body);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            // Gövde okunup YERİNE TAZE BİR KOPYASI konur. Okuma akışı tüketiyor;
            // değiştirmeseydik bu yardımcı, çağıranın aynı cevabı okumasını sessizce
            // imkânsız kılardı (ObjectDisposedException) — yani izleme eklemek,
            // ilgisiz testleri bozardı.
            var govde = await response.Content.ReadAsStringAsync();
            response.Content = new StringContent(govde, Encoding.UTF8, "application/json");

            var sonuc = JsonSerializer.Deserialize<ProvisionTenantResult>(
                govde, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (sonuc is not null)
            {
                _acilanKataloglar.Add(Namer.For(sonuc.Slug));
            }
        }

        return response;
    }

    /// <summary>
    /// Verili kiracıyı impersone edip DB'ye doğrudan veri yazar (çapraz-kiracı kurulum).
    /// Üretim kodundaki desenle aynı: kapsam <see cref="ITenantScopeFactory"/> ile açılır,
    /// kiracı kapsam DÖNMEDEN önce sabitlenir — "önce impersone et, sonra çöz" sırasına
    /// güvenmek yerine test altyapısı da üretimin taklit ettiği deseni kullanır.
    /// </summary>
    protected async Task SeedInTenantAsync(int tenantId, Func<AppDbContext, Task> seed)
    {
        using var kapsam = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(tenantId);
        var db = kapsam.Services.GetRequiredService<AppDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    protected async Task<HttpClient> AuthedClientAsync(string email, string password = "demo123")
    {
        var anonymous = Factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"giriş başarısız: {email}");
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    protected static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    /// <summary>Provizyon + davet kabul (şifre belirleme) — çoğu test için tek adımlık kurulum.</summary>
    protected async Task<ProvisionTenantResult> ProvisionAndActivateAsync(
        string companyName, string adminEmail, string password = DefaultPassword)
    {
        var result = await ProvisionTenantAsync(companyName, adminEmail);
        await AcceptInviteAsync(adminEmail, password);
        return result;
    }

    /// <summary>Davet e-postasındaki token'ı okuyup <c>accept-invite</c> ile şifre belirler.</summary>
    protected async Task AcceptInviteAsync(string email, string password = DefaultPassword)
    {
        var token = await ReadInviteTokenAsync(email);
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/accept-invite", new { email, token, newPassword = password });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, $"davet kabulü başarısız: {email}");
    }

    // Outbox'a bırakılan davet e-postasından ilgili adrese ait en yeni token'ı çıkarır.
    private async Task<string> ReadInviteTokenAsync(string email)
    {
        var outbox = Path.Combine(Factory.StorageRoot, "outbox");
        Directory.Exists(outbox).Should().BeTrue($"outbox klasörü yok: {outbox}");

        // En yeni dosyadan geriye tara — aynı adrese birden çok davet gitmiş olabilir.
        foreach (var file in Directory.EnumerateFiles(outbox, "*.json")
                     .OrderByDescending(File.GetCreationTimeUtc))
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(file));
            var root = doc.RootElement;
            if (!string.Equals(root.GetProperty("to").GetString(), email, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var body = root.GetProperty("body").GetString() ?? string.Empty;
            var match = Regex.Match(body, @"DAVET-KODU:\s*(?<token>.+)");
            if (match.Success)
            {
                return match.Groups["token"].Value.Trim();
            }
        }

        throw new InvalidOperationException($"'{email}' için davet e-postası outbox'ta bulunamadı.");
    }

    /// <summary>
    /// Test slug'ı — DAİMA <c>test-</c> ile başlar.
    ///
    /// Önek şart: slug'dan üretilen katalog adı <c>Ekipzen_test_...</c> olur ve
    /// böylece sunucudaki test artıkları hem bir bakışta ayırt edilir hem de
    /// fabrikanın koşum-başı süpürmesi tarafından desenle bulunabilir. Öneksiz
    /// olsaydı süpürme, gerçek bir kiracı veritabanını silme riski taşırdı.
    /// </summary>
    protected static string TestSlug(string companyName) =>
        $"test-{Slugify(companyName)}-{Guid.NewGuid():N}";

    // Slug yalnız ASCII [a-z0-9] olmalı (validator kuralı) — Türkçe/aksanlı harfler atılır.
    private static string Slugify(string name) =>
        new string(name.ToLowerInvariant()
            .Where(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            .ToArray());
}
