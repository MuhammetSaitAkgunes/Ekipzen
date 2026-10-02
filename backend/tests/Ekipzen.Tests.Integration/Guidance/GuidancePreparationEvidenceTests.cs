using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Recruitment;
using Ekipzen.Application.Features.Settings;
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
    private static object PreparationCompany(CompanyProfileDto current, string name, int? source = null, string? token = null)
        => new { name, current.Website, current.SystemEmail, current.Phone, current.HeadquartersAddress,
            expectedRecordToken = token ?? current.RecordToken, guidanceContentRevisionId = source };

    [Fact]
    public async Task PreparationEvidence_CompanyDepartmentActualChangesNoOpAndConcurrentOldDraft()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            var profileSource = await PublishOperationSourceAsync(author, reviewer, "company", "company-profile");
            var departmentSource = await PublishOperationSourceAsync(author, reviewer, "company", "departments");
            (await author.PutAsJsonAsync("/api/settings/company", new { name = "İlk şirket adı" })).StatusCode.Should().Be(HttpStatusCode.OK);
            var current = (await author.GetFromJsonAsync<SettingsDto>("/api/settings"))!.Company;
            (await author.PutAsJsonAsync("/api/settings/company", PreparationCompany(current, current.Name, profileSource.Content.Id))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            (await author.PutAsJsonAsync("/api/settings/company", new { name = "Kaynaklı şirket", guidanceContentRevisionId = profileSource.Content.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var body = PreparationCompany(current, "Kaynaklı şirket", profileSource.Content.Id);
            var concurrent = await Task.WhenAll(author.PutAsJsonAsync("/api/settings/company", body), author.PutAsJsonAsync("/api/settings/company", body));
            concurrent.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            var updated = (await author.GetFromJsonAsync<SettingsDto>("/api/settings"))!.Company;
            updated.Name.Should().Be("Kaynaklı şirket"); updated.RecordToken.Should().NotBe(current.RecordToken);
            (await author.PutAsJsonAsync("/api/settings/company", PreparationCompany(current, "Eski taslak", profileSource.Content.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var companyProof = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject.Evidence;
            companyProof.PreparationChanges!.ChangedFields.Should().BeEquivalentTo("Şirket adı");
            (await author.PostAsJsonAsync("/api/departments", new { name = "Öğrenme birimi", guidanceContentRevisionId = profileSource.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var response = await author.PostAsJsonAsync("/api/departments", new { name = "Öğrenme birimi", code = "OG", guidanceContentRevisionId = departmentSource.Content.Id });
            response.StatusCode.Should().Be(HttpStatusCode.Created); var department = (await response.Content.ReadFromJsonAsync<DepartmentDto>())!;
            var noOp = new { name = department.Name, code = department.Code, expectedRecordToken = department.RecordToken, guidanceContentRevisionId = departmentSource.Content.Id };
            (await author.PutAsJsonAsync($"/api/departments/{department.Id}", noOp)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().HaveCount(2);
            var change = new { name = "Düzeltilmiş birim", code = department.Code, expectedRecordToken = department.RecordToken, guidanceContentRevisionId = departmentSource.Content.Id };
            concurrent = await Task.WhenAll(author.PutAsJsonAsync($"/api/departments/{department.Id}", change), author.PutAsJsonAsync($"/api/departments/{department.Id}", change));
            concurrent.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            var own = await OwnOperationsAsync(author); own.Should().HaveCount(3);
            own.Single(x => x.Evidence.OperationKey == "department-update").Evidence.PreparationChanges!.ChangedFields.Should().BeEquivalentTo("Departman adı");
            own.Should().OnlyContain(x => x.IsCurrent);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            (await OwnOperationsAsync(await AuthedClientAsync("globex-admin@globex.local"))).Should().BeEmpty();
            var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
            (await manager.PutAsJsonAsync("/api/settings/company", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await manager.PostAsJsonAsync("/api/departments", new { name = "Yasak" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally { await CleanupAsync("company-company-profile"); await CleanupAsync("company-departments"); }
    }

    [Fact]
    public async Task PreparationEvidence_PositionCandidateSourceOwnerExportAndRealDailyTask()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0]; var reviewer = team.Clients[1];
        const string key = "recruitment-candidate-create";
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "recruitment", "candidate-create");
            var firstReview = await PublishOperationSourceAsync(author, reviewer, "recruitment", "first-review");
            (await author.PostAsJsonAsync("/api/positions", new { title = "Yanlış kaynak", guidanceContentRevisionId = firstReview.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await author.PostAsJsonAsync("/api/candidates", new { name = "Yanlış kaynak", appliedRole = "Uzman", guidanceContentRevisionId = 0 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var ordinary = await author.PostAsJsonAsync("/api/candidates", new { name = "Olağan aday", appliedRole = "Uzman" });
            ordinary.StatusCode.Should().Be(HttpStatusCode.Created); (await OwnOperationsAsync(author)).Should().BeEmpty();
            var positionResponse = await author.PostAsJsonAsync("/api/positions", new { title = "Uzman", openCount = 2, guidanceContentRevisionId = source.Content.Id });
            positionResponse.StatusCode.Should().Be(HttpStatusCode.Created); var position = (await positionResponse.Content.ReadFromJsonAsync<PositionDto>())!;
            var candidateResponse = await author.PostAsJsonAsync("/api/candidates", new { name = "Kaynaklı aday", appliedRole = "Uzman", positionId = position.Id, guidanceContentRevisionId = source.Content.Id });
            candidateResponse.StatusCode.Should().Be(HttpStatusCode.Created); var candidate = (await candidateResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            candidate.Score.Should().BeNull();
            var own = await OwnOperationsAsync(author); own.Should().HaveCount(2); own.Should().OnlyContain(x => x.IsCurrent);
            own.Single(x => x.Evidence.OperationKey == "candidate-create").Evidence.ResourceId.Should().Be(candidate.Id);
            own.Single(x => x.Evidence.OperationKey == "position-create").Evidence.ResourceId.Should().Be(position.Id);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            (await OwnOperationsAsync(await AuthedClientAsync("globex-admin@globex.local"))).Should().BeEmpty();
            var tasks = (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!;
            tasks.Should().Contain(x => x.Key == $"candidate-review:{candidate.Id}" && x.DueDate == null);
            using var export = JsonDocument.Parse(await (await author.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceOperations").GetArrayLength().Should().Be(2);
            export.RootElement.GetProperty("guidanceContentReadings").GetArrayLength().Should().Be(0);
            await ChangeAsync(reviewer, source, "retire", "Teslim örneği kaldırıldı");
            (await OwnOperationsAsync(author)).Should().OnlyContain(x => !x.IsCurrent);
            (await author.PostAsJsonAsync("/api/candidates", new { name = "Eski kaynak", appliedRole = "Uzman", guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            (await db.Candidates.CountAsync()).Should().Be(2);
            var sql = new AddGuidancePreparationEvidence().DownOperations.OfType<SqlOperation>().Single().Sql;
            (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(sql))).Number.Should().Be(51000);
        }
        finally { await CleanupAsync(key); await CleanupAsync("recruitment-first-review"); }
    }

    [Fact]
    public async Task PreparationEvidence_FailedProofRollsBackAllFiveBusinessWrites()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            var companySource = await PublishOperationSourceAsync(author, reviewer, "company", "company-profile");
            var departmentSource = await PublishOperationSourceAsync(author, reviewer, "company", "departments");
            var candidateSource = await PublishOperationSourceAsync(author, reviewer, "recruitment", "candidate-create");
            (await author.PutAsJsonAsync("/api/settings/company", new { name = "İlk şirket adı" })).StatusCode.Should().Be(HttpStatusCode.OK);
            var current = (await author.GetFromJsonAsync<SettingsDto>("/api/settings"))!.Company;
            var department = (await (await author.PostAsJsonAsync("/api/departments", new { name = "Korunacak birim" })).Content.ReadFromJsonAsync<DepartmentDto>())!;
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePreparationEvidences] WITH NOCHECK ADD CONSTRAINT [CK_Test_PreparationFailure] CHECK ([Id] < 0)");
            try
            {
                (await author.PutAsJsonAsync("/api/settings/company", PreparationCompany(current, "Kaydedilmemeli", companySource.Content.Id))).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await author.PostAsJsonAsync("/api/departments", new { name = "Yeni birim", guidanceContentRevisionId = departmentSource.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await author.PutAsJsonAsync($"/api/departments/{department.Id}", new { name = "Değişmemeli", expectedRecordToken = department.RecordToken, guidanceContentRevisionId = departmentSource.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await author.PostAsJsonAsync("/api/positions", new { title = "Kaydedilmemeli", guidanceContentRevisionId = candidateSource.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await author.PostAsJsonAsync("/api/candidates", new { name = "Kaydedilmemeli", appliedRole = "Uzman", guidanceContentRevisionId = candidateSource.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await author.GetFromJsonAsync<SettingsDto>("/api/settings"))!.Company.Should().BeEquivalentTo(current);
                (await db.Departments.AsNoTracking().SingleAsync()).Name.Should().Be(department.Name);
                (await db.Candidates.CountAsync()).Should().Be(0); (await db.CandidateHistory.CountAsync()).Should().Be(0);
                (await db.Positions.CountAsync()).Should().Be(0); (await db.GuidancePreparationEvidences.CountAsync()).Should().Be(0);
            }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePreparationEvidences] DROP CONSTRAINT [CK_Test_PreparationFailure]"); }
        }
        finally { await CleanupAsync("company-company-profile"); await CleanupAsync("company-departments"); await CleanupAsync("recruitment-candidate-create"); }
    }
}
