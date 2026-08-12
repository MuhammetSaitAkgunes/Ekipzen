using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Tenancy.Commands;
using IKPro.Application.Features.Tenancy.Queries;
using IKPro.Domain.Entities.Tenancy;
using IKPro.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Platform katmanı: kiracı kimliği uygulama veritabanından ayrı, kendi
/// veritabanında tutulur. Bu fazda kiracı VERİSİ hâlâ tek uygulama
/// veritabanındadır — ayrılan yalnız kiracının kendisidir.
/// </summary>
[Collection(ApiCollection.Name)]
public class PlatformKatmaniTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task PlatformVeritabani_KiraciYazilipOkunabilirVeAlanlariKorunur()
    {
        var slug = $"platform-test-{Guid.NewGuid():N}";

        // Yazma: bir scope'ta ekle ve kaydet.
        using (var yazmaScope = Factory.Services.CreateScope())
        {
            var yazma = yazmaScope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            yazma.Tenants.Add(new Tenant
            {
                Name = "Platform Test A.Ş.",
                Slug = slug,
                Status = TenantStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await yazma.SaveChangesAsync(CancellationToken.None);
        }

        // Okuma: TAMAMEN AYRI bir scope/context — change tracker'dan değil,
        // gerçekten veritabanından okunduğunu garanti eder.
        using var okumaScope = Factory.Services.CreateScope();
        var okuma = okumaScope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var kiraci = await okuma.Tenants.SingleAsync(t => t.Slug == slug);

        kiraci.Name.Should().Be("Platform Test A.Ş.");
        kiraci.Status.Should().Be(TenantStatus.Active);

        // Temizlik — test veritabanını sonraki testler için kirletmeden bırak.
        okuma.Tenants.Remove(kiraci);
        await okuma.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public Task PlatformVeritabani_UygulamaVeritabanindanFarklidir()
    {
        // Awaitlenecek bir şey yok (senkron bağlantı kontrolü) — async işaretlemek
        // CS1998 uyarısı doğurur ve derleme uyarısız olmalı (CI -warnaserror).
        using var scope = Factory.Services.CreateScope();
        var platform = (DbContext)scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var uygulama = (DbContext)scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        platform.Database.GetDbConnection().Database
            .Should().NotBe(uygulama.Database.GetDbConnection().Database,
                "platform kimliği uygulama verisinden ayrı bir veritabanında durmalı");

        return Task.CompletedTask;
    }

    [Fact]
    public Task KiraciSatiri_UygulamaVeritabaninda_ARTIK_YOK()
    {
        // Awaitlenecek bir şey yok (senkron model kontrolü) — async işaretlemek
        // CS1998 uyarısı doğurur ve derleme uyarısız olmalı (CI -warnaserror).
        using var scope = Factory.Services.CreateScope();
        var uygulama = (DbContext)scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var tenantsTablosuVar = uygulama.Model.GetEntityTypes()
            .Any(e => e.GetTableName() == "Tenants");

        tenantsTablosuVar.Should().BeFalse(
            "kiracı kimliği platform veritabanına taşındı; uygulama modelinde kalması iki doğruluk kaynağı demektir");

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProvizyonlananKiraci_PlatformVeritabaninda_Gorunur()
    {
        var kiraci = await ProvisionAndActivateAsync("PlatformKiraci", $"pk-{Guid.NewGuid():N}@ornek.local");

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

        var kayit = await platform.Tenants.FirstOrDefaultAsync(t => t.Id == kiraci.TenantId);

        kayit.Should().NotBeNull();
        kayit!.Slug.Should().Be(kiraci.Slug);
    }

    /// <summary>
    /// Sözleşme (Görev 3): dondurulmuş kiracıda login artık 403 döner, 401 değil.
    /// Kimlik bilgileri DOĞRU, engelleyen şey kiracının kapalı olması — 401
    /// "parolan yanlış" anlamına gelir ve kullanıcıyı yanlış yönlendirirdi.
    /// </summary>
    [Fact]
    public async Task DondurulmusKiraci_GirisYapamaz()
    {
        var eposta = $"donduk-{Guid.NewGuid():N}@ornek.local";
        var kiraci = await ProvisionAndActivateAsync("Donduk", eposta);

        // Giriş önce çalışıyor olmalı — testin anlamlı olması için.
        _ = await AuthedClientAsync(eposta);

        await DurumuDegistirAsync(kiraci.TenantId, TenantStatus.Frozen);

        var anonim = Factory.CreateClient();
        var yanit = await anonim.PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        yanit.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "dondurulmuş kiracıya giriş yapılamamalı");
    }

    private async Task DurumuDegistirAsync(int tenantId, TenantStatus durum)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var kiraci = await platform.Tenants.FirstAsync(t => t.Id == tenantId);
        kiraci.Status = durum;
        await platform.SaveChangesAsync(default);
        // Erişim kapısı ITenantRegistry'nin (singleton) önbelleğinden okur; bu satır
        // olmadan üstteki AuthedClientAsync çağrısının doldurduğu "Active" kaydı TTL
        // (5 dk) dolana kadar yapışık kalır ve test yanlışlıkla 200 alır.
        scope.ServiceProvider.GetRequiredService<ITenantRegistry>().Invalidate(tenantId);
    }

    [Fact]
    public async Task KullaniciOlusturulunca_DizineYazilir()
    {
        var eposta = $"dizin-{Guid.NewGuid():N}@ornek.local";
        var kiraci = await ProvisionAndActivateAsync("Dizin", eposta);

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

        var kayit = await platform.Directory
            .FirstOrDefaultAsync(d => d.NormalizedEmail == TenantDirectoryEntry.Normalize(eposta));

        kayit.Should().NotBeNull("admin oluşturulduğunda dizine yazılmalı");
        kayit!.TenantId.Should().Be(kiraci.TenantId);
    }

    [Fact]
    public async Task AyniEposta_IkinciKiracida_OnKontrolReddeder()
    {
        // Bu test yalnız kullanıcının gördüğü sonucu doğrular: ikinci kayıt denemesi
        // 409 alır. YOL ise burada Identity'nin FindByEmailAsync ön kontrolüdür
        // (TenantOnboarding.EmailExistsAsync) — dizinin birincil anahtar kısıtı hiç
        // devreye girmez, çünkü ilk admin zaten Identity'de bulunur ve provizyon
        // kiracı/kullanıcı oluşturmadan ÖNCE erken döner. Kısıtın kendisi ayrı testte
        // (AyniEposta_DizindeBaskaKiraciyaAitse_RezervasyondaReddedilir) sınanır.
        var eposta = $"tekil-{Guid.NewGuid():N}@ornek.local";
        await ProvisionAndActivateAsync("Birinci", eposta);

        var yanit = await ProvisionRawAsync(new
        {
            companyName = "Ikinci",
            slug = TestSlug("ikinci"),
            adminName = "Ikinci Yonetici",
            adminEmail = eposta,
        });

        yanit.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "tek e-posta = tek kiracı kuralı en azından ön kontrolle korunmalı");
    }

    [Fact]
    public async Task AyniEposta_DizindeBaskaKiraciyaAitse_RezervasyondaReddedilir()
    {
        // Yarış koşulunun simülasyonu: dizinde Identity'de karşılığı OLMAYAN bir satır
        // elle oluşturulur. Böylece EmailExistsAsync (Identity sorgusu) false döner ve
        // ön kontrolü geçer — ama ITenantDirectory.ReserveAsync, dizinde BAŞKA bir
        // kiracıya ait aynı e-postayı bulur ve ConflictException fırlatır.
        // DÜRÜST NOT: bu test ReserveAsync'in OKUMA-SONRA-KARAR dalını tetikler
        // (mevcut satır bulunur, TenantId eşleşmez → throw); alttaki gerçek
        // veritabanı-seviyesi kısıtı sınayan `catch (DbUpdateException)` dalı BURADAN
        // koşmaz — o yalnız GERÇEK eşzamanlı iki isteğin INSERT'i aynı anda
        // PK'ya çarptırdığı TOCTOU senaryosunda tetiklenir ve bu testte simüle edilmez.
        var eposta = $"yaris-{Guid.NewGuid():N}@ornek.local";

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            platform.Directory.Add(new TenantDirectoryEntry
            {
                NormalizedEmail = TenantDirectoryEntry.Normalize(eposta),
                TenantId = -1, // gerçek bir kiracıya ait değil; yalnız PK çakışmasını tetikler.
            });
            await platform.SaveChangesAsync(CancellationToken.None);
        }

        var yanit = await ProvisionRawAsync(new
        {
            companyName = "Yarisan",
            slug = TestSlug("yarisan"),
            adminName = "Yarisan Yonetici",
            adminEmail = eposta,
        });

        yanit.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "e-posta dizinde başka bir kiracıya ait olduğunda rezervasyon veritabanı kısıtına çarpmalı");

        // Bu yol, provizyonun ADIM 1 transaction'ını gerçekten tetikleyen tek
        // testtir: kiracı satırı YAZILDIKTAN sonra rezervasyon patlıyor. Transaction
        // olmasaydı geriye sahipsiz bir kiracı satırı kalır, slug'ı işgal eder ve
        // operatör aynı adla yeniden deneyemezdi.
        using (var kontrol = Factory.Services.CreateScope())
        {
            var platform = kontrol.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            (await platform.Tenants.AnyAsync(t => t.Name == "Yarisan"))
                .Should().BeFalse("rezervasyon çakışınca kiracı satırı geri alınmalı");
        }
    }

    [Fact]
    public async Task DizinYenidenKurma_SilinenKaydiGeriGetirir()
    {
        var eposta = $"kur-{Guid.NewGuid():N}@ornek.local";
        var kiraci = await ProvisionAndActivateAsync("Kurtar", eposta);
        var anahtar = TenantDirectoryEntry.Normalize(eposta);

        // Sapmayı simüle et: dizin kaydını sil.
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var kayit = await platform.Directory.FirstAsync(d => d.NormalizedEmail == anahtar);
            platform.Directory.Remove(kayit);
            await platform.SaveChangesAsync(default);
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", IKProApiFactory.PlatformKey);
        var yanit = await client.PostAsync($"/api/tenants/{kiraci.TenantId}/rebuild-directory", null);

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            (await platform.Directory.AnyAsync(d => d.NormalizedEmail == anahtar))
                .Should().BeTrue("dizin kiracı veritabanındaki kullanıcılardan yeniden kurulmalı");
        }
    }

    [Fact]
    public async Task DizinYenidenKurma_BaskaKiraciyaAitCakismaVarsa_AtlarKalaniniYazarVeRaporlar()
    {
        // Geri yükleme sonrası sapma senaryosu: kiraciA'nın gerçek admin e-postası
        // (epostaA1) dizinde HÂLÂ kiraciB'ye kayıtlı görünüyor — restore sonrası
        // platform veritabanı geri sarılmadığı için oluşan tam olarak bu durum.
        // kiraciA'nın ikinci bir kullanıcısı (epostaA2) ise hiç çakışmıyor.
        var epostaA1 = $"cakisan-{Guid.NewGuid():N}@ornek.local";
        var epostaA2 = $"temiz-{Guid.NewGuid():N}@ornek.local";
        var epostaB = $"digerkiraci-{Guid.NewGuid():N}@ornek.local";

        var kiraciA = await ProvisionAndActivateAsync("CakismaA", epostaA1);
        var kiraciB = await ProvisionAndActivateAsync("CakismaB", epostaB);

        // kiraciA'ya ikinci bir kullanıcı ekle (Users tablosuna doğrudan) — dizinden
        // BAĞIMSIZ olarak, yeniden kurmanın kiracı veritabanından okuduğunu kanıtlar.
        var anahtarA2 = TenantDirectoryEntry.Normalize(epostaA2);
        await SeedInTenantAsync(kiraciA.TenantId, db =>
        {
            db.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = epostaA2,
                NormalizedUserName = epostaA2.ToUpperInvariant(),
                Email = epostaA2,
                NormalizedEmail = anahtarA2,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                TenantId = kiraciA.TenantId,
                DisplayName = "Temiz Kullanici",
                IsActive = true,
            });
            return Task.CompletedTask;
        });

        var anahtarA1 = TenantDirectoryEntry.Normalize(epostaA1);
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var kayit = await platform.Directory.FirstAsync(d => d.NormalizedEmail == anahtarA1);
            kayit.TenantId = kiraciB.TenantId; // sapmayı simüle et: satır artık B'ye ait görünüyor.
            await platform.SaveChangesAsync(default);
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", IKProApiFactory.PlatformKey);
        var yanit = await client.PostAsync($"/api/tenants/{kiraciA.TenantId}/rebuild-directory", null);

        yanit.StatusCode.Should().Be(HttpStatusCode.OK,
            "tek satırlık çakışma tüm işlemi 500'e düşürmemeli");

        var sonuc = (await yanit.Content.ReadFromJsonAsync<RebuildDirectoryResult>())!;
        sonuc.YazilanKayit.Should().Be(1, "yalnız çakışmayan epostaA2 yazılmalı");
        sonuc.CakisanEpostalar.Should().ContainSingle(e => e == anahtarA1,
            "çakışan epostaA1 operatöre açıkça raporlanmalı");

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

            (await platform.Directory.AnyAsync(d => d.NormalizedEmail == anahtarA2 && d.TenantId == kiraciA.TenantId))
                .Should().BeTrue("çakışmayan kayıt yazılmalı — tek bozuk satır diğerlerini iptal etmemeli");

            var halaB = await platform.Directory.FirstAsync(d => d.NormalizedEmail == anahtarA1);
            halaB.TenantId.Should().Be(kiraciB.TenantId,
                "çakışan e-posta kiraciA'ya çalınmamalı — 'tek e-posta = tek kiracı' kuralı gevşetilmez");
        }
    }

    [Fact]
    public async Task DizinYenidenKurma_VarOlmayanKiraci_404Doner()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", IKProApiFactory.PlatformKey);

        var yanit = await client.PostAsync("/api/tenants/999999999/rebuild-directory", null);

        yanit.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Purge ile tutarlı olmalı: var olmayan kiracı için 404");
    }

    [Fact]
    public async Task DizinYenidenKurma_ArdArdaIkiKezCalisirsaAyniSonucuUretir()
    {
        // Tasarım belgesi (2026-08-06-kiraci-basina-veritabani-design.md, test listesi)
        // "dizin yeniden kurmanın aynı sonucu üretmesi"ni açıkça ister. Bu aynı zamanda
        // RemoveRange + aynı PK'yı yeniden Add eden yolu GERÇEKTEN koşturan tek testtir:
        // yukarıdaki iki rebuild testi (SilinenKaydiGeriGetirir, BaskaKiraciyaAitCakismaVarsa)
        // rebuild'den ÖNCE dizin satırını silip/başka kiracıya taşıdığı için 'mevcut'
        // (silinecek satır listesi) her ikisinde de BOŞ kalıyor — RemoveRange hiç koşmuyor.
        // Burada rebuild bilerek İKİ KEZ art arda çağrılır: ikinci koşumda mevcut'ta
        // birinci koşumun yazdığı satır bulunur, silinip aynı normalize e-postayla yeniden
        // eklenir — geri yükleme prosedürünün en sık koşacağı ve zorunlu adımı budur.
        var eposta = $"tekrar-{Guid.NewGuid():N}@ornek.local";
        var kiraci = await ProvisionAndActivateAsync("Tekrarli", eposta);
        var anahtar = TenantDirectoryEntry.Normalize(eposta);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", IKProApiFactory.PlatformKey);

        var ilkYanit = await client.PostAsync($"/api/tenants/{kiraci.TenantId}/rebuild-directory", null);
        ilkYanit.StatusCode.Should().Be(HttpStatusCode.OK);
        var ilkSonuc = (await ilkYanit.Content.ReadFromJsonAsync<RebuildDirectoryResult>())!;

        var ikinciYanit = await client.PostAsync($"/api/tenants/{kiraci.TenantId}/rebuild-directory", null);
        ikinciYanit.StatusCode.Should().Be(HttpStatusCode.OK,
            "ikinci art arda koşum da başarılı olmalı (RemoveRange + aynı PK ile yeniden Add çakışmamalı)");
        var ikinciSonuc = (await ikinciYanit.Content.ReadFromJsonAsync<RebuildDirectoryResult>())!;

        ikinciSonuc.YazilanKayit.Should().Be(ilkSonuc.YazilanKayit, "iki koşum aynı sayıda kayıt yazmalı");
        ikinciSonuc.CakisanEpostalar.Should().BeEquivalentTo(ilkSonuc.CakisanEpostalar,
            "iki koşum aynı çakışma sonucunu üretmeli");

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platform.Directory.CountAsync(d => d.TenantId == kiraci.TenantId && d.NormalizedEmail == anahtar))
            .Should().Be(1, "ikinci koşum satırı çoğaltmamalı, yalnız yeniden kurmalı");
    }

    [Fact]
    public async Task ProvizyonBasarili_DurumuActiveBirakir()
    {
        var kiraci = await ProvisionTenantAsync("Asama", $"asama-{Guid.NewGuid():N}@ornek.local");

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var kayit = await platform.Tenants.FirstAsync(t => t.Id == kiraci.TenantId);

        kayit.Status.Should().Be(TenantStatus.Active,
            "başarılı provizyon sonunda kiracı kullanılabilir olmalı");
    }

    [Fact]
    public async Task YaridaKalanProvizyon_TakiliListesindeGorunur()
    {
        // Yarıda kalmayı simüle et: kiracıyı Provisioning'de ve eski tarihli bırak.
        int tenantId;
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var takili = new Tenant
            {
                Name = "Takili",
                Slug = $"takili{Guid.NewGuid():N}"[..20],
                Status = TenantStatus.Provisioning,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            };
            platform.Tenants.Add(takili);
            await platform.SaveChangesAsync(default);
            tenantId = takili.Id;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", IKProApiFactory.PlatformKey);
        var takililar = await GetAsync<List<StuckTenantDto>>(client, "/api/tenants/stuck?olderThanMinutes=60");

        takililar.Should().Contain(t => t.TenantId == tenantId,
            "yarıda kalan provizyon görünür olmalı — sessiz enkaz bırakılmaz");
    }

    [Fact]
    public async Task PurgingKiraci_YasindanBagimsizGorunur_TazeProvisioningGorunmez()
    {
        // Yaş eşiği YALNIZ Provisioning'e uygulanmalı. İki kiracı da YENİ oluşturulmuş
        // (eşiğin çok altında) — biri Purging, biri Provisioning. Purging her zaman
        // listede olmalı (silme saniyeler sürer, orada kalmış olmak zaten anormaldir);
        // taze Provisioning ise davet kabulünü bekliyor olabilir, henüz "takılı" değil.
        int purgingId;
        int provisioningId;
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

            var purging = new Tenant
            {
                Name = "TazePurging",
                Slug = $"tazepurging{Guid.NewGuid():N}"[..20],
                Status = TenantStatus.Purging,
                CreatedAtUtc = DateTime.UtcNow, // taze — eşiğin çok altında.
            };
            var provisioning = new Tenant
            {
                Name = "TazeProvisioning",
                Slug = $"tazeprovision{Guid.NewGuid():N}"[..20],
                Status = TenantStatus.Provisioning,
                CreatedAtUtc = DateTime.UtcNow, // aynı yaş — ama Provisioning için eşik geçerli.
            };
            platform.Tenants.AddRange(purging, provisioning);
            await platform.SaveChangesAsync(default);
            purgingId = purging.Id;
            provisioningId = provisioning.Id;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", IKProApiFactory.PlatformKey);
        var takililar = await GetAsync<List<StuckTenantDto>>(client, "/api/tenants/stuck?olderThanMinutes=60");

        takililar.Should().Contain(t => t.TenantId == purgingId,
            "Purging durumundaki kiracı yaşına bakılmaksızın her zaman görünmeli — orada kalmış olmak zaten anormaldir");
        takililar.Should().NotContain(t => t.TenantId == provisioningId,
            "taze Provisioning kiracı henüz takılı sayılmaz — eşik yalnız Provisioning'e uygulanır");
    }
}
