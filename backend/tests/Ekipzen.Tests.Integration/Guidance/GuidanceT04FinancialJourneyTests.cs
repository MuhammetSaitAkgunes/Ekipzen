using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Offboarding;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Domain.Entities.Attendance;
using Ekipzen.Domain.Enums;
using Ekipzen.Infrastructure.Persistence;
using Ekipzen.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Data.SqlClient;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    private async Task<(PayrollPeriodDetailDto Period, int EmployeeId)> T04PayrollAsync(HttpClient client, int tenantId)
    {
        (await client.InitializeReviewedSetupAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var employee = await CreateProofEmployeeAsync(client);
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(tenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var settings = await db.PayrollSettings.SingleAsync();
            settings.Status = PayrollSettingsStatus.Approved;
            settings.ApprovedAtUtc = DateTime.UtcNow; settings.ApprovedByUserId = "fixture-only";
            for (var day = new DateOnly(2026, 10, 1); day.Month == 10; day = day.AddDays(1))
                if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                    db.AttendanceRecords.Add(new AttendanceRecord { EmployeeId = employee.Id, WorkDate = day,
                        CheckIn = new TimeOnly(9, 0), CheckOut = new TimeOnly(18, 0), BreakMinutes = 60, WorkedMinutes = 480 });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var response = await client.CreateReviewedPayrollPeriodAsync("/api/payroll/periods", new { year = 2026, month = 10 });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return ((await response.Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(FinancialJsonOptions))!, employee.Id);
    }

    private static object T04RowBody(int? guidance = null, string? revision = null) => new {
        grossSalary = 33030, statutoryProfile = "domesticPrivateSectorStandardFourA", workedDays = 30,
        sgkPremiumDaysConfirmed = true, overtimeHours = 0, overtimeTreatmentConfirmed = true, disabilityReliefType = "none",
        otherIncomeTaxBaseDeductionsConfirmedNone = true, cumulativeIncomeTaxBaseConfirmed = true, ibanComplete = true,
        guidanceContentRevisionId = guidance, expectedRevision = revision,
    };

    [Fact]
    public async Task T04_RowAndCheckProofs_ReconcileChangedTimesheetsWithoutApprovingOrChangingInputs()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        const string key = "payroll-row-review";
        try
        {
            var author = team.Clients[0];
            var source = await PublishOperationSourceAsync(author, team.Clients[1], "payroll", "row-review");
            var (period, employeeId) = await T04PayrollAsync(author, team.TenantId);
            var row = period.Rows.Single(); var path = $"/api/payroll/periods/{period.Id}"; var rowPath = path + $"/rows/{row.Id}";
            var saved = await author.PutReviewedPayrollRowAsync(rowPath, T04RowBody(source.Content.Id));
            saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
            var before = (await saved.Content.ReadFromJsonAsync<PayrollRowDto>(FinancialJsonOptions))!;
            (await author.PostAsJsonAsync(path + "/check", new { guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Select(x => x.Evidence.OperationKey).Should()
                .BeEquivalentTo(["payroll-row-update", "payroll-period-check"]);
            using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
            {
                var db = scope.Services.GetRequiredService<IApplicationDbContext>();
                var attendance = await db.AttendanceRecords.FirstAsync(x => x.EmployeeId == employeeId);
                attendance.OvertimeMinutes += 60; await db.SaveChangesAsync(CancellationToken.None);
            }
            var changed = (await author.GetFromJsonAsync<PayrollPeriodDetailDto>(path, FinancialJsonOptions))!.Rows.Single();
            changed.TimesheetComplete.Should().BeTrue(); changed.TimesheetNeedsReview.Should().BeTrue();
            changed.OvertimeHours.Should().Be(0, "kaynak değişimi kullanıcı girdisini sessizce değiştirmez");
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!
                .Should().Contain(x => x.Key == $"payroll-timesheet-review:{row.Id}");
            (await author.PutAsJsonAsync(rowPath, T04RowBody(source.Content.Id, before.Revision))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await author.ApproveReviewedPayrollRowAsync(rowPath + "/approve", new { })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await author.PostAsJsonAsync(path + "/check", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.GetFromJsonAsync<PayrollPeriodDetailDto>(path, FinancialJsonOptions))!.Rows.Single().TimesheetNeedsReview.Should().BeTrue();
            (await OwnOperationsAsync(author)).Should().HaveCount(2);
            (await author.PutReviewedPayrollRowAsync(rowPath, T04RowBody(source.Content.Id))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!
                .Should().NotContain(x => x.Key == $"payroll-timesheet-review:{row.Id}");
            (await OwnOperationsAsync(team.Clients[1])).Should().BeEmpty();
        }
        finally { await CleanupAsync(key); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T04_OriginalExternalProofs_CloseOnlyTheirKind_KeepHistoryAndRejectRaces(bool offboarding)
    {
        var team = await TeamAsync(); await using var host = team.Host;
        const string key = "daily-external-followup";
        try
        {
            var author = team.Clients[0];
            var source = await PublishOperationSourceAsync(author, team.Clients[1], "daily", "external-followup");
            string path; string prefix; int id;
            if (offboarding)
            {
                var employee = await CreateProofEmployeeAsync(author);
                var response = await author.PutAsJsonAsync($"/api/employees/{employee.Id}/offboarding-draft", new {
                    terminationDate = "2026-10-01", exitCode = "TEST-03", exitReason = "Test dosyası", terminationReason = "employee-resignation",
                    noticeHandling = "not-applicable", settlementReference = "Test uzman kaynağı" });
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                var draft = (await response.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
                path = $"/api/employees/{employee.Id}/offboarding/{draft.Id}";
                (await author.ApplyReviewedOffboardingAsync(path + "/complete", new { expectedVersion = draft.Version })).StatusCode.Should().Be(HttpStatusCode.OK);
                id = draft.Id; prefix = "offboarding";
            }
            else
            {
                var (period, _) = await T04PayrollAsync(author, team.TenantId);
                id = period.Rows.Single().Id; path = $"/api/payroll/periods/{period.Id}/rows/{id}"; prefix = "payroll";
                (await author.PutReviewedPayrollRowAsync(path, T04RowBody())).StatusCode.Should().Be(HttpStatusCode.OK);
                (await author.ApproveReviewedPayrollRowAsync(path + "/approve", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
                (await author.PostAsJsonAsync($"/api/payroll/periods/{period.Id}/submit", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
            }
            path += "/external-reconciliation";
            var initial = (await author.GetFromJsonAsync<FinancialExternalReconciliationDto>(path))!;
            initial.CanRecord.Should().BeTrue(); initial.History.Should().BeEmpty();
            (await OwnOperationsAsync(author)).Should().BeEmpty("okuma işlem kanıtı üretmez");
            var body = new FinancialExternalActionBody("payment", "completed", "Mali sorumlu", "BANK-TEST", new DateOnly(2026, 10, 1),
                100, "Test dış belge", null, true, initial.SourceToken, source.Content.Id);
            (await author.PostAsJsonAsync(path, body with { ExpectedSourceToken = "stale" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var results = await Task.WhenAll(author.PostAsJsonAsync(path, body), author.PostAsJsonAsync(path, body));
            results.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var payment = (await author.GetFromJsonAsync<FinancialExternalReconciliationDto>(path))!.Payment!;
            var tasks = (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!;
            tasks.Should().NotContain(x => x.Key == $"{prefix}-original-external-payment:{id}");
            tasks.Should().Contain(x => x.Key == $"{prefix}-original-external-notification:{id}");
            var pending = body with { Kind = "notification", Outcome = "pending", EvidenceReference = "", ActionDate = null, RecordedAmount = null };
            (await author.PostAsJsonAsync(path, pending)).StatusCode.Should().Be(HttpStatusCode.OK);
            var state = (await author.GetFromJsonAsync<FinancialExternalReconciliationDto>(path))!;
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!
                .Should().Contain(x => x.Key == $"{prefix}-original-external-notification:{id}");
            (await author.PostAsJsonAsync(path, pending with { Outcome = "not-required", EvidenceReference = "EXPERT-TEST",
                Notes = "Uzman gerekçesi", ExpectedLatestRecordId = state.Notification!.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.PostAsJsonAsync(path, body with { Outcome = "pending", EvidenceReference = "", ActionDate = null,
                RecordedAmount = null, Notes = "Hatalı kanıt için tekrar inceleme", ExpectedLatestRecordId = payment.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            var final = (await author.GetFromJsonAsync<FinancialExternalReconciliationDto>(path))!;
            final.OriginalTotal.Should().Be(initial.OriginalTotal); final.History.Should().HaveCount(4);
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!
                .Should().Contain(x => x.Key == $"{prefix}-original-external-payment:{id}");
            (await OwnOperationsAsync(author)).Should().HaveCount(4).And.OnlyContain(x => x.Evidence.Content.ContentRevisionId == source.Content.Id);
            (await OwnOperationsAsync(team.Clients[1])).Should().BeEmpty();
            (await (await AuthedClientAsync("ece.arslan@hrmaster.local")).GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await (await AuthedClientAsync("ahmet.yilmaz@hrmaster.local")).PostAsJsonAsync(path, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await (await AuthedClientAsync("globex-admin@globex.local")).GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var guardScope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var guardDb = guardScope.Services.GetRequiredService<AppDbContext>();
            var downGuard = new AddFinancialExternalAndTimesheetReview().DownOperations.OfType<SqlOperation>().Single().Sql;
            Func<Task> rollback = () => guardDb.Database.ExecuteSqlRawAsync(downGuard);
            (await rollback.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
            (await author.GetFromJsonAsync<FinancialExternalReconciliationDto>(path))!.History.Should().HaveCount(4);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task T04_SettingsDraftAndApproval_PinSourcesAndRejectStaleVersions()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        const string key = "payroll-settings-review";
        try
        {
            var author = team.Clients[0];
            var source = await PublishOperationSourceAsync(author, team.Clients[1], "payroll", "settings-review");
            (await author.InitializeReviewedSetupAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.OK);
            var items = JsonNode.Parse(await (await author.GetAsync("/api/payroll/settings/drafts")).Content.ReadAsStringAsync())!.AsArray();
            var body = items.Single()!.DeepClone().AsObject();
            body["reviewNotes"] = "Yalnız test uzman inceleme kaydı; gerçek uzman kabulü değildir.";
            body["guidanceContentRevisionId"] = source.Content.Id; body["expectedVersion"] = body["version"]!.DeepClone();
            var save = await author.PutAsJsonAsync("/api/payroll/settings", body);
            save.StatusCode.Should().Be(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
            var draft = (await save.Content.ReadFromJsonAsync<PayrollSettingsDto>())!;
            (await author.PutAsJsonAsync("/api/payroll/settings", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await author.PostAsJsonAsync($"/api/payroll/settings/{draft.Id}/approve", new { id = draft.Id,
                expectedVersion = draft.Version, guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            var history = await OwnOperationsAsync(author);
            history.Select(x => x.Evidence.OperationKey).Should().BeEquivalentTo(["payroll-settings-save", "payroll-settings-approve"]);
            history.Should().OnlyContain(x => x.Evidence.ResourceId == draft.Id && x.Evidence.Content.ContentRevisionId == source.Content.Id);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task T04_ExternalProofFailure_RollsBackBusinessRecordAndLearningEvidenceTogether()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        const string key = "daily-external-followup";
        try
        {
            var author = team.Clients[0];
            var source = await PublishOperationSourceAsync(author, team.Clients[1], "daily", "external-followup");
            var (period, _) = await T04PayrollAsync(author, team.TenantId);
            var row = period.Rows.Single(); var path = $"/api/payroll/periods/{period.Id}/rows/{row.Id}";
            (await author.PutReviewedPayrollRowAsync(path, T04RowBody())).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.ApproveReviewedPayrollRowAsync(path + "/approve", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.PostAsJsonAsync($"/api/payroll/periods/{period.Id}/submit", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
            path += "/external-reconciliation";
            var state = (await author.GetFromJsonAsync<FinancialExternalReconciliationDto>(path))!;
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE GuidancePreparationEvidences ADD CONSTRAINT CK_T04_ProofFailure CHECK ([OperationKey] <> N'financial-external-record')");
            try
            {
                var response = await author.PostAsJsonAsync(path, new FinancialExternalActionBody("payment", "pending",
                    "Test sorumlusu", "", null, null, "", null, true, state.SourceToken, source.Content.Id));
                response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await db.FinancialExternalActionRecords.CountAsync()).Should().Be(0);
                (await OwnOperationsAsync(author)).Should().BeEmpty();
            }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE GuidancePreparationEvidences DROP CONSTRAINT CK_T04_ProofFailure"); }
        }
        finally { await CleanupAsync(key); }
    }
}
