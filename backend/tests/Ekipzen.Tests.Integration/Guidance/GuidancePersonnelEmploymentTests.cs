using System.Net;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Guidance;
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
    private static object EmploymentSalaryBody(DateOnly date, int? source = null) => new {
        effectiveFrom = date, monthlyGrossSalary = 50000, regularPremium = 0, roadAllowance = 0,
        mealAllowance = 0, otherRegularBenefits = 0, sourceReference = "Test ücret kararı", guidanceContentRevisionId = source,
    };

    [Fact]
    public async Task EmploymentEvidence_RehirePreservesOldHistoryAndReconcilesNewPeriodSalary()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        string[] keys = ["personnel-rehire", "personnel-entry-tab-mali"];
        try
        {
            var rehire = await PublishOperationSourceAsync(author, reviewer, "personnel", "rehire");
            var salary = await PublishOperationSourceAsync(author, reviewer, "personnel-entry", "tab-mali");
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            var employee = await CreateProofEmployeeAsync(author);
            var oldFoundation = (await (await author.PostAsJsonAsync($"/api/employees/{employee.Id}/compensation-packages", EmploymentSalaryBody(employee.HireDate))).Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!;
            var oldPackage = oldFoundation.CompensationPackages.Single();
            var completed = await OffboardingTestHelper.CompleteAsync(author, employee.Id, today.AddDays(-2));
            var closedFoundation = (await author.GetFromJsonAsync<EmployeeEmploymentFoundationDto>($"/api/employees/{employee.Id}/employment-foundation"))!;
            var closedPackage = closedFoundation.CompensationPackages.Single();
            var path = $"/api/employees/{employee.Id}/rehire";
            var body = new { rehireDate = today.AddDays(-1), contractType = "fixed-term", reason = "Yeni test sözleşmesi", guidanceContentRevisionId = rehire.Content.Id };
            (await author.PostAsJsonAsync(path, withSource(salary.Content.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await author.PostAsJsonAsync(path, new { body.rehireDate, body.contractType, guidanceContentRevisionId = rehire.Content.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var responses = await Task.WhenAll(author.PostAsJsonAsync(path, body), author.PostAsJsonAsync(path, body));
            responses.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var foundation = (await responses.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!;
            foundation.EmploymentPeriods.Should().HaveCount(2);
            var newPeriod = foundation.EmploymentPeriods.Single(x => x.EndDate == null);
            newPeriod.StartDate.Should().Be(body.rehireDate);
            foundation.CompensationPackages.Should().ContainSingle().Which.EmploymentPeriodId.Should().Be(oldPackage.EmploymentPeriodId);
            var proof = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject.Evidence;
            proof.OperationKey.Should().Be("employee-rehire"); proof.ResourceId.Should().Be(newPeriod.Id);
            proof.PersonnelChanges!.EmploymentPeriodId.Should().Be(newPeriod.Id); proof.PersonnelChanges.Reason.Should().Be(body.reason);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            var key = $"employee-compensation:{employee.Id}";
            var task = (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().ContainSingle(x => x.Key == key).Subject;
            task.Reason.Should().Contain("Önceki dönemin ücret paketi"); task.DueDate.Should().BeNull();
            (await author.PostAsJsonAsync($"/api/employees/{employee.Id}/compensation-packages", EmploymentSalaryBody(today.AddDays(10), salary.Content.Id))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().Contain(x => x.Key == key, "ileri tarihli paket bugünkü hazırlığı kapatmaz");
            (await author.PostAsJsonAsync($"/api/employees/{employee.Id}/compensation-packages", EmploymentSalaryBody(today, salary.Content.Id))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().NotContain(x => x.Key == key);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var oldCase = await db.EmployeeOffboardingCases.AsNoTracking().SingleAsync(x => x.Id == completed.Id);
            oldCase.Version.Should().Be(completed.Version); oldCase.Status.Should().Be(EmployeeOffboardingStatus.Completed);
            var preserved = await db.EmployeeCompensationPackages.AsNoTracking().SingleAsync(x => x.Id == oldPackage.Id);
            preserved.Version.Should().Be(closedPackage.Version);
            preserved.EffectiveTo.Should().Be(closedPackage.EffectiveTo);
            preserved.MonthlyGrossSalary.Should().Be(closedPackage.MonthlyGrossSalary);
            (await db.LeaveLedgerEntries.CountAsync(x => x.EmploymentPeriodId == newPeriod.Id)).Should().Be(0, "eski izin yeni döneme taşınmaz");
            object withSource(int id) => new { body.rehireDate, body.contractType, body.reason, guidanceContentRevisionId = id };
        }
        finally { foreach (var key in keys) await CleanupAsync(key); }
    }

    [Fact]
    public async Task EmploymentEvidence_SalaryCorrectionsKeepVersionsAndDateChangesReopenDailyWork()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1]; const string sourceKey = "personnel-entry-tab-mali";
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "personnel-entry", "tab-mali");
            var employee = await CreateProofEmployeeAsync(author); var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            var path = $"/api/employees/{employee.Id}/compensation-packages";
            var created = await author.PostAsJsonAsync(path, EmploymentSalaryBody(today, source.Content.Id));
            created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
            var package = (await created.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!.CompensationPackages.Single();
            (await author.PostAsJsonAsync(path, EmploymentSalaryBody(today, source.Content.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var correction = new CorrectEmployeeCompensationPackageCommand(employee.Id, package.Id, package.Version, 55000, 0, 0, 0, 0, "Yeni test kaynağı", "Tutar doğrulandı", source.Content.Id);
            var responses = await Task.WhenAll(author.PutAsJsonAsync($"{path}/{package.Id}", correction), author.PutAsJsonAsync($"{path}/{package.Id}", correction));
            responses.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            (await author.PutAsJsonAsync($"{path}/{package.Id}", correction with { ExpectedVersion = 2 })).StatusCode.Should().Be(HttpStatusCode.Conflict, "değişiklik yoksa kanıt oluşmaz");
            var proofs = await OwnOperationsAsync(author); proofs.Should().HaveCount(2);
            var proof = proofs.Single(x => x.Evidence.OperationKey == "employee-compensation-correct").Evidence;
            proof.PersonnelChanges!.FromVersion.Should().Be(1); proof.PersonnelChanges.ToVersion.Should().Be(2);
            proof.PersonnelChanges.ChangedFields.Should().BeEquivalentTo("Ücret / Aylık brüt maaş", "Ücret / Kaynak referansı");
            var preview = await author.PostAsJsonAsync($"{path}/{package.Id}/date-correction-preview", new { proposedEffectiveFrom = today.AddDays(7) });
            preview.StatusCode.Should().Be(HttpStatusCode.OK);
            var checkedDate = (await preview.Content.ReadFromJsonAsync<CompensationDateCorrectionPreviewDto>())!; checkedDate.CanApply.Should().BeTrue();
            (await OwnOperationsAsync(author)).Should().HaveCount(2);
            var dateBody = new { proposedEffectiveFrom = today.AddDays(7), expectedPackageVersion = 2, expectedPreviousPackageId = (int?)null,
                expectedPreviousPackageVersion = (int?)null, reason = "Tarih kaynağı doğrulandı", guidanceContentRevisionId = source.Content.Id };
            (await author.PutAsJsonAsync($"{path}/{package.Id}/effective-date", dateBody)).StatusCode.Should().Be(HttpStatusCode.OK);
            var dailyKey = $"employee-compensation:{employee.Id}";
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().Contain(x => x.Key == dailyKey);
            (await author.PutAsJsonAsync($"{path}/{package.Id}/effective-date", dateBody)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await author.PutAsJsonAsync($"{path}/{package.Id}/effective-date", new { proposedEffectiveFrom = today, expectedPackageVersion = 3,
                expectedPreviousPackageId = (int?)null, expectedPreviousPackageVersion = (int?)null, reason = "İkinci tarih doğrulaması", guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().NotContain(x => x.Key == dailyKey);
            var history = await OwnOperationsAsync(author); history.Should().HaveCount(4);
            history.Where(x => x.Evidence.OperationKey == "employee-compensation-date").Select(x => x.Evidence.EvidenceKey).Should().OnlyHaveUniqueItems();
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var sql = new AddPersonnelEmploymentEvidence().DownOperations.OfType<SqlOperation>().Single().Sql;
            Func<Task> down = () => db.Database.ExecuteSqlRawAsync(sql);
            (await down.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
        }
        finally { await CleanupAsync(sourceKey); }
    }

    [Fact]
    public async Task EmploymentEvidence_FailedEvidenceInsertRollsBackEveryEmploymentAndSalaryWrite()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0];
        var employee = await CreateProofEmployeeAsync(author); var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var path = $"/api/employees/{employee.Id}/compensation-packages";
        var original = (await (await author.PostAsJsonAsync(path, EmploymentSalaryBody(employee.HireDate))).Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!.CompensationPackages.Single();
        using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
        var db = scope.Services.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePersonnelEvidences] WITH NOCHECK ADD CONSTRAINT [CK_Test_EmploymentEvidenceFailure] CHECK ([Id] < 0)");
        try
        {
            (await author.PostAsJsonAsync(path, EmploymentSalaryBody(today))).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await author.PutAsJsonAsync($"{path}/{original.Id}", new CorrectEmployeeCompensationPackageCommand(employee.Id, original.Id, original.Version, 55000, 0, 0, 0, 0, "Kaynak", "Gerekçe"))).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await author.PutAsJsonAsync($"{path}/{original.Id}/effective-date", new { proposedEffectiveFrom = employee.HireDate.AddDays(1), expectedPackageVersion = original.Version,
                expectedPreviousPackageId = (int?)null, expectedPreviousPackageVersion = (int?)null, reason = "Tarih denemesi" })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await db.EmployeeCompensationPackages.AsNoTracking().SingleAsync(x => x.Id == original.Id)).Version.Should().Be(original.Version);
            (await db.EmployeeCompensationPackageCorrections.CountAsync()).Should().Be(0); (await db.EmployeeCompensationDateCorrections.CountAsync()).Should().Be(0);
            await OffboardingTestHelper.CompleteAsync(author, employee.Id, today.AddDays(-1));
            var queueCount = await db.IdentityProvisioningOperations.CountAsync();
            (await author.PostAsJsonAsync($"/api/employees/{employee.Id}/rehire", new { rehireDate = today, contractType = "indefinite", reason = "Yeni dönem denemesi" })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await db.Employees.AsNoTracking().SingleAsync(x => x.Id == employee.Id)).Status.Should().Be(EmployeeStatus.Passive);
            (await db.EmployeeEmploymentPeriods.CountAsync()).Should().Be(1);
            (await db.IdentityProvisioningOperations.CountAsync()).Should().Be(queueCount);
            (await db.GuidancePersonnelEvidences.CountAsync()).Should().Be(1);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePersonnelEvidences] DROP CONSTRAINT [CK_Test_EmploymentEvidenceFailure]"); }
    }
}
