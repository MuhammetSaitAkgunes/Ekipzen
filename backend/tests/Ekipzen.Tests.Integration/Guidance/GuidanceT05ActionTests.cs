using System.Net;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Actions;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    private static object T05Body(GlobalActionDto action, int? source, string? owner = null) => new {
        title = action.Title, source = action.Source, owner = owner ?? action.Owner, sourceRoute = action.SourceRoute,
        due = action.Due, priority = action.Priority, recommendedAction = action.Action,
        expectedRevision = action.Revision, guidanceContentRevisionId = source,
    };

    [Fact]
    public async Task T05_ActionJourney_NoOpAndReadAreNotProof_OtherUserClosesTask_DeletionPreservesHistory()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        try
        {
            var author = team.Clients[0]; var reviewer = team.Clients[1];
            var source = await PublishOperationSourceAsync(author, reviewer, "daily", "task-action");
            var response = await author.PostAsJsonAsync("/api/actions", new { title = "T05 takip", source = "Bordro", owner = "İK", due = "Bugün" });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var item = (await response.Content.ReadFromJsonAsync<GlobalActionDto>())!;
            var path = $"/api/actions/{item.Id}";
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            (await author.PutAsJsonAsync(path, T05Body(item, source.Content.Id))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().BeEmpty("değişiklik olmayan kayıt kanıt değildir");
            var changed = await author.PutAsJsonAsync(path, T05Body(item, source.Content.Id, "Yeni sorumlu"));
            changed.StatusCode.Should().Be(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
            var updated = (await changed.Content.ReadFromJsonAsync<GlobalActionDto>())!;
            (await author.PutAsJsonAsync(path, T05Body(item, source.Content.Id, "Eski form"))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            foreach (var client in new[] { author, reviewer })
            {
                var jobs = (await client.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!;
                var task = jobs.Should().ContainSingle(x => x.Key == $"action-followup:{item.Id}").Subject;
                task.DueDate.Should().BeNull(); task.DateNote.Should().Contain("Bugün");
                task.Reason.Should().Contain("Yeni sorumlu");
            }
            (await reviewer.PatchAsJsonAsync(path + "/status", new { status = "done", expectedRevision = updated.Revision,
                guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!
                .Should().NotContain(x => x.Key == $"action-followup:{item.Id}");
            var done = (await author.GetFromJsonAsync<List<GlobalActionDto>>("/api/actions"))!.Single(x => x.Id == item.Id);
            (await reviewer.PatchAsJsonAsync(path + "/status", new { status = "open", expectedRevision = done.Revision,
                guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
            (await employee.PostAsJsonAsync("/api/actions", new { title = "Yetkisiz", source = "X", owner = "Y" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var outsider = await AuthedClientAsync("globex-admin@globex.local");
            (await outsider.PutAsJsonAsync(path, T05Body(done, null))).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await author.DeleteAsync(path + $"?expectedRevision={done.Revision}&guidanceContentRevisionId={source.Content.Id}"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            var own = await OwnOperationsAsync(author);
            own.Select(x => x.Evidence.OperationKey).Should().BeEquivalentTo(["action-update", "action-delete"]);
            own.Single(x => x.Evidence.OperationKey == "action-delete").Evidence.Outcome.Should().Be("deleted");
            (await OwnOperationsAsync(reviewer)).Should().ContainSingle().Subject.Evidence.OperationKey.Should().Be("action-status");
        }
        finally { await CleanupAsync("daily-task-action"); }
    }

    [Fact]
    public async Task T05_ConcurrentCorrectionUsesOneSourceRevisionAndOneProof()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        try
        {
            var author = team.Clients[0]; var reviewer = team.Clients[1];
            var source = await PublishOperationSourceAsync(author, reviewer, "daily", "task-action");
            var created = await author.PostAsJsonAsync("/api/actions", new { title = "T05 yarış", source = "Takip", owner = "İK", guidanceContentRevisionId = source.Content.Id });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var item = (await created.Content.ReadFromJsonAsync<GlobalActionDto>())!;
            var results = await Task.WhenAll(author.PutAsJsonAsync($"/api/actions/{item.Id}", T05Body(item, source.Content.Id, "A")),
                reviewer.PutAsJsonAsync($"/api/actions/{item.Id}", T05Body(item, source.Content.Id, "B")));
            results.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var all = (await OwnOperationsAsync(author)).Concat(await OwnOperationsAsync(reviewer)).ToList();
            all.Select(x => x.Evidence.OperationKey).Should().BeEquivalentTo(["action-create", "action-update"]);
        }
        finally { await CleanupAsync("daily-task-action"); }
    }

    [Fact]
    public async Task T05_ProofFailureRollsBackRealAction_AndWrongStepNeverWrites()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        try
        {
            var author = team.Clients[0]; var reviewer = team.Clients[1];
            var source = await PublishOperationSourceAsync(author, reviewer, "daily", "task-action");
            var wrong = await PublishOperationSourceAsync(author, reviewer, "daily", "task-review");
            (await author.PostAsJsonAsync("/api/actions", new { title = "T05 yanlış", source = "Takip", owner = "İK", guidanceContentRevisionId = wrong.Content.Id }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePreparationEvidences] ADD CONSTRAINT [CK_T05_Reject] CHECK ([OperationKey] <> 'action-create');");
            try
            {
                (await author.PostAsJsonAsync("/api/actions", new { title = "T05 geri alma", source = "Takip", owner = "İK", guidanceContentRevisionId = source.Content.Id }))
                    .StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await db.GlobalActions.CountAsync()).Should().Be(0);
                (await OwnOperationsAsync(author)).Should().BeEmpty();
            }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePreparationEvidences] DROP CONSTRAINT [CK_T05_Reject];"); }
        }
        finally { await CleanupAsync("daily-task-action"); await CleanupAsync("daily-task-review"); }
    }
}
