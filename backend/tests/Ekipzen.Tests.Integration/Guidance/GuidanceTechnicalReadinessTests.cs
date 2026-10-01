using System.Net;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Compliance;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Application.Features.Payroll.Periods;
using Ekipzen.Application.Features.Setup;
using Ekipzen.Domain.Entities.Leaves;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    [Fact]
    public async Task TechnicalReadiness_SetupPreviewIsReadOnlyAndRejectsChangedInputs()
    {
        var team = await TeamAsync(); await using var host = team.Host; var owner = team.Clients[0];
        var preview = (await owner.GetFromJsonAsync<TenantSetupPreviewDto>("/api/setup/preview"))!;
        preview.LeaveTypes.Should().HaveCount(4); preview.Holidays.Should().HaveCount(14);
        preview.PayrollDraft!.Status.Should().Be("draft");
        (await owner.PostAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.LeaveTypes.CountAsync()).Should().Be(0); (await db.CompanyProfiles.CountAsync()).Should().Be(0);
            db.LeaveTypes.Add(new LeaveType { Code = "Yıllık", Name = "Şirketin korunan türü", RequiresApproval = true });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        (await owner.PostAsJsonAsync("/api/setup", new { expectedPreviewToken = preview.Token })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var fresh = (await owner.GetFromJsonAsync<TenantSetupPreviewDto>("/api/setup/preview"))!;
        fresh.LeaveTypes.Should().HaveCount(3); fresh.Token.Should().NotBe(preview.Token);
        (await owner.PostAsJsonAsync("/api/setup", new { expectedPreviewToken = fresh.Token })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PostAsJsonAsync("/api/setup", new { expectedPreviewToken = fresh.Token })).StatusCode.Should().Be(HttpStatusCode.OK, "repeated completed setup is a no-op");
        (await OwnOperationsAsync(owner)).Should().BeEmpty();
        var tasks = (await owner.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!;
        tasks.Should().NotContain(x => x.Kind == "company-setup"); tasks.Should().ContainSingle(x => x.Kind == "payroll-settings-review");
    }

    [Fact]
    public async Task TechnicalReadiness_ComplianceCorrectionIsAtomicAndRevisionChecked()
    {
        var team = await TeamAsync(); await using var host = team.Host; var owner = team.Clients[0];
        var employee = await CreateProofEmployeeAsync(owner);
        var response = await owner.PostAsJsonAsync("/api/compliance/documents", new { employeeId = employee.Id, documentName = "İlk ad", ownerName = "İlk sorumlu" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var original = (await response.Content.ReadFromJsonAsync<ComplianceDocumentDto>())!;
        var path = $"/api/compliance/documents/{original.Id}";
        (await owner.PutAsJsonAsync(path, new { documentName = "Geçersiz değişiklik", level = "high" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var input = new { documentName = "Doğrulanan ad", dueDate = "2026-10-20", level = "high", ownerName = "Yeni sorumlu", updateOwner = true, expectedRevision = original.Revision };
        var writes = await Task.WhenAll(owner.PutAsJsonAsync(path, input), owner.PutAsJsonAsync(path, input));
        writes.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var current = (await writes.Single(x => x.IsSuccessStatusCode).Content.ReadFromJsonAsync<ComplianceDocumentDto>())!;
        current.Document.Should().Be("Doğrulanan ad"); current.Owner.Should().Be("Yeni sorumlu");
        (await owner.PatchAsJsonAsync(path + "/status", new { status = "Tamamlandı", expectedRevision = original.Revision })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var remove = await owner.PutAsJsonAsync(path, new { documentName = current.Document, dueDate = current.DueDate, level = current.Level, ownerName = (string?)null, updateOwner = true, expectedRevision = current.Revision });
        remove.StatusCode.Should().Be(HttpStatusCode.OK); var cleared = (await remove.Content.ReadFromJsonAsync<ComplianceDocumentDto>())!;
        cleared.Owner.Should().BeNull();
        var noOp = await owner.PutAsJsonAsync(path, new { documentName = cleared.Document, dueDate = cleared.DueDate, level = cleared.Level, ownerName = (string?)null, updateOwner = true, expectedRevision = cleared.Revision });
        (await noOp.Content.ReadFromJsonAsync<ComplianceDocumentDto>())!.Revision.Should().Be(cleared.Revision);
        (await team.Clients[1].GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().Contain(x => x.Key == $"compliance-document:{original.Id}");
    }

    [Fact]
    public async Task TechnicalReadiness_PayrollReviewRejectsStaleInputsAndCreatesNoEvidence()
    {
        var team = await TeamAsync(); await using var host = team.Host; var owner = team.Clients[0];
        (await owner.InitializeReviewedSetupAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        await CreateProofEmployeeAsync(owner);
        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var settings = await db.PayrollSettings.SingleAsync();
            // Fixture-only approval keeps this test independent of seeded payroll states.
            settings.Status = Ekipzen.Domain.Enums.PayrollSettingsStatus.Approved;
            settings.ApprovedAtUtc = DateTime.UtcNow; settings.ApprovedByUserId = "fixture";
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var created = await owner.CreateReviewedPayrollPeriodAsync("/api/payroll/periods", new { year = 2026, month = 10 });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = (await created.Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(FinancialJsonOptions))!;
        var period = detail;
        var row = detail.Rows.First(x => x.ApprovalStatus != "Onaylandı");
        var path = $"/api/payroll/periods/{period.Id}/rows/{row.Id}";
        var preview = (await owner.GetFromJsonAsync<PayrollRowApprovalPreviewDto>(path + "/approval-preview", FinancialJsonOptions))!;
        preview.Row.Revision.Should().Be(row.Revision);
        var count = (await OwnOperationsAsync(owner)).Count;
        (await owner.PostAsJsonAsync(path + "/approve", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsJsonAsync(path + "/approve", new { expectedRevision = "stale" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await OwnOperationsAsync(owner)).Should().HaveCount(count);
        // Two reads and a rejected approval do not persist readiness refreshes.
        var again = (await owner.GetFromJsonAsync<PayrollRowApprovalPreviewDto>(path + "/approval-preview", FinancialJsonOptions))!;
        again.Row.Revision.Should().Be(preview.Row.Revision);
    }
}
