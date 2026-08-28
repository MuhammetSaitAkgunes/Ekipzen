using FluentAssertions;
using IKPro.Application.Features.Attendance;
using IKPro.Application.Features.Auth;
using IKPro.Application.Features.Departments;
using IKPro.Application.Features.Employees;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IKPro.Tests.Integration.Attendance;

/// <summary>
/// Faz 5 uçtan uca: manuel giriş → worked/overtime/status hesaplama, gün başına tek
/// kayıt kuralı, satır düzenleme, canlı yoklama panosu (kayıtsız = absent), aylık
/// puantaj toplamları ve SQL view özeti. Testler ayrık tarihler kullanır:
/// Ahmet → Tem 2026 (20–27), Ece → Ağu 2026 (timesheet/özet).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AttendanceTests(IKProApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task CreateEntry_ComputesWorkedOvertimeAndStatus()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ahmetId = await FindEmployeeIdAsync(admin, "Ahmet");

        // 09:00–20:00, 60 dk mola → 600 dk net, 120 dk fazla mesai, tip türetilir: Mesai.
        var overtime = await CreateEntryAsync(admin, ahmetId, "2026-07-21", "09:00", "20:00");
        overtime.WorkedMinutes.Should().Be(600);
        overtime.OvertimeMinutes.Should().Be(120);
        overtime.Type.Should().Be("Mesai");
        overtime.Status.Should().Be("overtime");

        // 09:15 giriş → geç.
        var late = await CreateEntryAsync(admin, ahmetId, "2026-07-22", "09:15", "18:15");
        late.WorkedMinutes.Should().Be(480);
        late.Status.Should().Be("late");
        late.Type.Should().Be("Tam");

        // 08:30 giriş → erken (satır durumu ok).
        var early = await CreateEntryAsync(admin, ahmetId, "2026-07-23", "08:30", "17:30");
        early.Status.Should().Be("ok");
    }

    [Fact]
    public async Task CreateEntry_SameDayTwice_Returns409()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ahmetId = await FindEmployeeIdAsync(admin, "Ahmet");

        (await PostEntryAsync(admin, ahmetId, "2026-07-24", "09:00", "18:00"))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostEntryAsync(admin, ahmetId, "2026-07-24", "10:00", "18:00"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateEntry_Recomputes_AndGuardsDayUniqueness()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ahmetId = await FindEmployeeIdAsync(admin, "Ahmet");

        var row = await CreateEntryAsync(admin, ahmetId, "2026-07-27", "09:00", "18:00");
        row.OvertimeMinutes.Should().Be(0);

        var updateResponse = await admin.PutAsJsonAsync($"/api/attendance/{row.Id}", new
        {
            workDate = "2026-07-27",
            checkIn = "09:00",
            checkOut = "21:00",
            breakMinutes = 60,
            note = "Sürüm gecesi",
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<TimesheetRowDto>())!;
        updated.WorkedMinutes.Should().Be(660);
        updated.OvertimeMinutes.Should().Be(180);
        updated.Status.Should().Be("overtime");

        // Dolu bir güne taşıma 409: aynı test kendi çakışma gününü oluşturur.
        await CreateEntryAsync(admin, ahmetId, "2026-07-29", "09:00", "18:00");
        (await admin.PutAsJsonAsync($"/api/attendance/{row.Id}", new
        {
            workDate = "2026-07-29",
            checkIn = "09:00",
            checkOut = "18:00",
            breakMinutes = 60,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LiveBoard_ReflectsEntries_AndDefaultsToAbsent()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ahmetId = await FindEmployeeIdAsync(admin, "Ahmet");

        await PostEntryAsync(admin, ahmetId, "2026-07-20", "08:45", "18:00");

        var board = await GetAsync<List<LiveBoardCardDto>>(admin, "/api/attendance/live?date=2026-07-20");

        var ahmet = board.Single(c => c.Name == "Ahmet Yılmaz");
        ahmet.Status.Should().Be("ontime");
        ahmet.CheckIn.Should().Be(new TimeOnly(8, 45));

        board.Single(c => c.Name == "Ayşe Demir").Status
            .Should().Be("absent", "kaydı olmayan aktif personel devamsız görünür");
        board.Should().NotContain(c => c.Name == "Selin Koç", "pasif personel panoda yer almaz");
    }

    [Fact]
    public async Task Timesheet_And_MonthlySummary_AggregateCorrectly()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var eceId = await FindEmployeeIdAsync(admin, "Ece");

        await PostEntryAsync(admin, eceId, "2026-08-03", "09:00", "20:00"); // 600 dk, 120 fazla mesai
        await PostEntryAsync(admin, eceId, "2026-08-04", "09:15", "18:15"); // 480 dk, geç
        await PostEntryAsync(admin, eceId, "2026-08-05", null, null, "Rapor"); // devamsız/rapor

        var timesheet = await GetAsync<TimesheetDto>(
            admin, $"/api/attendance?employeeId={eceId}&year=2026&month=8");
        timesheet.Rows.Should().HaveCount(3);
        timesheet.TotalWorkedMinutes.Should().Be(1080);
        timesheet.TotalOvertimeMinutes.Should().Be(120);
        timesheet.Rows.Single(r => r.WorkDate == new DateOnly(2026, 8, 5)).Status.Should().Be("absent");

        var summary = await GetAsync<List<AttendanceSummaryDto>>(
            admin, "/api/attendance/summary?year=2026&month=8");
        var ece = summary.Single(s => s.EmployeeName == "Ece Arslan");
        ece.TotalDays.Should().Be(3);
        ece.PresentDays.Should().Be(2);
        ece.AbsentDays.Should().Be(1);
        ece.LateDays.Should().Be(1);
        ece.TotalWorkedMinutes.Should().Be(1080);
        ece.TotalOvertimeMinutes.Should().Be(120, "fazla mesai bordroya beslenecek değer");
    }

    [Fact]
    public async Task Scope_ManagerLimitedToTeam_EmployeeBlocked()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ahmetId = await FindEmployeeIdAsync(admin, "Ahmet");
        var ayseId = await FindEmployeeIdAsync(admin, "Ayşe");

        // Manager kendi ekibi için giriş yapabilir…
        (await PostEntryAsync(manager, ahmetId, "2026-07-28", "09:00", "18:00"))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // …ekip dışı için 403.
        (await PostEntryAsync(manager, ayseId, "2026-07-28", "09:00", "18:00"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await manager.GetAsync($"/api/attendance?employeeId={ayseId}&year=2026&month=7"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Çalışan rolü puantaj modülüne giremez (routes.js: hr-admin+manager).
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (await employee.GetAsync("/api/attendance/live")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Entries_MustStayWithinEmploymentDates()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var createEmployeeResponse = await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Puantaj",
            lastName = $"Sinir{suffix}",
            title = "Test Uzmani",
            departmentId = departments[0].Id,
            hireDate = "2026-08-01",
            status = "active",
        });
        createEmployeeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var employee = (await createEmployeeResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;

        var historical = await CreateEntryAsync(
            admin, employee.Id, "2026-08-10", "09:00", "18:00");

        (await admin.PatchAsJsonAsync($"/api/employees/{employee.Id}/status", new
        {
            status = "passive",
            terminationDate = "2026-08-15",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await PostEntryAsync(admin, employee.Id, "2026-08-14", "09:00", "18:00"))
            .StatusCode.Should().Be(HttpStatusCode.Created,
                "ayrilis tarihi dahil gecmis puantaj duzeltmeleri korunmali");

        (await PostEntryAsync(admin, employee.Id, "2026-08-16", "09:00", "18:00"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict,
                "ayrilis sonrasi puantaj olusturulamamali");

        (await PostEntryAsync(admin, employee.Id, "2026-07-31", "09:00", "18:00"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict,
                "ise giris oncesi puantaj olusturulamamali");

        (await admin.PutAsJsonAsync($"/api/attendance/{historical.Id}", new
        {
            workDate = "2026-08-16",
            checkIn = "09:00",
            checkOut = "18:00",
            breakMinutes = 60,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "mevcut bir satir istihdam araligi disina tasinamamali");
    }

    // --- yardımcılar ---

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

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<int> FindEmployeeIdAsync(HttpClient adminClient, string search)
    {
        var response = await adminClient.GetAsync($"/api/employees?search={Uri.EscapeDataString(search)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<
            IKPro.Application.Common.Models.PagedResult<
                IKPro.Application.Features.Employees.EmployeeListItemDto>>())!;
        return page.Items.First().Id;
    }

    private static Task<HttpResponseMessage> PostEntryAsync(
        HttpClient client, int employeeId, string date, string? checkIn, string? checkOut, string? type = null)
        => client.PostAsJsonAsync("/api/attendance", new
        {
            employeeId,
            model = new { workDate = date, checkIn, checkOut, breakMinutes = 60, type },
        });

    private static async Task<TimesheetRowDto> CreateEntryAsync(
        HttpClient client, int employeeId, string date, string checkIn, string checkOut)
    {
        var response = await PostEntryAsync(client, employeeId, date, checkIn, checkOut);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TimesheetRowDto>())!;
    }
}
