using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Recruitment;
using Ekipzen.Application.Features.Recruitment.Commands;
using Ekipzen.Domain.Enums;
using Ekipzen.Infrastructure.Persistence;
using Ekipzen.Infrastructure.Persistence.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    private static async Task<CandidateDetailDto> DecisionCandidateAsync(HttpClient client)
        => (await (await client.PostAsJsonAsync("/api/candidates", new { name = "Karar aday", appliedRole = "Uzman" })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;

    [Fact]
    public async Task RecruitmentDecisionHire_SubsequentDecisionsOwnHistoryNoOpStaleAndRetirement()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "recruitment", "first-review");
            var c = await DecisionCandidateAsync(author); var path = $"/api/candidates/{c.Id}/status";
            c = (await (await author.PatchAsJsonAsync(path, new { status = "Mülakat", expectedVersion = c.Version, reason = "İlk inceleme" })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            (await OwnOperationsAsync(author)).Should().BeEmpty("eski sıradan ilk karar sonradan damgalanmaz");
            var input = new { status = "Teklif", expectedVersion = c.Version, reason = "Teklif koşulları incelendi", guidanceContentRevisionId = source.Content.Id };
            var responses = await Task.WhenAll(reviewer.PatchAsJsonAsync(path, input), reviewer.PatchAsJsonAsync(path, input));
            responses.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var proof = (await OwnOperationsAsync(reviewer)).Should().ContainSingle().Subject.Evidence;
            proof.OperationKey.Should().Be("candidate-stage-decision"); proof.RecruitmentChanges!.FromVersion.Should().Be(c.Version);
            proof.RecruitmentChanges.ToVersion.Should().Be(c.Version + 1); proof.RecruitmentChanges.Reason.Should().Be(input.reason);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            (await reviewer.PatchAsJsonAsync(path, input)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await reviewer.PatchAsJsonAsync(path, new { status = "Teklif", expectedVersion = c.Version + 1, reason = "Değişiklik yok", guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(reviewer)).Should().ContainSingle();
            var export = await reviewer.GetFromJsonAsync<JsonElement>("/api/me/data-export"); export.GetProperty("guidanceOperations").GetArrayLength().Should().Be(1);
            (await OwnOperationsAsync(await AuthedClientAsync("globex-admin@globex.local"))).Should().BeEmpty();
            await ChangeAsync(reviewer, source, "retire", "Yeni karar dersi hazırlanacak");
            (await reviewer.PatchAsJsonAsync(path, new { status = "Red", expectedVersion = c.Version + 1, reason = "Yeni karar", guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(reviewer)).Single().IsCurrent.Should().BeFalse();
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var down = new AddCandidateStageGuidance().DownOperations.OfType<SqlOperation>().Single().Sql;
            (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(down))).Number.Should().Be(51000);
        }
        finally { await CleanupAsync("recruitment-first-review"); }
    }

    [Fact]
    public async Task RecruitmentDecisionHire_HireVersionScopeAndSeparateRetryIntent()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "recruitment", "hire-preparation");
            var wrong = await PublishOperationSourceAsync(author, reviewer, "recruitment", "first-review");
            var department = await (await author.PostAsJsonAsync("/api/departments", new { name = "İşe alma" })).Content.ReadFromJsonAsync<JsonElement>();
            var c = await DecisionCandidateAsync(author); var email = $"hire-{Guid.NewGuid():N}@test.local"; var path = $"/api/candidates/{c.Id}/hire";
            object body(int? version, int? revision) => new { departmentId = department.GetProperty("id").GetInt32(), email, hireDate = GuidanceContentCatalog.Today, contractType = "indefinite", expectedCandidateVersion = version, guidanceContentRevisionId = revision };
            (await author.PostAsJsonAsync(path, body(null, source.Content.Id))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await author.PostAsJsonAsync(path, body(c.Version + 1, source.Content.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await author.PostAsJsonAsync(path, body(c.Version, wrong.Content.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await (await AuthedClientAsync("ece.arslan@hrmaster.local")).PostAsJsonAsync(path, body(c.Version, source.Content.Id))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var hiredResponse = await author.PostAsJsonAsync(path, body(c.Version, source.Content.Id)); hiredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var hired = (await hiredResponse.Content.ReadFromJsonAsync<HireResultDto>())!;
            var first = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject.Evidence;
            first.OperationKey.Should().Be("candidate-hire-create"); first.Outcome.Should().Be("hire-recorded"); first.ResourceId.Should().Be(c.Id);
            (await author.PostAsJsonAsync(path, body(c.Version, source.Content.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId); var db = scope.Services.GetRequiredService<AppDbContext>();
            var operation = await db.IdentityProvisioningOperations.SingleAsync(x => x.CandidateId == c.Id);
            operation.Status = IdentityProvisioningStatus.Failed; operation.LeaseUntilUtc = DateTime.UtcNow.AddMinutes(3); await db.SaveChangesAsync();
            var retryPath = $"/api/candidates/{c.Id}/provisioning/retry";
            (await reviewer.PostAsJsonAsync(retryPath, new { guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            db.ChangeTracker.Clear(); operation = await db.IdentityProvisioningOperations.SingleAsync(x => x.CandidateId == c.Id);
            operation.LeaseUntilUtc.Should().BeAfter(DateTime.UtcNow); operation.LeaseUntilUtc = null; await db.SaveChangesAsync();
            (await reviewer.PostAsJsonAsync(retryPath, new { guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            var retryProof = (await OwnOperationsAsync(reviewer)).Should().ContainSingle().Subject.Evidence;
            retryProof.OperationKey.Should().Be("candidate-provisioning-retry"); retryProof.Outcome.Should().Be("retry-requested");
            (await OwnOperationsAsync(author)).Single().Evidence.Should().BeEquivalentTo(first);
            (await db.Employees.CountAsync(x => x.Id == hired.EmployeeId)).Should().Be(1);
            (await db.IdentityProvisioningOperations.CountAsync(x => x.CandidateId == c.Id)).Should().Be(1);
            (await author.GetFromJsonAsync<CandidateDetailDto>($"/api/candidates/{c.Id}"))!.LinkedEmployee!.Id.Should().Be(hired.EmployeeId);
            await ChangeAsync(reviewer, source, "retire", "Yeni işe alma dersi");
            (await OwnOperationsAsync(author)).Single().IsCurrent.Should().BeFalse(); (await OwnOperationsAsync(reviewer)).Single().IsCurrent.Should().BeFalse();
        }
        finally { await CleanupAsync("recruitment-hire-preparation"); await CleanupAsync("recruitment-first-review"); }
    }

    [Fact]
    public async Task RecruitmentDecisionHire_FailedEvidenceRollsBackStageAndHire()
    {
        var team = await TeamAsync(); await using var host = team.Host; var client = team.Clients[0];
        try
        {
            var review = await PublishOperationSourceAsync(client, team.Clients[1], "recruitment", "first-review");
            var hire = await PublishOperationSourceAsync(client, team.Clients[1], "recruitment", "hire-preparation");
            var department = await (await client.PostAsJsonAsync("/api/departments", new { name = "Rollback" })).Content.ReadFromJsonAsync<JsonElement>();
            var c = await DecisionCandidateAsync(client); var statusPath = $"/api/candidates/{c.Id}/status";
            c = (await (await client.PatchAsJsonAsync(statusPath, new { status = "Mülakat", expectedVersion = c.Version, reason = "İlk" })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId); var db = scope.Services.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [CandidateStageDecisions] WITH NOCHECK ADD CONSTRAINT [CK_Test_StageGuidance] CHECK ([GuidanceSnapshotJson] IS NULL)");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePreparationEvidences] WITH NOCHECK ADD CONSTRAINT [CK_Test_HireGuidance] CHECK ([OperationKey] NOT IN ('candidate-hire-create', 'candidate-provisioning-retry'))");
            try
            {
                (await client.PatchAsJsonAsync(statusPath, new { status = "Teklif", expectedVersion = c.Version, reason = "Değişmemeli", guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/hire", new { departmentId = department.GetProperty("id").GetInt32(), email = $"rollback-{Guid.NewGuid():N}@test.local", hireDate = GuidanceContentCatalog.Today, contractType = "indefinite", expectedCandidateVersion = c.Version, guidanceContentRevisionId = hire.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await client.GetFromJsonAsync<CandidateDetailDto>($"/api/candidates/{c.Id}"))!.Should().BeEquivalentTo(c);
                (await db.IdentityProvisioningOperations.CountAsync()).Should().Be(0); (await db.Employees.CountAsync()).Should().Be(0);
                (await OwnOperationsAsync(client)).Should().BeEmpty();
                // Ordinary hire supplies an existing delivery for the retry rollback test.
                (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/hire", new { departmentId = department.GetProperty("id").GetInt32(), email = $"retry-rollback-{Guid.NewGuid():N}@test.local", hireDate = GuidanceContentCatalog.Today, contractType = "indefinite" })).StatusCode.Should().Be(HttpStatusCode.OK);
                var delivery = await db.IdentityProvisioningOperations.SingleAsync();
                delivery.Status = IdentityProvisioningStatus.Failed; delivery.LastError = "Korunmalı"; delivery.LeaseUntilUtc = null; await db.SaveChangesAsync();
                var beforeRetry = delivery.NextAttemptAtUtc;
                (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/provisioning/retry", new { guidanceContentRevisionId = hire.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                db.ChangeTracker.Clear(); delivery = await db.IdentityProvisioningOperations.SingleAsync();
                delivery.Status.Should().Be(IdentityProvisioningStatus.Failed); delivery.LastError.Should().Be("Korunmalı");
                delivery.NextAttemptAtUtc.Should().Be(beforeRetry); (await OwnOperationsAsync(client)).Should().BeEmpty();
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE [CandidateStageDecisions] DROP CONSTRAINT [CK_Test_StageGuidance]");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePreparationEvidences] DROP CONSTRAINT [CK_Test_HireGuidance]");
            }
        }
        finally { await CleanupAsync("recruitment-first-review"); await CleanupAsync("recruitment-hire-preparation"); }
    }
}
