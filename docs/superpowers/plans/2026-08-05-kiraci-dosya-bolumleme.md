# Kiracı Bazlı Dosya Bölümlemesi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Yüklenen dosyaları kiracı klasörlerine ayırmak; böylece kiracı bazlı yedek/geri yükleme mümkün olsun, purge tüm dosya türlerini kapsasın ve kiracılar arası yol kaçışı yapısal olarak imkânsız hale gelsin.

**Architecture:** Kiracı ön ekini **depo katmanı** uygular. `LocalFileStorage` scoped olur, `ICurrentTenant` alır ve her işlemde etkin kökü `{root}/tenant-{id}/` olarak çözer. Çağıran taraf kiracıyı hiç bilmez — ön eki koymayı unutmak imkânsızdır. Veritabanındaki yol değişmez (zaten kiracıdan bağımsız görelidir).

**Tech Stack:** .NET 9, EF Core, xUnit + FluentAssertions, PowerShell.

Tasarım: `docs/superpowers/specs/2026-08-05-kiraci-dosya-bolumleme-design.md`

## Global Constraints

- Mevcut testler kırılmayacak: backend 147/147.
- TDD: her görevde önce kırmızı test.
- Derleme uyarısız kalacak (CI `-warnaserror` ile koşuyor).
- **Veritabanı satırlarına dokunulmayacak.** `FilePath`/`PhotoPath`/`LogoPath` kiracıdan bağımsız göreli yol olarak kalır.
- Kiracı bağlamı yoksa dosya işlemi **reddedilir** — sessizce paylaşılan köke yazılmaz.
- `outbox/` kapsam dışıdır; `FileOutboxEmailSender` diske doğrudan yazar ve bu işten etkilenmez.
- Yıkıcı dosya işlemi yok: migrasyon **taşır**, sahibi çözülemeyeni yerinde bırakır ve loglar.
- Her görev ayrı commit.

---

## Uygulama durumu (2026-08-06)

| Görev | Durum |
| --- | --- |
| 1 · Depo katmanı kiracıya bağlanır | ✅ Tamam |
| 2 · Purge tüm dosya türlerini kapsar | ✅ Tamam |
| 3 · Mevcut dosyaları taşıyan migrasyon | ⛔ **Yapılmadı — gerek kalmadı** |
| 4 · Kiracı başına yedek arşivi | ✅ Tamam |

**Görev 3 neden atlandı:** Mevcut kiracılar demo amaçlı sahte veriydi. Taşımak
yerine veritabanı düşürülüp yeniden kuruldu ve depo klasörü silindi; uygulama
açılışta taze seed üretti. Gerçek veri taşıma ihtiyacı doğarsa plandaki script
tasarımı hâlâ geçerlidir.

**Plan dışı eklenenler:** doğrulama sırasında kiracı tablolarında `TenantId`
indeksi bulunmadığı görüldü ve eklendi (29 tablo + 2 Identity tablosu).

---

## File Structure

| Dosya | Sorumluluk |
| --- | --- |
| `backend/src/Ekipzen.Application/Common/Interfaces/IFileStorage.cs` | `DeleteTenantSpaceAsync` eklenir (değiştir) |
| `backend/src/Ekipzen.Infrastructure/Storage/LocalFileStorage.cs` | Kiracı ön eki + alan silme (değiştir) |
| `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs` | Singleton → Scoped (değiştir) |
| `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs` | Dosya silmeyi alan silmeye çevir (değiştir) |
| `backend/tests/Ekipzen.Tests.Unit/Storage/LocalFileStorageTests.cs` | Kiracı ön eki testleri (değiştir) |
| `backend/tests/Ekipzen.Tests.Integration/Tenancy/TenantPurgeTests.cs` | Foto + logo purge testi (değiştir) |
| `scripts/migrate-files-to-tenant-layout.ps1` | Tek seferlik migrasyon (oluştur) |
| `scripts/backup-restore-drill.ps1` | Kiracı başına arşiv (değiştir) |
| `docs/yedekleme-ve-kurtarma.md` | Kiracı bazlı yedek bölümü (değiştir) |

---

### Task 1: Depo katmanı kiracıya bağlanır

**Files:**
- Modify: `backend/src/Ekipzen.Application/Common/Interfaces/IFileStorage.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/Storage/LocalFileStorage.cs`
- Modify: `backend/src/Ekipzen.Infrastructure/DependencyInjection.cs:94`
- Modify: `backend/tests/Ekipzen.Tests.Unit/Storage/LocalFileStorageTests.cs`

**Interfaces:**
- Consumes: `ICurrentTenant` (`int? TenantId`, `int TenantIdOrThrow()`, `void Impersonate(int)`)
- Produces:
  - `LocalFileStorage.TenantFolder(int tenantId) → string` (`"tenant-{id}"`)
  - `IFileStorage.DeleteTenantSpaceAsync(int tenantId, CancellationToken) → Task`
  - `LocalFileStorage(IConfiguration, ICurrentTenant)` yapıcısı

- [ ] **Step 1: Failing testleri yaz**

`backend/tests/Ekipzen.Tests.Unit/Storage/LocalFileStorageTests.cs` dosyasının TAMAMINI şununla değiştir:

```csharp
using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ekipzen.Tests.Unit.Storage;

/// <summary>
/// Depo kiracıya bağlıdır: her işlem {kök}/tenant-{id}/ altında geçer.
/// Ön ek her zaman AKTİF kiracıdan uygulandığı için kiracılar arası kaçış
/// yapısal olarak imkânsızdır.
/// </summary>
public class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ekipzen-storage-{Guid.NewGuid():N}");
    private readonly TestKiraci _kiraci = new(1);

    private sealed class TestKiraci(int? tenantId) : ICurrentTenant
    {
        public int? TenantId { get; private set; } = tenantId;
        public int TenantIdOrThrow() => TenantId ?? throw new InvalidOperationException("Aktif kiracı yok.");
        public void Impersonate(int id) => TenantId = id;
        public void Temizle() => TenantId = null;
    }

    private LocalFileStorage Depo(ICurrentTenant? kiraci = null) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Root"] = _root })
            .Build(),
            kiraci ?? _kiraci);

    private static Stream Icerik(string metin) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(metin));

    [Fact]
    public async Task SaveAsync_DosyayiKiraciKlasorununAltinaYazar()
    {
        var depo = Depo();

        var stored = await depo.SaveAsync(Icerik("evrak"), "ozluk.pdf", "documents-emp-5", CancellationToken.None);

        // Dönen yol kiracıdan BAĞIMSIZ görelidir — DB bunu saklar.
        stored.Path.Should().StartWith("documents-emp-5/");
        stored.Path.Should().NotContain("tenant-");

        // Diskte ise kiracı klasörünün altındadır.
        var beklenen = Path.Combine(_root, LocalFileStorage.TenantFolder(1), stored.Path.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(beklenen).Should().BeTrue();
    }

    [Fact]
    public async Task OpenReadAsync_BaskaKiracininDosyasiniOkuyamaz()
    {
        var kiraciA = new TestKiraci(1);
        var yol = (await Depo(kiraciA).SaveAsync(Icerik("gizli"), "a.pdf", "documents-emp-5", CancellationToken.None)).Path;

        var kiraciB = new TestKiraci(2);

        // Aynı göreli yol, farklı kiracı: B'nin alanında böyle bir dosya yok.
        await FluentActions.Invoking(() => Depo(kiraciB).OpenReadAsync(yol, CancellationToken.None))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task OpenReadAsync_KiraciAlanindanKacanYolReddedilir()
    {
        var depo = Depo();
        var kacis = Path.Combine("..", LocalFileStorage.TenantFolder(2), "documents-emp-5", "x.pdf");

        await FluentActions.Invoking(() => depo.OpenReadAsync(kacis, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveAsync_KiraciBaglamiYoksaReddedilir()
    {
        var kiracisiz = new TestKiraci(null);

        await FluentActions
            .Invoking(() => Depo(kiracisiz).SaveAsync(Icerik("x"), "a.pdf", "documents-emp-5", CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteTenantSpaceAsync_TumDosyaTurleriniSiler_BaskaKiraciyaDokunmaz()
    {
        var kiraciA = new TestKiraci(1);
        var depoA = Depo(kiraciA);
        await depoA.SaveAsync(Icerik("evrak"), "a.pdf", "documents-emp-5", CancellationToken.None);
        await depoA.SaveAsync(Icerik("foto"), "a.png", "photos", CancellationToken.None);
        await depoA.SaveAsync(Icerik("logo"), "a.png", "branding", CancellationToken.None);

        var kiraciB = new TestKiraci(2);
        var bYolu = (await Depo(kiraciB).SaveAsync(Icerik("b"), "b.pdf", "documents-emp-9", CancellationToken.None)).Path;

        await depoA.DeleteTenantSpaceAsync(1, CancellationToken.None);

        Directory.Exists(Path.Combine(_root, LocalFileStorage.TenantFolder(1)))
            .Should().BeFalse("kiracının tüm dosya alanı silinmeli");

        // B etkilenmemeli.
        await using var stream = await Depo(kiraciB).OpenReadAsync(bYolu, CancellationToken.None);
        stream.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteTenantSpaceAsync_AlanYoksaSessizceGecer()
    {
        await FluentActions.Invoking(() => Depo().DeleteTenantSpaceAsync(99, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 2: Testlerin başarısız olduğunu gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~LocalFileStorage"`
Expected: derleme hatası — `LocalFileStorage` iki argümanlı yapıcı ve `TenantFolder` / `DeleteTenantSpaceAsync` yok.

- [ ] **Step 3: Arayüze alan silmeyi ekle**

`IFileStorage.cs` içindeki `DeleteAsync` satırından SONRA:

```csharp

    /// <summary>
    /// Kiracının tüm dosya alanını siler (purge). Dizin yoksa sessizce geçer.
    /// Kiracı AÇIKÇA parametredir: purge sırasında silinen kiracı ile aktif
    /// bağlam ayrışabilir, örtük çözümleme burada tehlikelidir.
    /// </summary>
    Task DeleteTenantSpaceAsync(int tenantId, CancellationToken cancellationToken);
```

- [ ] **Step 4: LocalFileStorage'ı kiracıya bağla**

`LocalFileStorage.cs` dosyasının TAMAMINI şununla değiştir:

```csharp
using Ekipzen.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Ekipzen.Infrastructure.Storage;

/// <summary>
/// Yerel disk dosya deposu. Kök dizin "Storage:Root" ayarından gelir.
/// Dosyalar {kök}/tenant-{id}/{kategori}/{guid}{uzantı} olarak saklanır;
/// orijinal ad DB'de meta olarak tutulur.
///
/// Kiracı ön ekini ÇAĞIRAN DEĞİL BU SINIF uygular. Böylece yeni bir yükleme
/// ucu eklendiğinde ön eki koymayı unutmak imkânsızdır — veritabanı kiracı
/// filtresindeki reflection yaklaşımıyla aynı ilke.
///
/// Dönen/alınan yollar kiracıdan BAĞIMSIZ görelidir (DB bunları saklar).
/// Ön ek her işlemde AKTİF kiracıdan yeniden uygulandığı için, kayıttaki yol
/// kurcalansa bile başka kiracının alanına erişilemez.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly ICurrentTenant _currentTenant;

    public LocalFileStorage(IConfiguration configuration, ICurrentTenant currentTenant)
    {
        var configured = configuration["Storage:Root"] ?? Path.Combine("App_Data", "storage");
        _root = Path.GetFullPath(configured);
        Directory.CreateDirectory(_root);
        _currentTenant = currentTenant;
    }

    /// <summary>Kiracı klasör adı — yedekleme ve migrasyon script'leri de bu şemayı kullanır.</summary>
    public static string TenantFolder(int tenantId) => $"tenant-{tenantId}";

    public async Task<StoredFile> SaveAsync(
        Stream content, string fileName, string category, CancellationToken cancellationToken)
    {
        var tenantRoot = AktifKiraciKoku();
        var safeCategory = SanitizeSegment(category);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var relativePath = Path.Combine(safeCategory, $"{Guid.NewGuid():N}{extension}");

        var fullPath = ResolveSafe(tenantRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var target = File.Create(fullPath);
        await content.CopyToAsync(target, cancellationToken);

        // Yol ayracı platformdan bağımsız saklanır; kiracı ön eki DB'ye YAZILMAZ.
        return new StoredFile(relativePath.Replace('\\', '/'), fileName, target.Length);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafe(AktifKiraciKoku(), path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Depodaki dosya bulunamadı.", path);
        }

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        var fullPath = ResolveSafe(AktifKiraciKoku(), path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task DeleteTenantSpaceAsync(int tenantId, CancellationToken cancellationToken)
    {
        var tenantRoot = Path.Combine(_root, TenantFolder(tenantId));
        if (Directory.Exists(tenantRoot))
        {
            Directory.Delete(tenantRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>Aktif kiracının kök dizini; kiracı yoksa işlem reddedilir.</summary>
    private string AktifKiraciKoku()
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException(
                "Dosya işlemi için aktif kiracı gerekli. Kiracısız yazma, kiracılar arası sızıntının tohumudur.");

        return Path.Combine(_root, TenantFolder(tenantId));
    }

    /// <summary>
    /// Kiracı kökü dışına çıkan yolları (../ vb.) reddeder. Karşılaştırma kök +
    /// dizin ayracı ile yapılır: ayraçsız bir StartsWith, kök adının ön ekini
    /// paylaşan KARDEŞ dizinleri ("tenant-1" kökü için "tenant-11") kök içinde sanır.
    /// </summary>
    private static string ResolveSafe(string tenantRoot, string relativePath)
    {
        var boundary = tenantRoot.EndsWith(Path.DirectorySeparatorChar)
            ? tenantRoot
            : tenantRoot + Path.DirectorySeparatorChar;

        var fullPath = Path.GetFullPath(Path.Combine(tenantRoot, relativePath));
        if (!fullPath.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Geçersiz dosya yolu.");
        }

        return fullPath;
    }

    private static string SanitizeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(segment.Where(c => !invalid.Contains(c) && c != '.').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "misc" : cleaned;
    }
}
```

- [ ] **Step 5: DI kaydını scoped'a çevir**

`DependencyInjection.cs` içinde:

```csharp
        // Kiracıya bağlı olduğu için SCOPED: singleton olsaydı ilk isteğin kiracısı
        // sonsuza kadar yapışırdı (captive dependency).
        services.AddScoped<IFileStorage, Storage.LocalFileStorage>();
```

(Eski satır: `services.AddSingleton<IFileStorage, Storage.LocalFileStorage>();`)

- [ ] **Step 6: Testlerin geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~LocalFileStorage"`
Expected: 6 test PASS

- [ ] **Step 7: Tüm backend testlerini koştur**

Run: `cd backend && dotnet build -warnaserror && dotnet test`
Expected: uyarısız derleme; entegrasyon testleri de geçer (dosya yükleyen testler artık kiracı klasörüne yazar).

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat(depo): dosyaları kiracı klasörüne böl"
```

---

### Task 2: Purge tüm dosya türlerini kapsar

**Files:**
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs`
- Modify: `backend/tests/Ekipzen.Tests.Integration/Tenancy/TenantPurgeTests.cs`

**Interfaces:**
- Consumes: `IFileStorage.DeleteTenantSpaceAsync(int, CancellationToken)`, `LocalFileStorage.TenantFolder(int)`
- Produces: yok

- [ ] **Step 1: Failing test yaz**

`TenantPurgeTests.cs` içine yeni test ekle (sınıfın mevcut yardımcılarını kullanır; `Storage:Root` fabrikada `EkipzenApiFactory.StorageRoot` olarak açıktır):

```csharp
    [Fact]
    public async Task Purge_FotografVeLogoDosyalariniDaSiler()
    {
        // Evrak dışındaki dosya türleri de kiracı alanındadır ve purge'de gitmeli.
        // Eski davranış yalnız EmployeeDocuments yollarını siliyordu; foto ve logo kalıyordu.
        var tenantId = await ProvisionTenantAsync();
        var tenantKlasoru = Path.Combine(factory.StorageRoot, LocalFileStorage.TenantFolder(tenantId));

        Directory.CreateDirectory(Path.Combine(tenantKlasoru, "photos"));
        Directory.CreateDirectory(Path.Combine(tenantKlasoru, "branding"));
        await File.WriteAllTextAsync(Path.Combine(tenantKlasoru, "photos", "foto.png"), "foto");
        await File.WriteAllTextAsync(Path.Combine(tenantKlasoru, "branding", "logo.png"), "logo");

        await PurgeTenantAsync(tenantId);

        Directory.Exists(tenantKlasoru).Should().BeFalse("kiracının tüm dosya alanı silinmeli");
    }
```

> **Not:** `ProvisionTenantAsync` ve `PurgeTenantAsync` bu dosyada zaten var; yoksa
> mevcut purge testindeki provizyon/purge çağrılarını birebir kopyalayarak
> yerel yardımcı olarak ekle. `using Ekipzen.Infrastructure.Storage;` gerekir.

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~FotografVeLogo"`
Expected: FAIL — dizin hâlâ duruyor (purge yalnız evrak yollarını siliyor).

- [ ] **Step 3: Purge'ü alan silmeye çevir**

`TenantPurger.cs` içinde dosya toplama bloğunu (satır ~23-28) SİL:

```csharp
        // Fiziksel dosyaları önce topla (yalnız EmployeeDocument saklar). Global filtre için impersone et.
        currentTenant.Impersonate(tenantId);
        var filePaths = await context.EmployeeDocuments
            .Where(d => d.FilePath != "")
            .Select(d => d.FilePath)
            .ToListAsync(cancellationToken);
```

Yerine:

```csharp
        // Global filtre için impersone et (tablo silmeleri açık TenantId ile yapılsa da
        // aradaki sorgular filtreye tabidir).
        currentTenant.Impersonate(tenantId);
```

Ardından dosya silme döngüsünü (transaction'dan SONRAKİ blok) şununla değiştir:

```csharp
        // 4) Dosyalar: kiracının TÜM alanı silinir. Tek tek yol silmek evrak dışındaki
        // türleri (fotoğraf, logo) kaçırıyordu; alan silme ileride eklenecek her türü
        // de otomatik kapsar.
        try
        {
            await fileStorage.DeleteTenantSpaceAsync(tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Sessizce yutma: DB temizlendi ama dosyalar kaldıysa KVKK açısından
            // PII riski sürer; elle temizlik için LOUD loglanır.
            logger.LogError(ex,
                "Kiracı {TenantId} dosya alanı silinemedi. Elle temizlik gerekiyor.", tenantId);
        }
```

- [ ] **Step 4: Testin geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~Purge"`
Expected: yeni test dahil tüm purge testleri PASS

- [ ] **Step 5: Tüm backend testlerini koştur**

Run: `cd backend && dotnet build -warnaserror && dotnet test`
Expected: uyarısız; hepsi geçer.

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "fix(kvkk): purge fotoğraf ve logo dosyalarını da silsin"
```

---

### Task 3: Mevcut dosyaları taşıyan migrasyon

**Files:**
- Create: `scripts/migrate-files-to-tenant-layout.ps1`

**Interfaces:**
- Consumes: `LocalFileStorage.TenantFolder` şeması (`tenant-{id}`)
- Produces: `migrate-files-to-tenant-layout.ps1 -StoragePath <yol> -Database <ad> [-WhatIf]`

**Dosya sahipliği SQL'i** (kiracıyı bulmak için):

```sql
-- Evrak: EmployeeDocuments.FilePath → Employee.TenantId
SELECT d.FilePath, e.TenantId
FROM EmployeeDocuments d JOIN Employees e ON e.Id = d.EmployeeId;

-- Fotoğraf: EmployeeProfiles.PhotoPath → Employee.TenantId
SELECT p.PhotoPath, e.TenantId
FROM EmployeeProfiles p JOIN Employees e ON e.Id = p.EmployeeId
WHERE p.PhotoPath IS NOT NULL AND p.PhotoPath <> '';

-- Logo: CompanyProfiles.LogoPath → CompanyProfiles.TenantId
SELECT LogoPath, TenantId FROM CompanyProfiles
WHERE LogoPath IS NOT NULL AND LogoPath <> '';
```

- [ ] **Step 1: Script'i yaz**

`scripts/migrate-files-to-tenant-layout.ps1` (UTF-8 **BOM ile** kaydedilmeli — Windows PowerShell 5.1 BOM'suz UTF-8'i ANSI okur ve Türkçe karakterleri bozar):

```powershell
<#
.SYNOPSIS
    Mevcut dosyaları kiracı klasörü düzenine taşır (tek seferlik).

.DESCRIPTION
    Dosya sahipliği veritabanından çözülür ve dosya {kök}/tenant-{id}/ altına
    TAŞINIR. Veritabanı satırlarına DOKUNULMAZ: saklanan yol zaten kiracıdan
    bağımsız görelidir.

    Sahibi çözülemeyen dosya taşınmaz, yerinde bırakılır ve raporlanır —
    sessizce silinmez.

.PARAMETER StoragePath
    Depo kökü (ör. backend\src\Ekipzen.API\App_Data\storage).

.PARAMETER Database
    Veritabanı adı.

.PARAMETER ServerInstance
    SQL Server örneği. Varsayılan: localhost

.EXAMPLE
    pwsh scripts/migrate-files-to-tenant-layout.ps1 -StoragePath .\App_Data\storage -Database EkipzenDb -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$StoragePath,
    [Parameter(Mandatory = $true)][string]$Database,
    [string]$ServerInstance = "localhost"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $StoragePath)) { Write-Error "Depo bulunamadı: $StoragePath"; exit 1 }
$kok = (Resolve-Path $StoragePath).Path

function Invoke-Sql {
    param([string]$Query)
    $out = & sqlcmd -S $ServerInstance -d $Database -E -b -h -1 -W -s "|" -Q $Query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL hatası: $out" }
    return $out
}

# Yol → kiracı eşlemesi (üç dosya türü).
$sorgu = @"
SET NOCOUNT ON;
SELECT d.FilePath, e.TenantId FROM EmployeeDocuments d JOIN Employees e ON e.Id = d.EmployeeId
WHERE d.FilePath IS NOT NULL AND d.FilePath <> ''
UNION ALL
SELECT p.PhotoPath, e.TenantId FROM EmployeeProfiles p JOIN Employees e ON e.Id = p.EmployeeId
WHERE p.PhotoPath IS NOT NULL AND p.PhotoPath <> ''
UNION ALL
SELECT LogoPath, TenantId FROM CompanyProfiles
WHERE LogoPath IS NOT NULL AND LogoPath <> '';
"@

$sahiplik = @{}
foreach ($satir in (Invoke-Sql -Query $sorgu)) {
    $parcalar = "$satir" -split '\|'
    if ($parcalar.Count -ge 2) {
        $yol = $parcalar[0].Trim()
        $kiraci = $parcalar[1].Trim()
        if ($yol -and $kiraci -match '^\d+$') { $sahiplik[$yol] = [int]$kiraci }
    }
}
Write-Host "Veritabanında $($sahiplik.Count) dosya kaydı bulundu."

$tasinan = 0; $atlanan = @()

foreach ($giris in $sahiplik.GetEnumerator()) {
    $goreliYol = $giris.Key -replace '/', [IO.Path]::DirectorySeparatorChar
    $kaynak = Join-Path $kok $goreliYol
    if (-not (Test-Path $kaynak)) { continue }  # zaten taşınmış ya da hiç yok

    $hedef = Join-Path (Join-Path $kok "tenant-$($giris.Value)") $goreliYol
    if ($PSCmdlet.ShouldProcess($goreliYol, "tenant-$($giris.Value) altına taşı")) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $hedef) -Force | Out-Null
        Move-Item -Path $kaynak -Destination $hedef -Force
    }
    $tasinan++
}

# Sahipsiz kalanları raporla (outbox hariç — bilinçli kapsam dışı).
foreach ($dosya in Get-ChildItem -Path $kok -Recurse -File) {
    $goreli = $dosya.FullName.Substring($kok.Length).TrimStart([IO.Path]::DirectorySeparatorChar)
    if ($goreli -like "tenant-*") { continue }
    if ($goreli -like "outbox*") { continue }
    $atlanan += $goreli
}

Write-Host ""
Write-Host "Taşınan: $tasinan" -ForegroundColor Green
if ($atlanan.Count -gt 0) {
    Write-Host "Sahibi çözülemeyen (YERİNDE BIRAKILDI, silinmedi): $($atlanan.Count)" -ForegroundColor Yellow
    $atlanan | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    Write-Host "Bunları elle inceleyin; veritabanında karşılığı olmayan artık dosyalar olabilir."
}
exit 0
```

- [ ] **Step 2: Önce deneme modunda koştur**

Run: `pwsh scripts/migrate-files-to-tenant-layout.ps1 -StoragePath "backend/src/Ekipzen.API/App_Data/storage" -Database EkipzenDb -WhatIf`
Expected: taşınacak dosyalar listelenir, **hiçbiri taşınmaz**.

- [ ] **Step 3: Gerçekten koştur ve doğrula**

Run: `pwsh scripts/migrate-files-to-tenant-layout.ps1 -StoragePath "backend/src/Ekipzen.API/App_Data/storage" -Database EkipzenDb`
Expected: dosyalar `tenant-{id}/` altına taşınır; `outbox/` yerinde kalır; sahipsizler raporlanır ama silinmez.

Doğrulama: `find backend/src/Ekipzen.API/App_Data/storage -type d` → `tenant-1/documents-emp-14` benzeri yapı görünür.

- [ ] **Step 4: Commit**

```bash
git add scripts/migrate-files-to-tenant-layout.ps1
git commit -m "ops: mevcut dosyaları kiracı düzenine taşıyan migrasyon"
```

---

### Task 4: Kiracı başına yedek arşivi

**Files:**
- Modify: `scripts/backup-restore-drill.ps1`
- Modify: `docs/yedekleme-ve-kurtarma.md`

**Interfaces:**
- Consumes: `tenant-{id}` klasör şeması
- Produces: `{BackupPath}/{Database}-tenant-{id}-{damga}.zip` (kiracı başına bir arşiv)

- [ ] **Step 1: Evrak arşivleme bloğunu kiracı başına çevir**

`backup-restore-drill.ps1` içindeki `[4b]` bloğunu şununla değiştir:

```powershell
    # --- Evrak dosyaları (kiracı başına) -------------------------------
    # Kiracı başına ayrı arşiv: tek müşteriyi geri yükleyebilmek ve KVKK gereği
    # tek müşterinin yedeğini imha edebilmek için gerekli.
    if ($StoragePath) {
        Write-Host "[4b] Evrak dosyaları arşivleniyor: $StoragePath"
        if (-not (Test-Path $StoragePath)) {
            throw "Evrak dizini bulunamadı: $StoragePath"
        }

        $kiraciKlasorleri = Get-ChildItem -Path $StoragePath -Directory -Filter "tenant-*"
        if ($kiraciKlasorleri.Count -eq 0) {
            Write-Host "       kiracı klasörü yok; evrak arşivi üretilmedi"
        }
        foreach ($klasor in $kiraciKlasorleri) {
            $arsiv = Join-Path (Resolve-Path $BackupPath) "$Database-$($klasor.Name)-$stamp.zip"
            Compress-Archive -Path (Join-Path $klasor.FullName "*") -DestinationPath $arsiv -Force -ErrorAction Stop
            $script:storageArchives += $arsiv
            $dosyaSayisi = (Get-ChildItem -Path $klasor.FullName -Recurse -File).Count
            Write-Host ("       {0}: {1} dosya → {2} KB" -f `
                $klasor.Name, $dosyaSayisi, [Math]::Round((Get-Item $arsiv).Length / 1KB, 1))
        }
    }
```

- [ ] **Step 2: Değişkeni çoğula çevir**

Script başındaki `$script:storageArchive = $null` satırını şununla değiştir:

```powershell
$script:storageArchives = @()
```

Off-site kopyalama bloğundaki kaynak listesini güncelle:

```powershell
        foreach ($kaynak in (@($backupFile) + $script:storageArchives) | Where-Object { $_ }) {
```

Ve sonuç loglamasındaki alanı güncelle:

```powershell
        evrakArsivleri = $script:storageArchives
```

(Eski alan: `evrakArsivi = $script:storageArchive`)

- [ ] **Step 3: Tatbikatı gerçekten koştur**

Run:
```powershell
pwsh scripts/backup-restore-drill.ps1 -Database EkipzenDb `
  -BackupPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup" `
  -StoragePath "backend/src/Ekipzen.API/App_Data/storage" `
  -OffsitePath "$env:TEMP\ekipzen-offsite" -LogPath "$env:TEMP\ekipzen-yedek-log.jsonl"
```
Expected: çıkış kodu 0; her kiracı için ayrı zip üretilir ve off-site kopyalanıp doğrulanır.

- [ ] **Step 4: Runbook'u güncelle**

`docs/yedekleme-ve-kurtarma.md` içindeki bileşen tablosunda evrak satırını şununla değiştir:

```markdown
| Evrak dosyaları | `-StoragePath` | **Kiracı başına ayrı zip** üretilir (`{db}-tenant-{id}-{damga}.zip`). Tek müşteriyi geri yükleyebilir, KVKK gereği tek müşterinin yedeğini imha edebilirsiniz. **Veritabanı tek başına yetmez:** DB yalnız dosya yollarını tutar. |
```

Ve "Kalan eksikler" listesinden varsa dosya yedeğiyle ilgili maddeyi çıkar.

- [ ] **Step 5: Commit**

```bash
git add scripts/backup-restore-drill.ps1 docs/yedekleme-ve-kurtarma.md
git commit -m "ops: kiracı başına dosya yedeği arşivi"
```

---

## Self-Review notları

- **Spec kapsamı:** yol şeması → T1 · depo mimarisi → T1 · purge kapsamı → T2 · migrasyon → T3 · yedekleme → T4 · testler → T1/T2. Boşluk yok.
- **Tip tutarlılığı:** `LocalFileStorage.TenantFolder(int)` T1'de tanımlanıp T2 testinde ve T3/T4 script şemasında kullanılıyor. `DeleteTenantSpaceAsync(int, CancellationToken)` T1'de tanımlanıp T2'de çağrılıyor.
- **Bilinen varsayım:** T2'deki `ProvisionTenantAsync` / `PurgeTenantAsync` yardımcılarının `TenantPurgeTests` içinde mevcut olduğu varsayıldı; yoksa uygulayan kişi mevcut purge testindeki çağrıları kopyalayarak yerel yardımcı ekler (adımda not edildi).
- **Kapsam dışı hatırlatma:** `outbox/` bölümlenmiyor; migrasyon script'i onu bilinçli atlıyor ve raporlamıyor.
