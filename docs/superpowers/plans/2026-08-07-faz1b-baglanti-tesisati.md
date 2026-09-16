# Faz 1b — Bağlantı Tesisatı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kiracıdan veritabanı bağlantısına giden yolu kurmak — çözücü, kütük, erişim kapısı ve kiracıya sabitlenmiş kapsam — ama çözücü bu fazda herkese **aynı** veritabanını döndürsün, yani davranış değişmesin.

**Architecture:** Bağlantı dizesi tek yerden (`DependencyInjection.cs`) okunmak yerine `ITenantConnectionResolver` üzerinden çözülür; bu fazda çözücü katalog adını değiştirmez. Kiracının durumu `ITenantRegistry` ile bellekte önbelleklenir ve durum değişince anında düşürülür. Erişim kapısı iki darboğaza yerleşir: token üretimi (`IssueTokensAsync` — login ve refresh'in ortak yolu) ve HTTP boru hattı (kimlik doğrulamadan sonra). Dizin erişimi Identity'den ayrılıp kendi soyutlamasına çıkar, çünkü artık login yolu da onu kullanacak.

**Tech Stack:** .NET 9, EF Core 9, SQL Server, ASP.NET Core middleware, `IMemoryCache`, xUnit + FluentAssertions.

Tasarım: `docs/superpowers/specs/2026-08-06-kiraci-basina-veritabani-design.md`
Önceki faz: `docs/superpowers/plans/2026-08-06-faz1a-platform-katmani.md`

## Global Constraints

- **Davranış değişmemeli.** Çözücü herkese `ConnectionStrings:DefaultConnection` döndürür. Kiracı verisi bu fazda da bölünmez.
- **Tek görünür değişiklik:** dondurulmuş/silinen kiracının isteklerinin reddedilmesi. Bugün yalnız yeni girişler kesiliyor; bu fazdan sonra refresh ve yetkili istekler de kesilecek.
- Mevcut testler geçmeye devam etmeli. **Faz başlangıcı: 47 birim + 128 entegrasyon = 175.**
- Derleme uyarısız (`-warnaserror`). `await` içermeyen `async` test metodu CS1998 verir.
- Türkçe kod yorumları ve test adları; yorumlar "ne" değil "neden" anlatır.
- Testler gerçek SQL Server'a bağlanır (localhost, Windows auth). Derlemeden önce `taskkill //F //IM Ekipzen.API.exe`.
- Her görev ayrı commit.
- `Tenants.DatabaseName` bu fazda **yok** — kütük yalnız `Status` tutar. Katalog adı Faz 2'de gelir.

## Faz 1a'dan devredilenler — bu planda kapatılanlar

| Madde | Nerede |
| --- | --- |
| `RefreshAsync` kiracı `Status` kontrolü | Görev 3 (kapı `IssueTokensAsync`'e girince yapısal olarak kapanır) |
| Dizine yazma sırası — öksüz satır testi | Görev 1 |
| İşe alım yolu dizin testi | Görev 1 |
| Dizinin ayrı soyutlamaya çıkarılması | Görev 1 |

**Bu planda kapatılmayanlar** (Faz 2'ye kalır): `AcceptInviteAsync` durum makinesi gerilimi, `TenantOnboarding`/`RegisterTenantCommand` sıra kopyası, `TenantStatus` migration `defaultValue`.

---

## File Structure

| Dosya | Sorumluluk |
| --- | --- |
| `backend/src/Ekipzen.Application/Common/Interfaces/ITenantDirectory.cs` | Dizin okuma/yazma sözleşmesi (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Tenancy/TenantDirectory.cs` | Dizin uygulaması (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs` | Dizin yazımı soyutlamaya devredilir (değiştir) |
| `backend/src/Ekipzen.Application/Common/Interfaces/ITenantRegistry.cs` | Kiracı kütüğü sözleşmesi (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Tenancy/TenantRegistry.cs` | Önbellekli kütük (oluştur) |
| `backend/src/Ekipzen.Application/Common/Interfaces/ITenantAccessGuard.cs` | Erişim kapısı sözleşmesi (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Tenancy/TenantAccessGuard.cs` | Kapı uygulaması (oluştur) |
| `backend/src/Ekipzen.Api/Middleware/TenantAccessMiddleware.cs` | Yetkili isteklerde kapı (oluştur) |
| `backend/src/Ekipzen.Application/Common/Interfaces/ITenantConnectionResolver.cs` | Bağlantı çözücü sözleşmesi (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Tenancy/TenantConnectionResolver.cs` | Çözücü uygulaması (oluştur) |
| `backend/src/Ekipzen.Application/Common/Interfaces/ITenantScopeFactory.cs` | Kiracıya sabitlenmiş kapsam (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Tenancy/TenantScopeFactory.cs` | Fabrika uygulaması (oluştur) |
| `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs` | Kayıtlar (değiştir) |
| `backend/src/Ekipzen.Api/Program.cs` | Middleware sırası (değiştir) |
| `backend/tests/Ekipzen.Tests.Integration/Tenancy/ErisimKapisiTests.cs` | Kapı testleri (oluştur) |
| `backend/tests/Ekipzen.Tests.Integration/Tenancy/DizinButunluguTests.cs` | Devredilen dizin testleri (oluştur) |

---

### Task 1: Dizin erişimi Identity'den ayrılır

Bugün dizine yazan tek yol `IdentityService.DizineYazAsync`. Login yolu da dizini kullanmaya başlayacağı (Görev 6) ve bağlantı katmanı da ona bakacağı için, dizin Identity'nin içinden çıkıp kendi soyutlamasına taşınır. Faz 1a'dan devredilen iki test de burada kapatılır.

**Files:**
- Create: `backend/src/Ekipzen.Application/Common/Interfaces/ITenantDirectory.cs`
- Create: `backend/src/Ekipzen.Infrastructure/Tenancy/TenantDirectory.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/DizinButunluguTests.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs`
- Modify: `backend/src/Ekipzen.Application/Common/Interfaces/IIdentityService.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/RebuildDirectoryCommand.cs`

**Interfaces:**
- Produces:
  - `ITenantDirectory.ReserveAsync(string email, int tenantId, CancellationToken) → Task` — idempotent; aynı kiracı için no-op, farklı kiracı için `ConflictException`
  - `ITenantDirectory.FindTenantIdAsync(string email, CancellationToken) → Task<int?>`
  - `ITenantDirectory.RemoveForTenantAsync(int tenantId, CancellationToken) → Task<int>`
  - `ITenantDirectory.RebuildForTenantAsync(int tenantId, IReadOnlyList<string> normalizedEmails, CancellationToken) → Task<RebuildOutcome>`
  - `record RebuildOutcome(int YazilanKayit, IReadOnlyList<string> CakisanEpostalar)`

- [ ] **Step 1: Devredilen iki testi yaz (kırmızı olmalı)**

`backend/tests/Ekipzen.Tests.Integration/Tenancy/DizinButunluguTests.cs`:

```csharp
using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Dizin bütünlüğü: kullanıcı yaratan HER yol dizine yazmalı. Dizinde olmayan
/// kullanıcı, kiracı veritabanları ayrıldığında (Faz 2) hangi veritabanına
/// bakılacağı çözülemediği için giriş yapamaz — ve bu, yazım anında hiçbir
/// hata vermediği için sessizce kaybolur.
/// </summary>
[Collection(ApiCollection.Name)]
public class DizinButunluguTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    /// <summary>
    /// İşe alım (aday → personel), ürünün en yüksek hacimli kullanıcı yaratma
    /// yoludur ve Faz 1a'da dizin iddiası olan tek testi yoktu.
    ///
    /// DİKKAT: `POST /api/employees` Identity kullanıcısı YARATMAZ — yalnız
    /// personel kaydı açar. Kullanıcı yaratan tek yol işe alımdır
    /// (`RecruitmentCommands.cs:378` → `CreateEmployeeLoginAsync`).
    /// </summary>
    [Fact]
    public async Task IseAlimlaOlusanKullanici_DizineYazilir()
    {
        var kiraci = await ProvisionAndActivateAsync("DizinIsealim", $"iad-{Guid.NewGuid():N}@ornek.local");
        var client = await AuthedClientAsync(kiraci.AdminEmail);

        var departmanId = await DepartmanIdAsync(kiraci.TenantId);
        var adayId = await AdayIdAsync(kiraci.TenantId, departmanId);
        var personelEpostasi = $"personel-{Guid.NewGuid():N}@ornek.local";

        var iseAl = await client.PostAsJsonAsync($"/api/candidates/{adayId}/hire", new
        {
            departmentId = departmanId,
            email = personelEpostasi,
            title = "Uzman",
            hireDate = "2024-01-01",
        });
        iseAl.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var kayit = await platform.Directory
            .FirstOrDefaultAsync(d => d.NormalizedEmail == TenantDirectoryEntry.Normalize(personelEpostasi));

        kayit.Should().NotBeNull("işe alımla oluşan kullanıcı da dizine yazılmalı");
        kayit!.TenantId.Should().Be(kiraci.TenantId);
    }

    /// <summary>
    /// Dizine yazma, kullanıcı yaratmadan ÖNCE yapılır. Kullanıcı yaratma
    /// başarısız olursa dizinde kullanıcısız bir satır kalır; aynı kiracı için
    /// yeniden denendiğinde bu satır kilit oluşturmamalı, idempotentlik
    /// sayesinde akış devam etmeli.
    /// </summary>
    [Fact]
    public async Task DizindeOksuzSatirVarsa_AyniKiraciIcinIseAlimCalisir()
    {
        var kiraci = await ProvisionAndActivateAsync("DizinOksuz", $"okz-{Guid.NewGuid():N}@ornek.local");
        var client = await AuthedClientAsync(kiraci.AdminEmail);

        var departmanId = await DepartmanIdAsync(kiraci.TenantId);
        var adayId = await AdayIdAsync(kiraci.TenantId, departmanId);
        var personelEpostasi = $"oksuz-{Guid.NewGuid():N}@ornek.local";

        // Öksüz satırı simüle et: dizinde var, Identity'de karşılığı yok.
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            platform.Directory.Add(new TenantDirectoryEntry
            {
                NormalizedEmail = TenantDirectoryEntry.Normalize(personelEpostasi),
                TenantId = kiraci.TenantId,
            });
            await platform.SaveChangesAsync(default);
        }

        var iseAl = await client.PostAsJsonAsync($"/api/candidates/{adayId}/hire", new
        {
            departmentId = departmanId,
            email = personelEpostasi,
            title = "Uzman",
            hireDate = "2024-01-01",
        });

        iseAl.IsSuccessStatusCode.Should().BeTrue(
            "aynı kiracıya ait öksüz dizin satırı, o kiracının yeniden denemesini engellememeli");
    }

    private async Task<int> DepartmanIdAsync(int tenantId)
    {
        var departman = new Domain.Entities.Organization.Department { Name = $"Dizin {Guid.NewGuid():N}"[..20] };
        await SeedInTenantAsync(tenantId, db =>
        {
            db.Departments.Add(departman);
            return Task.CompletedTask;
        });
        return departman.Id;
    }

    private async Task<int> AdayIdAsync(int tenantId, int departmanId)
    {
        var aday = new Domain.Entities.Recruitment.Candidate
        {
            Name = "Dizin Adayı",
            Email = $"aday-{Guid.NewGuid():N}@ornek.local",
        };
        await SeedInTenantAsync(tenantId, db =>
        {
            db.Candidates.Add(aday);
            return Task.CompletedTask;
        });
        return aday.Id;
    }
}
```

`using System.Net.Http.Json;` eklemeyi unutma.

- [ ] **Step 2: Testleri koş, davranışı gör**

```bash
cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~DizinButunlugu"
```

Uç nokta ve gövde alanları doğrulanmıştır: `POST /api/candidates/{id}/hire`, gövde
`CandidateHireBody` (`DepartmentId`, `Email`, `Title?`, `HireDate?`) —
bkz. `RecruitmentController.cs:69-75`.

`Candidate` varlığının zorunlu alanları farklıysa (`Name`/`Email` dışında)
`Domain/Entities/Recruitment/Candidate.cs`'i okuyup tohumlamayı düzelt; işe alım
komutu adayın `Position`'ı yoksa da çalışır (`RecruitmentCommands.cs:369` null
kontrolü var).

Beklenen sonuç: iki test de **yeşil** olmalı — Faz 1a'daki düzeltmeler bu
davranışı zaten sağladı. Kırmızı çıkarsa gerçek bir açık bulmuşsun demektir;
Görev 1 kapsamında düzelt. Bu testler koruma amaçlıdır: dizin yazımı sessiz
olduğu için regresyon aksi hâlde Faz 2'ye kadar fark edilmez.

- [ ] **Step 3: `ITenantDirectory` sözleşmesini yaz**

`backend/src/Ekipzen.Application/Common/Interfaces/ITenantDirectory.cs`:

```csharp
namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// E-posta → kiracı yönlendirme dizini. Kiracı veritabanları ayrıldığında
/// (Faz 2) login'in hangi veritabanına bakacağını buradan çözülür.
///
/// TÜRETİLMİŞ bir tablodur: asıl doğruluk kiracı veritabanındaki kullanıcılardır.
/// Bu yüzden <see cref="RebuildForTenantAsync"/> ile yetkili kaynaktan yeniden
/// kurulabilir.
///
/// Neden Identity'den ayrı: dizine artık login yolu ve bağlantı katmanı da
/// bakıyor; Identity'nin içinde kalsaydı bu katmanlar Identity'ye bağımlı olurdu.
/// </summary>
public interface ITenantDirectory
{
    /// <summary>
    /// E-postayı kiracıya rezerve eder. İdempotenttir: aynı kiracı için tekrar
    /// çağrılırsa no-op; BAŞKA bir kiracıya aitse <c>ConflictException</c>.
    /// "Tek e-posta = tek kiracı" kuralı burada, veritabanı seviyesinde uygulanır.
    /// </summary>
    Task ReserveAsync(string email, int tenantId, CancellationToken cancellationToken);

    /// <summary>E-postanın hangi kiracıya ait olduğunu döner; yoksa null.</summary>
    Task<int?> FindTenantIdAsync(string email, CancellationToken cancellationToken);

    /// <summary>Kiracının tüm dizin satırlarını siler; silinen satır sayısını döner.</summary>
    Task<int> RemoveForTenantAsync(int tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Kiracının dizinini verilen normalize e-posta listesinden yeniden kurar.
    /// Başka kiracıya ait e-postalar ATLANIR ve sonuçta raporlanır — tek çakışma
    /// yüzünden tüm yeniden kurma başarısız olmamalı (bu bir kurtarma aracıdır).
    /// </summary>
    Task<RebuildOutcome> RebuildForTenantAsync(
        int tenantId, IReadOnlyList<string> normalizedEmails, CancellationToken cancellationToken);
}

/// <summary>Yeniden kurma sonucu: yazılan kayıt ve atlanan (çakışan) e-postalar.</summary>
public sealed record RebuildOutcome(int YazilanKayit, IReadOnlyList<string> CakisanEpostalar);
```

- [ ] **Step 4: Uygulamayı yaz**

`backend/src/Ekipzen.Infrastructure/Tenancy/TenantDirectory.cs` — mevcut `IdentityService.DizineYazAsync` ve `RebuildDirectoryCommandHandler` gövdelerini buraya taşı. Davranışı **değiştirme**, yalnız yerini değiştir: idempotent rezervasyon (yok→ekle, aynı kiracı→no-op, farklı kiracı→`ConflictException`, `DbUpdateException`→`ConflictException`), ve atla-ve-raporla yeniden kurma.

- [ ] **Step 5: Çağıranları yeni soyutlamaya bağla**

- `IdentityService`: `DizineYazAsync` gövdesi `directory.ReserveAsync(...)` çağrısına iner. `IIdentityService.ReserveEmailAsync` üyesi **kaldırılır** — çağıranlar (`TenantOnboarding`, `RegisterTenantCommandHandler`, `AppDbContextInitializer`) doğrudan `ITenantDirectory` alır.
- `RebuildDirectoryCommandHandler`: `platform.Directory`'ye doğrudan erişmeyi bırakır, `directory.RebuildForTenantAsync(...)` çağırır. `RebuildDirectoryResult` alanları değişmez.
- `TenantPurger`: dizin satırı silmeyi `directory.RemoveForTenantAsync(...)` üzerinden yapar.
- DI: `services.AddScoped<ITenantDirectory, Tenancy.TenantDirectory>();`

`platform.Directory`'ye doğrudan erişen üretim kodu kalmamalı — testler erişebilir.

- [ ] **Step 6: Tüm suite'i koş**

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
```

Beklenen: 175 + eklediğin testler, hepsi PASS, uyarısız.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "refactor(tenancy): dizin erişimi Identity'den ayrı soyutlamaya çıktı"
```

---

### Task 2: Kiracı kütüğü

Kiracının durumunu her istekte platform veritabanından sormak, tek veritabanına dolambaçlı bir dönüş olurdu. Kütük bunu bellekte tutar ve durum değişince **anında** düşürür.

**Files:**
- Create: `backend/src/Ekipzen.Application/Common/Interfaces/ITenantRegistry.cs`
- Create: `backend/src/Ekipzen.Infrastructure/Tenancy/TenantRegistry.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/KiraciKutuguTests.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Produces:
  - `ITenantRegistry.GetStatusAsync(int tenantId, CancellationToken) → Task<TenantStatus?>` — kiracı yoksa null
  - `ITenantRegistry.Invalidate(int tenantId) → void`

- [ ] **Step 1: Failing testi yaz**

`backend/tests/Ekipzen.Tests.Integration/Tenancy/KiraciKutuguTests.cs`:

```csharp
using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Kiracı kütüğü: durum bellekte önbelleklenir ki her istek platform
/// veritabanına gitmesin. Ama durum değiştiğinde ANINDA düşmeli — dondurma
/// işleminin bir sonraki istekte etkili olması buna bağlı.
/// </summary>
[Collection(ApiCollection.Name)]
public class KiraciKutuguTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Kutuk_KiracininDurumunuDoner()
    {
        var kiraci = await ProvisionAndActivateAsync("Kutuk", $"kut-{Guid.NewGuid():N}@ornek.local");

        using var scope = Factory.Services.CreateScope();
        var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();

        (await kutuk.GetStatusAsync(kiraci.TenantId, default)).Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task Kutuk_VarOlmayanKiraciIcinNullDoner()
    {
        using var scope = Factory.Services.CreateScope();
        var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();

        (await kutuk.GetStatusAsync(-999, default)).Should().BeNull();
    }

    [Fact]
    public async Task Kutuk_DurumDegisipDusurulunce_YeniDurumuDoner()
    {
        var kiraci = await ProvisionAndActivateAsync("KutukDusur", $"kdz-{Guid.NewGuid():N}@ornek.local");

        // Önce önbelleğe girsin.
        using (var scope = Factory.Services.CreateScope())
        {
            var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            (await kutuk.GetStatusAsync(kiraci.TenantId, default)).Should().Be(TenantStatus.Active);
        }

        // Veritabanında değiştir ve kütüğü düşür.
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var satir = await platform.Tenants.FirstAsync(t => t.Id == kiraci.TenantId);
            satir.Status = TenantStatus.Frozen;
            await platform.SaveChangesAsync(default);

            scope.ServiceProvider.GetRequiredService<ITenantRegistry>().Invalidate(kiraci.TenantId);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var kutuk = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            (await kutuk.GetStatusAsync(kiraci.TenantId, default)).Should().Be(TenantStatus.Frozen,
                "düşürülen kayıt bir sonraki okumada veritabanından tazelenmeli");
        }
    }
}
```

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~KiraciKutugu"
```

Beklenen: derleme hatası — `ITenantRegistry` yok.

- [ ] **Step 3: Sözleşmeyi yaz**

`backend/src/Ekipzen.Application/Common/Interfaces/ITenantRegistry.cs`:

```csharp
using Ekipzen.Domain.Entities.Tenancy;

namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracı kütüğü: kiracının durumunu bellekte önbellekler.
///
/// Neden var: erişim kapısı her istekte kiracının durumunu soruyor. Bunu her
/// seferinde platform veritabanından okumak, ayırdığımız katmanı her istekte
/// yeniden birleştirmek olurdu.
///
/// Faz 2'de bu kütük katalog adını da taşıyacak; bugün yalnız durum yeterli
/// çünkü tüm kiracılar aynı veritabanını paylaşıyor.
/// </summary>
public interface ITenantRegistry
{
    /// <summary>Kiracının durumu; kiracı yoksa <c>null</c>.</summary>
    Task<TenantStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Önbellek kaydını düşürür. Durum değiştiren her yol bunu çağırmalı —
    /// dondurmanın bir sonraki istekte etkili olması buna bağlıdır, süre
    /// dolmasını beklemeyiz.
    /// </summary>
    void Invalidate(int tenantId);
}
```

- [ ] **Step 4: Uygulamayı yaz**

`backend/src/Ekipzen.Infrastructure/Tenancy/TenantRegistry.cs`:

```csharp
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ekipzen.Infrastructure.Tenancy;

/// <summary>
/// Kütüğün önbelleği SINGLETON'dır (tüm istekler paylaşır), ama platform
/// context'i SCOPED'dır. Bu yüzden okuma anında kendi kapsamını açar —
/// singleton'ın scoped bir bağımlılığı yakalaması (captive dependency)
/// bağlantıyı ilk isteğe yapıştırırdı.
/// </summary>
public sealed class TenantRegistry(IServiceScopeFactory scopeFactory, IMemoryCache cache) : ITenantRegistry
{
    // Kısa TTL yalnız emniyet ağıdır: asıl tazeleme Invalidate ile ANINDA olur.
    // Bir yol Invalidate çağırmayı unutursa değişiklik en geç bu süre içinde görünür.
    private static readonly TimeSpan Omur = TimeSpan.FromMinutes(5);

    private static string Anahtar(int tenantId) => $"kiraci-durum-{tenantId}";

    public async Task<TenantStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Anahtar(tenantId), out TenantStatus? onbellekli))
        {
            return onbellekli;
        }

        using var scope = scopeFactory.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

        var durum = await platform.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => (TenantStatus?)t.Status)
            .FirstOrDefaultAsync(cancellationToken);

        cache.Set(Anahtar(tenantId), durum, Omur);
        return durum;
    }

    public void Invalidate(int tenantId) => cache.Remove(Anahtar(tenantId));
}
```

`using Microsoft.Extensions.DependencyInjection;` eklemeyi unutma.

- [ ] **Step 5: Kayıt ve düşürme çağrıları**

DI'a ekle:

```csharp
        services.AddMemoryCache();
        services.AddSingleton<ITenantRegistry, Tenancy.TenantRegistry>();
```

`Tenant.Status`'u değiştiren **her** yol `Invalidate` çağırmalı. Bunları bul ve ekle: `TenantOnboarding` (provizyon sonu), `IdentityService` davet kabulü, `TenantPurger` (Purging'e geçiş), `RebuildDirectoryCommand` durum değiştirmiyorsa dokunma. Kod tabanında `Status =` ataması yapan tüm yerleri **grep ile** bul, hiçbirini atlama.

- [ ] **Step 6: Testleri koş ve commit'le**

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
git add -A && git commit -m "feat(tenancy): kiracı kütüğü — durum önbelleği ve anında düşürme"
```

---

### Task 3: Erişim kapısı

Bugün dondurma yalnız yeni girişleri kesiyor; elinde refresh token olan biri oturumunu süresiz uzatabiliyor. Kapı iki darboğaza yerleşince bu yapısal olarak kapanır.

**Files:**
- Create: `backend/src/Ekipzen.Application/Common/Interfaces/ITenantAccessGuard.cs`
- Create: `backend/src/Ekipzen.Infrastructure/Tenancy/TenantAccessGuard.cs`
- Create: `backend/src/Ekipzen.Api/Middleware/TenantAccessMiddleware.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/ErisimKapisiTests.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs`
- Modify: `backend/src/Ekipzen.Api/Program.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `ITenantRegistry` (Görev 2)
- Produces: `ITenantAccessGuard.EnsureAccessibleAsync(int tenantId, CancellationToken) → Task` — erişilebilir değilse `TenantInaccessibleException`

- [ ] **Step 1: Failing testleri yaz**

`backend/tests/Ekipzen.Tests.Integration/Tenancy/ErisimKapisiTests.cs`:

```csharp
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
/// Erişim kapısı: dondurulmuş bir kiracının HİÇBİR yolu çalışmamalı.
///
/// Faz 1a'da kapı yalnız login'deydi; elinde geçerli token olan kullanıcı
/// çalışmaya, elinde refresh token olan da oturumunu uzatmaya devam
/// edebiliyordu. Bu testler üç yolun üçünü de kapatır.
/// </summary>
[Collection(ApiCollection.Name)]
public class ErisimKapisiTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    /// <summary>
    /// Login 403 döner, 401 değil: kimlik bilgileri DOĞRU, engelleyen şey
    /// kiracının kapalı olması. 401 "parolan yanlış" anlamına gelir ve
    /// kullanıcıyı yanlış yönlendirirdi.
    /// </summary>
    [Fact]
    public async Task DondurulmusKiraci_LoginYapamaz()
    {
        var (eposta, tenantId) = await AktifKiraciAsync("KapiLogin");
        await DurumDegistirAsync(tenantId, TenantStatus.Frozen);

        var yanit = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });

        yanit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DondurulmusKiraci_ElindekiTokenlaIstekYapamaz()
    {
        var (eposta, tenantId) = await AktifKiraciAsync("KapiIstek");
        var client = await AuthedClientAsync(eposta);

        // Token dondurmadan ÖNCE alındı ve hâlâ geçerli.
        (await client.GetAsync("/api/departments")).StatusCode.Should().Be(HttpStatusCode.OK);

        await DurumDegistirAsync(tenantId, TenantStatus.Frozen);

        var yanit = await client.GetAsync("/api/departments");
        yanit.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "geçerli token, dondurulmuş kiracıya erişim hakkı vermemeli");
    }

    [Fact]
    public async Task DondurulmusKiraci_RefreshIleOturumUzatamaz()
    {
        var (eposta, tenantId) = await AktifKiraciAsync("KapiRefresh");

        var girisYaniti = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = eposta, password = DefaultPassword });
        var auth = (await girisYaniti.Content.ReadFromJsonAsync<Application.Features.Auth.AuthResponse>())!;

        await DurumDegistirAsync(tenantId, TenantStatus.Frozen);

        var yanit = await Factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });

        yanit.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "dondurulmuş kiracının refresh token'ı yeni access token üretmemeli");
    }

    [Fact]
    public async Task AktifKiraci_UcYoldaDaCalisir()
    {
        var (eposta, _) = await AktifKiraciAsync("KapiAktif");
        var client = await AuthedClientAsync(eposta);

        (await client.GetAsync("/api/departments")).StatusCode.Should().Be(HttpStatusCode.OK,
            "kapı, aktif kiracıyı engellememelidir");
    }

    private async Task<(string Eposta, int TenantId)> AktifKiraciAsync(string ad)
    {
        var eposta = $"{ad.ToLowerInvariant()}-{Guid.NewGuid():N}@ornek.local";
        var kiraci = await ProvisionAndActivateAsync(ad, eposta);
        return (eposta, kiraci.TenantId);
    }

    private async Task DurumDegistirAsync(int tenantId, TenantStatus durum)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var satir = await platform.Tenants.FirstAsync(t => t.Id == tenantId);
        satir.Status = durum;
        await platform.SaveChangesAsync(default);
        scope.ServiceProvider.GetRequiredService<ITenantRegistry>().Invalidate(tenantId);
    }
}
```

Refresh uç noktasının gerçek yolunu ve gövde alan adını `AuthController`'dan **doğrula**; farklıysa testi ona göre düzelt.

- [ ] **Step 2: Testleri koş**

```bash
cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~ErisimKapisi"
```

Beklenen: `LoginYapamaz` ve `UcYoldaDaCalisir` yeşil (mevcut davranış), `ElindekiTokenlaIstekYapamaz` ve `RefreshIleOturumUzatamaz` **kırmızı** — kapatacağımız açık bunlar.

- [ ] **Step 3: İstisna tipini ve sözleşmeyi yaz**

`backend/src/Ekipzen.Application/Common/Exceptions/` altındaki mevcut istisna desenini izleyerek `TenantInaccessibleException` ekle ve `GlobalExceptionHandler`'da **403 Forbidden**'a eşle. Gerekçe: kimlik doğrulama başarılı (401 değil), ama kiracı erişime kapalı.

`backend/src/Ekipzen.Application/Common/Interfaces/ITenantAccessGuard.cs`:

```csharp
namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracının erişilebilir olduğunu doğrular. Yalnız <c>Active</c> geçer;
/// <c>Provisioning</c> (kurulum sürüyor/yarıda kaldı), <c>Frozen</c>
/// (bakım/geri yükleme) ve <c>Purging</c> (siliniyor) reddedilir.
/// </summary>
public interface ITenantAccessGuard
{
    /// <summary>Erişilebilir değilse <c>TenantInaccessibleException</c> fırlatır.</summary>
    Task EnsureAccessibleAsync(int tenantId, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Kapıyı uygula**

`backend/src/Ekipzen.Infrastructure/Tenancy/TenantAccessGuard.cs` — `ITenantRegistry`'den durumu okur, `Active` değilse `TenantInaccessibleException` fırlatır. Kiracı bulunamazsa da fırlatır (var olmayan kiracıya erişim verilmez). Mesaj kullanıcıya anlamlı olsun ama **iç durum adını sızdırmasın** — "Şirket hesabınız şu anda kullanıma kapalı. Yöneticinizle iletişime geçin." yeterli; gerçek durum loga yazılır.

- [ ] **Step 5: Token darboğazına yerleştir**

`IdentityService.IssueTokensAsync`'in **en başına** kapı çağrısını ekle:

```csharp
        // Kapı burada: login ve refresh'in ORTAK yolu burasıdır, dolayısıyla
        // ikisi de tek noktadan korunur. Faz 1a'da kapı yalnız login'deydi ve
        // refresh onu atlıyordu.
        await accessGuard.EnsureAccessibleAsync(user.TenantId, cancellationToken);
```

`LoginAsync`'teki mevcut kiracı kontrolünü **kaldır** — iki doğruluk kaynağı bırakma; kapı artık `IssueTokensAsync`'te.

**Bilinçli sözleşme değişikliği:** dondurulmuş kiracıda login artık **401 değil 403** döner. Gerekçe: kimlik bilgileri doğru, engelleyen şey kiracının kapalı olması; 401 "parolan yanlış" anlamına gelir ve kullanıcıyı yanlış yönlendirir. Bunu koda yorum olarak yaz.

Bu değişiklik üç yerde takip gerektirir, üçünü de yap:

1. **`PlatformKatmaniTests.DondurulmusKiraci_GirisYapamaz`** (`PlatformKatmaniTests.cs:103-119`) — `HttpStatusCode.Unauthorized` bekliyor, **403'e güncelle.**
2. **`TenantProvisioningTests.Login_InactiveTenant_Rejected`** (`TenantProvisioningTests.cs:80-99`) — aynı davranışı sınıyor ve o da 401 bekliyor, **403'e güncelle.** Bu iki test kısmen örtüşüyor (Faz 1a ledger'ında not edilmişti); `ErisimKapisiTests` artık aynı davranışı daha geniş kapsamda sınadığı için ikisinden birini **silmek** de meşru bir seçim — kararını commit mesajında gerekçelendir.
3. **Frontend:** `frontend/src/api/client.ts` 401'i özel olarak ele alıyor (önce refresh dener, sonra oturumu düşürür). 403 o yola girmez — dondurulmuş kiracının kullanıcısı sonsuz refresh döngüsüne düşmez, bu doğru davranış. Yine de frontend'in 403'te anlamlı bir mesaj gösterdiğini **kontrol et**; göstermiyorsa bu Faz 1b kapsamı dışıdır, yalnız rapora not düş.

**Uyarı:** bu iki testi güncellemeden kapıyı devreye alırsan suite kırmızıya döner ve sebebini aramakla vakit kaybedersin.

- [ ] **Step 6: HTTP boru hattına yerleştir**

`backend/src/Ekipzen.Api/Middleware/TenantAccessMiddleware.cs` — kimliği doğrulanmış istekte JWT'deki `tenant` claim'ini okur, kapıyı çağırır. Claim yoksa (anonim uç, platform-key ucu, health) dokunmadan geçirir.

`Program.cs`'te **`UseAuthentication()`'dan SONRA, `UseAuthorization()`'dan ÖNCE** ekle — claim'ler ancak kimlik doğrulandıktan sonra okunabilir.

- [ ] **Step 7: Testleri koş ve commit'le**

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
git add -A && git commit -m "feat(tenancy): erişim kapısı — token üretimi ve HTTP boru hattı"
```

---

### Task 4: Bağlantı çözücü

Bağlantı dizesi artık kiracıdan çözülür. Bu fazda çözücü herkese aynı dizeyi döndürür — ayrılma noktası kurulur, ama henüz ayrılmaz.

**Files:**
- Create: `backend/src/Ekipzen.Application/Common/Interfaces/ITenantConnectionResolver.cs`
- Create: `backend/src/Ekipzen.Infrastructure/Tenancy/TenantConnectionResolver.cs`
- Create: `backend/tests/Ekipzen.Tests.Unit/Tenancy/BaglantiCozucuTests.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Produces: `ITenantConnectionResolver.ResolveFor(int? tenantId) → string`

- [ ] **Step 1: Failing birim testini yaz**

`backend/tests/Ekipzen.Tests.Unit/Tenancy/BaglantiCozucuTests.cs`:

```csharp
using FluentAssertions;
using Ekipzen.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ekipzen.Tests.Unit.Tenancy;

/// <summary>
/// Bağlantı çözücü, kiracıdan veritabanına giden ayrılma noktasıdır.
/// Faz 1b'de HERKESE aynı dizeyi döndürür — tesisat kurulur ama veri bölünmez.
/// Faz 2'de burası kiracının katalog adını üretecek.
/// </summary>
public class BaglantiCozucuTests
{
    private const string Dize = "Server=localhost;Database=EkipzenDb;Trusted_Connection=True;";

    private static TenantConnectionResolver Cozucu() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = Dize,
            })
            .Build());

    [Fact]
    public void Cozucu_BuFazdaTumKiracilaraAyniDizeyiDoner()
    {
        var cozucu = Cozucu();

        cozucu.ResolveFor(1).Should().Be(Dize);
        cozucu.ResolveFor(2).Should().Be(Dize);
        cozucu.ResolveFor(null).Should().Be(Dize,
            "kiracı bağlamı olmayan işler (migration, platform işlemleri) de çalışabilmeli");
    }

    [Fact]
    public void Cozucu_BaglantiDizesiYoksaAnlamliHataVerir()
    {
        var cozucu = new TenantConnectionResolver(new ConfigurationBuilder().Build());

        FluentActions.Invoking(() => cozucu.ResolveFor(1))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*DefaultConnection*");
    }
}
```

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~BaglantiCozucu"
```

Beklenen: derleme hatası.

- [ ] **Step 3: Sözleşme ve uygulamayı yaz**

`ITenantConnectionResolver.ResolveFor(int? tenantId)` — bu fazda `tenantId` **kullanılmaz**. Bunu yoruma açıkça yaz, yoksa okuyan "ölü parametre" sanıp siler:

```csharp
    /// <summary>
    /// Kiracının veritabanı bağlantı dizesi.
    ///
    /// Faz 1b'de tüm kiracılar aynı veritabanını paylaştığı için parametre
    /// KULLANILMAZ — ama imzada durur, çünkü ayrılma noktası budur: Faz 2'de
    /// burası Tenants.DatabaseName'den katalog adı üretecek ve çağıranların
    /// hiçbiri değişmeyecek.
    /// </summary>
    string ResolveFor(int? tenantId);
```

- [ ] **Step 4: `AddDbContext`'i çözücüye bağla**

`DependencyInjection.cs`'te `AppDbContext` kaydını değiştir:

```csharp
        services.AddScoped<ITenantConnectionResolver, Tenancy.TenantConnectionResolver>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            // Bağlantı KAPSAM BAŞINA çözülür: aktif kiracı neyse ona göre.
            // Faz 1b'de sonuç herkes için aynı, ama çağrı yolu artık doğru yerden
            // geçiyor — Faz 2'de yalnız çözücünün içi değişecek.
            var cozucu = sp.GetRequiredService<ITenantConnectionResolver>();
            var kiraci = sp.GetRequiredService<ICurrentTenant>();

            options.UseSqlServer(cozucu.ResolveFor(kiraci.TenantId), sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });
```

`PlatformDbContext` kaydına **dokunma** — platform veritabanı kiracıdan bağımsızdır.

- [ ] **Step 5: Tüm suite'i koş**

Bu adım kritik: 175+ testin tamamı geçmeli. Geçmiyorsa çözücü davranışı değiştirmiş demektir — bu fazın tek kuralı davranışın değişmemesi.

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(tenancy): bağlantı çözücü — kiracıdan veritabanına giden ayrılma noktası"
```

---

### Task 5: Kiracıya sabitlenmiş kapsam fabrikası

`ICurrentTenant.Impersonate` bugün yalnız bir tamsayı atıyor. Görev 4'ten sonra kiracı, DbContext oluşturulurken **bağlantıyı belirliyor** — yani `Impersonate`'i context alındıktan sonra çağırırsan context yanlış kiracıyla kurulmuş olur ve bu **sessizce** olur. Sıraya güvenmek yerine sırayı imkânsız kılıyoruz.

**Files:**
- Create: `backend/src/Ekipzen.Application/Common/Interfaces/ITenantScopeFactory.cs`
- Create: `backend/src/Ekipzen.Infrastructure/Tenancy/TenantScopeFactory.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/KiraciKapsamiTests.cs`
- Modify: `backend/src/Ekipzen.Api/Services/UnverifiedTenantCleanupService.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs`
- Modify: `backend/tests/Ekipzen.Tests.Integration/Tenancy/TenancyTestBase.cs`

**Interfaces:**
- Produces:
  - `ITenantScopeFactory.Create(int tenantId) → ITenantScope`
  - `ITenantScope : IDisposable` — `IServiceProvider Services { get; }`

- [ ] **Step 1: Failing testi yaz**

`backend/tests/Ekipzen.Tests.Integration/Tenancy/KiraciKapsamiTests.cs`:

```csharp
using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Kiracıya sabitlenmiş kapsam: kiracı, kapsam açılırken sabitlenir ve
/// içinden çıkan HER servis onu görür. Böylece "önce impersonate, sonra
/// context al" sırasını yanlış yapmak imkânsızlaşır — o hata sessizce
/// yanlış veritabanına bağlanmakla sonuçlanırdı.
/// </summary>
[Collection(ApiCollection.Name)]
public class KiraciKapsamiTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Kapsam_IcindekiServisleriVerilenKiraciyaBaglar()
    {
        var kiraci = await ProvisionAndActivateAsync("Kapsam", $"kps-{Guid.NewGuid():N}@ornek.local");

        var fabrika = Factory.Services.GetRequiredService<ITenantScopeFactory>();
        using var kapsam = fabrika.Create(kiraci.TenantId);

        kapsam.Services.GetRequiredService<ICurrentTenant>().TenantId
            .Should().Be(kiraci.TenantId, "kapsam kiracıyı, içinden servis çözülmeden ÖNCE sabitlemeli");
    }
}
```

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~KiraciKapsami"
```

Beklenen: derleme hatası.

- [ ] **Step 3: Sözleşme ve uygulamayı yaz**

Fabrika `IServiceScopeFactory` ile kapsam açar, **kapsamı döndürmeden önce** `ICurrentTenant.Impersonate(tenantId)` çağırır. Sıra yorumla açıklansın.

- [ ] **Step 4: Elle impersonate eden yolları fabrikaya geçir**

Kod tabanında `Impersonate(` çağıran **tüm** yerleri grep ile bul. HTTP dışı olanları (arka plan servisi, purger, seed, test yardımcıları) fabrikaya geçir. `TenancyTestBase.SeedInTenantAsync` de fabrikayı kullanmalı — test altyapısı üretim desenini taklit etsin.

`ICurrentTenant.Impersonate`'i kaldırma (fabrika onu kullanıyor), ama XML dokümanına "doğrudan çağırma; `ITenantScopeFactory` kullan" notu ekle.

- [ ] **Step 5: Testleri koş ve commit'le**

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
git add -A && git commit -m "feat(tenancy): kiracıya sabitlenmiş kapsam fabrikası"
```

---

### Task 6: Login dizin üzerinden

Login bugün `FindByEmailAsync` ile kullanıcıyı bulup `user.TenantId`'yi okuyor. Dizin üzerinden çözmek, dizini "yazılan ama kullanılmayan" bir tablodan **yük taşıyan** bir tabloya çevirir — böylece bir dizin hatası Faz 2'de değil, bugün görünür olur.

**Files:**
- Modify: `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/LoginDizinTests.cs`

- [ ] **Step 1: Failing testi yaz**

`backend/tests/Ekipzen.Tests.Integration/Tenancy/LoginDizinTests.cs`:

```csharp
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
}
```

- [ ] **Step 2: Testi koş**

Beklenen: ilk iddia **kırmızı** (bugün login dizine bakmıyor, giriş başarılı olur).

- [ ] **Step 3: `LoginAsync`'i dizin üzerinden çöz**

Sıra: e-posta → `directory.FindTenantIdAsync` → yoksa `UnauthorizedException` (genel mesaj, hesabın varlığını sızdırma) → kullanıcıyı bul → parola doğrula → `IssueTokensAsync` (kapı orada).

Kullanıcı bulunduktan sonra `user.TenantId` ile dizinden gelen kiracı **uyuşmuyorsa** bu bir tutarsızlıktır: girişi reddet ve **loga yüksek sesle yaz**. Faz 2'de bu, yanlış veritabanına bakmak demek olurdu.

- [ ] **Step 4: Testleri koş ve commit'le**

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
git add -A && git commit -m "feat(auth): login kiracıyı yönlendirme dizininden çözer"
```

---

### Task 7: Doğrulama ve dokümantasyon

**Files:**
- Modify: `docs/superpowers/plans/2026-08-07-faz1b-baglanti-tesisati.md` (bu dosya — sonuç bölümü)
- Modify: `docs/rehberler/` altındaki mimari belgesi (varsa)
- Modify: `docs/yedekleme-ve-kurtarma.md`

- [x] **Step 1: Uçtan uca elle doğrulama**

İki veritabanını da düşür, uygulamayı aç, şunları sırayla yap ve çıktıları rapora yaz:

1. Demo kullanıcıyla giriş yap → başarılı olmalı
2. Kiracıyı `Frozen` yap → aynı token'la istek at → **403**
3. Refresh dene → **403** (gerçek ve test edilen davranış; erişim kapısı bu ucu da
   diğerleriyle aynı şekilde keser — düzeltildi, bkz. nihai dal incelemesi madde 4)
4. Yeni giriş dene → reddedilmeli
5. Kiracıyı `Active`'e döndür → üçü de tekrar çalışmalı
6. Dizin kaydını sil → giriş başarısız → `rebuild-directory` → giriş tekrar çalışıyor

- [x] **Step 2: Runbook'a dondurma prosedürünü ekle**

`docs/yedekleme-ve-kurtarma.md`'deki geri yükleme adımlarında "kiracıyı dondur" adımı artık **gerçek**: `Tenants.Status = Frozen` yapmanın tüm erişimi kestiğini ve kütüğün anında düştüğünü yaz. Faz 1a'da bu adım yalnız yeni girişleri kesiyordu — o uyarıyı kaldır.

- [x] **Step 3: Plan sonucu bölümü**

Bu plana "Uygulama sonucu" bölümü ekle: görev sayısı, commit sayısı, test tabanı değişimi, incelemede yakalananlar, Faz 2'ye devredilenler.

- [x] **Step 4: Tam doğrulama ve commit**

```bash
cd backend && dotnet build --configuration Release -warnaserror && dotnet test --configuration Release
cd ../frontend && npx vitest run && npx tsc -b && npx oxlint
git add -A && git commit -m "docs(tenancy): Faz 1b sonucu ve dondurma prosedürü"
```

---

## Faz sonu doğrulama

- [x] Dondurulmuş kiracı: login, refresh ve yetkili istek — **üçü de** reddediliyor
- [x] Aktif kiracı hiçbir yerde engellenmiyor
- [x] Kütük durum değişiminde anında düşüyor
- [x] Arka plan işleri ve seed kiracıya sabitlenmiş kapsam kullanıyor; elle `Impersonate`
      tek istisnayla kalmıştır — `AppDbContextInitializer.cs:187`, BİLİNÇLİ ve gerekçeli
      (dosyada 173-186 satırlarında 14 satırlık açıklama var: bu sınıfın tüm bağımlılıkları
      constructor'da zaten kurulduğundan yeni bir fabrika kapsamı açmak ambient `context`'i
      etkilemez, düzeltme Program.cs'in bu sınıfı hangi kapsamdan çözdüğünü değiştirmeyi
      gerektirir — Faz 2'ye devredildi). Nihai dal incelemesi madde 1 kapsamında
      `UserDirectorySource` (`backend/src/Ekipzen.Infrastructure/Identity/UserDirectorySource.cs`)
      ikinci bir istisna olacaktı — bu görevde `ITenantScopeFactory`'ye geçirilerek kapatıldı.
- [x] Login dizin üzerinden çözüyor; dizin silinince giriş kesiliyor, yeniden kurulunca dönüyor
- [x] Bağlantı çözücü devrede ve tüm testler geçiyor (davranış değişmedi)
- [x] Derleme uyarısız, backend ve frontend suite'leri yeşil

**Sonraki:** Faz 2 — ayrılma. Provizyon gerçek `CREATE DATABASE` yapar, `Tenants.DatabaseName` gelir, `Subscriptions` platforma taşınır, çözücünün içi katalog adı üretmeye başlar.

---

## Uygulama sonucu

**Görev sayısı:** 7 (Task 1–7, planlandığı gibi tamamlandı).

**Commit sayısı:** Plan commit'i (`ce7cad4`) dahil olmak üzere Faz 1b'de toplam
12 commit: 5 özellik commit'i (dizin ayrımı, kiracı kütüğü, erişim kapısı,
bağlantı çözücü, sabitlenmiş kapsam fabrikası + login dizin çözümü), 2 düzeltme
turu commit'i (erişim kapısının aşırı-kapatması; bağlantı dizesi doğrulamasının
açılışa geri getirilmesi), 1 dizin/Identity tutarsızlığı testi ve bu görevin
(Task 7) dondurma/çözme ucu + doğrulama + dokümantasyon commit'leri.

**Test tabanı değişimi:** Faz 1b başlangıcında 175 test (Faz 1a sonu) idi;
Task 1–6 sonunda 50 birim + 142 entegrasyon = **192**'ye çıktı. Bu görev
(Task 7) planın öngörmediği bir uç (dondurma/çözme) eklediği için 10 yeni
entegrasyon testi (`TenantFreezeTests.cs`) gerekti: **50 birim + 152
entegrasyon = 202 test**, hepsi yeşil. Hiçbir test azalmadı/kaldırılmadı.

**Plandan sapma — dondurma/çözme ucu:** Plan Task 7'yi yalnız "doğrulama +
dokümantasyon" olarak tanımlıyordu, ama erişim kapısının kurduğu yeteneğin
(kiracı durumu → erişim) operatör tarafından desteklenen bir yolu yoktu.
Yedekleme runbook'unun geri yükleme prosedürü "kiracıyı dondur" diyordu, tek
seçenek `Tenants.Status`'a doğrudan SQL yazmaktı — ki bu kütüğü (`ITenantRegistry`)
düşürmediği için değişiklik en geç 5 dakika (TTL) gecikirdi. Bu yüzden
`TenancyController`'a platform-key korumalı iki yeni uç eklendi:

- `POST /api/tenants/{id}/freeze` — Active → Frozen, kütüğü ANINDA düşürür.
- `POST /api/tenants/{id}/unfreeze` — Frozen → Active, kütüğü ANINDA düşürür.

Her ikisi de `Provisioning`/`Purging` durumundaki bir kiracıyı 409 ile
reddeder (o durumlar kendi yaşam döngüsüne aittir, elle oynanmaz) ve hedef
durumdaki bir kiracı için idempotent no-op döner (operatör retry'ında hataya
çarpmaz). Var olmayan kiracı 404. Bu, teslim edilebilirin tamamlanması olarak
eklendi — kapsamın genişletilmesi değil.

**İncelemede yakalananlar (Task 1–6, önceki turlar):**
- Erişim kapısının isteğin taşıdığı (dondurulmuş kiracıdan kalma) token'a değil,
  UCUN kendi yetkilendirme gereksinimine bağlı olması gerektiği — aksi halde
  eski bir token taşıyan istemci BAŞKA aktif bir kiracıya login yapamazdı
  [inceleme #1/#2].
- Bağlantı dizesi doğrulamasının açılışta (fail-fast) kalması gerektiği; kapsam
  fabrikasına taşınırken kaybolma riski [düzeltme turu 1].
- Dizin/Identity tutarsızlığında (dizin var ama Identity'de kullanıcı yok gibi
  uç durumlar) girişin güvenli tarafta reddedilmesi gerektiği — bu görevde
  (Task 7 öncesi) ayrı bir testle güvenceye alındı.

**Faz 2'ye devredilenler:**
- Provizyonun gerçek `CREATE DATABASE` yapması, `Tenants.DatabaseName` alanı.
- `Subscriptions`'ın platform veritabanına taşınması.
- Bağlantı çözücünün içinin katalog adı üretmeye başlaması (bugün hâlâ tek
  paylaşılan `EkipzenDb`).
- Kiracı başına gerçek fiziksel izolasyon ve tek-kiracı nokta-zaman geri yükleme
  (bkz. `docs/superpowers/specs/2026-08-06-kiraci-basina-veritabani-design.md`).
  Bugünkü `freeze`/`unfreeze` ucu bu tasarımın öngördüğü "1. `Status = Frozen`"
  adımını zaten karşılıyor; Faz 2 yalnız 2–4. adımları (gerçek `RESTORE
  DATABASE`, dosya arşivi, dizin yeniden kurma) otomatikleştirecek.

---

## Faz 2'ye devredilenler (nihai inceleme sonrası)

### Üretim öncesi kapatılmalı — bu fazın hatası değil, ama kapının yanındaki delik

**`POST /api/auth/register` anonim olarak gerçek bir müşterinin kiracısına hesap açıyor.**

`RegisterCommand` → `IdentityService.RegisterAsync` → `DefaultTenantIdAsync()` =
**platformdaki en düşük Id'li kiracı** (üretimde gerçek bir müşteri). Yeni kullanıcı
`employee` rolüyle, `EmailConfirmed=true` olarak oluşuyor, dizine yazılıyor ve
erişim kapısı **bilinçli olarak atlanarak** (`skipAccessCheck: true`) anında token
alıyor.

O token şunlara erişir: `/api/dashboard/overview` (personel sayısı, bekleyen
izinler, aday hunisi), `/api/leaves/*`, `/api/me`, `/api/payroll/my`,
`/api/settings/company/logo`.

Bu açık Faz 1a'dan önce de vardı; bu dal onu ne getirdi ne büyüttü — bu yüzden
birleştirmeyi bloke etmedi. Ama **ilk gerçek müşteriden önce kapatılmalı**:
ya uç kaldırılmalı (şirket kaydı için `/api/tenants/signup` ayrı ve doğru akış
zaten var), ya da davet zorunlu kılınmalı.

### Faz 2'de yapılması zorunlu olanlar

| Madde | Neden |
| --- | --- |
| `LoginAsync` / `RefreshAsync` kiracı kapsamına alınmalı | Bugün dizinden kiracıyı çözüyor ama `userManager` ve `context.RefreshTokens` hâlâ kiracısız kapsamın context'iyle çalışıyor. Kiracı DB'leri ayrılınca yanlış katalogda arama demek. Kodda yorumla işaretlendi. |
| `AppDbContextInitializer.SeedDefaultTenantAsync` | Tek kalan elle `Impersonate` (satır ~187). Düzeltme bu dosyada değil, `Program.cs`'in initializer'ı kiracı kapsamından çözmesinde. Yalnız Development seed'ini etkiliyor. |
| `AcceptInviteAsync` durum makinesini atlıyor | Herhangi bir `Provisioning` kiracıyı şifre belirlenir belirlenmez `Active` yapıyor. Faz 2'de **var olmayan bir veritabanına Active kiracı** demek. |
| `TenantOnboarding` / `RegisterTenantCommand` sıra kopyası | Self-servis kayıt ortak yolu kullanmıyor, sırayı kopyalıyor. İki kopya = sessiz sapma riski. |

### Ertelenebilir notlar

- `TenantRegistry` `IMemoryCache` (process-içi) kullanıyor. Runbook "kütük anında
  düşer" garantisi veriyor — bu **tek instance** varsayımıdır. Ölçeklenmiş kurulumda
  diğer instance'lar en geç TTL (5 dk) kadar gecikir.
- `docs/rehberler/06-multi-tenancy.md` hâlâ `Impersonate`'i doğrudan çağırmayı
  öğretiyor ve erişim kapısı / freeze uçlarından söz etmiyor.
- Login'de zamanlama yan kanalı (var olmayan e-posta parola hash'i hesaplanmadan
  döner). Bu diff'le gelmedi; sabit-zamanlı dummy-hash ayrı bir sertleştirme işi.
- `frontend/src/api/client.ts` yalnız 401'i özel ele alıyor; oturum ortasında kiracı
  dondurulunca 403 dönüyor ama oturum temizlenmiyor.
- Platform-key karşılaştırması sabit-zamanlı değil (`TenancyController`).
- `TenantInaccessibleException` `ForbiddenAccessException`'dan türemiyor;
  `GlobalExceptionHandler`'da iki özdeş 403 kolu var.
