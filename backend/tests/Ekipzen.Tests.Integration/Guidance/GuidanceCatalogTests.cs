using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Constants;
using Ekipzen.Infrastructure.Identity;
using Ekipzen.Infrastructure.Persistence;
using Ekipzen.Infrastructure.Persistence.Migrations.Platform;
using Ekipzen.Tests.Integration.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ekipzen.Tests.Integration.Guidance;

[Collection(ApiCollection.Name)]
public sealed class GuidanceCatalogTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    private const string Root = "/api/guidance/catalog";

    [Fact]
    public async Task HelpPreferences_ArePersonalRevisionCheckedAndSeparateFromLearningEvidence()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var owner = team.Clients[0]; var colleague = team.Clients[1];
        const string path = "/api/guidance/help-preferences";
        (await owner.GetFromJsonAsync<List<GuidanceHelpPreferenceDto>>(path))!.Should().BeEmpty();
        var command = new { mode = "compact", expectedRevision = 0 };
        var result = await owner.PutAsJsonAsync(path + "/daily", command);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<GuidanceHelpPreferenceDto>())!.Revision.Should().Be(1);
        (await owner.PutAsJsonAsync(path + "/daily", command)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetFromJsonAsync<List<GuidanceHelpPreferenceDto>>(path))!.Single().Revision.Should().Be(1);
        (await colleague.GetFromJsonAsync<List<GuidanceHelpPreferenceDto>>(path))!.Should().BeEmpty();
        (await colleague.PutAsJsonAsync(path + "/daily", command)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PutAsJsonAsync(path + "/daily", new { mode = "detailed", expectedRevision = 0 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.PutAsJsonAsync(path + "/daily", new { mode = "detailed", expectedRevision = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetFromJsonAsync<List<GuidanceHelpPreferenceDto>>(path))!.Single().Should().Be(new GuidanceHelpPreferenceDto("daily", "detailed", 2));
        (await colleague.GetFromJsonAsync<List<GuidanceHelpPreferenceDto>>(path))!.Single().Mode.Should().Be("compact");
        (await owner.PutAsJsonAsync(path + "/daily", command)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.PutAsJsonAsync(path + "/daily", new { mode = "off", expectedRevision = 2 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PutAsJsonAsync(path + "/unknown", command)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PutAsJsonAsync(path + "/daily", new { mode = "compact" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var outsider = await AuthedClientAsync("globex-admin@globex.local");
        (await outsider.GetFromJsonAsync<List<GuidanceHelpPreferenceDto>>(path))!.Should().BeEmpty();
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        (await manager.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (await employee.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await employee.PutAsJsonAsync(path + "/daily", command)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var export = System.Text.Json.JsonDocument.Parse(await (await owner.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
        export.RootElement.GetProperty("guidanceHelpPreferences").GetArrayLength().Should().Be(1);
        export.RootElement.GetProperty("guidanceHelpPreferences")[0].GetProperty("mode").GetString().Should().Be("detailed");
        export.RootElement.GetProperty("guidancePublishedPractices").GetArrayLength().Should().Be(0);
        export.RootElement.GetProperty("guidanceContentReadings").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishedProcessSteps_PreservePerStepRoleBoundariesAndPersonalHistory(bool financialProcesses)
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var owner = team.Clients[0]; var reviewer = team.Clients[1];
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (string Process, string Step, bool Manager)[] cases = financialProcesses ? [
            ("payroll", "settings-review", false), ("payroll", "period-create", false),
            ("payroll", "row-review", false), ("payroll", "approval", false),
            ("payroll", "payslip-review", false), ("payroll", "correction-external", false),
            ("compliance", "record-review", true), ("compliance", "status-review", false), ("compliance", "record-correction", false),
            ("offboarding", "preparation", false), ("offboarding", "settlement-review", false),
            ("offboarding", "completion", false), ("offboarding", "reversal-review", false), ("offboarding", "correction-external", false),
        ] : [
            ("company", "initial-setup", false), ("company", "company-profile", false),
            ("company", "departments", false), ("company", "access-preparation", false),
            ("personnel", "record-review", true), ("personnel", "import-review", false), ("personnel", "rehire", false),
            ("attendance", "entry-review", true), ("attendance", "entry-correction", true), ("attendance", "period-summary", true),
            ("daily", "task-review", true), ("daily", "task-action", true), ("daily", "external-followup", false),
        ];
        try
        {
            foreach (var example in cases)
            {
                var key = $"{example.Process}-{example.Step}";
                var body = CompleteBody("Yeni alt adım") with { Audience = Roles.All, StepKey = example.Step,
                    Practice = new($"{example.Step} kontrolü", ["Doğrula", "Tahmin et"], 0, "Gerçek kaynağı ve sonucu doğrulayın.") };
                var created = await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = example.Process, body });
                created.StatusCode.Should().Be(HttpStatusCode.Created, key);
                var item = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
                item = await ChangeAsync(owner, item, "submit");
                var published = await PublishAsync(reviewer, item);
                published.StatusCode.Should().Be(HttpStatusCode.OK, key);
                var stepPath = $"{Root}/steps/{example.Process}/{example.Step}";
                var check = (await owner.GetFromJsonAsync<GuidancePublishedPracticeDto>(stepPath))!;
                check.ContentRevisionId.Should().Be(item.Content.Id); check.StepKey.Should().Be(example.Step);
                check.AttemptCount.Should().Be(0); check.Explanation.Should().BeNull();
                var path = $"{Root}/versions/{item.Content.Id}/practice";
                var answer = new { selectedChoice = 0, expectedAttemptCount = 0, attemptId = Guid.NewGuid() };
                var expected = example.Manager ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
                (await manager.GetAsync(stepPath)).StatusCode.Should().Be(expected, key);
                (await manager.GetAsync(path)).StatusCode.Should().Be(expected, key);
                (await manager.PostAsJsonAsync(path + "/attempts", answer)).StatusCode.Should().Be(expected, key);
                (await employee.GetAsync(stepPath)).StatusCode.Should().Be(HttpStatusCode.Forbidden, key);
                (await employee.PostAsJsonAsync(path + "/attempts", answer)).StatusCode.Should().Be(HttpStatusCode.Forbidden, key);
                (await owner.PostAsJsonAsync(path + "/attempts", answer)).StatusCode.Should().Be(HttpStatusCode.OK, key);
                (await reviewer.GetFromJsonAsync<GuidancePublishedPracticeDto>(path))!.AttemptCount.Should().Be(0,
                    "başka kişinin denemesi yayın inceleyen kişiye taşınmaz");
            }
            var progress = (await owner.GetFromJsonAsync<List<GuidancePublishedPracticeProgressDto>>($"{Root}/practice-progress"))!;
            progress.Should().HaveCount(cases.Length).And.OnlyContain(x => x.IsCurrent && x.LastCorrect && x.AttemptCount == 1);
            using var export = System.Text.Json.JsonDocument.Parse(await (await owner.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidancePublishedPractices").GetArrayLength().Should().Be(cases.Length);
            export.RootElement.GetProperty("employeeCreationEvidences").GetArrayLength().Should().Be(0);
            export.RootElement.GetProperty("guidanceContentReadings").GetArrayLength().Should().Be(0);
            (await owner.GetAsync($"{Root}/steps/attendance/unknown-step")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            var mismatched = CompleteBody("Hatalı bağ") with { StepKey = "rehire", Practice = new("Soru", ["A", "B"], 0, "Açıklama") };
            (await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = "attendance-rehire", processKey = "attendance", body = mismatched }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally { foreach (var example in cases) await CleanupAsync($"{example.Process}-{example.Step}"); }
    }

    [Fact]
    public async Task PublishedPractice_IsReviewedPinnedPersonalIdempotent_AndDoesNotTransferToNewVersion()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var owner = team.Clients[0]; var reviewer = team.Clients[1];
        var outsider = await AuthedClientAsync("globex-admin@globex.local");
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        const string key = "personnel-entry-tab-kimlik";
        var body = CompleteBody("Alıştırma sürümü") with { StepKey = "tab-kimlik",
            Practice = new("Doğru kişiyi nasıl seçersiniz?", ["Tahmin ederim", "Kaynakla doğrularım", "Başkasını kopyalarım"], 1, "Kaynakla doğrulayın.") };
        try
        {
            var empty = await owner.GetAsync($"{Root}/steps/personnel-entry/tab-kimlik");
            empty.StatusCode.Should().Be(HttpStatusCode.OK);
            (await empty.Content.ReadAsStringAsync()).Should().Be("null");
            (await employee.GetAsync($"{Root}/steps/personnel-entry/tab-kimlik")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await manager.GetAsync($"{Root}/steps/personnel-entry/tab-kimlik")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var created = await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel-entry", body });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var item = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item.Content.Body.Practice!.CorrectChoice.Should().Be(1, "yayın incelemesi cevabı da kapsar");
            var versionPath = $"{Root}/versions/{item.Content.Id}/practice";
            (await outsider.GetAsync(versionPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            item = await ChangeAsync(owner, item, "submit");
            (await PublishAsync(owner, item)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            item = (await (await PublishAsync(reviewer, item)).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            var publicJson = await (await owner.GetAsync($"{Root}/versions/{item.Content.Id}")).Content.ReadAsStringAsync();
            publicJson.Should().NotContain("correctChoice").And.NotContain("Kaynakla doğrulayın.");
            var first = (await owner.GetFromJsonAsync<GuidancePublishedPracticeDto>(versionPath))!;
            first.AttemptCount.Should().Be(0); first.Explanation.Should().BeNull();
            (await (await owner.GetAsync(versionPath)).Content.ReadAsStringAsync()).Should().NotContain("correctChoice");
            (await employee.GetAsync(versionPath)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var attempts = versionPath + "/attempts";
            var answer = new { selectedChoice = 1, expectedAttemptCount = 0, attemptId = Guid.NewGuid() };
            (await Factory.CreateClient().PostAsJsonAsync(attempts, answer)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await owner.PostAsJsonAsync(attempts, answer with { selectedChoice = 4 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await owner.PostAsJsonAsync(attempts, answer with { attemptId = Guid.Empty })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var repeat = await Task.WhenAll(owner.PostAsJsonAsync(attempts, answer), owner.PostAsJsonAsync(attempts, answer));
            repeat.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
            foreach (var response in repeat)
            {
                var saved = (await response.Content.ReadFromJsonAsync<GuidancePublishedPracticeDto>())!;
                saved.AttemptCount.Should().Be(1); saved.LastCorrect.Should().BeTrue(); saved.Explanation.Should().Be(body.Practice.Explanation);
            }
            (await owner.PostAsJsonAsync(attempts, answer with { selectedChoice = 0 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await reviewer.GetFromJsonAsync<GuidancePublishedPracticeDto>(versionPath))!.Explanation.Should().BeNull();
            (await outsider.GetFromJsonAsync<GuidancePublishedPracticeDto>(versionPath))!.AttemptCount.Should().Be(0);
            (await reviewer.GetFromJsonAsync<List<GuidancePublishedPracticeProgressDto>>($"{Root}/practice-progress"))!.Should().BeEmpty();
            (await outsider.GetFromJsonAsync<List<GuidancePublishedPracticeProgressDto>>($"{Root}/practice-progress"))!.Should().NotContain(x => x.ContentKey == key);
            var concurrent = await Task.WhenAll(owner.PostAsJsonAsync(attempts, answer with { expectedAttemptCount = 1, attemptId = Guid.NewGuid() }),
                owner.PostAsJsonAsync(attempts, answer with { expectedAttemptCount = 1, attemptId = Guid.NewGuid() }));
            concurrent.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
            concurrent.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
            var forkBody = body with { Practice = body.Practice with { Question = "Yeni sürümün sorusu", CorrectChoice = 0 } };
            var fork = await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel-entry", basedOnId = item.Content.Id, body = forkBody });
            var next = (await fork.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            next = await ChangeAsync(owner, next, "submit");
            next = (await (await PublishAsync(reviewer, next)).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            (await owner.PostAsJsonAsync(attempts, answer with { expectedAttemptCount = 2, attemptId = Guid.NewGuid() })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var old = (await owner.GetFromJsonAsync<GuidancePublishedPracticeDto>(versionPath))!;
            old.IsCurrent.Should().BeFalse(); old.Question.Should().Be(body.Practice.Question); old.AttemptCount.Should().Be(2);
            var current = (await owner.GetFromJsonAsync<GuidancePublishedPracticeDto>($"{Root}/steps/personnel-entry/tab-kimlik"))!;
            current.ContentRevisionId.Should().Be(next.Content.Id); current.AttemptCount.Should().Be(0); current.Explanation.Should().BeNull();
            var history = (await owner.GetFromJsonAsync<List<GuidancePublishedPracticeProgressDto>>($"{Root}/practice-progress"))!;
            history.Should().ContainSingle().Which.IsCurrent.Should().BeFalse();
            using var export = System.Text.Json.JsonDocument.Parse(await (await owner.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidancePublishedPractices").GetArrayLength().Should().Be(1);
            export.RootElement.GetProperty("guidancePublishedPractices")[0].GetProperty("lastAnsweredAtUtc").GetString().Should().EndWith("Z");
            export.RootElement.GetProperty("guidanceDecisionProgresses").GetArrayLength().Should().Be(0);
            export.RootElement.GetProperty("guidanceContentReadings").GetArrayLength().Should().Be(0);
            export.RootElement.GetProperty("employeeCreationEvidences").GetArrayLength().Should().Be(0);
            using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
            {
                var db = scope.Services.GetRequiredService<AppDbContext>();
                var guard = new Ekipzen.Infrastructure.Persistence.Migrations.AddGuidancePublishedPractices().DownOperations.OfType<SqlOperation>().Single().Sql;
                Func<Task> rollback = () => db.Database.ExecuteSqlRawAsync(guard);
                (await rollback.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
                (await db.GuidancePublishedPracticeProgresses.CountAsync()).Should().Be(1);
            }
            await ChangeAsync(reviewer, next, "retire", "Test yayından kaldırma");
            (await owner.GetFromJsonAsync<GuidancePublishedPracticeDto?>($"{Root}/steps/personnel-entry/tab-kimlik")).Should().BeNull("eski sürüme geri dönülmez");
            (await owner.PostAsJsonAsync($"{Root}/versions/{next.Content.Id}/practice/attempts", answer)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task Practice_BindingAndQuestionAreValidatedBeforeReview()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0];
        const string key = "recruitment-candidate-create";
        var body = CompleteBody("Eksik soru") with { StepKey = "candidate-create", Practice = new("", ["", ""], 0, "") };
        try
        {
            (await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = "wrong-step-key", processKey = "recruitment", body })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var created = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "recruitment", body });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var item = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            (await author.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/submit", new { expectedRevision = item.Revision })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var invalid = body with { Practice = new("Soru", ["A", "B"], 2, "Açıklama") };
            (await author.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", new { expectedRevision = item.Revision, body = invalid })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            invalid = body with { Practice = new("Soru", ["A", " a "], 0, "Açıklama") };
            var saved = await author.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", new { expectedRevision = item.Revision, body = invalid });
            item = (await saved.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            (await author.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/submit", new { expectedRevision = item.Revision })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task LeavePractice_AllowsManagerButDoesNotGrantEmployeeDecisionAccess()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var owner = team.Clients[0]; var reviewer = team.Clients[1];
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        const string key = "leave-review-request";
        var body = CompleteBody("İzin kapsamı") with { Audience = Roles.All, StepKey = "review-request",
            Practice = new("Çelişki nasıl çözülür?", ["Tahmin", "Netleştirme"], 1, "Talebi netleştirin.") };
        try
        {
            var created = await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "leave", body });
            var item = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item = await ChangeAsync(owner, item, "submit");
            item = (await (await PublishAsync(reviewer, item)).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            var path = $"{Root}/versions/{item.Content.Id}/practice";
            (await manager.GetFromJsonAsync<GuidancePublishedPracticeDto>($"{Root}/steps/leave/review-request"))!.ContentRevisionId.Should().Be(item.Content.Id);
            var answer = new { selectedChoice = 1, expectedAttemptCount = 0, attemptId = Guid.NewGuid() };
            (await manager.PostAsJsonAsync(path + "/attempts", answer)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await employee.GetAsync($"{Root}/versions/{item.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.OK,
                "içerik okuma yetkisi tek başına yönetici alıştırması yetkisi değildir");
            (await employee.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await employee.PostAsJsonAsync(path + "/attempts", answer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await owner.GetFromJsonAsync<List<GuidancePublishedPracticeProgressDto>>($"{Root}/practice-progress"))!.Should().BeEmpty();
            (await manager.GetFromJsonAsync<List<GuidancePublishedPracticeProgressDto>>($"{Root}/practice-progress"))!.Should().ContainSingle(x => x.ContentKey == key);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task Reading_IsExplicitIdempotentPersonalAndVersioned_WithoutCreatingWorkEvidence()
    {
        var team = await TeamAsync();
        await using var host = team.Host;
        var owner = team.Clients[0]; var other = team.Clients[1];
        var outsider = await AuthedClientAsync("globex-admin@globex.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var key = $"reading-{Guid.NewGuid():N}";
        try
        {
            var create = await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel", body = CompleteBody("Okuma testi") });
            var item = (await create.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            (await owner.PostAsJsonAsync($"{Root}/versions/{item.Content.Id}/readings", new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
            item = await ChangeAsync(owner, item, "submit");
            var publish = await PublishAsync(other, item);
            publish.StatusCode.Should().Be(HttpStatusCode.OK);
            item = (await publish.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            var readingPath = $"{Root}/versions/{item.Content.Id}/readings";
            (await owner.GetAsync($"{Root}/versions/{item.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().BeEmpty("ekran açılması okuma beyanı üretmez");
            (await owner.PostAsJsonAsync(readingPath, new { confirmed = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await manager.PostAsJsonAsync(readingPath, new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.NotFound, "hedef rol dışındaki okuyucu kayıt oluşturamaz");
            (await Factory.CreateClient().PostAsJsonAsync(readingPath, new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var responses = await Task.WhenAll(owner.PostAsJsonAsync(readingPath, new { confirmed = true }), owner.PostAsJsonAsync(readingPath, new { confirmed = true }));
            responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
            var first = (await responses[0].Content.ReadFromJsonAsync<GuidanceContentReadingDto>())!;
            var repeated = (await responses[1].Content.ReadFromJsonAsync<GuidanceContentReadingDto>())!;
            repeated.Should().BeEquivalentTo(first);
            (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().ContainSingle();
            (await other.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().BeEmpty("aynı şirkette başka kişinin inceleme beyanı görünmez");
            (await outsider.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().NotContain(x => x.ContentKey == key);
            var export = System.Text.Json.JsonDocument.Parse(await (await owner.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceContentReadings").GetArrayLength().Should().Be(1);
            export.RootElement.GetProperty("guidanceContentReadings")[0].GetProperty("firstReadAtUtc").GetString().Should().EndWith("Z");
            export.RootElement.GetProperty("employeeCreationEvidences").GetArrayLength().Should().Be(0);
            export.RootElement.GetProperty("candidateReviewEvidences").GetArrayLength().Should().Be(0);
            export.RootElement.GetProperty("guidanceDecisionProgresses").GetArrayLength().Should().Be(0);
            var fork = await owner.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel", basedOnId = item.Content.Id, body = CompleteBody("Yeni metin") });
            var next = (await fork.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            next = await ChangeAsync(owner, next, "submit");
            next = (await (await PublishAsync(other, next)).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            var history = (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!;
            history.Should().ContainSingle().Which.IsCurrent.Should().BeFalse("yeni sürüm eski okuma kaydını devralmaz");
            history[0].FirstReadAtUtc.Should().Be(first.FirstReadAtUtc);
            (await owner.PostAsJsonAsync(readingPath, new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await owner.PostAsJsonAsync($"{Root}/versions/{next.Content.Id}/readings", new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().HaveCount(2);
            using (var tenantScope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
            {
                var db = tenantScope.Services.GetRequiredService<AppDbContext>();
                var rollbackGuard = new Ekipzen.Infrastructure.Persistence.Migrations.AddGuidanceContentReadings()
                    .DownOperations.OfType<SqlOperation>().Single().Sql;
                var rollback = async () => await db.Database.ExecuteSqlRawAsync(rollbackGuard);
                (await rollback.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
                (await db.GuidanceContentReadings.CountAsync()).Should().Be(2);
            }
            next = await ChangeAsync(other, next, "retire", "Okuma güncellik testi");
            (await owner.PostAsJsonAsync($"{Root}/versions/{next.Content.Id}/readings", new { confirmed = true })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await owner.GetFromJsonAsync<List<GuidanceContentReadingDto>>($"{Root}/readings"))!.Should().OnlyContain(x => !x.IsCurrent);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task CentralCatalog_RequiresIndependentTeamApproval_AndPreservesPublishedVersions()
    {
        var team = await TeamAsync();
        await using var editorHost = team.Host;
        var author = team.Clients[0]; var contributor = team.Clients[1]; var approver = team.Clients[2];
        var outsiderLogin = await AuthedClientAsync("globex-admin@globex.local");
        var outsider = editorHost.CreateClient();
        outsider.DefaultRequestHeaders.Authorization = outsiderLogin.DefaultRequestHeaders.Authorization;
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var key = $"catalog-{Guid.NewGuid():N}";
        var body = CompleteBody("İlk yayımlanmış gerekçe");
        try
        {
            (await outsider.GetFromJsonAsync<GuidanceEditorialAccessDto>($"{Root}/access"))!.CanManage.Should().BeFalse();
            (await team.Clients[3].GetFromJsonAsync<GuidanceEditorialAccessDto>($"{Root}/access"))!.CanManage.Should().BeFalse();
            (await team.Clients[3].GetAsync($"{Root}/editorial")).StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "ekip kiracısında hr-admin olmak tek başına yayın yetkisi vermez");
            (await outsider.GetAsync($"{Root}/editorial")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await outsider.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel", body }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Factory.CreateClient().GetAsync(Root)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var created = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel", body });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var item = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item.Revision.Should().Be(1); item.Content.ContentVersion.Should().Be(1);
            (await outsider.GetAsync($"{Root}/versions/{item.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            var updateBody = new { expectedRevision = 1, body };
            var updates = await Task.WhenAll(author.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", updateBody),
                author.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", updateBody));
            updates.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
            updates.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
            item = (await updates.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item = await ChangeAsync(author, item, "submit");
            item.Content.State.Should().Be("in-review"); item.CanPublish.Should().BeFalse();
            (await PublishAsync(author, item)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await contributor.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", new { expectedRevision = item.Revision, body }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await contributor.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/return", new { expectedRevision = item.Revision }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            item = await ChangeAsync(contributor, item, "return", "Örneğin açıklamasını yeniden kontrol ediyoruz.");
            var edit = await contributor.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", new { expectedRevision = item.Revision, body });
            edit.StatusCode.Should().Be(HttpStatusCode.OK);
            item = (await edit.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item.Contributors.Should().HaveCount(2);
            item = await ChangeAsync(contributor, item, "submit");
            (await PublishAsync(contributor, item)).StatusCode.Should().Be(HttpStatusCode.Conflict,
                "taslağı düzenleyen farklı kullanıcı da bu sürümün hazırlayanıdır");
            var publish = await PublishAsync(approver, item);
            publish.StatusCode.Should().Be(HttpStatusCode.OK, await publish.Content.ReadAsStringAsync());
            item = (await publish.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item.Content.IsCurrent.Should().BeTrue();
            var originalId = item.Content.Id;
            using (var platformScope = Factory.Services.CreateScope())
            {
                var platformDb = platformScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var rollbackGuard = new AddCentralGuidanceContentCatalog().DownOperations.OfType<SqlOperation>().Single().Sql;
                Func<Task> guardedRollback = () => platformDb.Database.ExecuteSqlRawAsync(rollbackGuard);
                (await guardedRollback.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
                (await platformDb.GuidanceContentRevisions.AnyAsync(x => x.Id == originalId)).Should().BeTrue();
            }
            var outsideRead = await outsider.GetAsync($"{Root}/versions/{originalId}");
            outsideRead.StatusCode.Should().Be(HttpStatusCode.OK, "katalog şirketlere ortak yayımlanır");
            var publicJson = await outsideRead.Content.ReadAsStringAsync();
            publicJson.Should().NotContain("PRIVATE-REVIEW").And.NotContain("contributors").And.NotContain("actorUserId");
            using (var json = System.Text.Json.JsonDocument.Parse(publicJson))
                json.RootElement.GetProperty("publishedAtUtc").GetString().Should().EndWith("Z");
            (await employee.GetAsync($"{Root}/versions/{originalId}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await manager.GetAsync($"{Root}/versions/{originalId}")).StatusCode.Should().Be(HttpStatusCode.NotFound,
                "okur hedef rolüne sunucuda göre süzülür");
            (await author.PutAsJsonAsync($"{Root}/editorial/{originalId}", new { expectedRevision = item.Revision, body = body with { Why = "Yerinde değişiklik" } }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
            var forkRequest = new { contentKey = key, processKey = "personnel", basedOnId = originalId, body = body with { Why = "İkinci sürüm gerekçesi" } };
            var forks = await Task.WhenAll(author.PostAsJsonAsync($"{Root}/editorial", forkRequest), author.PostAsJsonAsync($"{Root}/editorial", forkRequest));
            forks.Count(x => x.StatusCode == HttpStatusCode.Created).Should().Be(1);
            forks.Count(x => x.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
            var next = (await forks.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            next.Content.ContentVersion.Should().Be(2); next.BasedOnId.Should().Be(originalId);
            next = await ChangeAsync(author, next, "submit");
            var nextPublish = await PublishAsync(approver, next);
            nextPublish.StatusCode.Should().Be(HttpStatusCode.OK);
            next = (await nextPublish.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            var original = (await outsider.GetFromJsonAsync<GuidanceContentDto>($"{Root}/versions/{originalId}"))!;
            original.Body.Why.Should().Be("İlk yayımlanmış gerekçe"); original.IsCurrent.Should().BeFalse();
            var current = (await outsider.GetFromJsonAsync<List<GuidanceContentDto>>(Root))!.Where(x => x.ContentKey == key).ToList();
            current.Should().ContainSingle().Which.Id.Should().Be(next.Content.Id);
            next = await ChangeAsync(approver, next, "retire", "Dayanak yeniden değerlendirilecek.");
            next.Content.State.Should().Be("retired");
            (await outsider.GetFromJsonAsync<List<GuidanceContentDto>>(Root))!.Should().NotContain(x => x.ContentKey == key,
                "son sürüm kaldırılınca eski yayımlanmış sürüme sessizce geri dönülmez");
            (await outsider.GetAsync($"{Root}/versions/{next.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            var audit = (await approver.GetFromJsonAsync<List<GuidanceEditorialDto>>($"{Root}/editorial"))!.Single(x => x.Content.Id == originalId);
            audit.Events.Select(x => x.Action).Should().Contain(["created", "updated", "submit", "return", "publish"]);
            audit.Content.IsCurrent.Should().BeFalse();
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task Publication_RequiresCompleteOfficialSourceAndCurrentExpertReview()
    {
        var team = await TeamAsync();
        await using var editorHost = team.Host;
        var author = team.Clients[0]; var approver = team.Clients[2];
        var key = $"validation-{Guid.NewGuid():N}";
        var incomplete = CompleteBody("İnceleme") with { Example = "", SourceKind = "legislation", SourceReference = "https://unofficial.example.test/rule" };
        try
        {
            var created = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "leave", body = incomplete });
            created.StatusCode.Should().Be(HttpStatusCode.Created, "eksik içerik taslak olarak saklanabilir");
            var item = (await created.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            (await author.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/submit", new { expectedRevision = item.Revision }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
            var body = CompleteBody("İncelenen gerekçe") with { SourceKind = "legislation", SourceReference = "https://www.mevzuat.gov.tr/" };
            var updated = await author.PutAsJsonAsync($"{Root}/editorial/{item.Content.Id}", new { expectedRevision = item.Revision, body });
            updated.StatusCode.Should().Be(HttpStatusCode.OK);
            item = (await updated.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            item = await ChangeAsync(author, item, "submit");
            var review = Review();
            (await approver.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/publish", new { expectedRevision = item.Revision, review = review with { Confirmed = false } }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await approver.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/publish", new { expectedRevision = item.Revision, review = review with { ReviewedOn = GuidanceContentCatalog.Today.AddDays(-1) } }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict, "inceleme son düzenlemeden önce olamaz");
            (await approver.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/publish", new { expectedRevision = item.Revision, review = review with { ReviewDueOn = GuidanceContentCatalog.Today.AddDays(-1) } }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PublishAsync(approver, item)).StatusCode.Should().Be(HttpStatusCode.OK);
            // Gelecek sürüm bugünkü rehberi erken değiştiremez; süresi dolan son
            // sürüm eski sürüme düşmeden katalogdan çıkar.
            using var scope = Factory.Services.CreateScope();
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var stored = await platform.GuidanceContentRevisions.SingleAsync(x => x.Id == item.Content.Id);
            stored.EffectiveFrom = GuidanceContentCatalog.Today.AddDays(1);
            await platform.SaveChangesAsync(CancellationToken.None);
            (await author.GetFromJsonAsync<List<GuidanceContentDto>>(Root))!.Should().NotContain(x => x.ContentKey == key);
            stored.EffectiveFrom = GuidanceContentCatalog.Today;
            stored.ReviewDueOn = GuidanceContentCatalog.Today.AddDays(-1);
            await platform.SaveChangesAsync(CancellationToken.None);
            (await author.GetFromJsonAsync<List<GuidanceContentDto>>(Root))!.Should().NotContain(x => x.ContentKey == key);
            (await author.GetFromJsonAsync<GuidanceContentDto>($"{Root}/versions/{item.Content.Id}"))!.IsCurrent.Should().BeFalse();
        }
        finally { await CleanupAsync(key); }
    }

    private async Task<(WebApplicationFactory<Program> Host, HttpClient[] Clients, int TenantId)> TeamAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var emails = Enumerable.Range(0, 4).Select(x => $"editor-{x}-{suffix}@example.test").ToArray();
        var tenant = await ProvisionAndActivateAsync("Ekipzen yayın testi", emails[0]);
        var ids = new List<string>();
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(tenant.TenantId))
        {
            var users = scope.Services.GetRequiredService<UserManager<ApplicationUser>>();
            var directory = scope.Services.GetRequiredService<ITenantDirectory>();
            ids.Add((await users.FindByEmailAsync(emails[0]))!.Id);
            for (var i = 1; i < emails.Length; i++)
            {
                var user = new ApplicationUser { UserName = emails[i], Email = emails[i], EmailConfirmed = true,
                    TenantId = tenant.TenantId, DisplayName = $"Yayın inceleyen {i}" };
                (await users.CreateAsync(user, DefaultPassword)).Succeeded.Should().BeTrue();
                (await users.AddToRoleAsync(user, Roles.HrAdmin)).Succeeded.Should().BeTrue();
                await directory.ReserveAsync(emails[i], tenant.TenantId, CancellationToken.None);
                ids.Add(user.Id);
            }
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GuidanceEditorial:TenantId"] = tenant.TenantId.ToString(),
            ["GuidanceEditorial:UserIds:0"] = ids[0], ["GuidanceEditorial:UserIds:1"] = ids[1], ["GuidanceEditorial:UserIds:2"] = ids[2],
        }).Build();
        var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGuidanceEditorialAccess>();
            services.AddScoped<IGuidanceEditorialAccess>(sp => new GuidanceEditorialAccess(configuration,
                sp.GetRequiredService<ICurrentUser>(), sp.GetRequiredService<ICurrentTenant>()));
        }));
        var clients = new List<HttpClient>();
        foreach (var email in emails)
        {
            var login = await host.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = DefaultPassword });
            login.StatusCode.Should().Be(HttpStatusCode.OK);
            var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
            var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            clients.Add(client);
        }
        return (host, clients.ToArray(), tenant.TenantId);
    }
    private async Task CleanupAsync(string key)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPlatformDbContext>().GuidanceContentRevisions
            .Where(x => x.ContentKey == key).ExecuteDeleteAsync();
    }
    private static GuidanceContentBody CompleteBody(string why) => new(
        "Test rehber başlığı", [Roles.HrAdmin, Roles.Employee], "Kaydı doğrulamayı öğren", "Doğru kaydı bul",
        "Kartı karşılaştır", why, "Anonim ayrıntılı örnek", "Yanlış kişiyi seçme", "Kart: personel kaydı",
        "Doğru kişiyi belirle", "İşlem sunucu sonucu", "İlgili kayda dön", "İşlem durumuna göre düzelt",
        "Çelişkide uzman incelemesi iste", "good-practice", "Ekipzen test kaynağı", "İşlem güvenliği",
        "Standart günlük işlem", GuidanceContentCatalog.Today, null);
    private static GuidanceExpertReview Review() => new("İnceleme uzmanı", "İK uzmanı", "PRIVATE-REVIEW-123",
        GuidanceContentCatalog.Today, GuidanceContentCatalog.Today.AddDays(30), true);
    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, GuidanceEditorialDto item) =>
        client.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/publish", new { expectedRevision = item.Revision, review = Review() });
    private static async Task<GuidanceEditorialDto> ChangeAsync(HttpClient client, GuidanceEditorialDto item, string action, string? reason = null)
    {
        var response = await client.PostAsJsonAsync($"{Root}/editorial/{item.Content.Id}/{action}", new { expectedRevision = item.Revision, reason });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
    }
}
