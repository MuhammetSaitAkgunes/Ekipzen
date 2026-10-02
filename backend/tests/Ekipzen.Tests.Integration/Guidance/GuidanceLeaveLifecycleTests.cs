using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Leaves;
using Ekipzen.Application.Features.Leaves.Queries;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Enums;
using Ekipzen.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    private const string LeaveLifecycleKey = "leave-request-lifecycle";
    private static async Task<GuidanceEditorialDto> PublishLeaveLifecycleAsync(HttpClient author, HttpClient reviewer)
    {
        var body = CompleteBody("Kendi izin kaydını doğrula") with {
            Audience = [Roles.HrAdmin, Roles.Manager, Roles.Employee], StepKey = "request-lifecycle",
            Practice = new("Önizleme işlem midir?", ["Evet", "Hayır"], 1, "Salt okuma kayıt oluşturmaz."),
        };
        var response = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = LeaveLifecycleKey, processKey = "leave", body });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var item = await ChangeAsync(author, (await response.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!, "submit");
        var published = await PublishAsync(reviewer, item);
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        return (await published.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
    }
    private async Task<int> LeaveDemoTenantAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Tenants.SingleAsync(x => x.Slug == "demo");
        tenant.DatabaseName.Should().StartWith(EkipzenApiFactory.KiraciKatalogOneki);
        return tenant.Id;
    }
    private static async Task<List<GuidanceOperationHistoryDto>> LeaveOperationsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<GuidanceOperationHistoryDto>>("/api/leaves/learning-operations"))!;
    private static DateOnly LeaveTestDate(int offset)
    {
        var date = GuidanceContentCatalog.Today.AddDays(offset);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        return date;
    }
    private static async Task<LeaveRequestPreviewDto> LeavePreviewAsync(HttpClient employee, int typeId, DateOnly date) =>
        (await (await employee.PostAsJsonAsync("/api/leaves/preview", new { leaveTypeId = typeId, startDate = date, endDate = date }))
            .Content.ReadFromJsonAsync<LeaveRequestPreviewDto>())!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuidedLeave_RequestAndCancellationKeepOwnSourcesAndRecordedEffects(bool approve)
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var outsider = await AuthedClientAsync("globex-admin@globex.local");
        try
        {
            var source = await PublishLeaveLifecycleAsync(team.Clients[0], team.Clients[1]);
            (await employee.GetAsync($"{Root}/steps/leave/request-lifecycle")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await employee.GetAsync($"{Root}/steps/leave/review-request")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await employee.GetAsync(Operations)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var before = await LeaveOperationsAsync(employee);
            var type = (await employee.GetFromJsonAsync<List<LeaveTypeDto>>("/api/leaves/types"))!.Single(x => x.Code == "Yıllık");
            var date = LeaveTestDate(3100 + (approve ? 7 : 0));
            var preview = await LeavePreviewAsync(employee, type.Id, date);
            preview.CanSubmit.Should().BeTrue(preview.BlockReason);
            (await LeaveOperationsAsync(employee)).Should().BeEquivalentTo(before);
            var input = new { leaveTypeId = type.Id, startDate = date, endDate = date,
                description = "PRIVATE-LEAVE-REASON", expectedRecordToken = preview.RecordToken, guidanceContentRevisionId = source.Content.Id };
            var created = await employee.PostAsJsonAsync("/api/leaves", input);
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var leave = (await created.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
            (await employee.PostAsJsonAsync("/api/leaves", input)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var pendingPreview = (await employee.GetFromJsonAsync<LeaveCancellationPreviewDto>($"/api/leaves/{leave.Id}/cancellation-preview"))!;
            pendingPreview.ReleasedReservationDays.Should().Be(leave.Days); pendingPreview.RefundedDays.Should().Be(0);
            (await manager.GetAsync($"/api/leaves/{leave.Id}/cancellation-preview")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await outsider.GetAsync($"/api/leaves/{leave.Id}/cancellation-preview")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "bu hesapta personel bağlantısı yok; başka şirketin talebi okunmaz");
            (await Factory.CreateClient().GetAsync("/api/leaves/learning-operations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            if (approve)
            {
                (await manager.PostAsJsonAsync($"/api/leaves/{leave.Id}/approve", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
                (await employee.PostAsync($"/api/leaves/{leave.Id}/cancel?expectedRecordToken={pendingPreview.RecordToken}&guidanceContentRevisionId={source.Content.Id}", null))
                    .StatusCode.Should().Be(HttpStatusCode.Conflict, "önizlemeden sonra onaylanan kayıt yeniden incelenmelidir");
            }
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(await LeaveDemoTenantAsync());
            var db = scope.Services.GetRequiredService<AppDbContext>();
            Holiday? changedCalendar = null;
            if (approve)
            {
                changedCalendar = new Holiday { Date = date, Name = "Sonradan eklenen test tatili" };
                db.Holidays.Add(changedCalendar); await db.SaveChangesAsync();
            }
            try
            {
                var cancellation = (await employee.GetFromJsonAsync<LeaveCancellationPreviewDto>($"/api/leaves/{leave.Id}/cancellation-preview"))!;
                cancellation.RefundedDays.Should().Be(approve ? leave.Days : 0);
                cancellation.ReleasedReservationDays.Should().Be(approve ? 0 : leave.Days);
                var path = $"/api/leaves/{leave.Id}/cancel?expectedRecordToken={cancellation.RecordToken}&guidanceContentRevisionId={source.Content.Id}";
                var cancellations = await Task.WhenAll(employee.PostAsync(path, null), employee.PostAsync(path, null));
                cancellations.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);
                var ledger = await db.LeaveLedgerEntries.AsNoTracking().Where(x => x.LeaveRequestId == leave.Id).ToListAsync();
                ledger.Sum(x => x.Days).Should().Be(0);
                ledger.Count(x => x.Type == LeaveLedgerEntryType.UsageReversal).Should().Be(approve ? 1 : 0);
                var history = (await LeaveOperationsAsync(employee)).Where(x => x.Evidence.ResourceId == leave.Id).ToList();
                history.Should().HaveCount(2);
                history.Select(x => x.Evidence.OperationKey).Should().BeEquivalentTo(["leave-request-create", "leave-request-cancel"]);
                history.Should().OnlyContain(x => x.Evidence.Content.ContentRevisionId == source.Content.Id);
                (await LeaveOperationsAsync(manager)).Should().NotContain(x => x.Evidence.ResourceId == leave.Id);
                (await LeaveOperationsAsync(outsider)).Should().BeEmpty();
                var json = await (await employee.GetAsync("/api/leaves/learning-operations")).Content.ReadAsStringAsync();
                json.Should().NotContain("PRIVATE-LEAVE-REASON");
                using var export = JsonDocument.Parse(await (await employee.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
                export.RootElement.GetProperty("guidanceOperations").EnumerateArray().Count(x => x.GetProperty("resourceId").GetInt32() == leave.Id
                    && x.GetProperty("operationKey").GetString()!.StartsWith("leave-request-")).Should().Be(2);
            }
            finally { if (changedCalendar is not null) { db.Holidays.Remove(changedCalendar); await db.SaveChangesAsync(); } }
        }
        finally { await CleanupAsync(LeaveLifecycleKey); }
    }

    [Fact]
    public async Task GuidedLeave_RejectsMissingPreviewChangedTypeAndRetiredSourceWithoutWrites()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        try
        {
            var source = await PublishLeaveLifecycleAsync(team.Clients[0], team.Clients[1]);
            var date = LeaveTestDate(3200);
            var type = (await employee.GetFromJsonAsync<List<LeaveTypeDto>>("/api/leaves/types"))!.Single(x => x.Code == "Mazeret");
            var preview = await LeavePreviewAsync(employee, type.Id, date);
            var before = await LeaveOperationsAsync(employee);
            var missing = await employee.PostAsJsonAsync("/api/leaves", new { leaveTypeId = type.Id, startDate = date, endDate = date, guidanceContentRevisionId = source.Content.Id });
            missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var input = new { leaveTypeId = type.Id, startDate = date, endDate = date, expectedRecordToken = preview.RecordToken, guidanceContentRevisionId = source.Content.Id };
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(await LeaveDemoTenantAsync());
            var db = scope.Services.GetRequiredService<AppDbContext>(); var stored = await db.LeaveTypes.SingleAsync(x => x.Id == type.Id);
            stored.RequiresApproval = false; await db.SaveChangesAsync();
            try { (await employee.PostAsJsonAsync("/api/leaves", input)).StatusCode.Should().Be(HttpStatusCode.Conflict); }
            finally { stored.RequiresApproval = true; await db.SaveChangesAsync(); }
            await ChangeAsync(team.Clients[1], source, "retire", "Yeni inceleme gerekli");
            (await employee.PostAsJsonAsync("/api/leaves", input)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await db.LeaveRequests.AnyAsync(x => x.StartDate == date)).Should().BeFalse();
            (await LeaveOperationsAsync(employee)).Should().BeEquivalentTo(before);
        }
        finally { await CleanupAsync(LeaveLifecycleKey); }
    }

    [Fact]
    public async Task GuidedLeave_ProofFailureRollsBackAutomaticUsageAndCancellation()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        try
        {
            var source = await PublishLeaveLifecycleAsync(team.Clients[0], team.Clients[1]);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(await LeaveDemoTenantAsync());
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var type = new LeaveType { Name = "Test otomatik yıllık", Code = "Test-auto", DeductsFromAnnualBalance = true, RequiresApproval = false };
            db.LeaveTypes.Add(type); await db.SaveChangesAsync();
            var date = LeaveTestDate(3300); var preview = await LeavePreviewAsync(employee, type.Id, date);
            preview.CanSubmit.Should().BeTrue(preview.BlockReason);
            var beforeLedger = await db.LeaveLedgerEntries.AsNoTracking().ToListAsync();
            var input = new { leaveTypeId = type.Id, startDate = date, endDate = date, expectedRecordToken = preview.RecordToken, guidanceContentRevisionId = source.Content.Id };
            const string add = "ALTER TABLE [GuidancePreparationEvidences] WITH NOCHECK ADD CONSTRAINT [CK_Test_LeaveProof] CHECK ([Id] < 0)";
            const string drop = "ALTER TABLE [GuidancePreparationEvidences] DROP CONSTRAINT [CK_Test_LeaveProof]";
            await db.Database.ExecuteSqlRawAsync(add);
            try
            {
                (await employee.PostAsJsonAsync("/api/leaves", input)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await db.LeaveRequests.AnyAsync(x => x.LeaveTypeId == type.Id)).Should().BeFalse();
                (await db.LeaveLedgerEntries.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(beforeLedger);
            }
            finally { await db.Database.ExecuteSqlRawAsync(drop); }
            var response = await employee.PostAsJsonAsync("/api/leaves", input);
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            var leave = (await response.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
            var cancellation = (await employee.GetFromJsonAsync<LeaveCancellationPreviewDto>($"/api/leaves/{leave.Id}/cancellation-preview"))!;
            await db.Database.ExecuteSqlRawAsync(add);
            try
            {
                (await employee.PostAsync($"/api/leaves/{leave.Id}/cancel?expectedRecordToken={cancellation.RecordToken}&guidanceContentRevisionId={source.Content.Id}", null)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await db.LeaveRequests.AsNoTracking().SingleAsync(x => x.Id == leave.Id)).Status.Should().Be(LeaveStatus.Approved);
                (await db.LeaveLedgerEntries.Where(x => x.LeaveRequestId == leave.Id).SumAsync(x => x.Days)).Should().Be(-leave.Days);
                (await LeaveOperationsAsync(employee)).Count(x => x.Evidence.ResourceId == leave.Id).Should().Be(1);
            }
            finally { await db.Database.ExecuteSqlRawAsync(drop); await employee.PostAsync($"/api/leaves/{leave.Id}/cancel", null); }
        }
        finally { await CleanupAsync(LeaveLifecycleKey); }
    }
}
