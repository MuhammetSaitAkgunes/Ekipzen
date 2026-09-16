# Excel'den Personel İçe Aktarma Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** İK yöneticisinin bir `.xlsx` dosyasından toplu personel kartı oluşturmasını sağlamak; hatalı satırları aktarmadan önce raporlamak.

**Architecture:** Ayrıştırma (dosya biçimi) ve doğrulama (iş kuralı) ayrı sınıflarda. `preview` ve `import` uçları aynı ayrıştırıcı + doğrulayıcıyı kullanır; `preview` tek farkla `import`'tur — kaydetmez. Sunucuda geçici durum tutulmaz (durumsuz çift yükleme).

**Tech Stack:** .NET 9, MediatR, FluentValidation, EF Core, ClosedXML (MIT), React + TanStack Query.

Tasarım: `docs/superpowers/specs/2026-08-05-excel-personel-ice-aktarma-design.md`

## Global Constraints

- Mevcut testler kırılmayacak: backend 122/122, frontend 145/145.
- TDD: her görevde önce kırmızı test.
- Derleme uyarısız kalacak (CI `-warnaserror` ile koşuyor).
- Yetki: tüm uçlar `Policies.HrAdminOnly`.
- Sınırlar: yalnız `.xlsx`, ≤ 5 MB, ≤ 1000 veri satırı.
- Kiracı izolasyonu global sorgu filtresinden gelir; elle `TenantId` filtresi YAZILMAYACAK.
- Departman/TC karşılaştırmaları Türkçe kültürle normalize edilir (`tr-TR`), çünkü `I/ı/İ/i` dönüşümü `InvariantCulture`'da yanlıştır.
- Her görev ayrı commit.

---

## File Structure

| Dosya | Sorumluluk |
| --- | --- |
| `backend/Directory.Packages.props` | ClosedXML sürümü (değiştir) |
| `backend/src/Ekipzen.Application/Ekipzen.Application.csproj` | ClosedXML referansı (değiştir) |
| `.../Features/Employees/Import/ImportContracts.cs` | `ImportRow`, DTO'lar, `ValidatedImport` |
| `.../Features/Employees/Import/EmployeeImportTemplate.cs` | Şablon `.xlsx` üretir |
| `.../Features/Employees/Import/EmployeeImportParser.cs` | `.xlsx` → `ImportRow[]` |
| `.../Features/Employees/Import/EmployeeImportValidator.cs` | `ImportRow[]` → geçerli model + sorunlar |
| `.../Features/Employees/Import/PreviewEmployeeImportCommand.cs` | Önizleme akışı |
| `.../Features/Employees/Import/ImportEmployeesCommand.cs` | Aktarım akışı (transaction) |
| `backend/src/Ekipzen.API/Controllers/EmployeesController.cs` | 3 uç (değiştir) |
| `backend/tests/Ekipzen.Tests.Unit/Employees/EmployeeImportValidatorTests.cs` | Doğrulayıcı testleri |
| `backend/tests/Ekipzen.Tests.Unit/Employees/EmployeeImportParserTests.cs` | Ayrıştırıcı testleri |
| `backend/tests/Ekipzen.Tests.Integration/Personnel/EmployeeImportTests.cs` | Uçtan uca |
| `frontend/src/features/personnel/ImportModal.tsx` | İçe aktarma modalı |
| `frontend/src/features/personnel/ImportModal.test.tsx` | Modal testleri |
| `frontend/src/features/personnel/queries.ts` | 2 mutation (değiştir) |
| `frontend/src/features/personnel/PersonnelPage.tsx` | Düğme + modal (değiştir) |

---

### Task 1: ClosedXML bağımlılığı ve şablon ucu

**Files:**
- Modify: `backend/Directory.Packages.props`
- Modify: `backend/src/Ekipzen.Application/Ekipzen.Application.csproj`
- Create: `backend/src/Ekipzen.Application/Features/Employees/Import/EmployeeImportTemplate.cs`
- Modify: `backend/src/Ekipzen.API/Controllers/EmployeesController.cs`
- Create: `backend/tests/Ekipzen.Tests.Unit/Employees/EmployeeImportTemplateTests.cs`

**Interfaces:**
- Produces: `EmployeeImportTemplate.SutunBasliklari` (string[]), `EmployeeImportTemplate.Olustur()` → `byte[]`

- [ ] **Step 1: Failing test yaz**

`backend/tests/Ekipzen.Tests.Unit/Employees/EmployeeImportTemplateTests.cs`:

```csharp
using ClosedXML.Excel;
using FluentAssertions;
using Ekipzen.Application.Features.Employees.Import;
using Xunit;

namespace Ekipzen.Tests.Unit.Employees;

public class EmployeeImportTemplateTests
{
    [Fact]
    public void Olustur_BasliklariVeOrnekSatiriIcerir()
    {
        var bytes = EmployeeImportTemplate.Olustur();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);

        // 1. satır başlık, 2. satır örnek.
        var basliklar = EmployeeImportTemplate.SutunBasliklari
            .Select((_, i) => sheet.Cell(1, i + 1).GetString())
            .ToArray();

        basliklar.Should().Equal(EmployeeImportTemplate.SutunBasliklari);
        sheet.Cell(2, 1).GetString().Should().NotBeEmpty("örnek satır kullanıcıya biçimi gösterir");
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~EmployeeImportTemplate"`
Expected: derleme hatası — `EmployeeImportTemplate` bulunamıyor.

- [ ] **Step 3: ClosedXML'i ekle**

`backend/Directory.Packages.props` içindeki `<ItemGroup>` bloğuna:

```xml
    <PackageVersion Include="ClosedXML" Version="0.104.2" />
```

`backend/src/Ekipzen.Application/Ekipzen.Application.csproj` içindeki `<ItemGroup>` bloğuna:

```xml
    <PackageReference Include="ClosedXML" />
```

- [ ] **Step 4: Şablonu yaz**

`backend/src/Ekipzen.Application/Features/Employees/Import/EmployeeImportTemplate.cs`:

```csharp
using ClosedXML.Excel;

namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>
/// İçe aktarma şablonu. Başlıklar buradan üretilir ve ayrıştırıcı da buradan
/// okur — tek kaynak olduğu için şablon ile ayrıştırıcı ayrışamaz.
/// </summary>
public static class EmployeeImportTemplate
{
    public const string SayfaAdi = "Personel";

    public static readonly string[] SutunBasliklari =
    [
        "Ad", "Soyad", "Unvan", "Departman", "İşe Giriş Tarihi",
        "TC Kimlik No", "Durum", "Kişisel E-posta", "Telefon", "IBAN",
    ];

    /// <summary>Zorunlu sütunlar (başlık doğrulamasında aranır).</summary>
    public static readonly string[] ZorunluSutunlar =
        ["Ad", "Soyad", "Unvan", "Departman", "İşe Giriş Tarihi"];

    public static byte[] Olustur()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SayfaAdi);

        for (var i = 0; i < SutunBasliklari.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = SutunBasliklari[i];
            cell.Style.Font.Bold = true;
        }

        // Örnek satır: kullanıcı beklenen biçimi görsün (özellikle tarih).
        string[] ornek =
        [
            "Ahmet", "Yılmaz", "Yazılım Uzmanı", "Yazılım", "01.03.2026",
            "12345678901", "active", "ahmet@ornek.com", "0555 111 22 33", "TR000000000000000000000000",
        ];
        for (var i = 0; i < ornek.Length; i++) sheet.Cell(2, i + 1).Value = ornek[i];

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
```

- [ ] **Step 5: Testin geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~EmployeeImportTemplate"`
Expected: PASS

- [ ] **Step 6: Ucu ekle**

`EmployeesController.cs` içine (mevcut `using` bloğuna `using Ekipzen.Application.Features.Employees.Import;` ekleyerek):

```csharp
    /// <summary>İçe aktarma şablonunu indirir (başlıklar sistemle garantili aynı).</summary>
    [HttpGet("import/template")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DownloadImportTemplate()
        => File(EmployeeImportTemplate.Olustur(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "ekipzen-personel-sablonu.xlsx");
```

- [ ] **Step 7: Derle ve testleri koştur**

Run: `cd backend && dotnet build -warnaserror && dotnet test`
Expected: uyarısız derleme, tüm testler geçer.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "feat(personel): içe aktarma şablonu ucu (ClosedXML)"
```

---

### Task 2: Ayrıştırıcı

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Employees/Import/ImportContracts.cs`
- Create: `backend/src/Ekipzen.Application/Features/Employees/Import/EmployeeImportParser.cs`
- Create: `backend/tests/Ekipzen.Tests.Unit/Employees/EmployeeImportParserTests.cs`

**Interfaces:**
- Consumes: `EmployeeImportTemplate.SutunBasliklari`, `ZorunluSutunlar`
- Produces:
  - `record ImportRow(int SatirNo, string? Ad, string? Soyad, string? Unvan, string? Departman, string? IseGirisTarihi, string? TcKimlikNo, string? Durum, string? KisiselEposta, string? Telefon, string? Iban)`
  - `record ImportRowIssueDto(int SatirNo, string Alan, string Mesaj)`
  - `EmployeeImportParser.Ayristir(Stream) → (IReadOnlyList<ImportRow> Satirlar, IReadOnlyList<ImportRowIssueDto> Sorunlar)`
  - `EmployeeImportParser.MaksimumSatir` = 1000

- [ ] **Step 1: Failing test yaz**

`backend/tests/Ekipzen.Tests.Unit/Employees/EmployeeImportParserTests.cs`:

```csharp
using ClosedXML.Excel;
using FluentAssertions;
using Ekipzen.Application.Features.Employees.Import;
using Xunit;

namespace Ekipzen.Tests.Unit.Employees;

public class EmployeeImportParserTests
{
    /// <summary>Verilen satırlardan bellekte .xlsx üretir (1. satır başlık).</summary>
    private static Stream DosyaUret(params string[][] satirlar)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(EmployeeImportTemplate.SayfaAdi);
        for (var i = 0; i < EmployeeImportTemplate.SutunBasliklari.Length; i++)
            sheet.Cell(1, i + 1).Value = EmployeeImportTemplate.SutunBasliklari[i];

        for (var r = 0; r < satirlar.Length; r++)
            for (var c = 0; c < satirlar[r].Length; c++)
                sheet.Cell(r + 2, c + 1).Value = satirlar[r][c];

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Ayristir_SatirlariVeExcelSatirNumarasiniOkur()
    {
        using var dosya = DosyaUret(
            ["Ayşe", "Demir", "Analist", "Yazılım", "01.03.2026", "12345678901", "active", "", "", ""]);

        var (satirlar, sorunlar) = EmployeeImportParser.Ayristir(dosya);

        sorunlar.Should().BeEmpty();
        satirlar.Should().ContainSingle();
        satirlar[0].SatirNo.Should().Be(2, "başlık 1. satır; ilk veri 2. satırdır");
        satirlar[0].Ad.Should().Be("Ayşe");
        satirlar[0].Departman.Should().Be("Yazılım");
    }

    [Fact]
    public void Ayristir_TamamenBosSatirlariAtlar()
    {
        using var dosya = DosyaUret(
            ["Ayşe", "Demir", "Analist", "Yazılım", "01.03.2026", "", "", "", "", ""],
            ["", "", "", "", "", "", "", "", "", ""],
            ["Mehmet", "Kaya", "Uzman", "Yazılım", "02.03.2026", "", "", "", "", ""]);

        var (satirlar, _) = EmployeeImportParser.Ayristir(dosya);

        satirlar.Should().HaveCount(2, "boş satır veri değildir");
        satirlar.Select(s => s.SatirNo).Should().Equal(2, 4);
    }

    [Fact]
    public void Ayristir_ZorunluSutunEksikse_Sorunlar()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Personel");
        sheet.Cell(1, 1).Value = "Ad";
        sheet.Cell(1, 2).Value = "Soyad";
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var (satirlar, sorunlar) = EmployeeImportParser.Ayristir(ms);

        satirlar.Should().BeEmpty();
        sorunlar.Should().NotBeEmpty();
        sorunlar[0].Mesaj.Should().Contain("Departman");
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~EmployeeImportParser"`
Expected: derleme hatası — `EmployeeImportParser` yok.

- [ ] **Step 3: Sözleşmeleri yaz**

`ImportContracts.cs`:

```csharp
namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>Ham satır: ayrıştırıcı çıktısı. Değerler yorumlanmamış metindir.</summary>
public sealed record ImportRow(
    int SatirNo,
    string? Ad, string? Soyad, string? Unvan, string? Departman,
    string? IseGirisTarihi, string? TcKimlikNo, string? Durum,
    string? KisiselEposta, string? Telefon, string? Iban);

/// <summary>SatirNo EXCEL satır numarasıdır; kullanıcı hatayı dosyada bulabilsin.</summary>
public sealed record ImportRowIssueDto(int SatirNo, string Alan, string Mesaj);

public sealed record ImportPreviewDto(
    int ToplamSatir, int GecerliSatir, int HataliSatir, int MukerrerSatir,
    IReadOnlyList<string> BilinmeyenDepartmanlar,
    IReadOnlyList<ImportRowIssueDto> Sorunlar);

public sealed record ImportResultDto(
    int OlusturulanSatir, int AtlananSatir,
    IReadOnlyList<ImportRowIssueDto> Sorunlar);
```

- [ ] **Step 4: Ayrıştırıcıyı yaz**

`EmployeeImportParser.cs`:

```csharp
using ClosedXML.Excel;

namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>
/// .xlsx → ham satırlar. Yalnız OKUR: iş kuralı bilmez, doğrulama yapmaz.
/// Böylece doğrulayıcı Excel olmadan test edilebilir.
/// </summary>
public static class EmployeeImportParser
{
    public const int MaksimumSatir = 1000;

    public static (IReadOnlyList<ImportRow> Satirlar, IReadOnlyList<ImportRowIssueDto> Sorunlar)
        Ayristir(Stream stream)
    {
        var sorunlar = new List<ImportRowIssueDto>();

        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);

        // Başlık → sütun indeksi (büyük/küçük ve boşluk duyarsız).
        var sutunlar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var baslik = cell.GetString().Trim();
            if (baslik.Length > 0) sutunlar[baslik] = cell.Address.ColumnNumber;
        }

        var eksik = EmployeeImportTemplate.ZorunluSutunlar
            .Where(s => !sutunlar.ContainsKey(s))
            .ToArray();
        if (eksik.Length > 0)
        {
            sorunlar.Add(new ImportRowIssueDto(1, "Başlık",
                $"Zorunlu sütunlar eksik: {string.Join(", ", eksik)}. Şablonu indirip kullanın."));
            return ([], sorunlar);
        }

        string? Deger(IXLRow row, string baslik) =>
            sutunlar.TryGetValue(baslik, out var col)
                ? row.Cell(col).GetFormattedString().Trim() is { Length: > 0 } v ? v : null
                : null;

        var satirlar = new List<ImportRow>();
        var sonSatir = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 2; r <= sonSatir; r++)
        {
            var row = sheet.Row(r);
            var satir = new ImportRow(
                r,
                Deger(row, "Ad"), Deger(row, "Soyad"), Deger(row, "Unvan"), Deger(row, "Departman"),
                Deger(row, "İşe Giriş Tarihi"), Deger(row, "TC Kimlik No"), Deger(row, "Durum"),
                Deger(row, "Kişisel E-posta"), Deger(row, "Telefon"), Deger(row, "IBAN"));

            // Tamamen boş satır veri değildir (Excel'de sık görülür).
            var bosMu = satir.Ad is null && satir.Soyad is null && satir.Unvan is null
                && satir.Departman is null && satir.IseGirisTarihi is null
                && satir.TcKimlikNo is null && satir.Durum is null
                && satir.KisiselEposta is null && satir.Telefon is null && satir.Iban is null;
            if (bosMu) continue;

            satirlar.Add(satir);

            if (satirlar.Count > MaksimumSatir)
            {
                sorunlar.Add(new ImportRowIssueDto(r, "Dosya",
                    $"En fazla {MaksimumSatir} satır aktarılabilir. Dosyayı bölün."));
                return ([], sorunlar);
            }
        }

        return (satirlar, sorunlar);
    }
}
```

- [ ] **Step 5: Testlerin geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~EmployeeImportParser"`
Expected: 3 test PASS

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "feat(personel): içe aktarma .xlsx ayrıştırıcısı"
```

---

### Task 3: Doğrulayıcı

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Employees/Import/EmployeeImportValidator.cs`
- Create: `backend/tests/Ekipzen.Tests.Unit/Employees/EmployeeImportValidatorTests.cs`

**Interfaces:**
- Consumes: `ImportRow`, `ImportRowIssueDto`, `EmployeeUpsertModel`, `EmployeeProfileUpsertModel`
- Produces:
  - `record ValidatedImport(IReadOnlyList<EmployeeUpsertModel> Gecerli, IReadOnlyList<ImportRowIssueDto> Sorunlar, IReadOnlyList<string> BilinmeyenDepartmanlar, int MukerrerSatir)`
  - `EmployeeImportValidator.Dogrula(IReadOnlyList<ImportRow> satirlar, IReadOnlyDictionary<string,int> departmanlar, ISet<string> mevcutTcler) → ValidatedImport`
  - `EmployeeImportValidator.Normalize(string?) → string` (departman anahtarı; `tr-TR` küçük harf)

- [ ] **Step 1: Failing test yaz**

`backend/tests/Ekipzen.Tests.Unit/Employees/EmployeeImportValidatorTests.cs`:

```csharp
using FluentAssertions;
using Ekipzen.Application.Features.Employees.Import;
using Xunit;

namespace Ekipzen.Tests.Unit.Employees;

public class EmployeeImportValidatorTests
{
    private static readonly Dictionary<string, int> Departmanlar =
        new() { [EmployeeImportValidator.Normalize("Yazılım")] = 7 };

    private static ImportRow Satir(
        int no = 2, string? ad = "Ayşe", string? soyad = "Demir", string? unvan = "Analist",
        string? departman = "Yazılım", string? tarih = "01.03.2026", string? tc = null,
        string? durum = null, string? eposta = null, string? iban = null) =>
        new(no, ad, soyad, unvan, departman, tarih, tc, durum, eposta, null, iban);

    private static ValidatedImport Dogrula(params ImportRow[] satirlar) =>
        EmployeeImportValidator.Dogrula(satirlar, Departmanlar, new HashSet<string>());

    [Fact]
    public void GecerliSatir_ModelUretir_DepartmanIdCozulur()
    {
        var sonuc = Dogrula(Satir());

        sonuc.Sorunlar.Should().BeEmpty();
        sonuc.Gecerli.Should().ContainSingle();
        sonuc.Gecerli[0].DepartmentId.Should().Be(7);
        sonuc.Gecerli[0].Status.Should().Be("active", "durum boşsa varsayılan active");
    }

    [Fact]
    public void ZorunluAlanEksikse_Sorun()
    {
        var sonuc = Dogrula(Satir(ad: null));

        sonuc.Gecerli.Should().BeEmpty();
        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "Ad" && s.SatirNo == 2);
    }

    [Fact]
    public void BilinmeyenDepartman_SorunVeListe()
    {
        var sonuc = Dogrula(Satir(departman: "Pazarlama"));

        sonuc.Gecerli.Should().BeEmpty();
        sonuc.BilinmeyenDepartmanlar.Should().Contain("Pazarlama");
    }

    [Fact]
    public void DepartmanEslesmesi_BuyukKucukHarfVeBoslukDuyarsiz()
    {
        var sonuc = Dogrula(Satir(departman: "  YAZILIM  "));

        sonuc.Gecerli.Should().ContainSingle();
        sonuc.Gecerli[0].DepartmentId.Should().Be(7);
    }

    [Fact]
    public void GecersizTc_Sorun()
    {
        var sonuc = Dogrula(Satir(tc: "123"));

        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "TC Kimlik No");
    }

    [Fact]
    public void SistemdeKayitliTc_MukerrerSayilirVeAtlanir()
    {
        var sonuc = EmployeeImportValidator.Dogrula(
            [Satir(tc: "12345678901")], Departmanlar, new HashSet<string> { "12345678901" });

        sonuc.Gecerli.Should().BeEmpty();
        sonuc.MukerrerSatir.Should().Be(1);
    }

    [Fact]
    public void DosyaIcindeAyniTcIkiKez_IkincisiSorun()
    {
        var sonuc = Dogrula(
            Satir(no: 2, tc: "12345678901"),
            Satir(no: 3, tc: "12345678901"));

        sonuc.Gecerli.Should().ContainSingle("ilk satır geçerli");
        sonuc.Sorunlar.Should().ContainSingle(s => s.SatirNo == 3 && s.Alan == "TC Kimlik No");
    }

    [Fact]
    public void GecersizIban_Sorun()
    {
        var sonuc = Dogrula(Satir(iban: "TR123"));

        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "IBAN");
    }

    [Fact]
    public void OkunamayanTarih_Sorun()
    {
        var sonuc = Dogrula(Satir(tarih: "yakında"));

        sonuc.Sorunlar.Should().ContainSingle(s => s.Alan == "İşe Giriş Tarihi");
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~EmployeeImportValidator"`
Expected: derleme hatası.

- [ ] **Step 3: Doğrulayıcıyı yaz**

`EmployeeImportValidator.cs`:

```csharp
using Ekipzen.Application.Features.Employees.Upsert;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ekipzen.Application.Features.Employees.Import;

public sealed record ValidatedImport(
    IReadOnlyList<EmployeeUpsertModel> Gecerli,
    IReadOnlyList<ImportRowIssueDto> Sorunlar,
    IReadOnlyList<string> BilinmeyenDepartmanlar,
    int MukerrerSatir);

/// <summary>
/// Ham satırları iş kurallarına göre doğrular. Excel bilmez — düz nesnelerle
/// test edilebilir. preview ve import AYNI bu sınıfı kullanır.
/// </summary>
public static class EmployeeImportValidator
{
    private static readonly Regex TcRegex = new(@"^\d{11}$", RegexOptions.Compiled);
    private static readonly Regex IbanRegex = new(@"^TR\d{24}$", RegexOptions.Compiled);
    private static readonly CultureInfo Turkce = new("tr-TR");

    /// <summary>
    /// Departman anahtarı. Türkçe kültür kullanılır: InvariantCulture'da
    /// "I" → "i" dönüşümü yanlıştır ve "IK" ile "ık" eşleşmez.
    /// </summary>
    public static string Normalize(string? deger) =>
        Regex.Replace((deger ?? string.Empty).Trim(), @"\s+", " ").ToLower(Turkce);

    public static ValidatedImport Dogrula(
        IReadOnlyList<ImportRow> satirlar,
        IReadOnlyDictionary<string, int> departmanlar,
        ISet<string> mevcutTcler)
    {
        var gecerli = new List<EmployeeUpsertModel>();
        var sorunlar = new List<ImportRowIssueDto>();
        var bilinmeyenDepartmanlar = new List<string>();
        var dosyadakiTcler = new HashSet<string>();
        var mukerrer = 0;

        foreach (var satir in satirlar)
        {
            var satirSorunlari = new List<ImportRowIssueDto>();
            void Sorun(string alan, string mesaj) =>
                satirSorunlari.Add(new ImportRowIssueDto(satir.SatirNo, alan, mesaj));

            if (string.IsNullOrWhiteSpace(satir.Ad)) Sorun("Ad", "Zorunlu alan.");
            if (string.IsNullOrWhiteSpace(satir.Soyad)) Sorun("Soyad", "Zorunlu alan.");
            if (string.IsNullOrWhiteSpace(satir.Unvan)) Sorun("Unvan", "Zorunlu alan.");

            // Departman
            var departmanId = 0;
            if (string.IsNullOrWhiteSpace(satir.Departman))
            {
                Sorun("Departman", "Zorunlu alan.");
            }
            else if (departmanlar.TryGetValue(Normalize(satir.Departman), out var id))
            {
                departmanId = id;
            }
            else
            {
                Sorun("Departman", $"'{satir.Departman}' sistemde yok. Önce departmanı oluşturun.");
                if (!bilinmeyenDepartmanlar.Contains(satir.Departman))
                    bilinmeyenDepartmanlar.Add(satir.Departman);
            }

            // Tarih: Excel hücresi metin ya da tarih olabilir; Türkçe biçim önce denenir.
            DateOnly iseGiris = default;
            if (string.IsNullOrWhiteSpace(satir.IseGirisTarihi))
            {
                Sorun("İşe Giriş Tarihi", "Zorunlu alan.");
            }
            else if (!DateOnly.TryParse(satir.IseGirisTarihi, Turkce, DateTimeStyles.None, out iseGiris)
                  && !DateOnly.TryParse(satir.IseGirisTarihi, CultureInfo.InvariantCulture, DateTimeStyles.None, out iseGiris))
            {
                Sorun("İşe Giriş Tarihi", $"'{satir.IseGirisTarihi}' tarih olarak okunamadı (örn. 01.03.2026).");
            }

            // TC
            string? tc = null;
            if (!string.IsNullOrWhiteSpace(satir.TcKimlikNo))
            {
                tc = satir.TcKimlikNo.Trim();
                if (!TcRegex.IsMatch(tc)) Sorun("TC Kimlik No", "11 haneli rakam olmalı.");
                else if (!dosyadakiTcler.Add(tc))
                    Sorun("TC Kimlik No", "Bu TC dosyada birden fazla satırda geçiyor.");
            }

            // Durum
            var durum = string.IsNullOrWhiteSpace(satir.Durum) ? "active" : satir.Durum.Trim().ToLower(Turkce);
            if (durum is not ("active" or "passive"))
                Sorun("Durum", "'active' veya 'passive' olmalı (boş bırakılırsa active).");

            // IBAN
            string? iban = null;
            if (!string.IsNullOrWhiteSpace(satir.Iban))
            {
                iban = satir.Iban.Replace(" ", "");
                if (!IbanRegex.IsMatch(iban)) Sorun("IBAN", "'TR' ile başlayan 26 karakter olmalı.");
            }

            // E-posta
            var eposta = string.IsNullOrWhiteSpace(satir.KisiselEposta) ? null : satir.KisiselEposta.Trim();
            if (eposta is not null && !eposta.Contains('@'))
                Sorun("Kişisel E-posta", "Geçerli bir e-posta olmalı.");

            // Mükerrer (sistem): hata değildir — atlanır ve raporlanır.
            if (satirSorunlari.Count == 0 && tc is not null && mevcutTcler.Contains(tc))
            {
                mukerrer++;
                continue;
            }

            if (satirSorunlari.Count > 0)
            {
                sorunlar.AddRange(satirSorunlari);
                continue;
            }

            gecerli.Add(new EmployeeUpsertModel(
                FirstName: satir.Ad!.Trim(),
                LastName: satir.Soyad!.Trim(),
                Title: satir.Unvan!.Trim(),
                DepartmentId: departmanId,
                HireDate: iseGiris,
                NationalId: tc,
                ManagerId: null,
                Status: durum,
                Profile: new EmployeeProfileUpsertModel(
                    PersonalEmail: eposta,
                    MobilePhone: string.IsNullOrWhiteSpace(satir.Telefon) ? null : satir.Telefon.Trim(),
                    Iban: iban)));
        }

        return new ValidatedImport(gecerli, sorunlar, bilinmeyenDepartmanlar, mukerrer);
    }
}
```

- [ ] **Step 4: Testlerin geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Unit --filter "FullyQualifiedName~EmployeeImportValidator"`
Expected: 9 test PASS

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "feat(personel): içe aktarma doğrulayıcısı"
```

---

### Task 4: Önizleme ucu

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Employees/Import/PreviewEmployeeImportCommand.cs`
- Modify: `backend/src/Ekipzen.API/Controllers/EmployeesController.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Personnel/EmployeeImportTests.cs`

**Interfaces:**
- Consumes: `EmployeeImportParser.Ayristir`, `EmployeeImportValidator.Dogrula`
- Produces: `PreviewEmployeeImportCommand(Stream Dosya) : IRequest<ImportPreviewDto>`, `ImportLookups.YukleAsync(...)`

- [ ] **Step 1: Failing entegrasyon testi yaz**

`backend/tests/Ekipzen.Tests.Integration/Personnel/EmployeeImportTests.cs`:

```csharp
using ClosedXML.Excel;
using FluentAssertions;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Employees.Import;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Personnel;

[Collection(ApiCollection.Name)]
public sealed class EmployeeImportTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task Preview_GecerliVeHataliSatirlariAyirir()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        using var content = DosyaIcerigi(
            ["Ayşe", "Demir", "Analist", "Yazılım", "01.03.2026", "", "", "", "", ""],
            ["", "Kaya", "Uzman", "Yazılım", "02.03.2026", "", "", "", "", ""],
            ["Ali", "Vural", "Uzman", "Pazarlama Yok", "03.03.2026", "", "", "", "", ""]);

        var response = await admin.PostAsync("/api/employees/import/preview", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rapor = (await response.Content.ReadFromJsonAsync<ImportPreviewDto>())!;
        rapor.ToplamSatir.Should().Be(3);
        rapor.GecerliSatir.Should().Be(1);
        rapor.Sorunlar.Should().Contain(s => s.SatirNo == 3 && s.Alan == "Ad");
        rapor.BilinmeyenDepartmanlar.Should().Contain("Pazarlama Yok");
    }

    [Fact]
    public async Task Preview_HrAdminDisindakileriReddeder()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        using var content = DosyaIcerigi(["A", "B", "C", "Yazılım", "01.03.2026", "", "", "", "", ""]);

        (await manager.PostAsync("/api/employees/import/preview", content))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static MultipartFormDataContent DosyaIcerigi(params string[][] satirlar)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(EmployeeImportTemplate.SayfaAdi);
        for (var i = 0; i < EmployeeImportTemplate.SutunBasliklari.Length; i++)
            sheet.Cell(1, i + 1).Value = EmployeeImportTemplate.SutunBasliklari[i];
        for (var r = 0; r < satirlar.Length; r++)
            for (var c = 0; c < satirlar[r].Length; c++)
                sheet.Cell(r + 2, c + 1).Value = satirlar[r][c];

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(ms.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "personel.xlsx");
        return content;
    }

    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"demo giriş başarısız: {email}");
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }
}
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~EmployeeImportTests"`
Expected: derleme hatası ya da 404.

- [ ] **Step 3: Komutu yaz**

`PreviewEmployeeImportCommand.cs`:

```csharp
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Import;

public sealed record PreviewEmployeeImportCommand(Stream Dosya) : IRequest<ImportPreviewDto>;

/// <summary>Doğrulama için gereken kiracı verisi (departman adları + kayıtlı TC'ler).</summary>
public static class ImportLookups
{
    public static async Task<(Dictionary<string, int> Departmanlar, HashSet<string> Tcler)>
        YukleAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        // Global sorgu filtresi kiracıyı otomatik kapsar; elle TenantId filtresi YOK.
        var departmanlar = await context.Departments
            .AsNoTracking()
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);

        var tcler = await context.Employees
            .AsNoTracking()
            .Where(e => e.NationalId != null)
            .Select(e => e.NationalId!)
            .ToListAsync(cancellationToken);

        var sozluk = new Dictionary<string, int>();
        foreach (var d in departmanlar) sozluk[EmployeeImportValidator.Normalize(d.Name)] = d.Id;

        return (sozluk, tcler.ToHashSet());
    }
}

public sealed class PreviewEmployeeImportCommandHandler(IApplicationDbContext context)
    : IRequestHandler<PreviewEmployeeImportCommand, ImportPreviewDto>
{
    public async Task<ImportPreviewDto> Handle(
        PreviewEmployeeImportCommand request, CancellationToken cancellationToken)
    {
        var (satirlar, ayristirmaSorunlari) = EmployeeImportParser.Ayristir(request.Dosya);
        if (ayristirmaSorunlari.Count > 0)
        {
            return new ImportPreviewDto(0, 0, 0, 0, [], ayristirmaSorunlari);
        }

        var (departmanlar, tcler) = await ImportLookups.YukleAsync(context, cancellationToken);
        var sonuc = EmployeeImportValidator.Dogrula(satirlar, departmanlar, tcler);

        return new ImportPreviewDto(
            ToplamSatir: satirlar.Count,
            GecerliSatir: sonuc.Gecerli.Count,
            HataliSatir: satirlar.Count - sonuc.Gecerli.Count - sonuc.MukerrerSatir,
            MukerrerSatir: sonuc.MukerrerSatir,
            BilinmeyenDepartmanlar: sonuc.BilinmeyenDepartmanlar,
            Sorunlar: sonuc.Sorunlar);
    }
}
```

- [ ] **Step 4: Ucu ekle**

`EmployeesController.cs`:

```csharp
    /// <summary>Dosyayı doğrular, KAYDETMEZ. Aktarımdan önce rapor üretir.</summary>
    [HttpPost("import/preview")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<ImportPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportPreviewDto>> PreviewImport(
        IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await sender.Send(new PreviewEmployeeImportCommand(stream), cancellationToken));
    }
```

- [ ] **Step 5: Testlerin geçtiğini gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~EmployeeImportTests"`
Expected: 2 test PASS

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "feat(personel): içe aktarma önizleme ucu"
```

---

### Task 5: Aktarım ucu

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/Employees/Import/ImportEmployeesCommand.cs`
- Modify: `backend/src/Ekipzen.API/Controllers/EmployeesController.cs`
- Modify: `backend/tests/Ekipzen.Tests.Integration/Personnel/EmployeeImportTests.cs`

**Interfaces:**
- Consumes: `ImportLookups.YukleAsync`, `EmployeeImportValidator.Dogrula`
- Produces: `ImportEmployeesCommand(Stream Dosya) : IRequest<ImportResultDto>`

- [ ] **Step 1: Failing testleri ekle**

`EmployeeImportTests.cs` içine:

```csharp
    [Fact]
    public async Task Import_GecerliSatirlariOlusturur_MukerreriAtlar()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var tc = "988" + Random.Shared.Next(10000000, 99999999);

        using var ilk = DosyaIcerigi(
            ["Zeynep", "Aktaş", "Analist", "Yazılım", "01.03.2026", tc, "", "", "", ""]);
        var ilkYanit = await admin.PostAsync("/api/employees/import", ilk);
        ilkYanit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ilkYanit.Content.ReadFromJsonAsync<ImportResultDto>())!
            .OlusturulanSatir.Should().Be(1);

        // Aynı dosya ikinci kez: mükerrer olarak atlanmalı, yeni kayıt oluşmamalı.
        using var ikinci = DosyaIcerigi(
            ["Zeynep", "Aktaş", "Analist", "Yazılım", "01.03.2026", tc, "", "", "", ""]);
        var ikinciYanit = await admin.PostAsync("/api/employees/import", ikinci);
        var sonuc = (await ikinciYanit.Content.ReadFromJsonAsync<ImportResultDto>())!;

        sonuc.OlusturulanSatir.Should().Be(0, "aynı TC zaten kayıtlı");
        sonuc.AtlananSatir.Should().Be(1);
    }

    [Fact]
    public async Task Import_HrAdminDisindakileriReddeder()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        using var content = DosyaIcerigi(["A", "B", "C", "Yazılım", "01.03.2026", "", "", "", "", ""]);

        (await manager.PostAsync("/api/employees/import", content))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd backend && dotnet test tests/Ekipzen.Tests.Integration --filter "FullyQualifiedName~EmployeeImportTests"`
Expected: 404 (uç yok).

- [ ] **Step 3: Komutu yaz**

`ImportEmployeesCommand.cs`:

```csharp
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Application.Features.Employees.Upsert;
using MediatR;

namespace Ekipzen.Application.Features.Employees.Import;

public sealed record ImportEmployeesCommand(Stream Dosya) : IRequest<ImportResultDto>;

public sealed class ImportEmployeesCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ImportEmployeesCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(
        ImportEmployeesCommand request, CancellationToken cancellationToken)
    {
        var (satirlar, ayristirmaSorunlari) = EmployeeImportParser.Ayristir(request.Dosya);
        if (ayristirmaSorunlari.Count > 0)
        {
            return new ImportResultDto(0, 0, ayristirmaSorunlari);
        }

        var (departmanlar, tcler) = await ImportLookups.YukleAsync(context, cancellationToken);
        var sonuc = EmployeeImportValidator.Dogrula(satirlar, departmanlar, tcler);

        foreach (var model in sonuc.Gecerli)
        {
            context.Employees.Add(new Employee
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Title = model.Title,
                NationalId = model.NationalId,
                DepartmentId = model.DepartmentId,
                HireDate = model.HireDate,
                Status = EmployeeMappings.ParseStatus(model.Status),
                Profile = EmployeeUpsertGuards.MapProfile(new EmployeeProfile(), model.Profile),
            });
        }

        // Tek SaveChanges → tek transaction. Beklenmedik bir hatada HİÇBİRİ kaydedilmez;
        // yarım aktarım durumu oluşmaz.
        await context.SaveChangesAsync(cancellationToken);

        var atlanan = satirlar.Count - sonuc.Gecerli.Count;
        return new ImportResultDto(sonuc.Gecerli.Count, atlanan, sonuc.Sorunlar);
    }
}
```

- [ ] **Step 4: Ucu ekle**

`EmployeesController.cs`:

```csharp
    /// <summary>Geçerli satırları kaydeder; hatalı ve mükerrer satırları atlar.</summary>
    [HttpPost("import")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<ImportResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportResultDto>> Import(
        IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await sender.Send(new ImportEmployeesCommand(stream), cancellationToken));
    }
```

- [ ] **Step 5: Tüm backend testlerini koştur**

Run: `cd backend && dotnet build -warnaserror && dotnet test`
Expected: uyarısız derleme; 122 + yeni testler geçer.

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "feat(personel): Excel'den toplu personel aktarımı"
```

---

### Task 6: Arayüz

**Files:**
- Modify: `frontend/src/features/personnel/queries.ts`
- Create: `frontend/src/features/personnel/ImportModal.tsx`
- Create: `frontend/src/features/personnel/ImportModal.test.tsx`
- Modify: `frontend/src/features/personnel/PersonnelPage.tsx`
- Regenerate: `frontend/src/api/schema.d.ts` (`npm run gen:api`, backend açıkken)

**Interfaces:**
- Consumes: `POST /api/employees/import/preview`, `POST /api/employees/import`, `GET /api/employees/import/template`
- Produces: `<ImportModal open onClose />`

- [ ] **Step 1: Failing test yaz**

`frontend/src/features/personnel/ImportModal.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/stubApi";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { ImportModal } from "./ImportModal";

const rapor = {
  toplamSatir: 3, gecerliSatir: 1, hataliSatir: 2, mukerrerSatir: 0,
  bilinmeyenDepartmanlar: ["Pazarlama Yok"],
  sorunlar: [{ satirNo: 3, alan: "Ad", mesaj: "Zorunlu alan." }],
};

beforeEach(() => stubApi({ "/api/employees/import/preview": rapor }));
afterEach(() => vi.unstubAllGlobals());

const dosyaSec = async () => {
  const input = screen.getByLabelText(/Excel dosyası/i);
  await userEvent.upload(input, new File(["x"], "personel.xlsx", {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  }));
};

test("dosya seçilince önizleme raporu gösterilir", async () => {
  renderPage(<ToastProvider><ImportModal open onClose={() => {}} /></ToastProvider>);
  await dosyaSec();

  expect(await screen.findByText("Zorunlu alan.")).toBeInTheDocument();
  expect(screen.getByText("Pazarlama Yok")).toBeInTheDocument();
});

test("geçerli satır yoksa Aktar düğmesi pasiftir", async () => {
  stubApi({ "/api/employees/import/preview": { ...rapor, gecerliSatir: 0 } });
  renderPage(<ToastProvider><ImportModal open onClose={() => {}} /></ToastProvider>);
  await dosyaSec();

  await waitFor(() =>
    expect(screen.getByRole("button", { name: /Aktar/ })).toBeDisabled());
});
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd frontend && npx vitest run src/features/personnel/ImportModal.test.tsx`
Expected: FAIL — `./ImportModal` çözümlenemiyor.

- [ ] **Step 3: Sorguları ekle**

`frontend/src/features/personnel/queries.ts` sonuna:

```ts
export type ImportPreviewDto = {
  toplamSatir: number; gecerliSatir: number; hataliSatir: number; mukerrerSatir: number;
  bilinmeyenDepartmanlar: string[];
  sorunlar: { satirNo: number; alan: string; mesaj: string }[];
};
export type ImportResultDto = {
  olusturulanSatir: number; atlananSatir: number;
  sorunlar: { satirNo: number; alan: string; mesaj: string }[];
};

const dosyaGovdesi = (file: File) => {
  const form = new FormData();
  form.append("file", file);
  return form;
};

export const usePreviewImport = () =>
  useMutation({
    mutationFn: (file: File) =>
      apiFetch<ImportPreviewDto>("/employees/import/preview", { method: "POST", body: dosyaGovdesi(file) }),
  });

export const useImportEmployees = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) =>
      apiFetch<ImportResultDto>("/employees/import", { method: "POST", body: dosyaGovdesi(file) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["employees"] }),
  });
};
```

- [ ] **Step 4: Modalı yaz**

`frontend/src/features/personnel/ImportModal.tsx`:

```tsx
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { usePreviewImport, useImportEmployees, type ImportPreviewDto } from "./queries";

export function ImportModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { showToast } = useToast();
  const preview = usePreviewImport();
  const importer = useImportEmployees();
  const [file, setFile] = useState<File | null>(null);
  const [rapor, setRapor] = useState<ImportPreviewDto | null>(null);

  if (!open) return null;

  const dosyaSecildi = async (secilen: File | null) => {
    setFile(secilen);
    setRapor(null);
    if (!secilen) return;
    try {
      setRapor(await preview.mutateAsync(secilen));
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Dosya okunamadı.", "error");
    }
  };

  const aktar = async () => {
    if (!file) return;
    try {
      const sonuc = await importer.mutateAsync(file);
      showToast(`${sonuc.olusturulanSatir} personel aktarıldı, ${sonuc.atlananSatir} satır atlandı.`, "success");
      onClose();
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Aktarım başarısız.", "error");
    }
  };

  return (
    <div className="fullscreen-modal" style={{ display: "flex" }} role="dialog" aria-label="Excel'den personel içe aktar">
      <section className="card">
        <div className="card-header-clean">
          <h4>Excel'den Personel İçe Aktar</h4>
          <button className="btn-icon-sm" onClick={onClose} aria-label="Kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>

        <p className="text-muted">
          Önce <a href="/api/employees/import/template">şablonu indirin</a>, doldurun ve yükleyin.
          Yükleme yalnızca doğrulama yapar; aktarım için onayınız gerekir.
        </p>

        <div className="input-group">
          <label className="input-label" htmlFor="import-file">Excel dosyası (.xlsx)</label>
          <input id="import-file" className="input-control" type="file" accept=".xlsx"
            onChange={(e) => dosyaSecildi(e.target.files?.[0] ?? null)} />
        </div>

        {preview.isPending && <p className="pending-desc">Dosya doğrulanıyor…</p>}

        {rapor && (
          <>
            <div className="kpi-grid">
              <div className="kpi-card"><span className="kpi-label">Toplam</span><h3 className="kpi-value">{rapor.toplamSatir}</h3></div>
              <div className="kpi-card"><span className="kpi-label">Geçerli</span><h3 className="kpi-value">{rapor.gecerliSatir}</h3></div>
              <div className="kpi-card"><span className="kpi-label">Hatalı</span><h3 className="kpi-value">{rapor.hataliSatir}</h3></div>
              <div className="kpi-card"><span className="kpi-label">Mükerrer</span><h3 className="kpi-value">{rapor.mukerrerSatir}</h3></div>
            </div>

            {rapor.bilinmeyenDepartmanlar.length > 0 && (
              <div className="surface">
                <strong>Sistemde olmayan departmanlar:</strong>
                <ul>{rapor.bilinmeyenDepartmanlar.map((d) => <li key={d}>{d}</li>)}</ul>
                <small>Bu departmanları önce oluşturun, sonra dosyayı tekrar yükleyin.</small>
              </div>
            )}

            {rapor.sorunlar.length > 0 && (
              <table className="data-table">
                <thead><tr><th>Satır</th><th>Alan</th><th>Sorun</th></tr></thead>
                <tbody>
                  {rapor.sorunlar.map((s, i) => (
                    <tr key={`${s.satirNo}-${s.alan}-${i}`}>
                      <td>{s.satirNo}</td><td>{s.alan}</td><td>{s.mesaj}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}

        <div className="toolbar-actions">
          <button className="btn btn-secondary" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={aktar}
            disabled={!rapor || rapor.gecerliSatir === 0 || importer.isPending}>
            Aktar
          </button>
        </div>
      </section>
    </div>
  );
}
```

- [ ] **Step 5: Testlerin geçtiğini gör**

Run: `cd frontend && npx vitest run src/features/personnel/ImportModal.test.tsx`
Expected: 2 test PASS

- [ ] **Step 6: Sayfaya bağla**

`PersonnelPage.tsx`: `const [importOpen, setImportOpen] = useState(false);` ekle; araç çubuğuna düğme:

```tsx
<button className="btn btn-secondary" onClick={() => setImportOpen(true)}>
  <i aria-hidden="true" className="fa-solid fa-file-import" /> Excel'den İçe Aktar
</button>
```

ve render sonuna `<ImportModal open={importOpen} onClose={() => setImportOpen(false)} />`.

- [ ] **Step 7: Tüm frontend kontrollerini koştur**

Run: `cd frontend && npx vitest run && npx tsc -b && npx oxlint`
Expected: hepsi temiz.

- [ ] **Step 8: Tarayıcıda doğrula**

hr-admin ile giriş → Personel → "Excel'den İçe Aktar" → şablonu indir → doldur → yükle → rapor görünür → Aktar → liste büyür.

- [ ] **Step 9: Commit**

```bash
git add frontend
git commit -m "feat(personel): Excel içe aktarma arayüzü"
```

---

## Self-Review notları

- **Kapsam:** Spec'teki tüm gereksinimler görevlere dağıtıldı (şablon → T1, ayrıştırma → T2, doğrulama → T3, önizleme → T4, aktarım → T5, arayüz → T6).
- **Tip tutarlılığı:** `ImportRow`, `ImportRowIssueDto`, `ImportPreviewDto`, `ImportResultDto` T2'de tanımlanıp T3-T6'da aynı adlarla kullanılıyor. `EmployeeImportValidator.Normalize` T3'te tanımlanıp T4'te (`ImportLookups`) kullanılıyor.
- **Bilinen boşluk:** `stubApi` ve `renderPage` yardımcılarının mevcut imzaları T6 Step 1'de varsayıldı; uygulayan kişi mevcut test dosyalarındaki kullanımı esas almalı.
