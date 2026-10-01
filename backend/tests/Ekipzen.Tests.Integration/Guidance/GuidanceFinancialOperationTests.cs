using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Offboarding;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Domain.Entities.Attendance;
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
    private static readonly JsonSerializerOptions FinancialJsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    [Fact]
    public async Task FinancialEvidence_PayrollRealApprovalsAreAtomicAndKeepDistinctSources()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        string[] keys = ["payroll-period-create", "payroll-approval"];
        try
        {
            var createSource = await PublishOperationSourceAsync(author, reviewer, "payroll", "period-create");
            var approval = await PublishOperationSourceAsync(author, reviewer, "payroll", "approval");
            (await author.PostAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.OK);
            var employee = await CreateProofEmployeeAsync(author);
            using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
            {
                var db = scope.Services.GetRequiredService<IApplicationDbContext>();
                var settings = await db.PayrollSettings.SingleAsync();
                // Test fixture only; no production expert approval is inferred.
                settings.Status = PayrollSettingsStatus.Approved;
                settings.ApprovedAtUtc = DateTime.UtcNow; settings.ApprovedByUserId = "fixture";
                for (var day = new DateOnly(2026, 10, 1); day.Month == 10; day = day.AddDays(1))
                    if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                        db.AttendanceRecords.Add(new AttendanceRecord { EmployeeId = employee.Id, WorkDate = day,
                            CheckIn = new TimeOnly(9, 0), CheckOut = new TimeOnly(18, 0), BreakMinutes = 60, WorkedMinutes = 480 });
                await db.SaveChangesAsync(CancellationToken.None);
            }
            (await author.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 10, guidanceContentRevisionId = approval.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            var body = new { year = 2026, month = 10, guidanceContentRevisionId = createSource.Content.Id };
            var created = await Task.WhenAll(author.PostAsJsonAsync("/api/payroll/periods", body), author.PostAsJsonAsync("/api/payroll/periods", body));
            created.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
            var period = (await created.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(FinancialJsonOptions))!;
            var row = period.Rows.Should().ContainSingle().Subject;
            var periodPath = $"/api/payroll/periods/{period.Id}";
            var rowPath = periodPath + $"/rows/{row.Id}";
            var sourceBody = new { guidanceContentRevisionId = approval.Content.Id };
            (await author.PostAsJsonAsync(rowPath + "/approve", sourceBody)).StatusCode.Should().Be(HttpStatusCode.Conflict, "missing inputs cannot create approval evidence");
            (await author.PostAsJsonAsync(periodPath + "/submit", sourceBody)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(author)).Should().ContainSingle();
            (await author.PutAsJsonAsync(rowPath, new { grossSalary = 33030, statutoryProfile = "domesticPrivateSectorStandardFourA", workedDays = 30,
                sgkPremiumDaysConfirmed = true, overtimeHours = 0, overtimeTreatmentConfirmed = true, disabilityReliefType = "none",
                otherIncomeTaxBaseDeductionsConfirmedNone = true, cumulativeIncomeTaxBaseConfirmed = true, ibanComplete = true })).StatusCode.Should().Be(HttpStatusCode.OK);
            var approvals = await Task.WhenAll(author.PostAsJsonAsync(rowPath + "/approve", sourceBody), author.PostAsJsonAsync(rowPath + "/approve", sourceBody));
            approvals.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            (await author.PostAsJsonAsync(periodPath + "/submit", sourceBody)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.PostAsJsonAsync(periodPath + "/submit", sourceBody)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var history = await OwnOperationsAsync(author); history.Should().HaveCount(3);
            history.Select(x => x.Evidence.OperationKey).Should().BeEquivalentTo(["payroll-period-create", "payroll-row-approve", "payroll-period-submit"]);
            history.Should().Contain(x => x.Evidence.OperationKey == "payroll-row-approve" && x.Evidence.ResourceId == row.Id && x.Evidence.Content.ContentRevisionId == approval.Content.Id);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            (await OwnOperationsAsync(await AuthedClientAsync("globex-admin@globex.local"))).Should().BeEmpty();
            using var guardScope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var guardDb = guardScope.Services.GetRequiredService<AppDbContext>();
            var sql = new AddGuidanceFinancialOperationEvidence().DownOperations.OfType<SqlOperation>().Single().Sql;
            Func<Task> guard = () => guardDb.Database.ExecuteSqlRawAsync(sql);
            (await guard.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
            (await OwnOperationsAsync(author)).Should().HaveCount(3);
        }
        finally { foreach (var key in keys) await CleanupAsync(key); }
    }

    [Fact]
    public async Task FinancialEvidence_OffboardingCompletionAndReversalKeepVersionsAndRejectDuplicates()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        string[] keys = ["offboarding-completion", "offboarding-reversal-review"];
        try
        {
            var completion = await PublishOperationSourceAsync(author, reviewer, "offboarding", "completion");
            var reversal = await PublishOperationSourceAsync(author, reviewer, "offboarding", "reversal-review");
            var employee = await CreateProofEmployeeAsync(author);
            var draftResponse = await author.PutAsJsonAsync($"/api/employees/{employee.Id}/offboarding-draft", new {
                terminationDate = "2026-10-01", exitCode = "TEST-03", exitReason = "Gerçek dosya testi", terminationReason = "employee-resignation",
                noticeHandling = "not-applicable", settlementReference = "Test uzman hesap belgesi", settlementNotes = "Sıfır haklar test fixture'ıdır." });
            draftResponse.StatusCode.Should().Be(HttpStatusCode.OK, await draftResponse.Content.ReadAsStringAsync());
            var draft = (await draftResponse.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
            (await OwnOperationsAsync(author)).Should().BeEmpty("draft is not completion");
            var path = $"/api/employees/{employee.Id}/offboarding/{draft.Id}";
            (await author.PostAsJsonAsync(path + "/complete", new { expectedVersion = draft.Version, guidanceContentRevisionId = reversal.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var body = new { expectedVersion = draft.Version, guidanceContentRevisionId = completion.Content.Id };
            var completed = await Task.WhenAll(author.PostAsJsonAsync(path + "/complete", body), author.PostAsJsonAsync(path + "/complete", body));
            completed.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var item = (await completed.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
            (await author.PostAsJsonAsync(path + "/reverse", new { expectedVersion = draft.Version, reason = "Bayat sürüm", guidanceContentRevisionId = reversal.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(author)).Should().ContainSingle();
            var reversed = await author.PostAsJsonAsync(path + "/reverse", new { expectedVersion = item.Version, reason = "Hatalı tarih doğrulandı", guidanceContentRevisionId = reversal.Content.Id });
            reversed.StatusCode.Should().Be(HttpStatusCode.OK, await reversed.Content.ReadAsStringAsync());
            (await author.PostAsJsonAsync(path + "/reverse", new { expectedVersion = item.Version, reason = "Tekrar", guidanceContentRevisionId = reversal.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var history = await OwnOperationsAsync(author); history.Should().HaveCount(2);
            history.Should().Contain(x => x.Evidence.OperationKey == "offboarding-complete" && x.Evidence.Outcome == "completed" && x.Evidence.Content.ContentRevisionId == completion.Content.Id);
            history.Should().Contain(x => x.Evidence.OperationKey == "offboarding-reverse" && x.Evidence.Outcome == "reversed" && x.Evidence.Content.ContentRevisionId == reversal.Content.Id);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.GuidanceOffboardingEvidences.Select(x => x.CaseVersion).ToListAsync()).Should().BeEquivalentTo([item.Version, item.Version + 1]);
            // Remove dependent identity queue only in this disposable test tenant to test the proof cascade.
            db.IdentityProvisioningOperations.RemoveRange(await db.IdentityProvisioningOperations.Where(x => x.OffboardingCaseId == draft.Id).ToListAsync());
            await db.SaveChangesAsync(CancellationToken.None);
            db.EmployeeOffboardingCases.Remove(await db.EmployeeOffboardingCases.SingleAsync(x => x.Id == draft.Id));
            await db.SaveChangesAsync(CancellationToken.None);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
        }
        finally { foreach (var key in keys) await CleanupAsync(key); }
    }
}
