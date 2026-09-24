using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Models;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Compliance;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Leaves;
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
