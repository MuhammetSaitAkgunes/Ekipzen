# Faz 1a — Platform Katmanı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kiracı kimliğini (kim var, hangi durumda, hangi e-posta kime ait) ayrı bir platform veritabanına taşımak; böylece Faz 1b'deki bağlantı çözücü kiracıyı uygulama veritabanına sormadan bulabilsin.

**Architecture:** İki DbContext. `PlatformDbContext` yeni `EkipzenPlatform` veritabanına bağlanır ve `Tenants` + `TenantDirectory` tablolarını tutar. `AppDbContext` bugünkü `EkipzenDb`'de kalır ve tüm kiracıların İK verisini `TenantId` ile ayırmaya devam eder — bu fazda hiçbir veri bölünmez. Kiracı satırı uygulama veritabanından çıktığı için provizyon ve purge artık iki veritabanına dokunur; atomiklik kaybı `Tenants.Status` durum makinesiyle telafi edilir.

**Tech Stack:** .NET 9, EF Core 9, SQL Server, xUnit + FluentAssertions.

Tasarım: `docs/superpowers/specs/2026-08-06-kiraci-basina-veritabani-design.md`

## Global Constraints

- **Gerçek müşteri verisi yoktur.** Yalnız demo kiracılar var; taşıma script'i yazılmaz, demo veri seed'den yeniden üretilir.
- Bu fazda **hiçbir kiracı verisi bölünmez.** `AppDbContext` tek `EkipzenDb`'ye bağlı kalır, global query filter'lar ve `TenantId` sütunları aynen çalışır.
- Faz sonunda **158 testin tamamı geçmelidir** (47 birim + 111 entegrasyon). Yeni testler bunun üstüne eklenir.
- Derleme uyarısız kalır — CI `-warnaserror` ile koşar.
- TDD: her görevde önce kırmızı test.
- Her görev ayrı commit.
- Türkçe kod yorumları ve Türkçe test adları — mevcut kod tabanının kuralı.
- `Subscriptions` bu fazda **taşınmaz**; kiracı veritabanları gerçekten ayrıldığında (Faz 2) taşınır.
- Bağlantı çözücü, kiracı kütüğü ve durum kapısı bu planda **yok** — Faz 1b'de gelir.

## Kapsam dışı bırakılan, sonra gelecekler

| Konu | Nerede |
| --- | --- |
| `ITenantConnectionResolver`, kiracı kütüğü, durum kapısı | Faz 1b |
| Kiracıya sabitlenmiş kapsam fabrikası | Faz 1b |
| `Subscriptions`'ın platforma taşınması | Faz 2 |
| Gerçek `CREATE DATABASE` / kiracı başına DB | Faz 2 |
| Yedekleme, tatbikat, migration koşucusu | Faz 3 |

---

## File Structure

| Dosya | Sorumluluk |
| --- | --- |
| `backend/src/Ekipzen.Domain/Entities/Tenancy/Tenant.cs` | `IsActive` → `Status` (değiştir) |
| `backend/src/Ekipzen.Domain/Entities/Tenancy/TenantStatus.cs` | Kiracı durumu enum'ı (oluştur) |
| `backend/src/Ekipzen.Domain/Entities/Tenancy/TenantDirectoryEntry.cs` | E-posta → kiracı yönlendirme kaydı (oluştur) |
| `backend/src/Ekipzen.Application/Common/Interfaces/IPlatformDbContext.cs` | Platform context sözleşmesi (oluştur) |
| `backend/src/Ekipzen.Application/Common/Interfaces/IApplicationDbContext.cs` | `Tenants` çıkarılır (değiştir) |
| `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContext.cs` | Platform DbContext (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContextFactory.cs` | Tasarım zamanı fabrika (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbInitializer.cs` | Platform migrate + demo seed (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Persistence/Migrations/Platform/` | Platform migration'ları (oluştur) |
| `backend/src/Ekipzen.Infrastructure/Persistence/AppDbContext.cs` | `Tenants` DbSet çıkarılır (değiştir) |
| `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs` | Platform context kaydı (değiştir) |
| `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs` | Kiracı sorguları platform context'e (değiştir) |
| `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs` | Platform satırlarını da siler (değiştir) |
| `backend/src/Ekipzen.Application/Features/Tenancy/TenantOnboarding.cs` | Durum makinesi (değiştir) |
| `backend/src/Ekipzen.Application/Features/Tenancy/Commands/*.cs` | Platform context (değiştir) |
| `backend/src/Ekipzen.Api/Program.cs` | Platform initializer çağrısı (değiştir) |
| `backend/tests/Ekipzen.Tests.Integration/EkipzenApiFactory.cs` | Platform test DB'si (değiştir) |
| `backend/tests/Ekipzen.Tests.Integration/Tenancy/PlatformKatmaniTests.cs` | Yeni testler (oluştur) |

---

### Task 1: Platform veritabanı ve `PlatformDbContext`

Boş ama gerçek bir platform veritabanı ayağa kaldırılır. Henüz hiçbir şey taşınmaz — sadece ikinci context'in kurulabildiği ve testlerin onu düşürüp yeniden kurabildiği kanıtlanır.

**Files:**
- Create: `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContext.cs`
- Create: `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContextFactory.cs`
- Create: `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbInitializer.cs`
- Create: `backend/src/Ekipzen.Application/Common/Interfaces/IPlatformDbContext.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/PlatformKatmaniTests.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/Ekipzen.Api/Program.cs`
- Modify: `backend/src/Ekipzen.Api/appsettings.Development.json`
- Modify: `backend/tests/Ekipzen.Tests.Integration/EkipzenApiFactory.cs`

**Interfaces:**
- Produces:
  - `IPlatformDbContext` — `DbSet<Tenant> Tenants { get; }`, `Task<int> SaveChangesAsync(CancellationToken)`
  - `PlatformDbContext(DbContextOptions<PlatformDbContext>)`
  - `PlatformDbInitializer.InitialiseAsync() → Task`
  - Yapılandırma anahtarı: `ConnectionStrings:PlatformConnection`

- [ ] **Step 1: Failing testi yaz**

`backend/tests/Ekipzen.Tests.Integration/Tenancy/PlatformKatmaniTests.cs`:

```csharp
using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Platform katmanı: kiracı kimliği uygulama veritabanından ayrı, kendi
/// veritabanında tutulur. Bu fazda kiracı VERİSİ hâlâ tek uygulama
/// veritabanındadır — ayrılan yalnız kiracının kendisidir.
/// </summary>
[Collection(ApiCollection.Name)]
public class PlatformKatmaniTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task PlatformVeritabani_AyagaKalkarVeSorgulanabilir()
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();

        // Migration uygulanmışsa sorgu çalışır; uygulanmamışsa SqlException atar.
        var kiraciSayisi = await platform.Tenants.CountAsync();

        kiraciSayisi.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task PlatformVeritabani_UygulamaVeritabanindanFarklidir()
    {
        using var scope = Factory.Services.CreateScope();
        var platform = (DbContext)scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var uygulama = (DbContext)scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        platform.Database.GetDbConnection().Database
            .Should().NotBe(uygulama.Database.GetDbConnection().Database,
                "platform kimliği uygulama verisinden ayrı bir veritabanında durmalı");
    }
}
```

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
dotnet test backend/tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~PlatformKatmani"
```

Beklenen: derleme hatası — `IPlatformDbContext` tipi bulunamıyor.

- [ ] **Step 3: `IPlatformDbContext` sözleşmesini yaz**

`backend/src/Ekipzen.Application/Common/Interfaces/IPlatformDbContext.cs`:

```csharp
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Platform veritabanı: kiracı kimliği ve yönlendirme. Kiracı İK verisi burada
/// DEĞİLDİR — o <see cref="IApplicationDbContext"/> tarafındadır.
///
/// Bu context'te global kiracı filtresi YOKTUR: platform tablolarının kendisi
/// kiracı-üstüdür. Kiracıya göre süzmek gerektiğinde açıkça yazılır.
/// </summary>
public interface IPlatformDbContext
{
    DbSet<Tenant> Tenants { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 4: `PlatformDbContext`'i yaz**

`backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContext.cs`:

```csharp
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// Platform veritabanının (EkipzenPlatform) context'i. Yalnız kiracı kimliğini
/// tutar; İK verisi <see cref="AppDbContext"/> tarafındadır.
///
/// Migration'ları AYRI bir klasörde tutulur (Migrations/Platform) ve ayrı bir
/// __EFMigrationsHistory tablosuna yazılır — iki context'in geçmişi birbirine
/// karışmaz.
/// </summary>
public class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : DbContext(options), IPlatformDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(b =>
        {
            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.Property(t => t.Slug).IsRequired().HasMaxLength(64);
            b.HasIndex(t => t.Slug).IsUnique();
        });
    }
}
```

- [ ] **Step 5: Tasarım zamanı fabrikayı yaz**

`backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// Tasarım zamanı (dotnet ef) platform context üreticisi. Bağlantı dizesi
/// EKIPZEN_PLATFORM_CONNECTION ortam değişkeninden ya da varsayılan yerel
/// MSSQL'den alınır (migration üretimi için içerik önemsizdir).
/// </summary>
public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("EKIPZEN_PLATFORM_CONNECTION")
            ?? "Server=localhost;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Platform");
            })
            .Options;

        return new PlatformDbContext(options);
    }
}
```

- [ ] **Step 6: Initializer'ı yaz**

`backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbInitializer.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// Platform veritabanını migrate eder. Platform DB küçüktür ve onsuz hiçbir
/// kiracı çözülemez; bu yüzden açılışta migrate edilir. KİRACI veritabanları
/// açılışta migrate EDİLMEZ (bkz. tasarım belgesi, migration orkestrasyonu).
/// </summary>
public sealed class PlatformDbInitializer(PlatformDbContext context, ILogger<PlatformDbInitializer> logger)
{
    public async Task InitialiseAsync()
    {
        logger.LogInformation("Platform veritabanı migrate ediliyor.");
        await context.Database.MigrateAsync();
    }
}
```

- [ ] **Step 7: DI kaydını ekle**

`backend/src/Ekipzen.Infrastructure/DependencyInjection.cs` içinde, `AddDbContext<AppDbContext>` bloğundan hemen SONRA:

```csharp
        // Platform veritabanı: kiracı kimliği. Uygulama veritabanından AYRI bir
        // katalog; bağlantı dizesi tanımlı değilse fail-fast (sessizce uygulama
        // veritabanına düşmek, iki katmanı gizlice birleştirmek olurdu).
        var platformConnectionString = configuration.GetConnectionString("PlatformConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:PlatformConnection tanımlı değil.");

        services.AddDbContext<PlatformDbContext>(options =>
            options.UseSqlServer(platformConnectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory_Platform");
            }));

        services.AddScoped<IPlatformDbContext>(sp => sp.GetRequiredService<PlatformDbContext>());
        services.AddScoped<PlatformDbInitializer>();
```

- [ ] **Step 8: Development bağlantı dizesini ekle**

`backend/src/Ekipzen.Api/appsettings.Development.json` içindeki `ConnectionStrings` bloğuna ekle:

```json
    "PlatformConnection": "Server=localhost;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
```

- [ ] **Step 9: Açılışta platform migrate et**

`backend/src/Ekipzen.Api/Program.cs`, Development bloğunda `AppDbContextInitializer` satırlarından ÖNCE:

```csharp
        var platformInitializer = scope.ServiceProvider
            .GetRequiredService<Ekipzen.Infrastructure.Persistence.PlatformDbInitializer>();
        await platformInitializer.InitialiseAsync();
```

- [ ] **Step 10: Test fabrikasını platform DB'sini kuracak şekilde güncelle**

`backend/tests/Ekipzen.Tests.Integration/EkipzenApiFactory.cs`:

`TestDatabaseName` sabitinin yanına ekle:

```csharp
    private const string PlatformTestDatabaseName = "EkipzenPlatform_Test";
```

`TestConnectionString` alanının yanına ekle:

```csharp
    private static readonly string PlatformTestConnectionString = ConnectionFor(PlatformTestDatabaseName);
```

Yapıcıda `ConnectionStrings__DefaultConnection` satırının hemen ardına ekle:

```csharp
        Environment.SetEnvironmentVariable("ConnectionStrings__PlatformConnection", PlatformTestConnectionString);
```

`DropTestDatabase()` çağrısını şununla değiştir:

```csharp
        DropDatabase(TestDatabaseName);
        DropDatabase(PlatformTestDatabaseName);
```

Ve `DropTestDatabase` metodunu şununla değiştir:

```csharp
    private static void DropDatabase(string databaseName)
    {
        using var connection = new SqlConnection(ConnectionFor("master"));
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID('{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """;
        command.ExecuteNonQuery();
    }
```

- [ ] **Step 11: Platform migration'ını üret**

```bash
cd backend
EKIPZEN_PLATFORM_CONNECTION="Server=localhost;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef migrations add PlatformInitial --context PlatformDbContext --output-dir Persistence/Migrations/Platform --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.Api
```

- [ ] **Step 12: Testleri koş, geçtiğini doğrula**

```bash
dotnet test backend/tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~PlatformKatmani"
```

Beklenen: 2 test PASS.

- [ ] **Step 13: Tüm suite'i koş**

```bash
cd backend && dotnet test --configuration Release
```

Beklenen: 47 birim + 113 entegrasyon = 160 test, hepsi PASS.

- [ ] **Step 14: Commit**

```bash
git add -A
git commit -m "feat(platform): ayrı platform veritabanı ve PlatformDbContext"
```

---

### Task 2: `Tenant` platform veritabanına taşınır

Kiracı satırı uygulama veritabanından çıkar. 15 çağrı noktası platform context'ine geçer.

**Files:**
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `backend/src/Ekipzen.Application/Common/Interfaces/IApplicationDbContext.cs:22`
- Modify: `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/AppDbContextInitializer.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/TenantOnboarding.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/ProvisionTenantCommand.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/RegisterTenantCommand.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/PurgeTenantCommand.cs`
- Test: `backend/tests/Ekipzen.Tests.Integration/Tenancy/PlatformKatmaniTests.cs`

**Interfaces:**
- Consumes: `IPlatformDbContext` (Task 1)
- Produces: `IApplicationDbContext` artık `Tenants` içermez; tüm kiracı sorguları `IPlatformDbContext` üzerinden

- [ ] **Step 1: Failing testi yaz**

`PlatformKatmaniTests.cs` sınıfına ekle:

```csharp
    [Fact]
    public async Task KiraciSatiri_UygulamaVeritabaninda_ARTIK_YOK()
    {
        using var scope = Factory.Services.CreateScope();
        var uygulama = (DbContext)scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var tenantsTablosuVar = uygulama.Model.GetEntityTypes()
            .Any(e => e.GetTableName() == "Tenants");

        tenantsTablosuVar.Should().BeFalse(
            "kiracı kimliği platform veritabanına taşındı; uygulama modelinde kalması iki doğruluk kaynağı demektir");
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
```

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
dotnet test backend/tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~PlatformKatmani"
```

Beklenen: `KiraciSatiri_UygulamaVeritabaninda_ARTIK_YOK` FAIL — `Tenants` hâlâ uygulama modelinde.

- [ ] **Step 3: `Tenants`'ı uygulama context'inden çıkar**

`backend/src/Ekipzen.Infrastructure/Persistence/AppDbContext.cs` içinde şu iki satırı SİL:

```csharp
    // Tenancy
    public DbSet<Tenant> Tenants => Set<Tenant>();
```

ve `OnModelCreating` içindeki şu satırı SİL:

```csharp
        builder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
```

`backend/src/Ekipzen.Application/Common/Interfaces/IApplicationDbContext.cs:22` satırındaki `DbSet<Tenant> Tenants { get; }` satırını SİL.

- [ ] **Step 4: `IdentityService`'i platform context'e bağla**

Yapıcıya `IPlatformDbContext platform` parametresini ekle (mevcut `AppDbContext context` kalır).

`LoginAsync` içindeki kiracı kontrolünü şununla değiştir:

```csharp
        // Multi-tenant: kullanıcının kiracısı (şirketi) askıya alınmışsa girişe izin verilmez.
        // Kiracı kimliği platform veritabanındadır.
        var tenant = await platform.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
        {
            throw new UnauthorizedException("Şirket hesabı aktif değil. Yöneticinizle iletişime geçin.");
        }
```

`TenantNameAsync` içindeki `context.Tenants` → `platform.Tenants`.
`DefaultTenantIdAsync` içindeki `context.Tenants` → `platform.Tenants`.
Davet kabulündeki (satır ~150) kiracı etkinleştirmesinde `context.Tenants` → `platform.Tenants` ve `await context.SaveChangesAsync(...)` → `await platform.SaveChangesAsync(...)`.

- [ ] **Step 5: `TenantOnboarding`'i platform context'e bağla**

`CreateWithAdminAsync` imzasındaki `IApplicationDbContext context` parametresini `IPlatformDbContext platform` ile DEĞİŞTİR ve gövdedeki `context.Tenants.Add(tenant)` / `context.SaveChangesAsync(...)` çağrılarını `platform.*` yap. Çağıranlar (Task 2 Step 6) buna göre güncellenir.

- [ ] **Step 6: Tenancy komutlarını güncelle**

`ProvisionTenantCommandHandler`, `RegisterTenantCommandHandler` ve `PurgeTenantCommandHandler` yapıcılarındaki `IApplicationDbContext context` parametresini `IPlatformDbContext platform` ile değiştir; gövdelerdeki `context.Tenants` → `platform.Tenants`, `context.SaveChangesAsync` → `platform.SaveChangesAsync`.

`RegisterTenantCommand` içindeki `CreateTenantWithUniqueSlugAsync` ve `GenerateSlugAsync` metotlarında da aynı değişikliği yap.

- [ ] **Step 7: `TenantPurger`'ı güncelle**

Yapıcıya `IPlatformDbContext platform` ekle. `QualifiedTableName(typeof(Tenant))` ile üretilen `tenantTable` değişkenini ve onu kullanan `DELETE FROM {tenantTable}` satırını SİL (o tablo artık bu veritabanında değil). Transaction commit edildikten SONRA, dosya silmeden ÖNCE ekle:

```csharp
        // Kiracı satırı platform veritabanındadır — uygulama transaction'ı onu kapsamaz.
        // Bu yüzden ayrı silinir. Buradan önce patlarsa kiracı satırı kalır ama verisi
        // gitmiştir; durum Purging'de kaldığı için kiracı erişilemez olarak durur
        // (bkz. Task 6) ve operatör yeniden çalıştırabilir.
        var tenantRow = await platform.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenantRow is not null)
        {
            platform.Tenants.Remove(tenantRow);
            await platform.SaveChangesAsync(cancellationToken);
        }
```

`PurgeUnverifiedAsync` içindeki `context.Tenants` → `platform.Tenants`.

- [ ] **Step 8: Seed'i güncelle**

`AppDbContextInitializer` yapıcısına `IPlatformDbContext platform` ekle; `context.Tenants` geçen dört satırı (`152`, `162`, `177`, `186` civarı) `platform.Tenants` yap ve kiracı eklendikten sonraki kaydetmeyi `await platform.SaveChangesAsync(default);` ile yap.

- [ ] **Step 9: Uygulama veritabanından `Tenants` tablosunu düşüren migration'ı üret**

```bash
cd backend
EKIPZEN_CONNECTION="Server=localhost;Database=EkipzenDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef migrations add DropTenantsFromAppDb --context AppDbContext --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.Api
```

Üretilen migration'ın başına şu açıklamayı ekle:

```csharp
    /// <summary>
    /// Kiracı kimliği platform veritabanına taşındı; uygulama veritabanındaki
    /// Tenants tablosu düşürülür.
    ///
    /// YIKICI: bu tablodaki satırlar silinir. Uygulandığı anda sistemde gerçek
    /// müşteri verisi YOKTU (yalnız demo kiracılar) ve demo veri seed'den
    /// yeniden üretilmektedir. Gerçek veri bulunan bir kuruluma uygulanmadan
    /// önce satırlar platform veritabanına kopyalanmalıdır.
    /// </summary>
```

- [ ] **Step 10: Veritabanlarını sıfırla ve testleri koş**

```bash
cd backend
EKIPZEN_CONNECTION="Server=localhost;Database=EkipzenDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef database update --context AppDbContext --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.Api
EKIPZEN_PLATFORM_CONNECTION="Server=localhost;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef database update --context PlatformDbContext --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.Api
dotnet test --configuration Release
```

Beklenen: 47 + 115 = 162 test PASS.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat(platform): kiracı satırı platform veritabanına taşındı"
```

---

### Task 3: `TenantStatus`, `IsActive`'in yerini alır

İki durumlu bayrak dört durumu ifade edemez. Bugün `IsActive=false` iki ayrı anlamda kullanılıyor ("kayıt doğrulanmadı" ve "kiracı kapalı"); ayrım netleşir.

**Files:**
- Create: `backend/src/Ekipzen.Domain/Entities/Tenancy/TenantStatus.cs`
- Modify: `backend/src/Ekipzen.Domain/Entities/Tenancy/Tenant.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContext.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/TenantOnboarding.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/RegisterTenantCommand.cs`
- Test: `backend/tests/Ekipzen.Tests.Integration/Tenancy/PlatformKatmaniTests.cs`

**Interfaces:**
- Produces:
  - `enum TenantStatus { Provisioning, Active, Frozen, Purging }`
  - `Tenant.Status` (`TenantStatus`), `Tenant.IsActive` KALDIRILIR

- [ ] **Step 1: Failing testi yaz**

`PlatformKatmaniTests.cs` sınıfına ekle:

```csharp
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

        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "dondurulmuş kiracıya giriş yapılamamalı");
    }

    private async Task DurumuDegistirAsync(int tenantId, TenantStatus durum)
    {
        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var kiraci = await platform.Tenants.FirstAsync(t => t.Id == tenantId);
        kiraci.Status = durum;
        await platform.SaveChangesAsync(default);
    }
```

Dosyanın başına gereken using'leri ekle: `Ekipzen.Domain.Entities.Tenancy`, `System.Net`, `System.Net.Http.Json`.

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
dotnet test backend/tests/Ekipzen.Tests.Integration --filter "DondurulmusKiraci"
```

Beklenen: derleme hatası — `TenantStatus` yok.

- [ ] **Step 3: Enum'ı yaz**

`backend/src/Ekipzen.Domain/Entities/Tenancy/TenantStatus.cs`:

```csharp
namespace Ekipzen.Domain.Entities.Tenancy;

/// <summary>
/// Kiracının yaşam döngüsü durumu. Yalnız <see cref="Active"/> erişime izin verir.
///
/// İki durumlu bir bayrak (eski IsActive) yetersizdi: "kayıt henüz doğrulanmadı"
/// ile "kiracı kapatıldı" aynı değere sıkışıyordu ve bakım/geri yükleme sırasında
/// kiracıyı dondurmanın ayrı bir yolu yoktu.
/// </summary>
public enum TenantStatus
{
    /// <summary>Kurulum sürüyor ya da yarıda kaldı — erişim kapalı, müşteri verisi yok.</summary>
    Provisioning,

    /// <summary>Normal çalışma durumu — tek erişilebilir durum.</summary>
    Active,

    /// <summary>Bakım ya da geri yükleme sürüyor — erişim geçici olarak kapalı.</summary>
    Frozen,

    /// <summary>Silme sürüyor ya da yarıda kaldı — erişim kalıcı olarak kapalı.</summary>
    Purging,
}
```

- [ ] **Step 4: `Tenant`'ı güncelle**

`backend/src/Ekipzen.Domain/Entities/Tenancy/Tenant.cs` içinde `IsActive` özelliğini şununla DEĞİŞTİR:

```csharp
    /// <summary>
    /// Yaşam döngüsü durumu. Yalnız <see cref="TenantStatus.Active"/> erişime izin verir.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;
```

- [ ] **Step 5: Enum'ı string olarak sakla**

`PlatformDbContext.OnModelCreating` içindeki `Tenant` yapılandırmasına ekle:

```csharp
            // Okunabilirlik: durum veritabanında metin olarak durur (AppDbContext ile aynı kural).
            b.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
```

- [ ] **Step 6: Çağrı noktalarını güncelle**

`IdentityService.LoginAsync` içindeki kontrolü şununla değiştir:

```csharp
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            throw new UnauthorizedException("Şirket hesabı aktif değil. Yöneticinizle iletişime geçin.");
        }
```

`IdentityService` davet kabulündeki etkinleştirmeyi şununla değiştir:

```csharp
        // Şifre belirlendi = e-posta doğrulandı. Self-servis kayıtta Provisioning
        // durumunda oluşturulan kiracıyı ilk admin kabulünde etkinleştir.
        if (tenant is { Status: TenantStatus.Provisioning })
        {
            tenant.Status = TenantStatus.Active;
            await platform.SaveChangesAsync(cancellationToken);
        }
```

`TenantOnboarding.CreateWithAdminAsync` imzasındaki `bool isActive` parametresini
**`TenantStatus hedefDurum`** ile değiştir. Bu fazda kiracı her hâlükârda
`TenantStatus.Provisioning` ile oluşturulur; `hedefDurum` yalnız Task 6'da,
tüm adımlar bittikten sonra uygulanır. Bu adımda geçici olarak oluşturmada
`Status = hedefDurum` yazılır — Task 6 bunu iki aşamaya böler.

Çağıranlar: `ProvisionTenantCommand` → `TenantStatus.Active`,
`RegisterTenantCommand` → `TenantStatus.Provisioning` (davet kabulü etkinleştirir).

`RegisterTenantCommand.CreateTenantWithUniqueSlugAsync` içindeki `IsActive = false` → `Status = TenantStatus.Provisioning`.

`TenantPurger.PurgeUnverifiedAsync` içindeki `.Where(t => !t.IsActive && ...)` → `.Where(t => t.Status == TenantStatus.Provisioning && ...)`.

`AppDbContextInitializer` içindeki `IsActive = true` geçen iki satırı `Status = TenantStatus.Active` yap.

- [ ] **Step 7: Migration üret ve uygula**

```bash
cd backend
EKIPZEN_PLATFORM_CONNECTION="Server=localhost;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef migrations add TenantStatus --context PlatformDbContext --output-dir Persistence/Migrations/Platform --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.Api
EKIPZEN_PLATFORM_CONNECTION="Server=localhost;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef database update --context PlatformDbContext --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.Api
```

- [ ] **Step 8: Testleri koş**

```bash
cd backend && dotnet test --configuration Release
```

Beklenen: 47 + 116 = 163 test PASS.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(platform): TenantStatus IsActive'in yerini aldı"
```

---

### Task 4: `TenantDirectory` — e-posta → kiracı yönlendirmesi

Login'in kiracıyı uygulama veritabanına sormadan bulabilmesi için dizin kurulur. Bu fazda login hâlâ tek veritabanına gidiyor; dizin **yazılır ve doğrulanır**, Faz 1b'de kullanılır.

**Files:**
- Create: `backend/src/Ekipzen.Domain/Entities/Tenancy/TenantDirectoryEntry.cs`
- Modify: `backend/src/Ekipzen.Application/Common/Interfaces/IPlatformDbContext.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContext.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs`
- Test: `backend/tests/Ekipzen.Tests.Integration/Tenancy/PlatformKatmaniTests.cs`

**Interfaces:**
- Produces:
  - `TenantDirectoryEntry` — `string NormalizedEmail` (PK), `int TenantId`
  - `IPlatformDbContext.Directory` — `DbSet<TenantDirectoryEntry>`
  - `TenantDirectoryEntry.Normalize(string email) → string`

- [ ] **Step 1: Failing testi yaz**

`PlatformKatmaniTests.cs` sınıfına ekle:

```csharp
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
    public async Task AyniEposta_IkinciKiracida_Reddedilir()
    {
        var eposta = $"tekil-{Guid.NewGuid():N}@ornek.local";
        await ProvisionAndActivateAsync("Birinci", eposta);

        var yanit = await ProvisionRawAsync(new
        {
            companyName = "Ikinci",
            slug = $"ikinci{Guid.NewGuid():N}"[..20],
            adminName = "Ikinci Yonetici",
            adminEmail = eposta,
        });

        yanit.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "tek e-posta = tek kiracı kuralı dizin birincil anahtarıyla korunmalı");
    }
```

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
dotnet test backend/tests/Ekipzen.Tests.Integration --filter "Dizine|IkinciKiracida"
```

Beklenen: derleme hatası — `TenantDirectoryEntry` ve `platform.Directory` yok.

- [ ] **Step 3: Varlığı yaz**

`backend/src/Ekipzen.Domain/Entities/Tenancy/TenantDirectoryEntry.cs`:

```csharp
namespace Ekipzen.Domain.Entities.Tenancy;

/// <summary>
/// E-posta → kiracı yönlendirmesi. Login kiracıyı SORMADAN yapıldığı için,
/// hangi kiracının veritabanına bakılacağı buradan çözülür.
///
/// TÜRETİLMİŞ kayıttır, asıl kaynak değildir: gerçek doğruluk kiracı
/// veritabanındaki Users tablosudur. Bu tablo bozulur ya da bir geri
/// yüklemeden sonra saparsa, kiracı veritabanı taranarak yeniden kurulur.
///
/// Birincil anahtarın e-posta olması "tek e-posta = tek kiracı" kuralını
/// veritabanı seviyesinde zorlar.
/// </summary>
public class TenantDirectoryEntry
{
    public string NormalizedEmail { get; set; } = string.Empty;

    public int TenantId { get; set; }

    /// <summary>
    /// E-postayı arama biçimine indirger. Identity'nin NormalizedEmail'i ile
    /// aynı kural: büyük harfe çevir. Türkçe'ye özgü i/İ sorununu doğurmamak
    /// için INVARIANT kültür kullanılır — e-posta adresleri ASCII'dir ve
    /// tr-TR'de "i".ToUpper() = "İ" olurdu, bu da Identity ile uyuşmazlık
    /// yaratırdı.
    /// </summary>
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
```

- [ ] **Step 4: Context'e ekle**

`IPlatformDbContext` arayüzüne ekle:

```csharp
    DbSet<TenantDirectoryEntry> Directory { get; }
```

`PlatformDbContext` sınıfına ekle:

```csharp
    public DbSet<TenantDirectoryEntry> Directory => Set<TenantDirectoryEntry>();
```

`PlatformDbContext.OnModelCreating` içine, `Tenant` bloğundan sonra ekle:

```csharp
        builder.Entity<TenantDirectoryEntry>(b =>
        {
            b.HasKey(d => d.NormalizedEmail);
            b.Property(d => d.NormalizedEmail).HasMaxLength(256);
            b.HasIndex(d => d.TenantId);
        });
```

- [ ] **Step 5: Dizin yazımını tek bir yardımcıda topla**

`IdentityService` içine şu özel yardımcıyı ekle. **Dizine yazan tek yer burasıdır** — başka hiçbir yerde `platform.Directory.Add` çağrılmaz:

```csharp
    /// <summary>
    /// Kullanıcıyı yönlendirme dizinine yazar ve ÇAKIŞMAYI 409'a çevirir.
    ///
    /// Dizinin birincil anahtarı e-postadır; çakışma "bu adres zaten bir
    /// kiracıya ait" demektir. Kural burada, veritabanı seviyesinde uygulanır —
    /// önceden yapılan bir kontrol eşzamanlı iki kaydı ayıramazdı.
    /// </summary>
    private async Task DizineYazAsync(string email, int tenantId, CancellationToken cancellationToken)
    {
        platform.Directory.Add(new TenantDirectoryEntry
        {
            NormalizedEmail = TenantDirectoryEntry.Normalize(email),
            TenantId = tenantId,
        });

        try
        {
            await platform.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException($"'{email}' e-postasıyla kayıtlı bir hesap zaten var.");
        }
    }
```

Bunu `IIdentityService` arayüzüne de **açık** bir üye olarak ekle, çünkü Step 6
onu Application katmanından çağıracak:

```csharp
    /// <summary>E-postayı kiracıya rezerve eder; adres başka bir kiracıdaysa 409.</summary>
    Task ReserveEmailAsync(string email, int tenantId, CancellationToken cancellationToken);
```

`IdentityService` içindeki uygulaması yalnız `DizineYazAsync`'i çağırır.

Kullanıcı oluşturan diğer yollarda (`RegisterAsync`, personel daveti)
`userManager.CreateAsync` BAŞARILI olduktan hemen sonra `DizineYazAsync`
çağrılır. Kullanıcı silen bir yol varsa karşılık gelen dizin satırı silinir.

- [ ] **Step 6: Provizyonda e-postayı ÖNCEDEN rezerve et**

`TenantOnboarding.CreateWithAdminAsync` içinde, kiracı kaydedildikten hemen
sonra ve `identityService.CreateTenantAdminAsync` çağrısından ÖNCE:

```csharp
        // E-posta, admin kullanıcı oluşturulmadan ÖNCE rezerve edilir: eşzamanlı
        // iki kayıt aynı adresi alamaz. Rezervasyonu kullanıcı oluşturmaya
        // bıraksaydık iki müşteri yarışabilirdi.
        await identityService.ReserveEmailAsync(adminEmail, tenant.Id, cancellationToken);
```

**Önemli:** rezervasyon burada yapıldığı için `CreateTenantAdminAsync` içinde
dizine İKİNCİ kez yazılmaz — o yol yalnız kullanıcıyı oluşturur. Aksi halde
birincil anahtar çakışması kendi provizyonumuzu 409'a düşürürdü.

Metodun başındaki mevcut `identityService.EmailExistsAsync` ön kontrolü
**kalır**: kullanıcıya erken ve net hata verir, ancak yarış koşulunu
kapatan şey rezervasyondur.

- [ ] **Step 7: Migration üret ve uygula**

```bash
cd backend
EKIPZEN_PLATFORM_CONNECTION="Server=localhost;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef migrations add TenantDirectory --context PlatformDbContext --output-dir Persistence/Migrations/Platform --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.Api
EKIPZEN_PLATFORM_CONNECTION="Server=localhost;Database=EkipzenPlatform;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False" dotnet ef database update --context PlatformDbContext --project src/Ekipzen.Infrastructure --startup-project src/Ekipzen.Api
```

- [ ] **Step 8: Testleri koş**

```bash
cd backend && dotnet test --configuration Release
```

Beklenen: 47 + 118 = 165 test PASS.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(platform): e-posta → kiracı yönlendirme dizini"
```

---

### Task 5: Dizin yeniden kurma komutu

Dizin türetilmiş olduğunu ancak yeniden kurulabildiğinde kanıtlar. Geri yükleme prosedürünün zorunlu adımı budur.

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/RebuildDirectoryCommand.cs`
- Modify: `backend/src/Ekipzen.Api/Controllers/TenancyController.cs`
- Test: `backend/tests/Ekipzen.Tests.Integration/Tenancy/PlatformKatmaniTests.cs`

**Interfaces:**
- Consumes: `IPlatformDbContext.Directory` (Task 4)
- Produces: `RebuildDirectoryCommand(int TenantId) : IRequest<RebuildDirectoryResult>`, `RebuildDirectoryResult(int TenantId, int YazilanKayit)`

- [ ] **Step 1: Failing testi yaz**

`PlatformKatmaniTests.cs` sınıfına ekle:

```csharp
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
        client.DefaultRequestHeaders.Add("X-Platform-Key", EkipzenApiFactory.PlatformKey);
        var yanit = await client.PostAsync($"/api/tenants/{kiraci.TenantId}/rebuild-directory", null);

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            (await platform.Directory.AnyAsync(d => d.NormalizedEmail == anahtar))
                .Should().BeTrue("dizin kiracı veritabanındaki kullanıcılardan yeniden kurulmalı");
        }
    }
```

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
dotnet test backend/tests/Ekipzen.Tests.Integration --filter "DizinYenidenKurma"
```

Beklenen: 404 — uç nokta yok.

- [ ] **Step 3: Komutu yaz**

`backend/src/Ekipzen.Application/Features/Tenancy/Commands/RebuildDirectoryCommand.cs`:

```csharp
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Bir kiracının yönlendirme dizinini kiracı veritabanındaki kullanıcılardan
/// yeniden kurar.
///
/// Neden var: dizin TÜRETİLMİŞ bir tablodur. Bir geri yüklemeden sonra platform
/// veritabanı geri sarılmadığı için dizin, geri yüklenmiş kiracı veritabanıyla
/// sapabilir — dizinde olup kiracıda olmayan kullanıcılar kalabilir. Bu komut
/// sapmayı kalıcı olmaktan çıkarır ve geri yükleme prosedürünün zorunlu adımıdır.
/// </summary>
public sealed record RebuildDirectoryCommand(int TenantId) : IRequest<RebuildDirectoryResult>;

public sealed record RebuildDirectoryResult(int TenantId, int YazilanKayit);

public sealed class RebuildDirectoryCommandHandler(
    IPlatformDbContext platform,
    IUserDirectorySource kullanicilar)
    : IRequestHandler<RebuildDirectoryCommand, RebuildDirectoryResult>
{
    public async Task<RebuildDirectoryResult> Handle(
        RebuildDirectoryCommand request, CancellationToken cancellationToken)
    {
        var epostalar = await kullanicilar.NormalizedEmailsAsync(request.TenantId, cancellationToken);

        var mevcut = await platform.Directory
            .Where(d => d.TenantId == request.TenantId)
            .ToListAsync(cancellationToken);

        platform.Directory.RemoveRange(mevcut);

        foreach (var eposta in epostalar)
        {
            platform.Directory.Add(new TenantDirectoryEntry
            {
                NormalizedEmail = eposta,
                TenantId = request.TenantId,
            });
        }

        await platform.SaveChangesAsync(cancellationToken);
        return new RebuildDirectoryResult(request.TenantId, epostalar.Count);
    }
}
```

- [ ] **Step 4: Kullanıcı kaynağı arayüzünü yaz**

`backend/src/Ekipzen.Application/Common/Interfaces/IUserDirectorySource.cs`:

```csharp
namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Bir kiracının kullanıcı e-postalarını, dizin yeniden kurmak için verir.
/// Identity Infrastructure katmanında olduğu için Application katmanı ona
/// doğrudan bakamaz; bu arayüz aradaki köprüdür.
/// </summary>
public interface IUserDirectorySource
{
    Task<IReadOnlyList<string>> NormalizedEmailsAsync(int tenantId, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Uygulamasını yaz**

`backend/src/Ekipzen.Infrastructure/Identity/UserDirectorySource.cs`:

```csharp
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Infrastructure.Identity;

/// <summary>Kiracının kullanıcı e-postalarını uygulama veritabanından okur.</summary>
public sealed class UserDirectorySource(AppDbContext context) : IUserDirectorySource
{
    public async Task<IReadOnlyList<string>> NormalizedEmailsAsync(
        int tenantId, CancellationToken cancellationToken) =>
        await context.Set<ApplicationUser>()
            .Where(u => u.TenantId == tenantId && u.NormalizedEmail != null)
            .Select(u => u.NormalizedEmail!)
            .ToListAsync(cancellationToken);
}
```

`DependencyInjection.cs` içine ekle:

```csharp
        services.AddScoped<IUserDirectorySource, Identity.UserDirectorySource>();
```

- [ ] **Step 6: Uç noktayı ekle**

`backend/src/Ekipzen.Api/Controllers/TenancyController.cs` içine, mevcut platform-key korumalı uçların yanına:

```csharp
    /// <remarks>Kiracının yönlendirme dizinini yeniden kurar (geri yükleme sonrası zorunlu adım).</remarks>
    [HttpPost("{id:int}/rebuild-directory")]
    [ProducesResponseType<RebuildDirectoryResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RebuildDirectoryResult>> RebuildDirectory(
        int id, CancellationToken cancellationToken)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        return Ok(await sender.Send(new RebuildDirectoryCommand(id), cancellationToken));
    }
```

Sınıftaki mevcut düzen budur: `PlatformKeyValid()` her aksiyonun içinde tek tek
çağrılır ve `Unauthorized` döner. Yeni bir yetkilendirme yolu icat etme —
attribute ya da filtre ekleme.

- [ ] **Step 7: Testleri koş**

```bash
cd backend && dotnet test --configuration Release
```

Beklenen: 47 + 119 = 166 test PASS.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(platform): dizin yeniden kurma komutu"
```

---

### Task 6: Provizyon durum makinesi ve çapraz-veritabanı purge

Kiracı satırı platform veritabanına taşındığı için provizyon ve purge artık tek transaction değil. Yarıda kalan iş görünür kalmalı, asla erişilebilir kalmamalı.

**Files:**
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/TenantOnboarding.cs`
- Modify: `backend/src/Ekipzen.Application/Features/Tenancy/Commands/ProvisionTenantCommand.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs`
- Create: `backend/src/Ekipzen.Application/Features/Tenancy/Queries/GetStuckTenantsQuery.cs`
- Modify: `backend/src/Ekipzen.Api/Controllers/TenancyController.cs`
- Test: `backend/tests/Ekipzen.Tests.Integration/Tenancy/PlatformKatmaniTests.cs`

**Interfaces:**
- Consumes: `TenantStatus` (Task 3), `IPlatformDbContext` (Task 1)
- Produces: `GetStuckTenantsQuery(int OlderThanMinutes) : IRequest<IReadOnlyList<StuckTenantDto>>`, `StuckTenantDto(int TenantId, string Slug, TenantStatus Status, DateTime CreatedAtUtc)`

- [ ] **Step 1: Failing testi yaz**

`PlatformKatmaniTests.cs` sınıfına ekle:

```csharp
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
        client.DefaultRequestHeaders.Add("X-Platform-Key", EkipzenApiFactory.PlatformKey);
        var takililar = await GetAsync<List<StuckTenantDto>>(client, "/api/tenants/stuck?olderThanMinutes=60");

        takililar.Should().Contain(t => t.TenantId == tenantId,
            "yarıda kalan provizyon görünür olmalı — sessiz enkaz bırakılmaz");
    }
```

- [ ] **Step 2: Testi koş, kırıldığını doğrula**

```bash
dotnet test backend/tests/Ekipzen.Tests.Integration --filter "YaridaKalanProvizyon|ProvizyonBasarili"
```

Beklenen: `StuckTenantDto` derlenmiyor; uç nokta 404.

- [ ] **Step 3: Provizyonu durum makinesine çevir**

`TenantOnboarding.CreateWithAdminAsync` içinde `Tenant` oluşturma satırını
`Status = hedefDurum` yerine **`Status = TenantStatus.Provisioning`** yap —
`hedefDurum` parametresi kalır ama artık yalnız en sonda uygulanır. Metodun
sonuna, admin oluşturulduktan SONRA ekle:

```csharp
        // Son adım: kiracı ancak burada kullanılabilir hale gelir. Araya giren
        // bir hata kiracıyı Provisioning'de bırakır — erişilemez ama GÖRÜNÜR,
        // ve operatör yeniden deneyebilir ya da geri alabilir.
        tenant.Status = hedefDurum;
        await platform.SaveChangesAsync(cancellationToken);
```

Self-servis kayıtta `hedefDurum` zaten `Provisioning`'dir, yani bu atama
etkisizdir ve kiracı davet kabul edilene kadar `Provisioning`'de kalır —
istenen davranış budur.

- [ ] **Step 4: Takılı kiracı sorgusunu yaz**

`backend/src/Ekipzen.Application/Features/Tenancy/Queries/GetStuckTenantsQuery.cs`:

```csharp
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Queries;

/// <summary>
/// Provizyonu ya da silmesi yarıda kalmış kiracıları listeler.
///
/// Kiracı satırı platform veritabanında, verisi uygulama veritabanında olduğu
/// için bu iki iş tek transaction değildir. Yarıda kalan bir iş sessiz enkaz
/// bırakmamalı; bu sorgu onu görünür kılar.
/// </summary>
public sealed record GetStuckTenantsQuery(int OlderThanMinutes) : IRequest<IReadOnlyList<StuckTenantDto>>;

public sealed record StuckTenantDto(int TenantId, string Slug, TenantStatus Status, DateTime CreatedAtUtc);

public sealed class GetStuckTenantsQueryHandler(IPlatformDbContext platform)
    : IRequestHandler<GetStuckTenantsQuery, IReadOnlyList<StuckTenantDto>>
{
    public async Task<IReadOnlyList<StuckTenantDto>> Handle(
        GetStuckTenantsQuery request, CancellationToken cancellationToken)
    {
        var esik = DateTime.UtcNow.AddMinutes(-request.OlderThanMinutes);

        return await platform.Tenants
            // Eşik YALNIZ Provisioning'e uygulanır. Purging'de kalmış bir kiracı
            // tanımı gereği anormaldir — silme tek istek içinde saniyeler sürer —
            // dolayısıyla yaşına bakılmaz. Provizyon ise davet kabulü beklerken
            // meşru olarak Provisioning'de durabilir, orada eşik gerekir.
            //
            // [Uygulama sırasında düzeltildi] Bu kod önce eşiği ikisine birden
            // uyguluyordu; CreatedAtUtc kiracının aylar önceki oluşturulma tarihi
            // olduğu için sağlıklı ilerleyen her silme "takılı" görünüyordu.
            .Where(t => (t.Status == TenantStatus.Provisioning && t.CreatedAtUtc < esik)
                        || t.Status == TenantStatus.Purging)
            .OrderBy(t => t.CreatedAtUtc)
            .Select(t => new StuckTenantDto(t.Id, t.Slug, t.Status, t.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Uç noktayı ekle**

`TenancyController` içine, platform-key korumalı bölüme:

```csharp
    /// <remarks>Provizyonu/silmesi yarıda kalmış kiracılar (operatör görünürlüğü).</remarks>
    [HttpGet("stuck")]
    [ProducesResponseType<IReadOnlyList<StuckTenantDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<StuckTenantDto>>> Stuck(
        [FromQuery] int olderThanMinutes = 60, CancellationToken cancellationToken = default)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        return Ok(await sender.Send(new GetStuckTenantsQuery(olderThanMinutes), cancellationToken));
    }
```

- [ ] **Step 6: Purge'ü durum makinesine bağla**

`TenantPurger.PurgeAsync` metodunun EN BAŞINA, `Impersonate` çağrısından önce ekle:

```csharp
        // Silme başlar başlamaz kiracı erişilemez olur ve öyle KALIR. Aşağıdaki
        // adımlardan biri patlarsa kiracı Purging'de takılı kalır — yarım silinmiş
        // bir kiracı asla erişilebilir bırakılmaz.
        var tenantRow = await platform.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenantRow is not null)
        {
            tenantRow.Status = TenantStatus.Purging;
            await platform.SaveChangesAsync(cancellationToken);
        }
```

Task 2 Step 7'de eklenen kiracı satırı silme bloğu, dizin satırlarını da silecek şekilde genişletilir:

```csharp
        if (tenantRow is not null)
        {
            var dizinKayitlari = await platform.Directory
                .Where(d => d.TenantId == tenantId)
                .ToListAsync(cancellationToken);
            platform.Directory.RemoveRange(dizinKayitlari);
            platform.Tenants.Remove(tenantRow);
            await platform.SaveChangesAsync(cancellationToken);
        }
```

- [ ] **Step 7: Testleri koş**

```bash
cd backend && dotnet test --configuration Release
```

Beklenen: 47 + 121 = 168 test PASS.

- [ ] **Step 8: Derleme uyarısı kontrolü**

```bash
cd backend && dotnet build --configuration Release -warnaserror
```

Beklenen: uyarısız.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(platform): provizyon durum makinesi ve çapraz-DB purge"
```

---

### Task 7: CI ve dokümantasyon

Platform veritabanı CI'da da kurulmalı; runbook iki veritabanını yansıtmalı.

**Files:**
- Modify: `.github/workflows/` altındaki backend iş akışı
- Modify: `docs/yedekleme-ve-kurtarma.md`
- Modify: `backend/docker-compose.yml`

- [ ] **Step 1: CI'da değişiklik gerekmediğini doğrula**

İş akışı `EKIPZEN_TEST_SQL` değişkenini **katalog adı olmadan** veriyor
(`Server=localhost,1433;User Id=sa;...`), katalog adını `EkipzenApiFactory.ConnectionFor`
ekliyor. Platform test veritabanı da aynı yoldan türetildiği için **CI'da
değişiklik gerekmez.**

Doğrulama: iş akışındaki `EKIPZEN_TEST_SQL` satırında `Database=` ya da
`Initial Catalog=` bulunmadığını gör. Bulunuyorsa (biri sonradan eklemişse)
kaldır — yoksa fabrikanın belirlediği ad ezilir ve iki context aynı
veritabanına bağlanır.

- [ ] **Step 2: docker-compose'a platform bağlantısını ekle**

`backend/docker-compose.yml` içindeki `api` servisinin `environment` bloğuna:

```yaml
      ConnectionStrings__PlatformConnection: "Server=mssql,1433;Database=EkipzenPlatform;User Id=sa;Password=Your_strong_Pass123;TrustServerCertificate=True;Encrypt=False"
```

- [ ] **Step 3: Runbook'u güncelle**

`docs/yedekleme-ve-kurtarma.md` içindeki "Kalan eksikler" bölümünde `⬜ Veritabanı kiracı bazına bölünmüyor` maddesini şununla değiştir:

```markdown
- 🔄 **Kiracı bazına bölme sürüyor** — Faz 1a tamamlandı: kiracı kimliği
  `EkipzenPlatform` veritabanına ayrıldı. Kiracı VERİSİ hâlâ tek `EkipzenDb`
  içindedir; tek müşteriyi geri yükleme yeteneği Faz 2'de gelir.
  Tasarım: `docs/superpowers/specs/2026-08-06-kiraci-basina-veritabani-design.md`
```

Ayrıca "Yedek planı" tablosunun altına ekle:

```markdown
> **İki veritabanı:** `EkipzenDb` (kiracı verisi) ve `EkipzenPlatform` (kiracı
> kimliği ve yönlendirme). İkisi de yedek planına dahildir — platform küçüktür
> ama onsuz hiçbir kullanıcı giriş yapamaz.
```

- [ ] **Step 4: Tam doğrulama**

```bash
cd backend
dotnet build --configuration Release -warnaserror
dotnet test --configuration Release
```

Beklenen: uyarısız derleme, 168 test PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore(platform): CI, compose ve runbook platform veritabanını yansıtır"
```

---

## Uygulama sonucu (2026-08-07)

**Tamamlandı.** 7 görev, 24 commit. Test tabanı 158 → 175 (47 birim + 128
entegrasyon), derleme uyarısız (`-warnaserror`).

Yedi görevin altısında inceleme gerçek kusur buldu; hepsi "testler geçiyor ama
kod yanlış şeyi yapıyor" sınıfındaydı:

| Görev | Yakalanan |
| --- | --- |
| 1 | Test hiçbir şey doğrulamıyordu (`Count >= 0` her zaman doğru) |
| 4 | **Üç kullanıcı yolu dizine yazmıyordu** — self-servis kayıt, demo seed, purge sonrası temizlik. Faz 1b'de "müşteri giriş yapamıyor" olarak patlayacaktı. Ayrıca bir test iddia ettiği kısıtı değil, ondan önceki ön kontrolü sınıyordu. |
| 5 | Kurtarma aracı tek çakışmada tümden çöküyordu — en çok ihtiyaç duyulacağı anda |
| 6 | Takılı kiracı listesinde eşik `Purging`'e de uygulanıyordu (plan hatası; kullanıcı kararıyla düzeltildi) |
| 7 | Runbook "iki DB de yedeklenir" derken tüm örnekler tek DB'yi gösteriyordu; düzeltildikten sonra iki zamanlanmış görev aynı adı kullanıp birbirini eziyordu |
| Nihai | Bu dalı çeken geliştiricinin uygulaması **açılışta çöküyordu**; üretim migration belgesi platform DB'sini hiç migrate etmiyordu; purge'de dosya silme hatası operatöre görünmüyordu; dizine yazma sırası kullanıcıyı rolsüz ve dizinsiz bırakabiliyordu |

## Faz 1b'ye devredilenler

Bunlar bilinçli olarak ertelendi; 1b planına girdi olacak.

| Konu | Not |
| --- | --- |
| `RefreshAsync` kiracı `Status` kontrolü | Tasarım gereği 1b'de kapı bağlantı çözümüne taşınınca yapısal olarak kapanır. **Şart:** 1b gecikirse, `Frozen` bir kiracının kullanıcıları refresh token ömrü boyunca oturum uzatmaya devam eder — o durumda 1b'yi beklemeden üç satırlık kontrol eklenmeli. |
| Dizine yazma sırası — öksüz satır | `DizineYazAsync` başarılı olup `CreateAsync` başarısız olursa dizinde kullanıcısız satır kalır. Aynı kiracı için sonraki deneme idempotentlik sayesinde kendini onarır; test edilmiyor. 1b'de bağlantı katmanına geçmeden önce test eklenmeli. |
| İşe alım yolu dizin testi | `CreateEmployeeLoginAsync` en yüksek hacimli kullanıcı yaratma yolu ve dizin iddiası olan testi yok. Dizin yazımı sessiz olduğu için regresyon 1b'ye kadar fark edilmez. |
| `AcceptInviteAsync` durum makinesini atlıyor | Herhangi bir `Provisioning` kiracıyı şifre belirlenir belirlenmez `Active` yapıyor. 1a'da zararsız; **Faz 2'de var olmayan bir veritabanına Active kiracı demektir.** |
| Dizinin ayrı soyutlamaya çıkarılması | Bugün tek yazma yolu `IdentityService` içinde. 1b'de bağlantı çözücü de dizine erişecek — o noktada Identity'den ayrılmalı. |
| `TenantOnboarding` ile `RegisterTenantCommand` sıra kopyası | Self-servis kayıt slug-retry döngüsü yüzünden ortak yolu kullanmıyor, sırayı kopyalıyor. İki kopya = ileride sessiz sapma riski. |
| `TenantStatus` migration `defaultValue: ""` | Geçerli enum değeri değil; şemada kalıcı. EF her zaman açık yazdığı için zararsız. |

---

## Faz sonu doğrulama

- [ ] `EkipzenDb` ve `EkipzenPlatform` sıfırdan kurulup uygulama açılıyor
- [ ] Demo kiracılarla giriş yapılabiliyor
- [ ] Yeni kiracı provizyonu uçtan uca çalışıyor, durum `Active` bitiyor
- [ ] Dondurulmuş kiracı giriş yapamıyor
- [ ] Dizin silinip yeniden kurulabiliyor
- [ ] Purge hem uygulama satırlarını hem platform satırlarını siliyor
- [ ] 168 test yeşil, derleme uyarısız
- [ ] CI yeşil

**Sonraki:** Faz 1b — bağlantı çözücü, kiracı kütüğü, durum kapısı, kiracıya sabitlenmiş kapsam fabrikası.
