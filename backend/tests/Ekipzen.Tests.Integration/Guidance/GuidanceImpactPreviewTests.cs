using System.Net;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Offboarding;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Application.Features.Payroll.Periods;
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
    [Fact]
    public async Task ImpactPreview_EmployeeRetryIsAtomicScopedAndKeepsOneRealEvidence()
    {
        var team = await TeamAsync(); await using var host = team.Host; var owner = team.Clients[0];
        const string key = "personnel-entry-tab-is";
        try
        {
            var source = await PublishOperationSourceAsync(owner, team.Clients[1], "personnel-entry", "tab-is");
            var department = (await (await owner.PostAsJsonAsync("/api/departments", new { name = "Tekrar testi" })).Content.ReadFromJsonAsync<DepartmentDto>())!;
            var requestId = Guid.NewGuid();
            var input = new { firstName = "Tek", lastName = "Kayıt", title = "Uzman", departmentId = department.Id,
                hireDate = "2026-01-01", creationRequestId = requestId, guidanceContentRevisionId = source.Content.Id };
            (await owner.PostAsJsonAsync("/api/employees", new { input.firstName, input.lastName, input.title, input.departmentId, input.hireDate }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var responses = await Task.WhenAll(owner.PostAsJsonAsync("/api/employees", input), owner.PostAsJsonAsync("/api/employees", input));
            responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.Created);
            var first = (await responses[0].Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
            (await responses[1].Content.ReadFromJsonAsync<EmployeeDetailDto>())!.Id.Should().Be(first.Id);
            var replay = await owner.PostAsJsonAsync("/api/employees", input);
            (await replay.Content.ReadFromJsonAsync<EmployeeDetailDto>())!.Id.Should().Be(first.Id);
            (await OwnOperationsAsync(owner)).Should().ContainSingle(x => x.Evidence.ResourceId == first.Id);
            (await owner.PostAsJsonAsync("/api/employees", new { input.firstName, lastName = "Değişti", input.title, input.departmentId, input.hireDate,
                input.creationRequestId, input.guidanceContentRevisionId })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var resultPath = $"/api/employees/creation-results/{requestId}";
            (await owner.GetFromJsonAsync<EmployeeDetailDto>(resultPath))!.Id.Should().Be(first.Id);
            var other = await team.Clients[1].GetAsync(resultPath);
            // A null result may be formatted as 204 by MVC; neither representation leaks the record.
            (other.StatusCode is HttpStatusCode.NoContent || await other.Content.ReadAsStringAsync() == "null").Should().BeTrue();
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            (await db.EmployeeCreationEvidences.CountAsync(x => x.CreationRequestId == requestId)).Should().Be(1);
            (await db.EmployeeEmploymentPeriods.CountAsync(x => x.EmployeeId == first.Id)).Should().Be(1);
            var sql = new AddEmployeeCreationRequestSafety().DownOperations.OfType<SqlOperation>().Single().Sql;
            Func<Task> rollback = () => db.Database.ExecuteSqlRawAsync(sql);
            (await rollback.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task ImpactPreview_PayrollCreationUsesReviewedDraftAndRejectsChangedEligibility()
    {
        var team = await TeamAsync(); await using var host = team.Host; var owner = team.Clients[0];
        (await owner.InitializeReviewedSetupAsync("/api/setup")).StatusCode.Should().Be(HttpStatusCode.OK);
        var employee = await CreateProofEmployeeAsync(owner);
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>(); var settings = await db.PayrollSettings.SingleAsync();
            settings.Status = PayrollSettingsStatus.Approved; settings.ApprovedAtUtc = DateTime.UtcNow; settings.ApprovedByUserId = "fixture";
            await db.SaveChangesAsync(CancellationToken.None);
        }
        const string path = "/api/payroll/periods/creation-preview?year=2026&month=10";
        var preview = (await owner.GetFromJsonAsync<PayrollPeriodCreationPreviewDto>(path, FinancialJsonOptions))!;
        preview.Rows.Should().ContainSingle(x => x.EmployeeId == employee.Id && x.CompensationPackageId == null && x.GrossSalary == 0);
        var repeat = (await owner.GetFromJsonAsync<PayrollPeriodCreationPreviewDto>(path, FinancialJsonOptions))!;
        repeat.Token.Should().Be(preview.Token);
        (await OwnOperationsAsync(owner)).Should().BeEmpty();
        (await owner.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 10 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.PayrollPeriods.CountAsync()).Should().Be(0);
            (await db.Employees.SingleAsync(x => x.Id == employee.Id)).HireDate = new DateOnly(2026, 11, 1);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        (await owner.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 10, expectedPreviewToken = preview.Token })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var fresh = (await owner.GetFromJsonAsync<PayrollPeriodCreationPreviewDto>(path, FinancialJsonOptions))!;
        fresh.Rows.Should().BeEmpty();
        var created = await owner.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 10, expectedPreviewToken = fresh.Token });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(FinancialJsonOptions))!.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ImpactPreview_OffboardingReadsDoNotMutateAndPinCompletionAndReversalSources()
    {
        var team = await TeamAsync(); await using var host = team.Host; var owner = team.Clients[0];
        var employee = await CreateProofEmployeeAsync(owner);
        var draftResponse = await owner.PutAsJsonAsync($"/api/employees/{employee.Id}/offboarding-draft", new {
            terminationDate = "2026-09-30", exitCode = "TEST", exitReason = "Test incelemesi", terminationReason = "employee-resignation",
            noticeHandling = "not-applicable", unusedLeaveDays = 0m, unusedLeavePayment = 0m, severancePayment = 0m,
            noticePayment = 0m, settlementReference = "Uzman test kaynağı" });
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = (await draftResponse.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
        var path = $"/api/employees/{employee.Id}/offboarding/{draft.Id}";
        var preview = (await owner.GetFromJsonAsync<EmployeeOffboardingImpactPreviewDto>(path + "/completion-preview"))!;
        preview.CanApply.Should().BeTrue(); preview.TeamTransfers.Should().BeEmpty(); preview.AccessOperations.Should().HaveCount(2);
        (await owner.GetFromJsonAsync<EmployeeOffboardingImpactPreviewDto>(path + "/completion-preview"))!.Token.Should().Be(preview.Token);
        (await owner.PostAsJsonAsync(path + "/complete", new { expectedVersion = draft.Version })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.Employees.SingleAsync(x => x.Id == employee.Id)).Status.Should().Be(EmployeeStatus.Active);
            (await db.IdentityProvisioningOperations.CountAsync()).Should().Be(0);
            (await db.Employees.SingleAsync(x => x.Id == employee.Id)).FirstName = "Güncel";
            await db.SaveChangesAsync(CancellationToken.None);
        }
        (await owner.PostAsJsonAsync(path + "/complete", new { expectedVersion = draft.Version, expectedPreviewToken = preview.Token })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await OwnOperationsAsync(owner)).Should().BeEmpty();
        var completedResponse = await owner.ApplyReviewedOffboardingAsync(path + "/complete", new { expectedVersion = draft.Version });
        completedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = (await completedResponse.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
        var reversal = (await owner.GetFromJsonAsync<EmployeeOffboardingImpactPreviewDto>(path + "/reversal-preview"))!;
        reversal.CanApply.Should().BeTrue(); reversal.AccessOperations.Should().Contain(x => x.Operation.Contains("açılacak"));
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.Employees.SingleAsync(x => x.Id == employee.Id)).Status.Should().Be(EmployeeStatus.Passive);
            (await db.Employees.SingleAsync(x => x.Id == employee.Id)).FirstName = "Tekrar değişti";
            await db.SaveChangesAsync(CancellationToken.None);
        }
        (await owner.PostAsJsonAsync(path + "/reverse", new { expectedVersion = completed.Version, reason = "Hata düzeltmesi", expectedPreviewToken = reversal.Token })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.ApplyReviewedOffboardingAsync(path + "/reverse", new { expectedVersion = completed.Version, reason = "Hata düzeltmesi" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
