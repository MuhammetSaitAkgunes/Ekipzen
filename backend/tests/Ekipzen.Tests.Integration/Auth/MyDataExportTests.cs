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
    public async Task Export_IncludesOwnGuidanceRecords_WithoutLeakingThemToAnotherUser()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var bookmark = await admin.PutAsJsonAsync("/api/guidance/employee-entry/bookmark",
            new { stepKey = "tab-is" });
        bookmark.StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.PostAsJsonAsync(
            "/api/guidance/employee-entry/checks/tab-is/attempts",
            new { selectedChoice = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync(
            "/api/guidance/recruitment/checks/candidate-create/attempts",
            new { selectedChoice = 2 })).StatusCode.Should().Be(HttpStatusCode.OK);

        var departments = await admin.GetFromJsonAsync<List<Ekipzen.Application.Features.Departments.DepartmentDto>>(
            "/api/departments");
        var created = await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Dışa", lastName = "Aktarım", title = "Uzman",
            departmentId = departments![0].Id, hireDate = "2026-09-24",
            contractType = "indefinite", profile = new { employmentType = "Tam Zamanlı" },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var employee = (await created.Content.ReadFromJsonAsync<Ekipzen.Application.Features.Employees.EmployeeDetailDto>())!;

        var candidateResponse = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Dışa Aktarım Adayı " + Guid.NewGuid().ToString("N"),
            appliedRole = "Uzman",
        });
        candidateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var candidate = (await candidateResponse.Content.ReadFromJsonAsync<
            Ekipzen.Application.Features.Recruitment.CandidateDetailDto>())!;
        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status",
            new { status = "Mülakat" })).StatusCode.Should().Be(HttpStatusCode.OK);

        using var adminExport = JsonDocument.Parse(await admin.GetStringAsync("/api/me/data-export"));
        var adminRoot = adminExport.RootElement;
        adminRoot.GetProperty("guidanceBookmarks").EnumerateArray()
            .Should().ContainSingle(item =>
                item.GetProperty("processKey").GetString() == "personnel-entry" &&
                item.GetProperty("stepKey").GetString() == "tab-is");
        adminRoot.GetProperty("employeeCreationEvidences").EnumerateArray()
            .Should().ContainSingle(item => item.GetProperty("employeeId").GetInt32() == employee.Id);

        adminRoot.GetProperty("candidateReviewEvidences").EnumerateArray()
            .Should().ContainSingle(item =>
                item.GetProperty("candidateId").GetInt32() == candidate.Id &&
                item.GetProperty("decisionStatus").GetString() == "Interview");
        adminRoot.GetProperty("guidanceDecisionProgresses").EnumerateArray()
            .Should().ContainSingle(item =>
                item.GetProperty("stepKey").GetString() == "tab-is" &&
                item.GetProperty("contentVersion").GetInt32() == 1 &&
                item.GetProperty("lastCorrect").GetBoolean());
        adminRoot.GetProperty("guidanceDecisionProgresses").EnumerateArray()
            .Should().ContainSingle(item =>
                item.GetProperty("processKey").GetString() == "recruitment" &&
                item.GetProperty("stepKey").GetString() == "candidate-create" &&
                item.GetProperty("lastCorrect").GetBoolean());

        var otherUser = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        using var otherExport = JsonDocument.Parse(await otherUser.GetStringAsync("/api/me/data-export"));
        otherExport.RootElement.GetProperty("guidanceBookmarks").GetArrayLength().Should().Be(0);
        otherExport.RootElement.GetProperty("employeeCreationEvidences").GetArrayLength().Should().Be(0);
        otherExport.RootElement.GetProperty("candidateReviewEvidences").GetArrayLength().Should().Be(0);
        otherExport.RootElement.GetProperty("guidanceDecisionProgresses").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Export_ForUserWithoutEmployeeLink_DoesNotCrash()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local"); // hr-admin, EmployeeId'siz seed'lenir
        var response = await admin.GetAsync("/api/me/data-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("employee").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
