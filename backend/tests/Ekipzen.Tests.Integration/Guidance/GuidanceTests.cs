using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Models;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Guidance;
using FluentAssertions;

namespace Ekipzen.Tests.Integration.Guidance;

[Collection(ApiCollection.Name)]
public sealed class GuidanceTests(EkipzenApiFactory factory)
{
    private const string BookmarkUrl = "/api/guidance/employee-entry/bookmark";

    [Fact]
    public async Task Bookmark_IsDurableAndOnlyAvailableToHrAdmin()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var initial = await GetAsync<GuidanceBookmarkDto>(admin, BookmarkUrl);
        initial.StepKey.Should().Be("tab-kimlik");
        initial.ContentVersion.Should().Be(1);

        var saved = await admin.PutAsJsonAsync(BookmarkUrl, new { stepKey = "tab-is" });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<GuidanceBookmarkDto>(admin, BookmarkUrl)).StepKey.Should().Be("tab-is");

        var changed = await admin.PutAsJsonAsync(BookmarkUrl, new { stepKey = "tab-evrak" });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<GuidanceBookmarkDto>(await AuthedClientAsync("ik@hrmaster.local"), BookmarkUrl))
            .StepKey.Should().Be("tab-evrak", "konum farklı oturumda da korunur");

        (await admin.PutAsJsonAsync(BookmarkUrl, new { stepKey = "completed" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "istemci kendini tamamlandı sayamaz");
        (await GetAsync<GuidanceBookmarkDto>(admin, BookmarkUrl)).StepKey.Should().Be("tab-evrak");

        (await (await AuthedClientAsync("ece.arslan@hrmaster.local")).GetAsync(BookmarkUrl))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync(BookmarkUrl)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreationEvidence_OnlyAppearsAfterRealEmployeeCreate()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var seeded = await GetAsync<PagedResult<EmployeeListItemDto>>(admin, "/api/employees?search=Ahmet");
        var seededEvidence = await GetAsync<EmployeeCreationEvidenceDto>(admin,
            $"/api/guidance/employee-entry/employees/{seeded.Items.Single().Id}/evidence");
        seededEvidence.CreatedByCurrentUser.Should().BeFalse();

        var departments = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        var created = await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Rehber", lastName = "Kanıt", title = "Uzman",
            departmentId = departments[0].Id, hireDate = "2026-09-24",
            contractType = "indefinite", profile = new { employmentType = "Tam Zamanlı" },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var employee = (await created.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;

        var evidence = await GetAsync<EmployeeCreationEvidenceDto>(admin,
            $"/api/guidance/employee-entry/employees/{employee.Id}/evidence");
        evidence.CreatedByCurrentUser.Should().BeTrue();
        evidence.CreatedAtUtc.Should().NotBeNull();

        (await admin.GetAsync("/api/guidance/employee-entry/employees/2147483647/evidence"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await (await AuthedClientAsync("ece.arslan@hrmaster.local"))
            .GetAsync($"/api/guidance/employee-entry/employees/{employee.Id}/evidence"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Journey_CountsOnlyVerifiedEmployeeCreates_WithoutClaimingProcessCompletion()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var before = await GetAsync<PersonnelEntryJourneyDto>(admin, "/api/guidance/employee-entry/journey");
        (await admin.PutAsJsonAsync(BookmarkUrl, new { stepKey = "tab-mali" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var withBookmark = await GetAsync<PersonnelEntryJourneyDto>(admin, "/api/guidance/employee-entry/journey");
        withBookmark.BookmarkStepKey.Should().Be("tab-mali");
        withBookmark.VerifiedEmployeeRecords.Should().Be(before.VerifiedEmployeeRecords,
            "sekme seçmek gerçek işlem kanıtı değildir");

        var departments = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        var created = await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Yolculuk", lastName = "Kanıt", title = "Uzman",
            departmentId = departments[0].Id, hireDate = "2026-09-24",
            contractType = "indefinite", profile = new { employmentType = "Tam Zamanlı" },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await GetAsync<PersonnelEntryJourneyDto>(admin, "/api/guidance/employee-entry/journey");
        after.ProcessKey.Should().Be("personnel-entry");
        after.VerifiedEmployeeRecords.Should().Be(before.VerifiedEmployeeRecords + 1);
        after.LastVerifiedAtUtc.Should().NotBeNull();
        after.HasSavedPosition.Should().BeTrue();

        (await (await AuthedClientAsync("ece.arslan@hrmaster.local"))
            .GetAsync("/api/guidance/employee-entry/journey"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RecruitmentJourney_CountsRealFirstDecisionOnlyOnce()
    {
        const string url = "/api/guidance/recruitment/journey";
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var before = await GetAsync<RecruitmentJourneyDto>(admin, url);

        var created = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Yolculuk Adayı " + Guid.NewGuid().ToString("N"),
            appliedRole = "Uzman",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var candidate = (await created.Content.ReadFromJsonAsync<
            Ekipzen.Application.Features.Recruitment.CandidateDetailDto>())!;
        (await GetAsync<RecruitmentJourneyDto>(admin, url)).VerifiedFirstReviews
            .Should().Be(before.VerifiedFirstReviews, "aday kartını açmak veya oluşturmak ilk karar değildir");

        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status",
            new { status = "Yeni" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<RecruitmentJourneyDto>(admin, url)).VerifiedFirstReviews
            .Should().Be(before.VerifiedFirstReviews, "değişmeyen durum kanıt üretmez");

        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status",
            new { status = "Mülakat" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await GetAsync<RecruitmentJourneyDto>(admin, url);
        after.ProcessKey.Should().Be("recruitment");
        after.VerifiedFirstReviews.Should().Be(before.VerifiedFirstReviews + 1);
        after.LastVerifiedAtUtc.Should().NotBeNull();

        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status",
            new { status = "Yeni" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status",
            new { status = "Red" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<RecruitmentJourneyDto>(admin, url)).VerifiedFirstReviews
            .Should().Be(after.VerifiedFirstReviews, "aynı aday tekrar sayılamaz");

        (await manager.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DecisionChecks_SaveLastAttemptWithoutClaimingOperationEvidence()
    {
        const string checkUrl = "/api/guidance/employee-entry/checks/tab-is";
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var journeyBefore = await GetAsync<PersonnelEntryJourneyDto>(
            admin, "/api/guidance/employee-entry/journey");

        var initial = await GetAsync<PersonnelDecisionCheckDto>(admin, checkUrl);
        initial.ContentVersion.Should().Be(1);
        initial.Choices.Should().HaveCount(3);
        initial.LastSelectedChoice.Should().BeNull();
        initial.AttemptCount.Should().Be(0);

        (await admin.PostAsJsonAsync(checkUrl + "/attempts", new { selectedChoice = 3 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetAsync<PersonnelDecisionCheckDto>(admin, checkUrl)).AttemptCount.Should().Be(0);

        var wrongResponse = await admin.PostAsJsonAsync(checkUrl + "/attempts",
            new { selectedChoice = 0 });
        wrongResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var wrong = (await wrongResponse.Content.ReadFromJsonAsync<PersonnelDecisionCheckDto>())!;
        wrong.LastCorrect.Should().BeFalse();
        wrong.AttemptCount.Should().Be(1);
        wrong.Explanation.Should().NotBeNullOrWhiteSpace();

        var afterWrong = await GetAsync<PersonnelEntryJourneyDto>(
            admin, "/api/guidance/employee-entry/journey");
        afterWrong.AnsweredChecks.Should().Be(journeyBefore.AnsweredChecks + 1);
        afterWrong.CorrectChecks.Should().Be(journeyBefore.CorrectChecks);
        afterWrong.VerifiedEmployeeRecords.Should().Be(journeyBefore.VerifiedEmployeeRecords);

        var rightResponse = await admin.PostAsJsonAsync(checkUrl + "/attempts",
            new { selectedChoice = 1 });
        rightResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var right = (await rightResponse.Content.ReadFromJsonAsync<PersonnelDecisionCheckDto>())!;
        right.LastCorrect.Should().BeTrue();
        right.AttemptCount.Should().Be(2);
        (await GetAsync<PersonnelDecisionCheckDto>(
            await AuthedClientAsync("ik@hrmaster.local"), checkUrl))
            .LastSelectedChoice.Should().Be(1, "yanıt oturum değişiminde korunur");

        var afterRight = await GetAsync<PersonnelEntryJourneyDto>(
            admin, "/api/guidance/employee-entry/journey");
        afterRight.AnsweredChecks.Should().Be(journeyBefore.AnsweredChecks + 1);
        afterRight.CorrectChecks.Should().Be(journeyBefore.CorrectChecks + 1);
        afterRight.VerifiedEmployeeRecords.Should().Be(journeyBefore.VerifiedEmployeeRecords);

        (await manager.GetAsync(checkUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync(checkUrl)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecruitmentChecks_SavePracticeWithoutClaimingCandidateDecision()
    {
        const string createUrl = "/api/guidance/recruitment/checks/candidate-create";
        const string reviewUrl = "/api/guidance/recruitment/checks/first-review";
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var before = await GetAsync<RecruitmentJourneyDto>(
            admin, "/api/guidance/recruitment/journey");

        var initial = await GetAsync<RecruitmentDecisionCheckDto>(admin, createUrl);
        initial.ContentVersion.Should().Be(1);
        initial.Choices.Should().HaveCount(3);
        initial.Explanation.Should().BeNull();
        initial.AttemptCount.Should().Be(0);

        (await admin.GetAsync("/api/guidance/recruitment/checks/unknown"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync(createUrl + "/attempts", new { selectedChoice = 3 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var wrong = await admin.PostAsJsonAsync(createUrl + "/attempts",
            new { selectedChoice = 0 });
        wrong.StatusCode.Should().Be(HttpStatusCode.OK);
        (await wrong.Content.ReadFromJsonAsync<RecruitmentDecisionCheckDto>())!
            .LastCorrect.Should().BeFalse();

        var right = await admin.PostAsJsonAsync(createUrl + "/attempts",
            new { selectedChoice = 2 });
        right.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await GetAsync<RecruitmentDecisionCheckDto>(
            await AuthedClientAsync("ik@hrmaster.local"), createUrl);
        saved.LastCorrect.Should().BeTrue();
        saved.LastSelectedChoice.Should().Be(2);
        saved.AttemptCount.Should().Be(2);

        (await admin.PostAsJsonAsync(reviewUrl + "/attempts",
            new { selectedChoice = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await GetAsync<RecruitmentJourneyDto>(
            admin, "/api/guidance/recruitment/journey");
        after.ContentVersion.Should().Be(1);
        after.AnsweredChecks.Should().Be(before.AnsweredChecks + 2);
        after.CorrectChecks.Should().Be(before.CorrectChecks + 2);
        after.VerifiedFirstReviews.Should().Be(before.VerifiedFirstReviews);

        (await manager.GetAsync(createUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync(createUrl))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
