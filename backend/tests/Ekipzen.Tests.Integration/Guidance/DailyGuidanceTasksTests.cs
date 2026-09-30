using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Models;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Compliance;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Offboarding;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Leaves;
using Ekipzen.Application.Features.Recruitment;
using FluentAssertions;

namespace Ekipzen.Tests.Integration.Guidance;

[Collection(ApiCollection.Name)]
public sealed class DailyGuidanceTasksTests(EkipzenApiFactory factory)
{
    private const string Url = "/api/guidance/daily-tasks";

    [Fact]
    public async Task ComplianceTask_FollowsSourceStateAndKeepsUnknownDeadlineExplicit()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var employees = await GetAsync<PagedResult<EmployeeListItemDto>>(admin, "/api/employees?search=Ahmet");
        var created = await admin.PostAsJsonAsync("/api/compliance/documents", new
        {
            employeeId = employees.Items.Single().Id,
            documentName = "Rehber kontrolü " + Guid.NewGuid().ToString("N"),
            status = "Eksik",
            level = "medium",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = (await created.Content.ReadFromJsonAsync<ComplianceDocumentDto>())!;
        var key = $"compliance-document:{document.Id}";

        var first = await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url);
        first.Should().ContainSingle(task => task.Key == key)
            .Which.Should().Match<DailyGuidanceTaskDto>(task =>
                task.DueDate == null &&
                task.DateNote.Contains("Son tarih kayıtlı değil") &&
                task.TargetPath == $"/risk/compliance?documentId={document.Id}");
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().ContainSingle(task => task.Key == key, "yeniden değerlendirme görev çoğaltmaz");
        (await GetAsync<List<DailyGuidanceTaskDto>>(manager, Url))
            .Should().NotContain(task => task.Key == key, "yönetici belge durumunu değiştiremez");

        (await admin.PatchAsJsonAsync($"/api/compliance/documents/{document.Id}/status",
            new { status = "İncelemede" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url)).Should().NotContain(task => task.Key == key);

        (await admin.PatchAsJsonAsync($"/api/compliance/documents/{document.Id}/status",
            new { status = "Eksik" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url)).Should().ContainSingle(task => task.Key == key);

        (await admin.PatchAsJsonAsync($"/api/compliance/documents/{document.Id}/status",
            new { status = "Tamamlandı" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url)).Should().NotContain(task => task.Key == key);
    }

    [Fact]
    public async Task LeaveTask_DisappearsAfterDecisionAndIsRestrictedToManagement()
    {
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var types = await GetAsync<List<LeaveTypeDto>>(employee, "/api/leaves/types");
        var type = types.Single(item => item.Code == "Mazeret");
        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(900);
        while (start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            start = start.AddDays(1);
        var created = await employee.PostAsJsonAsync("/api/leaves", new
        {
            leaveTypeId = type.Id,
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = start.ToString("yyyy-MM-dd"),
            description = "Rehber görev testi",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var leave = (await created.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
        var key = $"leave-approval:{leave.Id}";

        (await employee.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var tasks = await GetAsync<List<DailyGuidanceTaskDto>>(manager, Url);
        tasks.Should().ContainSingle(task => task.Key == key)
            .Which.Should().Match<DailyGuidanceTaskDto>(task =>
                task.DueDate == null &&
                task.TargetPath == $"/inbox?tab=approvals&requestId={leave.Id}");

        (await manager.PostAsJsonAsync($"/api/leaves/{leave.Id}/approve", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(manager, Url)).Should().NotContain(task => task.Key == key);
    }

    [Fact]
    public async Task NewCandidateTask_FollowsPipelineStateAndStaysHrAdminOnly()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var created = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Rehber Adayı " + Guid.NewGuid().ToString("N"),
            appliedRole = "İK Uzmanı",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var candidate = (await created.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        var key = $"candidate-review:{candidate.Id}";

        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().ContainSingle(task => task.Key == key)
            .Which.Should().Match<DailyGuidanceTaskDto>(task =>
                task.Kind == "candidate-review" &&
                task.DueDate == null &&
                task.TargetPath == $"/recruitment?candidateId={candidate.Id}");
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().ContainSingle(task => task.Key == key, "aynı kaynak ikinci görev oluşturmaz");
        (await GetAsync<List<DailyGuidanceTaskDto>>(manager, Url))
            .Should().NotContain(task => task.Key == key, "yönetici aday aşamasını değiştiremez");

        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status",
            new { status = "Mülakat", expectedVersion = candidate.Version, reason = "İlk inceleme tamamlandı." })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url)).Should().NotContain(task => task.Key == key);

        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status",
            new { status = "Yeni", expectedVersion = candidate.Version + 1, reason = "Başvuru yeniden incelenecek." })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url)).Should().ContainSingle(task => task.Key == key);

        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status",
            new { status = "Red", expectedVersion = candidate.Version + 2, reason = "Başvuru uygun bulunmadı." })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url)).Should().NotContain(task => task.Key == key);
    }

    [Fact]
    public async Task CompensationTask_RequiresCurrentPackageAndDisappearsAfterSavingIt()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var start = today.AddDays(-1);
        var departments = await GetAsync<List<Ekipzen.Application.Features.Departments.DepartmentDto>>(
            admin, "/api/departments");
        var created = await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ücret", lastName = "Takip " + Guid.NewGuid().ToString("N"),
            title = "Uzman", departmentId = departments[0].Id,
            hireDate = start, contractType = "indefinite",
            profile = new { employmentType = "Tam Zamanlı" },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var employee = (await created.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
        var key = $"employee-compensation:{employee.Id}";

        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().ContainSingle(task => task.Key == key)
            .Which.Should().Match<DailyGuidanceTaskDto>(task =>
                task.Kind == "employee-compensation" &&
                task.DueDate == null &&
                task.DateNote.Contains("kayıtlı son tarih yok") &&
                task.TargetPath == $"/personnel/{employee.Id}?edit=1&guide=1&tab=tab-mali");
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().ContainSingle(task => task.Key == key, "aynı kaynak ikinci görev oluşturmaz");
        (await GetAsync<List<DailyGuidanceTaskDto>>(manager, Url))
            .Should().NotContain(task => task.Key == key);

        var future = await admin.PostAsJsonAsync(
            $"/api/employees/{employee.Id}/compensation-packages",
            new {
                effectiveFrom = today.AddDays(7), monthlyGrossSalary = 50000,
                regularPremium = 0, roadAllowance = 0, mealAllowance = 0,
                otherRegularBenefits = 0, sourceReference = "İleri tarihli ücret kararı",
            });
        future.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().ContainSingle(task => task.Key == key,
                "gelecekteki paket bugün yürürlükte değildir");

        var current = await admin.PostAsJsonAsync(
            $"/api/employees/{employee.Id}/compensation-packages",
            new {
                effectiveFrom = start, monthlyGrossSalary = 48000,
                regularPremium = 0, roadAllowance = 0, mealAllowance = 0,
                otherRegularBenefits = 0, sourceReference = "Doğrulanmış ücret kararı",
            });
        current.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().NotContain(task => task.Key == key);
    }

    [Fact]
    public async Task DraftOffboardingTask_OpensCaseForHrAdminAndDisappearsAfterCompletion()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var departments = await GetAsync<List<Ekipzen.Application.Features.Departments.DepartmentDto>>(
            admin, "/api/departments");
        var created = await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ayrılış", lastName = "Taslak " + Guid.NewGuid().ToString("N"),
            title = "Uzman", departmentId = departments[0].Id,
            hireDate = today.AddDays(-1), contractType = "indefinite",
            profile = new { employmentType = "Tam Zamanlı" },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var employee = (await created.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
        var draftResponse = await admin.PutAsJsonAsync(
            $"/api/employees/{employee.Id}/offboarding-draft", new
            {
                terminationDate = today,
                exitCode = "TEST-03",
                exitReason = "Kaynak durum testi",
                terminationReason = "employee-resignation",
                noticeHandling = "not-applicable",
                unusedLeaveDays = 0m,
                unusedLeavePayment = 0m,
                severancePayment = 0m,
                noticePayment = 0m,
                settlementReference = "Test uzman hesap belgesi",
            });
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = (await draftResponse.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
        var key = $"offboarding-case:{draft.Id}";

        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().ContainSingle(task => task.Key == key)
            .Which.Should().Match<DailyGuidanceTaskDto>(task =>
                task.Kind == "offboarding-case" &&
                task.RecordId == draft.Id &&
                task.DueDate == null &&
                task.DateNote.Contains("karar son tarihi değildir") &&
                task.TargetPath == $"/personnel/{employee.Id}?offboarding=1");
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().ContainSingle(task => task.Key == key, "aynı taslak ikinci görev oluşturmaz");
        (await GetAsync<List<DailyGuidanceTaskDto>>(manager, Url))
            .Should().NotContain(task => task.Key == key, "yönetici ayrılış vakasını tamamlayamaz");

        var completed = await admin.PostAsJsonAsync(
            $"/api/employees/{employee.Id}/offboarding/{draft.Id}/complete",
            new { expectedVersion = draft.Version });
        completed.StatusCode.Should().Be(HttpStatusCode.OK, await completed.Content.ReadAsStringAsync());
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, Url))
            .Should().NotContain(task => task.Key == key);
    }

    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var anonymous = factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = "demo123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
