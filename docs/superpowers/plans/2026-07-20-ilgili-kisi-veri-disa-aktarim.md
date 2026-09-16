# İlgili Kişi Verisi Dışa Aktarımı (KVKK Taşınabilirlik) Implementasyon Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline). Steps use checkbox (`- [ ]`) syntax.

**Goal:** Oturum açmış herhangi bir kullanıcının (rolden bağımsız) kendi kişisel verisini tek tıkla, makine-okunur (JSON) formatta indirebilmesi. KVKK "ilgili kişi hakları — erişim/taşınabilirlik" ilkesinin self-servis karşılığı; kiracı purge (silme) mekanizmasının doğal tamamlayıcısı (T5.4 Bölüm 7.2).

**Architecture:** Yeni `GetMyDataExportQuery` (parametresiz, `ICurrentUser`'dan kimlik alır) — hesap bilgisi + (varsa) bağlı `Employee`/`EmployeeProfile` + kendi izin talepleri/bakiyeleri + kendi puantaj kayıtları + kendi uyum belgeleri (metadata) + kendi bordro pusulası listesini (metadata) tek JSON paketine toplar. Mevcut `apiDownload`/dosya-indirme deseniyle (`(byte[] Content, string FileName)` → `File(...)`) aynı yolu izler. Tenant izolasyonu mevcut EF global filtresinden otomatik gelir; ek olarak sorgular `EmployeeId`'ye göre daraltılır (başka çalışanın verisi asla dahil edilmez).

**Tech Stack:** .NET 9, MediatR, EF Core, System.Text.Json; React 18 + TS.

## Global Constraints

- Yalnız **kendi** verisi döner; başka kullanıcının verisi asla sızmaz (test ile kanıt).
- `EmployeeId`'si olmayan kullanıcı (ör. bağlı personeli olmayan hr-admin) hata almadan yalnız hesap bilgisiyle export alır.
- Mevcut tüm testler yeşil kalır; commit sonu `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Dal: `main`'den `feature/data-export`.

---

## Dosya Yapısı

- **Create:** `backend/src/Ekipzen.Application/Features/Auth/DataExport/GetMyDataExportQuery.cs` — DTO'lar + query + handler.
- **Modify:** `backend/src/Ekipzen.API/Controllers/MeController.cs` — `GET /api/me/data-export`.
- **Create:** `backend/tests/Ekipzen.Tests.Integration/Auth/MyDataExportTests.cs`
- **Modify:** `frontend/src/layout/AppShell.tsx` — "Verilerimi indir" ikon butonu.
- **Modify:** `docs/kvkk-veri-izolasyonu.md`, `docs/gelistirme-gunlugu.md`.

---

## Task 1: GetMyDataExportQuery (backend çekirdek)

**Interfaces:**
- Produces: `GetMyDataExportQuery : IRequest<(byte[] Content, string FileName)>`; `MyDataExportDto(AccountInfo Account, EmployeeInfo? Employee, IReadOnlyList<LeaveRequestExportItem> LeaveRequests, IReadOnlyList<LeaveBalanceExportItem> LeaveBalances, IReadOnlyList<AttendanceExportItem> AttendanceRecords, IReadOnlyList<ComplianceDocumentExportItem> ComplianceDocuments, IReadOnlyList<PayslipExportItem> Payslips, DateTime ExportedAtUtc)`.

- [ ] **Step 1: Failing entegrasyon testi**

```csharp
// backend/tests/Ekipzen.Tests.Integration/Auth/MyDataExportTests.cs
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ekipzen.Tests.Integration.Auth;

/// <summary>
/// Self-servis veri dışa aktarımı (KVKK taşınabilirlik): oturum açmış kullanıcı kendi
/// verisini JSON olarak indirir. Yalnız kendi verisi; başka çalışanınki sızmaz.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MyDataExportTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await response.Content.ReadFromJsonAsync<Ekipzen.Application.Features.Auth.AuthResponse>())!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    [Fact]
    public async Task Export_ReturnsOwnData_NotOthers()
    {
        var ahmet = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var response = await ahmet.GetAsync("/api/me/data-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentDisposition!.FileName.Should().NotBeNullOrEmpty();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("account").GetProperty("email").GetString().Should().Be("ahmet.yilmaz@hrmaster.local");
        root.GetProperty("employee").ValueKind.Should().NotBe(JsonValueKind.Null);
        // Kendi dışında personel adı içermemeli.
        root.ToString().Should().NotContain("Ece Arslan");
    }

    [Fact]
    public async Task Export_ForUserWithoutEmployeeLink_DoesNotCrash()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local"); // hr-admin, EmployeeId genelde null
        var response = await admin.GetAsync("/api/me/data-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("employee").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
```

Not: `ik@hrmaster.local` seed'de gerçekten EmployeeId'siz mi kontrol et (`AppDbContextInitializer`); değilse başka bir bağlantısız hesap kullan ya da testi buna göre uyarla — asıl amaç "employee: null, crash yok" senaryosunu kanıtlamak.

- [ ] **Step 2:** Testi çalıştır → FAIL (404, uç yok).

- [ ] **Step 3: Query + handler**

```csharp
// backend/src/Ekipzen.Application/Features/Auth/DataExport/GetMyDataExportQuery.cs
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ekipzen.Application.Features.Auth.DataExport;

public sealed record AccountInfo(string UserId, string Name, string Email, IReadOnlyList<string> Roles);
public sealed record EmployeeInfo(
    string FullName, string Title, string DepartmentName, DateOnly HireDate, string Status,
    DateOnly? BirthDate, string? Gender, string? MaritalStatus, string? MobilePhone,
    string? PersonalEmail, string? HomeAddress, string? Iban, string? BankName);
public sealed record LeaveRequestExportItem(string LeaveTypeName, DateOnly StartDate, DateOnly EndDate, int Days, string Status);
public sealed record LeaveBalanceExportItem(int Year, int EntitledDays, int CarriedOverDays);
public sealed record AttendanceExportItem(DateOnly WorkDate, TimeOnly? CheckIn, TimeOnly? CheckOut, int WorkedMinutes, string Status);
public sealed record ComplianceDocumentExportItem(string DocumentName, string Status, DateOnly? DueDate);
public sealed record PayslipExportItem(string PeriodName, string Status);

public sealed record MyDataExportDto(
    AccountInfo Account,
    EmployeeInfo? Employee,
    IReadOnlyList<LeaveRequestExportItem> LeaveRequests,
    IReadOnlyList<LeaveBalanceExportItem> LeaveBalances,
    IReadOnlyList<AttendanceExportItem> AttendanceRecords,
    IReadOnlyList<ComplianceDocumentExportItem> ComplianceDocuments,
    IReadOnlyList<PayslipExportItem> Payslips,
    DateTime ExportedAtUtc);

/// <summary>
/// KVKK taşınabilirlik: oturum açmış kullanıcının kendi verisini JSON paketi olarak
/// döndürür. Yalnız EmployeeId'sine bağlı kayıtlar dahil edilir — başka kullanıcının
/// verisi asla sızmaz. Tenant izolasyonu EF global filtresinden otomatik gelir.
/// </summary>
public sealed record GetMyDataExportQuery : IRequest<(byte[] Content, string FileName)>;

public sealed class GetMyDataExportQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyDataExportQuery, (byte[] Content, string FileName)>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<(byte[] Content, string FileName)> Handle(
        GetMyDataExportQuery request, CancellationToken cancellationToken)
    {
        var account = new AccountInfo(
            currentUser.UserId ?? "", currentUser.UserName ?? "", currentUser.UserName ?? "", currentUser.Roles);

        EmployeeInfo? employeeInfo = null;
        IReadOnlyList<LeaveRequestExportItem> leaveRequests = Array.Empty<LeaveRequestExportItem>();
        IReadOnlyList<LeaveBalanceExportItem> leaveBalances = Array.Empty<LeaveBalanceExportItem>();
        IReadOnlyList<AttendanceExportItem> attendance = Array.Empty<AttendanceExportItem>();
        IReadOnlyList<ComplianceDocumentExportItem> compliance = Array.Empty<ComplianceDocumentExportItem>();
        IReadOnlyList<PayslipExportItem> payslips = Array.Empty<PayslipExportItem>();

        if (currentUser.EmployeeId is { } employeeId)
        {
            var employee = await context.Employees
                .Include(e => e.Department)
                .Include(e => e.Profile)
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

            if (employee is not null)
            {
                var p = employee.Profile;
                employeeInfo = new EmployeeInfo(
                    employee.FullName, employee.Title, employee.Department?.Name ?? "",
                    employee.HireDate, employee.Status.ToString(),
                    p?.BirthDate, p?.Gender, p?.MaritalStatus, p?.MobilePhone,
                    p?.PersonalEmail, p?.HomeAddress, p?.Iban, p?.BankName);
            }

            leaveRequests = await context.LeaveRequests
                .Where(r => r.EmployeeId == employeeId)
                .Select(r => new LeaveRequestExportItem(
                    r.LeaveType!.Name, r.StartDate, r.EndDate, r.Days, r.Status.ToString()))
                .ToListAsync(cancellationToken);

            leaveBalances = await context.LeaveBalances
                .Where(b => b.EmployeeId == employeeId)
                .Select(b => new LeaveBalanceExportItem(b.Year, b.EntitledDays, b.CarriedOverDays))
                .ToListAsync(cancellationToken);

            attendance = await context.AttendanceRecords
                .Where(a => a.EmployeeId == employeeId)
                .Select(a => new AttendanceExportItem(
                    a.WorkDate, a.CheckIn, a.CheckOut, a.WorkedMinutes, a.Status.ToString()))
                .ToListAsync(cancellationToken);

            compliance = await context.ComplianceDocuments
                .Where(d => d.EmployeeId == employeeId)
                .Select(d => new ComplianceDocumentExportItem(d.DocumentName, d.Status.ToString(), d.DueDate))
                .ToListAsync(cancellationToken);

            payslips = await context.PayrollEmployees
                .Where(pe => pe.EmployeeId == employeeId)
                .Select(pe => new PayslipExportItem(pe.Period!.Name, pe.Status.ToString()))
                .ToListAsync(cancellationToken);
        }

        var dto = new MyDataExportDto(
            account, employeeInfo, leaveRequests, leaveBalances, attendance, compliance, payslips,
            DateTime.UtcNow);

        var json = JsonSerializer.SerializeToUtf8Bytes(dto, SerializerOptions);
        var fileName = $"ekipzen-verilerim-{DateTime.UtcNow:yyyyMMdd}.json";
        return (json, fileName);
    }
}
```

Not: `PayrollEmployee.Period`/`Status` alan adlarını `PayrollDtos.cs`/entity'den doğrula; farklıysa uyarla. `LeaveRequest.LeaveType`, `ComplianceDocument.Status` (enum) — mevcut kullanımlarla tutarlı `.ToString()`.

- [ ] **Step 4: Controller ucu**

`MeController.cs`'e ekle (`using Ekipzen.Application.Features.Auth.DataExport;`):

```csharp
[HttpGet("data-export")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> DataExport(CancellationToken cancellationToken)
{
    var (content, fileName) = await sender.Send(new GetMyDataExportQuery(), cancellationToken);
    return File(content, "application/json", fileName);
}
```

- [ ] **Step 5:** Testi çalıştır → PASS. **Step 6:** Tüm backend paketi PASS. **Step 7:** Commit.

---

## Task 2: Frontend — "Verilerimi indir" butonu

**Files:** Modify `frontend/src/layout/AppShell.tsx`.

- [ ] **Step 1:** `user-profile` bloğuna (logout butonundan önce), `apiDownload` deseniyle (bkz. `MyPayslipsView.tsx`):

```tsx
<button className="btn-icon-sm" onClick={handleDataExport} title="Verilerimi indir" aria-label="Verilerimi indir (KVKK)">
  <i aria-hidden="true" className="fa-solid fa-file-export" />
</button>
```

`handleDataExport` fonksiyonu (component içinde, `handleLogout`'a yakın):
```tsx
const handleDataExport = async () => {
  try {
    const { blob, fileName } = await apiDownload("/me/data-export");
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = fileName ?? "ekipzen-verilerim.json";
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(link.href);
  } catch {
    // Sessiz — kritik olmayan yardımcı işlem; toast altyapısı burada mevcut değilse eklenmez.
  }
};
```
`import { apiDownload } from "../api/client";` (mevcut import'lara ekle/birleştir).

- [ ] **Step 2:** `cd frontend && npx vitest run && npm run build` → yeşil/hatasız.
- [ ] **Step 3:** Commit.

---

## Task 3: Dokümantasyon + kapanış

- [ ] KVKK dokümanı Bölüm 7.2'yi "mevcut" olarak güncelle; Bölüm 6 tablosuna ekle.
- [ ] `docs/gelistirme-gunlugu.md` güncelle.
- [ ] Tam doğrulama + commit + kullanıcı onayıyla merge/push.

## Self-Review Notları

- Yalnız kendi veri: tüm sorgular `EmployeeId == currentUser.EmployeeId`; tenant izolasyonu ayrıca EF global filtresinden gelir (çifte güvence). ✓
- EmployeeId'siz kullanıcı (hr-admin) crash almaz — `employee: null` + boş listeler. ✓
- Kapsam dışı: fiziksel dosyaların (evrak PDF'leri) zip'e dahil edilmesi — yalnız metadata; ayrı bir sertleştirme adımı olarak not düşülür.
