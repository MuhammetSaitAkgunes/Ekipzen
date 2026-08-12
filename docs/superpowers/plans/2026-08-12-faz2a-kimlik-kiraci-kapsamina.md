# Faz 2a — Kimliği Kiracı Kapsamına Taşı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kimlik doğrulamayı, kiracı veritabanları ayrıldığında çalışacak hale getirmek — ama veriyi henüz bölmeden, yani davranışı değiştirmeden.

**Architecture:** İki değişiklik. Birincisi: `RefreshToken` platform veritabanına taşınır, çünkü elinde yalnız bir token dizesi olan sunucu hangi kiracı veritabanına bakacağını bilemez; platformdaki satır `TenantId` taşıdığı için yönlendirmeyi o verir. İkincisi: `IdentityService`'in tüm kullanıcı işlemleri, ambient (kiracısız) kapsamdan değil, **kiracıya sabitlenmiş** bir kapsamdan çözülen `UserManager` ile çalışır. Kiracı, login'de dizinden (e-posta → kiracı), refresh'te platform token satırından gelir.

**Tech Stack:** .NET 9, EF Core 9, SQL Server, ASP.NET Core Identity, xUnit + FluentAssertions.

Tasarım: `docs/superpowers/specs/2026-08-06-kiraci-basina-veritabani-design.md`
Önceki faz: `docs/superpowers/plans/2026-08-07-faz1b-baglanti-tesisati.md`

## Neden bu faz var

`UserManager<ApplicationUser>`, `AddEntityFrameworkStores<AppDbContext>` ile `AppDbContext`'e bağlı; o da bağlantısını `ICurrentTenant`'tan alıyor (Faz 1b). Kiracı veritabanları ayrıldığı anda login **kırılır**: kullanıcıyı bulmak için hangi veritabanına bakılacağını bilmek gerekir, ama kiracıyı bulmak için kullanıcıyı bulmak gerekir.

Dizin bu düğümü çözüyor (e-posta → kiracı, Faz 1a/1b). Ama `IdentityService`'in 15 kullanıcı işlemi hâlâ ambient kapsamla çalışıyor. Bu faz o kurguyu değiştirir — **veriyi bölmeden**, yani her adım tek veritabanında doğrulanabilir kalır.

Refresh ayrı bir düğüm: elinde yalnız rastgele bir dize var, dizin yardım edemez. Çözüm kararlaştırıldı — **refresh token'lar platform veritabanına taşınır.** Satır zaten `TenantId` taşıyor, yani platformdaki arama kiracıyı doğrudan veriyor ve token biçimi değişmiyor.

## Global Constraints

- **Davranış değişmemeli.** Kiracı verisi bu fazda da bölünmez; `ITenantConnectionResolver` herkese aynı dizeyi döndürmeye devam eder.
- Mevcut testler geçmeye devam etmeli. **Faz başlangıcı: 51 birim + 149 entegrasyon = 200.**
- Derleme uyarısız (`-warnaserror`). `await` içermeyen `async` test metodu CS1998 verir.
- Türkçe kod yorumları ve test adları; yorumlar "ne" değil "neden" anlatır.
- Testler gerçek SQL Server'a bağlanır (localhost, Windows auth). Derlemeden önce `taskkill //F //IM IKPro.API.exe`.
- Her görev ayrı commit.
- **Kapsam dışı:** `CREATE DATABASE`, `Tenants.DatabaseName`, çözücünün katalog üretmesi, `Subscriptions` taşınması, purge'ün `DROP DATABASE` yapması — hepsi Faz 2b.

## Kabul edilen sonuç

Refresh token'lar platform veritabanına taşındığı için **bir müşteriyi geri yüklediğinizde oturumları geri sarılmaz.** Bu doğru davranıştır (oturum veri değil, durum) ama runbook'a yazılmalı: geri yükleme sonrası o kiracının kullanıcıları yeniden giriş yapar.

---

## File Structure

| Dosya | Sorumluluk |
| --- | --- |
| `backend/src/IKPro.Infrastructure/Identity/RefreshToken.cs` | `User` navigasyonu kalkar (çapraz-DB) |
| `backend/src/IKPro.Infrastructure/Persistence/PlatformDbContext.cs` | `RefreshTokens` DbSet + eşleme (değiştir) |
| `backend/src/IKPro.Infrastructure/Persistence/AppDbContext.cs` | `RefreshTokens` çıkar (değiştir) |
| `backend/src/IKPro.Application/Common/Interfaces/IPlatformDbContext.cs` | `RefreshTokens` eklenir (değiştir) |
| `backend/src/IKPro.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs` | FK kalkar; eşleme platforma taşınır (değiştir) |
| `backend/src/IKPro.Infrastructure/Identity/IdentityService.cs` | Tüm kullanıcı işlemleri kiracı kapsamında (değiştir) |
| `backend/src/IKPro.Infrastructure/Persistence/TenantPurger.cs` | Refresh silme platforma taşınır (değiştir) |
| `backend/tests/IKPro.Tests.Integration/Auth/KimlikKiraciKapsamiTests.cs` | Yeni testler (oluştur) |
| `docs/yedekleme-ve-kurtarma.md` | Oturumların geri sarılmadığı notu (değiştir) |

---

### Task 1: `RefreshToken` platform veritabanına taşınır

Elinde yalnız token dizesi olan sunucu hangi kiracı veritabanına bakacağını bilemez. Platformdaki satır `TenantId` taşıdığı için yönlendirmeyi o verir.

**Files:**
- Modify: `backend/src/IKPro.Infrastructure/Identity/RefreshToken.cs`
- Modify: `backend/src/IKPro.Infrastructure/Persistence/PlatformDbContext.cs`
- Modify: `backend/src/IKPro.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `backend/src/IKPro.Application/Common/Interfaces/IPlatformDbContext.cs`
- Modify: `backend/src/IKPro.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`
- Modify: `backend/src/IKPro.Infrastructure/Identity/IdentityService.cs`
- Modify: `backend/src/IKPro.Infrastructure/Persistence/TenantPurger.cs`

**Interfaces:**
- Produces: `IPlatformDbContext.RefreshTokens` — `DbSet<RefreshToken>`
- Değişir: `RefreshToken.User` navigasyonu **kaldırılır**; `UserId` düz sütun olarak kalır

- [ ] **Step 1: Failing testi yaz**

`backend/tests/IKPro.Tests.Integration/Auth/KimlikKiraciKapsamiTests.cs`:

```csharp
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
```

- [ ] **Step 2: Testleri koş, ilkinin kırıldığını doğrula**

```bash
cd backend && dotnet test tests/IKPro.Tests.Integration --filter "FullyQualifiedName~KimlikKiraciKapsami"
```

Beklenen: `RefreshToken_PlatformVeritabaninaYazilir` **kırmızı** (derleme hatası — `IPlatformDbContext.RefreshTokens` yok); diğer ikisi mevcut davranışı doğruladığı için yeşil olmalı.

- [ ] **Step 3: Navigasyonu kaldır**

`RefreshToken.cs` içinden şu satırı SİL:

```csharp
    public ApplicationUser? User { get; set; }
```

Yerine gerekçeyi yaz:

```csharp
    /// <summary>
    /// Sahip kullanıcının Identity kimliği. Navigasyon YOKTUR: token platform
    /// veritabanında, kullanıcı kiracı veritabanındadır ve SQL Server çapraz-veritabanı
    /// yabancı anahtarı desteklemez. Kullanıcı, <see cref="TenantId"/> ile açılan
    /// kiracı kapsamından bu kimlikle çözülür.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
```

`ApplicationUser.cs:22`'deki `ICollection<RefreshToken> RefreshTokens` koleksiyonunu da **kaldır** — aynı gerekçe (karşı taraf artık başka veritabanında).

- [ ] **Step 4: Eşlemeyi platforma taşı**

`RefreshTokenConfiguration`'daki `RefreshToken` yapılandırmasını **sil** (FK dahil) ve `PlatformDbContext.OnModelCreating` içine taşı:

```csharp
        builder.Entity<RefreshToken>(b =>
        {
            // Token sütununda ham değer değil SHA-256 hash saklanır (Base64, 44 karakter).
            b.Property(t => t.Token).IsRequired().HasMaxLength(256);
            b.Property(t => t.UserId).IsRequired().HasMaxLength(450);
            b.Ignore(t => t.IsActive);

            b.HasIndex(t => t.Token).IsUnique();

            // Kullanıcıya FK YOK: kullanıcı kiracı veritabanında. Kiracı bazlı
            // silme ve yönlendirme TenantId üzerinden yapılır.
            b.HasIndex(t => t.TenantId);
        });
```

`AppDbContext`'ten `RefreshTokens` DbSet'ini ve Identity indeks satırını çıkar. `IPlatformDbContext`'e `DbSet<RefreshToken> RefreshTokens { get; }` ekle.

- [ ] **Step 5: `RefreshAsync`'i yeniden kur**

`Include(t => t.User)` artık çalışmaz. Yeni sıra: platformdan token satırını bul → `TenantId`'yi al → o kiracının kapsamından `UserManager` ile kullanıcıyı çöz → kapıdan geçir → yeni token üret.

Kullanıcı bulunamazsa (token var ama kullanıcı yok — geri yükleme sonrası mümkün) girişi reddet ve **logla**.

- [ ] **Step 6: Purge'ü güncelle**

`TenantPurger` refresh token'ları artık uygulama veritabanından silmiyor — platform tarafında `platform.RefreshTokens.Where(t => t.TenantId == tenantId)` ile silinmeli. Ham SQL bloğundan `refreshTokenTable` satırını çıkar.

- [ ] **Step 7: Migration'ları üret ve uygula**

```bash
cd backend
IKPRO_PLATFORM_CONNECTION="Server=localhost;Database=IKProPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef migrations add RefreshTokensToPlatform --context PlatformDbContext --output-dir Persistence/Migrations/Platform --project src/IKPro.Infrastructure --startup-project src/IKPro.Api
IKPRO_CONNECTION="Server=localhost;Database=IKProDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef migrations add DropRefreshTokensFromAppDb --context AppDbContext --project src/IKPro.Infrastructure --startup-project src/IKPro.Api
```

Uygulama veritabanı migration'ının başına açıklama ekle: tablo düşürülüyor, satırlar kaybolur, **oturumlar geçersizleşir ve kullanıcılar bir kez yeniden giriş yapar** — gerçek müşteri verisi olmadığı için kabul edilmiştir.

Sonra ikisini de uygula (`dotnet ef database update --context ...`).

- [ ] **Step 8: Tam suite ve commit**

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
git add -A && git commit -m "refactor(auth): refresh token'lar platform veritabanına taşındı"
```

Beklenen: 200 + eklediğin testler, hepsi PASS.

---

### Task 2: Kullanıcı işlemleri kiracı kapsamında çalışır

`IdentityService`'in `UserManager`/`SignInManager` bağımlılıkları ambient kapsamdan gelir; kiracı DB'leri ayrılınca yanlış katalogda arama demektir. Her işlem, kiracıya sabitlenmiş kapsamdan çözülen `UserManager` ile çalışmalı.

**Files:**
- Modify: `backend/src/IKPro.Infrastructure/Identity/IdentityService.cs`
- Modify: `backend/tests/IKPro.Tests.Integration/Auth/KimlikKiraciKapsamiTests.cs`

**Interfaces:**
- Consumes: `ITenantScopeFactory.Create(int tenantId) → ITenantScope` (Faz 1b), `ITenantDirectory.FindTenantIdAsync` (Faz 1a)

- [ ] **Step 1: Failing testi yaz**

`KimlikKiraciKapsamiTests` sınıfına ekle:

```csharp
    /// <summary>
    /// Kullanıcı işlemleri, isteğin ambient kapsamından DEĞİL, kiracıya sabitlenmiş
    /// bir kapsamdan çözülen UserManager ile çalışmalı. Bunu doğrudan gözlemleyemeyiz,
    /// ama ambient kapsamda kiracı OLMADAN (anonim login isteği) kullanıcı
    /// bulunabiliyor olması, aramanın kiracı kapsamında yapıldığının kanıtıdır —
    /// Faz 2b'de ambient kapsam yanlış (ya da hiçbir) katalogda olacak.
    /// </summary>
    [Fact]
    public async Task Login_AmbientKapsamdaKiraciYokkenDeCalisir()
    {
        var eposta = $"ak-{Guid.NewGuid():N}@ornek.local";
        await ProvisionAndActivateAsync("AmbientKapsam", eposta);

        // Anonim istemci: JWT yok, dolayısıyla ambient kapsamda tenant claim'i yok.
        var yanit = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Davet kabulü de kiracıya sabitlenmiş kapsamda çalışmalı: kullanıcı kiracı
    /// veritabanındadır ve token doğrulaması onu bulmak zorundadır.
    /// </summary>
    [Fact]
    public async Task DavetKabulu_KiraciKapsamindaCalisir()
    {
        var eposta = $"dk-{Guid.NewGuid():N}@ornek.local";
        await ProvisionTenantAsync("DavetKapsam", eposta);

        // Davet kabulü (şifre belirleme) anonim bir istektir — ambient kapsamda kiracı yok.
        await AcceptInviteAsync(eposta);

        var giris = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        giris.StatusCode.Should().Be(HttpStatusCode.OK, "davet kabulü sonrası giriş çalışmalı");
    }
```

Bu iki test **bugünkü kodda da yeşildir** — tek DB olduğu için ambient kapsam doğru katalogda. Koruma testleridir: Faz 2b'de yapı bozulursa kırılırlar. Bunu yorumda belirt.

- [ ] **Step 2: Testleri koş, yeşil olduklarını gör**

Kırmızı beklemiyoruz; bu adım taban çizgisini kurar.

- [ ] **Step 3: `IdentityService`'i kiracı kapsamına çevir**

`UserManager<ApplicationUser>` ve `SignInManager<ApplicationUser>` yapıcı parametrelerini **kaldır**; yerine `ITenantScopeFactory` al. Her kullanıcı işlemi şu deseni izler:

```csharp
        using var kapsam = tenantScopeFactory.Create(tenantId);
        var kullanicilar = kapsam.Services.GetRequiredService<UserManager<ApplicationUser>>();
```

Kiracının nereden geldiği işleme göre değişir:

`IIdentityService`'in **dokuz** üyesi var; hiçbirini atlama:

| İşlem | Kiracı kaynağı | Not |
| --- | --- | --- |
| `LoginAsync` | Dizin (`FindTenantIdAsync(email)`) | Dizinde yoksa genel `UnauthorizedException` |
| `RefreshAsync` | Platform token satırının `TenantId`'si | Görev 1'den gelir |
| `LogoutAsync` | Platform token satırının `TenantId`'si | Token'ı iptal eder; kullanıcıya dokunmuyorsa kapsam gerekmeyebilir — kontrol et |
| `AcceptInviteAsync` | Dizin (`FindTenantIdAsync(email)`) | Anonim istek; ambient kapsamda kiracı yok |
| `EmailExistsAsync` | Dizin | Dizinde kayıt yoksa **`false` dön** — kullanıcı yok demektir, kapsam açma |
| `CreateTenantAdminAsync` | Parametre `tenantId` | Provizyon yolu |
| `CreateEmployeeLoginAsync` | `currentTenant.TenantIdOrThrow()` | İşe alım her zaman yetkili bağlamda |
| `ChangePasswordAsync` | `currentTenant.TenantIdOrThrow()` | Oturum içi |
| `GetUserAsync` | `currentTenant.TenantIdOrThrow()` | Oturum içi |

**Dikkat — `SignInManager`:** `CheckPasswordSignInAsync` lockout sayaçlarını `UserManager` üzerinden yazar; ikisi de **aynı** kapsamdan çözülmeli, yoksa lockout yanlış context'e yazılır ve sessizce çalışmaz. Aynı `kapsam` değişkeninden çöz.

**Dikkat — kapsam ömrü:** `UserManager`'dan dönen `ApplicationUser` nesnesi o kapsamın context'ine bağlıdır. Kapsam kapandıktan sonra üzerinde `SaveChanges` gerektiren bir iş yapma; token üretimi de dahil olmak üzere kullanıcıyla yapılacak her şey kapsam **içinde** bitmeli.

- [ ] **Step 4: `IssueTokensAsync`'i kapsam alacak şekilde uyarla**

`IssueTokensAsync` bugün ambient `context.RefreshTokens`'a yazıyor; Görev 1'den sonra platform context'ine yazacak (platform context kiracıdan bağımsız, ambient kalabilir).

Kullanıcı nesnesi kiracı kapsamından geldiği için `GetRolesAsync` çağrısı da o kapsamın `UserManager`'ıyla yapılmalı. Bu yüzden metot imzası **kapsamı parametre alacak** şekilde değişir:

```csharp
    private async Task<AuthResponse> IssueTokensAsync(
        ITenantScope kapsam, ApplicationUser user, CancellationToken cancellationToken)
```

Alternatif (metodu kapsam içinden çağırmak ama imzayı değiştirmemek) sırayı yine sözleşmeye bırakırdı — Faz 1b'de bu sınıf hatanın nasıl sessizce geçtiğini gördük. İmzaya koymak yanlış kullanımı derleme hatasına çevirir.

**Erişim kapısı çağrısı metodun en başında kalmalı** — kaldırma, koşullu yapma.

- [ ] **Step 5: Tam suite ve commit**

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
git add -A && git commit -m "refactor(auth): kullanıcı işlemleri kiracıya sabitlenmiş kapsamda çalışır"
```

---

### Task 3: Doğrulama ve dokümantasyon

**Files:**
- Modify: `docs/yedekleme-ve-kurtarma.md`
- Modify: `docs/rehberler/05-kimlik-ve-yetkilendirme.md`
- Modify: `docs/superpowers/plans/2026-08-12-faz2a-kimlik-kiraci-kapsamina.md` (bu dosya)

- [ ] **Step 1: Uçtan uca elle doğrulama**

İki veritabanını da düşür, uygulamayı aç ve sırayla: giriş → refresh → eski token reddi → çıkış → davet kabulü (yeni kiracı provizyonla) → dondurulmuş kiracıda login/refresh/istek reddi. Çıktıları rapora yaz.

- [ ] **Step 2: Runbook**

`docs/yedekleme-ve-kurtarma.md`'ye ekle: refresh token'lar platform veritabanında olduğu için **bir kiracıyı geri yüklemek oturumları geri sarmaz**; geri yükleme sonrası o kiracının kullanıcıları yeniden giriş yapar. Bu bilinçli bir karardır — oturum veri değil, durumdur.

- [ ] **Step 3: Kimlik rehberi**

`docs/rehberler/05-kimlik-ve-yetkilendirme.md`'ye refresh token'ların nerede durduğunu ve neden orada olduğunu ekle (yönlendirme).

- [ ] **Step 4: Plan sonucu ve tam doğrulama**

Bu plana "Uygulama sonucu" bölümü ekle. Sonra:

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
cd ../frontend && npx vitest run && npx tsc -b && npx oxlint
git add -A && git commit -m "docs(auth): Faz 2a sonucu — oturumların geri sarılmadığı notu"
```

---

## Faz sonu doğrulama

- [ ] Refresh token'lar platform veritabanında; uygulama veritabanında tablo kalmadı
- [ ] Login, refresh, rotasyon, çıkış, davet kabulü çalışıyor
- [ ] Dondurulmuş kiracı hâlâ üç yolda da reddediliyor (Faz 1b kapısı bozulmadı)
- [ ] `IdentityService`'te ambient `UserManager`/`SignInManager` kullanımı kalmadı
- [ ] Lockout sayacı çalışıyor (aynı kapsamdan çözüldüğü doğrulandı)
- [ ] Purge refresh token'ları platformdan siliyor
- [ ] Backend ve frontend suite'leri yeşil, derleme uyarısız

**Sonraki:** Faz 2b — ayrılma. `Tenants.DatabaseName`, gerçek `CREATE DATABASE`, çözücünün katalog üretmesi, purge'ün `DROP DATABASE` yapması, `Subscriptions` taşınması, migration orkestrasyonu, test altyapısının kiracı başına veritabanı kurması.

---

## Uygulama sonucu (2026-08-12)

**Tamamlandı.** 3 görev, 4 commit. Test tabanı 200 → 206, derleme uyarısız.

| Görev | Nasıl yapıldı | İncelemede yakalanan |
| --- | --- | --- |
| 1 · Refresh token'lar platforma | Alt ajan | Plan kusuru: `RefreshToken` Infrastructure'daydı, `IPlatformDbContext` ise Application'da — Application Infrastructure'a referans veremez. Varlık Domain'e taşındı. |
| 2 · Kimlik kiracı kapsamında | Kontrolör yazdı, alt ajan inceledi | **Gerileme:** `EmailExistsAsync` dizin-önce olunca seed'deki bilinçli bir koruma körleşti. **Fazla iddia:** kilitleme testinin kapsam değişmezini kanıtladığını yazmıştım; kanıtlamıyor. |
| 3 · Doğrulama + dokümantasyon | Kontrolör | — |

**Yaklaşım değişikliği:** Görev 2'de alt ajan iki kez oturum limitine takıldı (fazın en büyük görevi, her taze ajan sıfırdan okuyor). Uygulama kontrolöre alındı, **inceleme alt ajanda bırakıldı** — inceleme uygulamadan ucuz, güvenlik ağı en gerekli yerde kaldı. İnceleme haklı çıktı: kontrolörün kaçırdığı bir gerilemeyi ve bir fazla iddiayı buldu.

**Elle doğrulama:** iki veritabanı düşürülüp uygulama temiz açıldı; giriş, refresh + rotasyon, dondurma → login 403, çözme → login 200 doğrulandı. Refresh token'lar platform veritabanında (2 kayıt), uygulama veritabanında tablo yok. Dondurulmuş kiracıda yetkili istek ve refresh reddi script hatası yüzünden elle ölçülemedi; `ErisimKapisiTests` bunları uçtan uca kapsıyor ve geçiyor.

## Faz 2b'ye devredilenler

| Madde | Neden |
| --- | --- |
| `EmailExistsAsync` çözülemeyen kiracıda fırlatır | Dizin türetilmiştir ve sapabilir; `Create(...)` çözülemeyen kiracıda patlar ve boolean sözleşmesi 500'e döner. "Çözülemeyen kiracı → false" davranışı eklenmeli. |
| `AppDbContextInitializer` root kapsamdan çözülüyor | Tek kalan elle `Impersonate`. Düzeltme `Program.cs`'te: initializer kiracı kapsamından çözülmeli. |
| `AcceptInviteAsync` durum makinesini atlıyor | Herhangi bir `Provisioning` kiracıyı şifre belirlenir belirlenmez `Active` yapıyor — Faz 2b'de var olmayan veritabanına Active kiracı demek. |
| `TenantOnboarding` / `RegisterTenantCommand` sıra kopyası | İki kopya, sessiz sapma riski. |
| Kiracı veritabanı harmanlaması | Yerel `Turkish_CI_AS`, CI `SQL_Latin1_General_CP1_CI_AS`. `CREATE DATABASE` açık harmanlama vermeli; hangisi olacağı ürün kararı. |
