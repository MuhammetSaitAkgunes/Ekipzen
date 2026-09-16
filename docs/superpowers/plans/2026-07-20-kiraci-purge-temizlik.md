# Kiracı Purge & Doğrulanmamış Kiracı Temizliği Implementasyon Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) veya subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Bir kiracının TÜM verisini (tüm ITenantScoped tablolar, kullanıcılar, refresh token'lar, fiziksel dosyalar, kiracı satırı) tek işlemde güvenli silen bir mekanizma; ve bunu kullanarak doğrulanmamış (hiç etkinleşmemiş) self-servis kiracıların periyodik temizliği. KVKK "unutulma hakkı"nın operasyonel karşılığı (T5.4 Bölüm 7.1).

**Architecture:** Infrastructure'da `ITenantPurger`, EF Core model metadata'sından ITenantScoped **tablo** tiplerini (keyless view'ler hariç) bulur, FK bağımlılık grafiğine göre **çocuk→ebeveyn** sırada `DELETE FROM [tablo] WHERE TenantId=@id` çalıştırır; öncesinde fiziksel dosyaları, sonrasında kullanıcı/refresh token ve kiracı satırını siler — hepsi tek transaction. Platform-key korumalı uçlar tetikler.

**Tech Stack:** .NET 9, EF Core (raw SQL + metadata), MediatR, ASP.NET Identity.

## Global Constraints

- Yalnız hedef kiracının verisi silinir; **başka kiracı asla etkilenmez** (entegrasyon testiyle kanıt).
- Yıkıcı işlem: platform-key + `confirmSlug` eşleşmesi zorunlu.
- Tek transaction; kısmi silme bırakmaz.
- Mevcut tüm testler yeşil kalır; commit sonu `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Dal: `main`'den `feature/tenant-purge`.

---

## Dosya Yapısı

- **Create:** `backend/src/Ekipzen.Application/Common/Interfaces/ITenantPurger.cs` — soyutlama.
- **Create:** `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs` — implementasyon.
- **Modify:** `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs` — DI kaydı.
- **Create:** `backend/src/Ekipzen.Application/Features/Tenancy/Commands/PurgeTenantCommand.cs`
- **Create:** `backend/src/Ekipzen.Application/Features/Tenancy/Commands/CleanupUnverifiedTenantsCommand.cs`
- **Modify:** `backend/src/Ekipzen.API/Controllers/TenancyController.cs` — `DELETE /api/tenants/{id}`, `POST /api/tenants/cleanup-unverified`.
- **Create:** `backend/tests/Ekipzen.Tests.Integration/Tenancy/TenantPurgeTests.cs`
- **Modify:** `docs/kvkk-veri-izolasyonu.md` — Bölüm 7.1 güncelle.
- **Modify:** `docs/gelistirme-gunlugu.md` — kayıt.

---

## Task 1: TenantPurger (çekirdek silme mekanizması)

**Files:**
- Create: `backend/src/Ekipzen.Application/Common/Interfaces/ITenantPurger.cs`
- Create: `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/TenantPurgeTests.cs`

**Interfaces:**
- Produces: `interface ITenantPurger { Task PurgeAsync(int tenantId, CancellationToken); }`

- [ ] **Step 1: Soyutlama**

```csharp
// backend/src/Ekipzen.Application/Common/Interfaces/ITenantPurger.cs
namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Bir kiracının tüm verisini kalıcı siler (KVKK unutulma hakkı). Tüm ITenantScoped
/// tablolar + kullanıcılar + refresh token'lar + fiziksel dosyalar + kiracı satırı,
/// tek transaction. Yalnız hedef kiracı etkilenir.
/// </summary>
public interface ITenantPurger
{
    Task PurgeAsync(int tenantId, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Failing entegrasyon testi**

```csharp
// backend/tests/Ekipzen.Tests.Integration/Tenancy/TenantPurgeTests.cs
using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Tenancy;

[Collection(ApiCollection.Name)]
public sealed class TenantPurgeTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Purge_RemovesTargetTenantData_LeavesOthersIntact()
    {
        // İki kiracı; her birine departman + personel.
        var a = await ProvisionAndActivateAsync("Silinecek A.Ş.", $"a-{Guid.NewGuid():N}@purge.local");
        var b = await ProvisionAndActivateAsync("Kalacak A.Ş.", $"b-{Guid.NewGuid():N}@purge.local");
        await SeedInTenantAsync(a.TenantId, db => { db.Departments.Add(new Domain.Entities.Organization.Department { Name = "A-Dept" }); return Task.CompletedTask; });
        await SeedInTenantAsync(b.TenantId, db => { db.Departments.Add(new Domain.Entities.Organization.Department { Name = "B-Dept" }); return Task.CompletedTask; });

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITenantPurger>()
                .PurgeAsync(a.TenantId, CancellationToken.None);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            sp.GetRequiredService<ICurrentTenant>().Impersonate(b.TenantId);
            var db = sp.GetRequiredService<AppDbContext>();

            // A tamamen gitti (filtresiz sorgu): kiracı satırı + kullanıcılar yok.
            (await db.Tenants.AnyAsync(t => t.Id == a.TenantId)).Should().BeFalse();
            (await db.Set<Ekipzen.Infrastructure.Identity.ApplicationUser>()
                .AnyAsync(u => u.TenantId == a.TenantId)).Should().BeFalse();
            (await db.Departments.IgnoreQueryFilters().AnyAsync(d => d.TenantId == a.TenantId)).Should().BeFalse();

            // B dokunulmadı.
            (await db.Departments.AnyAsync(d => d.Name == "B-Dept")).Should().BeTrue();
            (await db.Tenants.AnyAsync(t => t.Id == b.TenantId)).Should().BeTrue();
        }
    }
}
```

- [ ] **Step 3: Testi çalıştır, başarısızlığı gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~TenantPurge"`
Expected: FAIL (ITenantPurger DI'da yok).

- [ ] **Step 4: TenantPurger implementasyonu**

```csharp
// backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// Kiracının tüm verisini FK-güvenli sırada siler. Silinecek tablolar EF model
/// metadata'sından türetilir (ITenantScoped + PK'lı = view'ler hariç); yeni bir
/// kiracı-kapsamlı tablo eklendiğinde otomatik kapsama girer (unutma sızıntısı yok).
/// </summary>
public sealed class TenantPurger(AppDbContext context, ICurrentTenant currentTenant, IFileStorage fileStorage)
    : ITenantPurger
{
    public async Task PurgeAsync(int tenantId, CancellationToken cancellationToken)
    {
        // Fiziksel dosyaları önce topla (yalnız EmployeeDocument saklar).
        currentTenant.Impersonate(tenantId);
        var filePaths = await context.EmployeeDocuments
            .Where(d => d.FilePath != "")
            .Select(d => d.FilePath)
            .ToListAsync(cancellationToken);

        var tables = TenantScopedTablesInDeleteOrder();

        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        // 1) ITenantScoped tablolar (çocuk→ebeveyn). Açık TenantId filtresi (impersonation'a bağlı değil).
        foreach (var table in tables)
        {
            await context.Database.ExecuteSqlRawAsync(
                $"DELETE FROM {table} WHERE [TenantId] = {{0}}", [tenantId], cancellationToken);
        }

        // 2) Kimlik: refresh token'lar, sonra kullanıcılar (AspNetUserRoles cascade).
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM [RefreshTokens] WHERE [TenantId] = {0}", [tenantId], cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM [AspNetUsers] WHERE [TenantId] = {0}", [tenantId], cancellationToken);

        // 3) Kiracı satırı.
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM [Tenants] WHERE [Id] = {0}", [tenantId], cancellationToken);

        await tx.CommitAsync(cancellationToken);

        // 4) Fiziksel dosyalar (DB tutarlılığından sonra; best-effort).
        foreach (var path in filePaths)
        {
            await fileStorage.DeleteAsync(path, cancellationToken);
        }
    }

    // ITenantScoped + tablo (PK'lı) tipleri FK bağımlılığına göre çocuk-önce sıralar.
    private List<string> TenantScopedTablesInDeleteOrder()
    {
        var entityTypes = context.Model.GetEntityTypes()
            .Where(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType)
                        && e.FindPrimaryKey() is not null // keyless view'leri (read-model) dışla
                        && e.GetTableName() is not null)
            .Distinct()
            .ToList();

        var set = entityTypes.ToHashSet();
        var visited = new HashSet<IEntityType>();
        var ordered = new List<IEntityType>();

        void Visit(IEntityType node)
        {
            if (!visited.Add(node)) return;
            // node'un referans ettiği (principal) kiracı-kapsamlı tipler önce silinmemeli;
            // yani node (child) onlardan ÖNCE eklenmeli → principal'leri sonra ziyaret et.
            foreach (var fk in node.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                if (principal != node && set.Contains(principal))
                {
                    Visit(principal);
                }
            }
            ordered.Add(node); // principal'ler zaten eklendi → child sonra eklenir
        }

        foreach (var e in entityTypes) Visit(e);

        // 'ordered' principal-önce; silme için TERS çevir (child-önce).
        ordered.Reverse();

        return ordered
            .Select(e => e.GetSchema() is { } s ? $"[{s}].[{e.GetTableName()}]" : $"[{e.GetTableName()}]")
            .Distinct()
            .ToList();
    }
}
```

Not: `ExecuteSqlRaw` içinde tablo adı sabit metadata'dan gelir (kullanıcı girdisi değil) → SQL injection riski yok; `TenantId` parametrelidir.

- [ ] **Step 5: DI kaydı**

`DependencyInjection.cs` — `ITenantPurger` scoped kaydı (AppDbContext ile aynı scope):
```csharp
services.AddScoped<Ekipzen.Application.Common.Interfaces.ITenantPurger, Persistence.TenantPurger>();
```
(Uygun `using` zaten varsa kısalt.)

- [ ] **Step 6: Testi çalıştır, geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~TenantPurge"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git checkout -b feature/tenant-purge
git add backend/src/Ekipzen.Application/Common/Interfaces/ITenantPurger.cs backend/src/Ekipzen.Infrastructure/ backend/tests/Ekipzen.Tests.Integration/Tenancy/TenantPurgeTests.cs
git commit -m "feat(tenancy): TenantPurger — kiracı verisini FK-güvenli topluca siler"
```

---

## Task 2: PurgeTenantCommand (confirm-slug güvenliği)

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/PurgeTenantCommand.cs`

**Interfaces:**
- Consumes: `ITenantPurger`, `IApplicationDbContext.Tenants`.
- Produces: `PurgeTenantCommand(int TenantId, string ConfirmSlug) : IRequest<PurgeTenantResult>`; `PurgeTenantResult(int TenantId, string Slug)`.

- [ ] **Step 1: Command + validator + handler**

```csharp
// backend/src/Ekipzen.Application/Features/Tenancy/Commands/PurgeTenantCommand.cs
using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Bir kiracının tüm verisini kalıcı siler (KVKK unutulma hakkı). Yıkıcıdır:
/// <c>ConfirmSlug</c> hedef kiracının slug'ıyla eşleşmezse reddedilir (yanlış-id koruması).
/// </summary>
public sealed record PurgeTenantCommand(int TenantId, string ConfirmSlug) : IRequest<PurgeTenantResult>;

public sealed record PurgeTenantResult(int TenantId, string Slug);

public sealed class PurgeTenantCommandValidator : AbstractValidator<PurgeTenantCommand>
{
    public PurgeTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.ConfirmSlug).NotEmpty();
    }
}

public sealed class PurgeTenantCommandHandler(IApplicationDbContext context, ITenantPurger purger)
    : IRequestHandler<PurgeTenantCommand, PurgeTenantResult>
{
    public async Task<PurgeTenantResult> Handle(PurgeTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException($"{request.TenantId} numaralı kiracı bulunamadı.");

        if (!string.Equals(tenant.Slug, request.ConfirmSlug.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Onay kısa adı (slug) kiracıyla eşleşmiyor; silme iptal edildi.");
        }

        var slug = tenant.Slug;
        await purger.PurgeAsync(tenant.Id, cancellationToken);
        return new PurgeTenantResult(request.TenantId, slug);
    }
}
```

Not: `NotFoundException` mevcut (Common/Exceptions). Yoksa `ConflictException` kullan.

- [ ] **Step 2: Derle**

Run: `cd backend && dotnet build`
Expected: 0 Hata.

- [ ] **Step 3: Commit**

```bash
git add backend/src/Ekipzen.Application/Features/Tenancy/Commands/PurgeTenantCommand.cs
git commit -m "feat(tenancy): PurgeTenantCommand (confirm-slug güvenlik kapısı)"
```

---

## Task 3: DELETE ucu + cleanup komutu/ucu

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/CleanupUnverifiedTenantsCommand.cs`
- Modify: `backend/src/Ekipzen.API/Controllers/TenancyController.cs`
- Modify: `backend/tests/Ekipzen.Tests.Integration/Tenancy/TenantPurgeTests.cs`

**Interfaces:**
- Produces: `DELETE /api/tenants/{id}?confirmSlug=`; `POST /api/tenants/cleanup-unverified`; `CleanupUnverifiedTenantsCommand(int OlderThanDays) : IRequest<CleanupUnverifiedResult>`; `CleanupUnverifiedResult(int PurgedCount)`.

- [ ] **Step 1: Cleanup komutu**

```csharp
// backend/src/Ekipzen.Application/Features/Tenancy/Commands/CleanupUnverifiedTenantsCommand.cs
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Infrastructure.Identity; // NOT: Application katmanı Identity'ye bağımlı olmasın — bkz. Step 1b
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Doğrulanmamış (hiç etkinleşmemiş) self-servis kiracıları temizler: pasif + verilen
/// günden eski + hiçbir kullanıcısı şifre belirlememiş (davet hiç kabul edilmemiş) kiracılar.
/// Askıya alınmış (şifreli kullanıcısı olan) kiracılar KORUNUR. Cron ile tetiklenebilir.
/// </summary>
public sealed record CleanupUnverifiedTenantsCommand(int OlderThanDays) : IRequest<CleanupUnverifiedResult>;

public sealed record CleanupUnverifiedResult(int PurgedCount);
```

- [ ] **Step 1b: Handler'ı Infrastructure'da yaz (Identity erişimi gerektiği için)**

Application handler'ı `IApplicationDbContext` üzerinden "şifreli kullanıcı var mı" bilgisine erişemez (ApplicationUser Identity tipidir). Bu yüzden handler'ı Infrastructure'a koy:

```csharp
// backend/src/Ekipzen.Infrastructure/Persistence/CleanupUnverifiedTenantsHandler.cs
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Tenancy.Commands;
using Ekipzen.Infrastructure.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Infrastructure.Persistence;

public sealed class CleanupUnverifiedTenantsHandler(AppDbContext context, ITenantPurger purger)
    : IRequestHandler<CleanupUnverifiedTenantsCommand, CleanupUnverifiedResult>
{
    public async Task<CleanupUnverifiedResult> Handle(
        CleanupUnverifiedTenantsCommand request, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Abs(request.OlderThanDays));

        // Pasif + eski + hiç şifre belirlenmemiş (davet hiç kabul edilmemiş) kiracılar.
        var candidateIds = await context.Tenants
            .Where(t => !t.IsActive && t.CreatedAtUtc < cutoff)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var verifiedTenantIds = await context.Set<ApplicationUser>()
            .Where(u => u.PasswordHash != null && candidateIds.Contains(u.TenantId))
            .Select(u => u.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var toPurge = candidateIds.Except(verifiedTenantIds).ToList();
        foreach (var id in toPurge)
        {
            await purger.PurgeAsync(id, cancellationToken);
        }
        return new CleanupUnverifiedResult(toPurge.Count);
    }
}
```

(MediatR handler'ları Infrastructure assembly'sinde de taranıyorsa otomatik bulunur; taranmıyorsa `AddMediatR` kayıt satırına Infrastructure assembly'sini ekle — Step 1c.)

- [ ] **Step 1c: MediatR Infrastructure assembly taraması (gerekirse)**

`Program.cs`/`DependencyInjection.cs` içinde `AddMediatR(... typeof(SomeApplicationType).Assembly)` varsa, Infrastructure handler'ı için ayrıca `RegisterServicesFromAssembly(typeof(TenantPurger).Assembly)` ekle. Zaten taranıyorsa atla.

- [ ] **Step 2: Uçlar (TenancyController)**

`Provision`/`Signup`'tan sonra, aynı platform-key kontrolüyle:

```csharp
[HttpDelete("{id:int}")]
[ProducesResponseType<PurgeTenantResult>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<PurgeTenantResult>> Purge(
    int id, [FromQuery] string confirmSlug, CancellationToken cancellationToken)
{
    if (!PlatformKeyValid()) return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
    return Ok(await sender.Send(new PurgeTenantCommand(id, confirmSlug ?? ""), cancellationToken));
}

[HttpPost("cleanup-unverified")]
[ProducesResponseType<CleanupUnverifiedResult>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<ActionResult<CleanupUnverifiedResult>> CleanupUnverified(
    [FromQuery] int olderThanDays, CancellationToken cancellationToken)
{
    if (!PlatformKeyValid()) return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
    return Ok(await sender.Send(new CleanupUnverifiedTenantsCommand(olderThanDays), cancellationToken));
}
```

Platform-key kontrolünü tek yere çıkar (mevcut `Provision` içindeki mantığı `private bool PlatformKeyValid()` yardımcısına taşı, DRY):

```csharp
private bool PlatformKeyValid()
{
    var expected = configuration["Platform:ProvisioningKey"];
    var provided = Request.Headers["X-Platform-Key"].ToString();
    return !string.IsNullOrEmpty(expected) && provided == expected;
}
```
`Provision`'ı da bu yardımcıyı kullanacak şekilde güncelle. `using Ekipzen.Application.Features.Tenancy.Commands;` mevcut.

- [ ] **Step 3: Testler (TenantPurgeTests'e ekle)**

```csharp
[Fact]
public async Task Delete_WithWrongSlug_Returns409_AndKeepsData()
{
    var t = await ProvisionAndActivateAsync("Yanlış Onay A.Ş.", $"w-{Guid.NewGuid():N}@purge.local");
    var client = Factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Platform-Key", EkipzenApiFactory.PlatformKey);

    var resp = await client.DeleteAsync($"/api/tenants/{t.TenantId}?confirmSlug=yanlis-slug");
    resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);

    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    (await db.Tenants.AnyAsync(x => x.Id == t.TenantId)).Should().BeTrue("yanlış slug'da silinmemeli");
}

[Fact]
public async Task Delete_WithoutPlatformKey_Returns401()
{
    var t = await ProvisionAndActivateAsync("Anahtarsız A.Ş.", $"k-{Guid.NewGuid():N}@purge.local");
    var resp = await Factory.CreateClient().DeleteAsync($"/api/tenants/{t.TenantId}?confirmSlug={t.Slug}");
    resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
}

[Fact]
public async Task CleanupUnverified_PurgesUnverified_KeepsVerifiedAndActive()
{
    // Doğrulanmamış (davet kabul edilmemiş) self-servis kiracı.
    var email = $"u-{Guid.NewGuid():N}@cleanup.local";
    await Factory.CreateClient().PostAsJsonAsync("/api/tenants/signup",
        new { companyName = "Doğrulanmamış A.Ş.", adminName = "X", adminEmail = email });

    // CreatedAtUtc'yi geçmişe çek (eski olsun).
    int unverifiedId;
    using (var scope = Factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = await db.Tenants.FirstAsync(x => x.Name == "Doğrulanmamış A.Ş.");
        unverifiedId = t.Id;
        t.CreatedAtUtc = DateTime.UtcNow.AddDays(-30);
        await db.SaveChangesAsync();
    }

    // Doğrulanmış aktif kiracı (korunmalı).
    var kept = await ProvisionAndActivateAsync("Aktif A.Ş.", $"a-{Guid.NewGuid():N}@cleanup.local");

    var client = Factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Platform-Key", EkipzenApiFactory.PlatformKey);
    var resp = await client.PostAsync("/api/tenants/cleanup-unverified?olderThanDays=7", null);
    resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

    using (var scope = Factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Tenants.AnyAsync(x => x.Id == unverifiedId)).Should().BeFalse("doğrulanmamış temizlenmeli");
        (await db.Tenants.AnyAsync(x => x.Id == kept.TenantId)).Should().BeTrue("aktif kiracı korunmalı");
    }
}
```

`using System.Net.Http.Json;` ve `using Microsoft.EntityFrameworkCore;` testte mevcut olmalı.

- [ ] **Step 4: Testleri çalıştır**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~TenantPurge"`
Expected: PASS (4 test).

- [ ] **Step 5: Tüm backend paketi**

Run: `cd backend && dotnet test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Ekipzen.Application/ backend/src/Ekipzen.Infrastructure/ backend/src/Ekipzen.API/ backend/tests/
git commit -m "feat(tenancy): purge/cleanup uçları (DELETE /tenants/{id}, cleanup-unverified)"
```

---

## Task 4: Dokümantasyon + kapanış

**Files:**
- Modify: `docs/kvkk-veri-izolasyonu.md` (Bölüm 7.1 → yapıldı)
- Modify: `docs/gelistirme-gunlugu.md`

- [ ] **Step 1:** `docs/kvkk-veri-izolasyonu.md` Bölüm 7.1'i güncelle: elle prosedür yerine artık `DELETE /api/tenants/{id}` (confirm-slug) + `cleanup-unverified` otomatik akışını anlat; Bölüm 6 tablosunda "Saklama/silme" satırını "otomatik purge mevcut" olarak güncelle.
- [ ] **Step 2:** `docs/gelistirme-gunlugu.md` "Şu an neredeyiz" + dated kayıt (purge mekanizması, confirm-slug, cleanup, testler).
- [ ] **Step 3:** Tam doğrulama: `cd backend && dotnet test` yeşil.
- [ ] **Step 4:** Commit + finishing-a-development-branch (kullanıcı onayıyla main'e merge + push).

---

## Self-Review Notları

- **Rot-proof:** silinecek tablolar EF metadata'sından türer; yeni ITenantScoped tablo otomatik kapsanır (global filtreyle aynı ilke). ✓
- **İzolasyon:** silme açık `WHERE TenantId=@id` + tek transaction; başka kiracı etkilenmez (test). ✓
- **Güvenlik:** platform-key + confirm-slug; yıkıcı işlem yanlış-id'ye karşı korumalı. ✓
- **FK sırası:** metadata topo-sort (child-önce); yanlışsa entegrasyon testi FK hatasıyla yakalar. ✓
- **Askıya alınmış vs doğrulanmamış:** cleanup yalnız şifresiz (hiç kabul edilmemiş) kullanıcıları olan pasif kiracıları siler; şifreli kullanıcısı olan (askıya alınmış) kiracılar korunur. ✓
- **Fiziksel dosyalar:** yalnız EmployeeDocument.FilePath; DB commit sonrası best-effort silinir. ✓
- **Kapsam dışı:** anonimleştirme (silme yerine PII maskeleme) ayrı iş; SMTP; ilgili kişi dışa aktarımı.
