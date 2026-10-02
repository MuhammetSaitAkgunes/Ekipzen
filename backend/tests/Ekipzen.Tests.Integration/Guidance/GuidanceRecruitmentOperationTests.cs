using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Recruitment;
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
    private static object RecruitmentCorrection(CandidateDetailDto c, int? source = null)
        => new { expectedVersion = c.Version, name = c.Name + " düzeltildi", c.AppliedRole, c.PositionId,
            expectedPositionVersion = (int?)null, c.Score, c.Location, c.ExperienceYears, c.Summary,
            reason = "Başvuru kaynağı kontrol edildi", confirmExistingEvidenceReview = false, guidanceContentRevisionId = source };

    [Fact]
    public async Task RecruitmentOperations_SixSeparateSourcesOwnerHistoryExportAndRetirement()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            var preparation = await PublishOperationSourceAsync(author, reviewer, "recruitment", "candidate-create");
            var review = await PublishOperationSourceAsync(author, reviewer, "recruitment", "first-review");
            var pos = (await (await author.PostAsJsonAsync("/api/positions", new { title = "Uzman", openCount = 2 })).Content.ReadFromJsonAsync<PositionDto>())!;
            (await author.PutAsJsonAsync($"/api/positions/{pos.Id}", new { expectedVersion = pos.Version, title = "Kıdemli uzman", pos.DepartmentId, openCount = 2, isOpen = true, reason = "Rol tanımı düzeltildi", guidanceContentRevisionId = preparation.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            var c = (await (await author.PostAsJsonAsync("/api/candidates", new { name = "Öğrenen aday", appliedRole = "Uzman", positionId = pos.Id })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            c = (await (await author.PutAsJsonAsync($"/api/candidates/{c.Id}", RecruitmentCorrection(c, preparation.Content.Id))).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            var note = (await (await author.PostAsJsonAsync($"/api/candidates/{c.Id}/notes", new { noteType = "Teknik Mülakat", text = "İlk gözlem", guidanceContentRevisionId = review.Content.Id })).Content.ReadFromJsonAsync<InterviewNoteDto>())!;
            var evaluation = (await (await author.PostAsJsonAsync($"/api/candidates/{c.Id}/evaluations", new { criterion = "SQL", score = 4, maxScore = 5, guidanceContentRevisionId = review.Content.Id })).Content.ReadFromJsonAsync<CandidateEvaluationDto>())!;
            c = (await author.GetFromJsonAsync<CandidateDetailDto>($"/api/candidates/{c.Id}"))!;
            c = (await (await author.PatchAsJsonAsync($"/api/candidates/{c.Id}/status", new { status = "Mülakat", expectedVersion = c.Version, reason = "Gözleme göre görüşme", evidence = new[] { new { kind = "evaluation", id = evaluation.Id, version = 1 } } })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            c = (await (await author.PostAsJsonAsync($"/api/candidates/{c.Id}/notes/{note.Id}/revisions", new { expectedCandidateVersion = c.Version, expectedRecordVersion = 1, noteType = "İK Görüşmesi", text = "Düzeltilen gözlem", reason = "Tür ve belge kontrolü", guidanceContentRevisionId = review.Content.Id })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            c = (await (await reviewer.PostAsJsonAsync($"/api/candidates/{c.Id}/evaluations/{evaluation.Id}/revisions", new { expectedCandidateVersion = c.Version, expectedRecordVersion = 1, criterion = "SQL", score = 4, maxScore = 10, reason = "Ölçek 10 üzerinden", guidanceContentRevisionId = review.Content.Id })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            c.Score.Should().BeNull(); c.Status.Should().Be("Mülakat");
            c.StageDecisions.Single().Evidence.Single().State.MaxScore.Should().Be(5);
            var noteHistory = (await author.GetFromJsonAsync<CandidateEvidenceHistoryDto>($"/api/candidates/{c.Id}/notes/{note.Id}/revisions"))!;
            noteHistory.Original.Text.Should().Be("İlk gözlem"); noteHistory.Revisions.Single().Version.Should().Be(2);
            var own = await OwnOperationsAsync(author); own.Should().HaveCount(5);
            own.Select(x => x.Evidence.OperationKey).Should().BeEquivalentTo("position-correct", "candidate-correct", "candidate-note-create", "candidate-evaluation-create", "candidate-note-revise");
            own.Single(x => x.Evidence.OperationKey == "candidate-note-create").Evidence.RecruitmentChanges!.ToVersion.Should().Be(1);
            var reviseProof = (await OwnOperationsAsync(reviewer)).Should().ContainSingle().Subject.Evidence;
            reviseProof.OperationKey.Should().Be("candidate-evaluation-revise"); reviseProof.RecruitmentChanges!.RecordId.Should().Be(evaluation.Id);
            reviseProof.RecruitmentChanges.FromVersion.Should().Be(1); reviseProof.RecruitmentChanges.ToVersion.Should().Be(2);
            (await OwnOperationsAsync(await AuthedClientAsync("globex-admin@globex.local"))).Should().BeEmpty();
            var export = await author.GetFromJsonAsync<JsonElement>("/api/me/data-export"); export.GetProperty("guidanceOperations").GetArrayLength().Should().Be(5);
            await ChangeAsync(reviewer, review, "retire", "Yeni kaynak hazırlanacak");
            own = await OwnOperationsAsync(author); own.Count(x => x.IsCurrent).Should().Be(2); own.Should().HaveCount(5);
            (await author.PostAsJsonAsync($"/api/candidates/{c.Id}/notes", new { noteType = "İK Görüşmesi", text = "Eski kaynak", guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var down = new AddRecruitmentOperationGuidance().DownOperations.OfType<SqlOperation>().Single().Sql;
            (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(down))).Number.Should().Be(51000);
        }
        finally { await CleanupAsync("recruitment-candidate-create"); await CleanupAsync("recruitment-first-review"); }
    }

    [Fact]
    public async Task RecruitmentOperations_StaleWrongSourceAndHiredHistoryCannotEarnProof()
    {
        var team = await TeamAsync(); await using var host = team.Host; var client = team.Clients[0];
        try
        {
            var preparation = await PublishOperationSourceAsync(client, team.Clients[1], "recruitment", "candidate-create");
            var review = await PublishOperationSourceAsync(client, team.Clients[1], "recruitment", "first-review");
            var c = (await (await client.PostAsJsonAsync("/api/candidates", new { name = "Aday", appliedRole = "Uzman" })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/notes", new { noteType = "İK Görüşmesi", text = "Yanlış kaynak", guidanceContentRevisionId = preparation.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var body = RecruitmentCorrection(c, preparation.Content.Id);
            var race = await Task.WhenAll(client.PutAsJsonAsync($"/api/candidates/{c.Id}", body), client.PutAsJsonAsync($"/api/candidates/{c.Id}", body));
            race.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            (await OwnOperationsAsync(client)).Should().ContainSingle();
            var manager = await AuthedClientAsync("ece.arslan@hrmaster.local"); (await manager.PutAsJsonAsync($"/api/candidates/{c.Id}", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>(); var hired = await db.Candidates.SingleAsync(); hired.Status = CandidateStatus.Hired; await db.SaveChangesAsync();
            c = (await client.GetFromJsonAsync<CandidateDetailDto>($"/api/candidates/{c.Id}"))!;
            (await client.PutAsJsonAsync($"/api/candidates/{c.Id}", RecruitmentCorrection(c, preparation.Content.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/notes", new { noteType = "İK Görüşmesi", text = "Kilitli", guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/evaluations", new { criterion = "SQL", score = 4, guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/notes/1/revisions", new { expectedCandidateVersion = c.Version, expectedRecordVersion = 1, noteType = "İK Görüşmesi", text = "Kilitli", reason = "Kaynak", guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/evaluations/1/revisions", new { expectedCandidateVersion = c.Version, expectedRecordVersion = 1, criterion = "SQL", score = 4, maxScore = 5, reason = "Kaynak", guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(client)).Should().ContainSingle(); (await db.InterviewNotes.CountAsync()).Should().Be(0); (await db.CandidateEvaluations.CountAsync()).Should().Be(0);
        }
        finally { await CleanupAsync("recruitment-candidate-create"); await CleanupAsync("recruitment-first-review"); }
    }

    [Fact]
    public async Task RecruitmentOperations_FailedSnapshotsRollbackAllSixBusinessWrites()
    {
        var team = await TeamAsync(); await using var host = team.Host; var client = team.Clients[0];
        try
        {
            var preparation = await PublishOperationSourceAsync(client, team.Clients[1], "recruitment", "candidate-create");
            var review = await PublishOperationSourceAsync(client, team.Clients[1], "recruitment", "first-review");
            var pos = (await (await client.PostAsJsonAsync("/api/positions", new { title = "Uzman" })).Content.ReadFromJsonAsync<PositionDto>())!;
            var c = (await (await client.PostAsJsonAsync("/api/candidates", new { name = "Aday", appliedRole = "Uzman" })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            var note = (await (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/notes", new { noteType = "İK Görüşmesi", text = "İlk not" })).Content.ReadFromJsonAsync<InterviewNoteDto>())!;
            var evaluation = (await (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/evaluations", new { criterion = "SQL", score = 4 })).Content.ReadFromJsonAsync<CandidateEvaluationDto>())!;
            c = (await client.GetFromJsonAsync<CandidateDetailDto>($"/api/candidates/{c.Id}"))!;
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId); var db = scope.Services.GetRequiredService<AppDbContext>();
            var tables = new[] { "PositionCorrections", "CandidateCorrections", "InterviewNotes", "CandidateEvaluations", "CandidateEvidenceRevisions" };
            foreach (var table in tables) { var constraintSql = $"ALTER TABLE [{table}] WITH NOCHECK ADD CONSTRAINT [CK_Test_Recruitment_{table}] CHECK ([GuidanceSnapshotJson] IS NULL)"; await db.Database.ExecuteSqlRawAsync(constraintSql); }
            try
            {
                (await client.PutAsJsonAsync($"/api/positions/{pos.Id}", new { expectedVersion = pos.Version, title = "Değişmemeli", openCount = 1, isOpen = true, reason = "Kaynak", guidanceContentRevisionId = preparation.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await client.PutAsJsonAsync($"/api/candidates/{c.Id}", RecruitmentCorrection(c, preparation.Content.Id))).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/notes", new { noteType = "İK Görüşmesi", text = "Değişmemeli", guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/evaluations", new { criterion = "SQL", score = 2, guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/notes/{note.Id}/revisions", new { expectedCandidateVersion = c.Version, expectedRecordVersion = 1, noteType = "İK Görüşmesi", text = "Yeni", reason = "Kaynak", guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await client.PostAsJsonAsync($"/api/candidates/{c.Id}/evaluations/{evaluation.Id}/revisions", new { expectedCandidateVersion = c.Version, expectedRecordVersion = 1, criterion = "SQL", score = 2, maxScore = 5, reason = "Kaynak", guidanceContentRevisionId = review.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await client.GetFromJsonAsync<CandidateDetailDto>($"/api/candidates/{c.Id}"))!.Should().BeEquivalentTo(c);
                (await db.Positions.AsNoTracking().SingleAsync()).Version.Should().Be(pos.Version);
                (await db.PositionCorrections.CountAsync()).Should().Be(0); (await db.CandidateCorrections.CountAsync()).Should().Be(0); (await db.CandidateEvidenceRevisions.CountAsync()).Should().Be(0);
                (await OwnOperationsAsync(client)).Should().BeEmpty();
            }
            finally { foreach (var table in tables) { var constraintSql = $"ALTER TABLE [{table}] DROP CONSTRAINT [CK_Test_Recruitment_{table}]"; await db.Database.ExecuteSqlRawAsync(constraintSql); } }
        }
        finally { await CleanupAsync("recruitment-candidate-create"); await CleanupAsync("recruitment-first-review"); }
    }
}
