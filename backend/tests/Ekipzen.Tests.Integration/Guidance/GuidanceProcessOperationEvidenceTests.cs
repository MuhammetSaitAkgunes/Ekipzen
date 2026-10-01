using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Attendance;
using Ekipzen.Application.Features.Compliance;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Setup;
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
    public async Task ProcessEvidence_SetupCountsActualAddsAndConcurrentNoOpDoesNotRepeat()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        const string key = "company-initial-setup";
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "company", "initial-setup");
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            var before = (await author.GetFromJsonAsync<TenantSetupStatusDto>("/api/setup"))!;
            var body = new { guidanceContentRevisionId = source.Content.Id };
            var responses = await Task.WhenAll(author.PostAsJsonAsync("/api/setup", body), author.PostAsJsonAsync("/api/setup", body));
            responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
            var evidence = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject.Evidence;
            evidence.OperationKey.Should().Be("tenant-setup"); evidence.ResourceId.Should().Be(before.TemplateYear);
            evidence.SetupChanges!.AddedCompanyProfile.Should().Be(!before.CompanyProfileExists);
            evidence.SetupChanges.AddedLeaveTypeCount.Should().Be(before.MissingLeaveTypeCount);
            evidence.SetupChanges.AddedHolidayCount.Should().Be(before.MissingFullDayHolidayCount);
            evidence.SetupChanges.AddedPayrollSettings.Should().Be(!before.PayrollSettingsExists);
            (await author.PostAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.OK, "legacy empty POST stays supported");
            (await OwnOperationsAsync(author)).Should().ContainSingle();
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            (await OwnOperationsAsync(await AuthedClientAsync("globex-admin@globex.local"))).Should().BeEmpty();
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var sql = new AddGuidanceProcessOperationEvidence().DownOperations.OfType<SqlOperation>().Single().Sql;
            Func<Task> guard = () => db.Database.ExecuteSqlRawAsync(sql);
            (await guard.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
            (await OwnOperationsAsync(author)).Should().ContainSingle();
            using var export = JsonDocument.Parse(await (await author.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceOperations")[0].GetProperty("setupChanges").GetProperty("addedLeaveTypeCount").GetInt32().Should().Be(before.MissingLeaveTypeCount);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task ProcessEvidence_AttendancePreviewNoOpAndRepeatedCorrectionsKeepDistinctEvents()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        var keys = new[] { "attendance-entry-review", "attendance-entry-correction" };
        try
        {
            var createSource = await PublishOperationSourceAsync(author, reviewer, "attendance", "entry-review");
            var updateSource = await PublishOperationSourceAsync(author, reviewer, "attendance", "entry-correction");
            var employee = await CreateProofEmployeeAsync(author);
            object Model(int source, string? note = null) => new { workDate = "2026-10-01", checkIn = "09:00", checkOut = "18:00", breakMinutes = 60, type = "Tam", note, guidanceContentRevisionId = source };
            var model = Model(createSource.Content.Id);
            (await author.PostAsJsonAsync("/api/attendance/preview", new { employeeId = employee.Id, model })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            (await author.PostAsJsonAsync("/api/attendance", new { employeeId = employee.Id, model = Model(updateSource.Content.Id) })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var response = await author.PostAsJsonAsync("/api/attendance", new { employeeId = employee.Id, model });
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            var row = (await response.Content.ReadFromJsonAsync<TimesheetRowDto>())!;
            var path = $"/api/attendance/{row.Id}";
            var noOp = await author.PutAsJsonAsync(path, new { model = Model(updateSource.Content.Id), expectedRevision = row.Revision });
            noOp.StatusCode.Should().Be(HttpStatusCode.OK);
            (await noOp.Content.ReadFromJsonAsync<TimesheetRowDto>())!.Revision.Should().Be(row.Revision);
            (await OwnOperationsAsync(author)).Should().ContainSingle();
            var input = new { model = Model(updateSource.Content.Id, "Gerçek düzeltme"), expectedRevision = row.Revision };
            var concurrent = await Task.WhenAll(author.PutAsJsonAsync(path, input), author.PutAsJsonAsync(path, input));
            concurrent.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            row = (await concurrent.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<TimesheetRowDto>())!;
            (await author.PutAsJsonAsync(path, new { model = Model(updateSource.Content.Id, "İkinci düzeltme"), expectedRevision = row.Revision })).StatusCode.Should().Be(HttpStatusCode.OK);
            var history = await OwnOperationsAsync(author);
            history.Should().HaveCount(3);
            history.Select(x => x.Evidence.EvidenceKey).Should().OnlyHaveUniqueItems();
            history.Should().OnlyContain(x => x.Evidence.ResourceId == row.Id);
            history.Count(x => x.Evidence.OperationKey == "attendance-update").Should().Be(2);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            db.AttendanceRecords.Remove(await db.AttendanceRecords.SingleAsync(x => x.Id == row.Id));
            await db.SaveChangesAsync(CancellationToken.None);
            (await OwnOperationsAsync(author)).Should().BeEmpty("source deletion cascades all revisions");
        }
        finally { foreach (var key in keys) await CleanupAsync(key); }
    }

    [Fact]
    public async Task ProcessEvidence_ComplianceSeparateWritesNoOpAndRetirementPreserveOnlyActualEvents()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        var keys = new[] { "compliance-record-review", "compliance-record-correction", "compliance-status-review" };
        try
        {
            var createSource = await PublishOperationSourceAsync(author, reviewer, "compliance", "record-review");
            var correction = await PublishOperationSourceAsync(author, reviewer, "compliance", "record-correction");
            var statusSource = await PublishOperationSourceAsync(author, reviewer, "compliance", "status-review");
            var employee = await CreateProofEmployeeAsync(author);
            var response = await author.PostAsJsonAsync("/api/compliance/documents", new { employeeId = employee.Id, documentName = "İş sözleşmesi", ownerName = "İK", guidanceContentRevisionId = createSource.Content.Id });
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            var document = (await response.Content.ReadFromJsonAsync<ComplianceDocumentDto>())!;
            var path = $"/api/compliance/documents/{document.Id}";
            object Details(string name) => new { documentName = name, dueDate = (string?)null, level = "medium", guidanceContentRevisionId = correction.Content.Id };
            (await author.PutAsJsonAsync(path, Details(" İş sözleşmesi "))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.PatchAsJsonAsync(path + "/owner", new { ownerName = " İK ", guidanceContentRevisionId = correction.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().ContainSingle();
            (await author.PutAsJsonAsync(path, Details("Doğrulanan sözleşme"))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.PatchAsJsonAsync(path + "/owner", new { ownerName = "Yeni sorumlu", guidanceContentRevisionId = correction.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            var statusBody = new { status = "İncelemede", guidanceContentRevisionId = statusSource.Content.Id };
            var results = await Task.WhenAll(author.PatchAsJsonAsync(path + "/status", statusBody), author.PatchAsJsonAsync(path + "/status", statusBody));
            results.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var history = await OwnOperationsAsync(author); history.Should().HaveCount(4);
            history.Select(x => x.Evidence.OperationKey).Should().BeEquivalentTo(["compliance-create", "compliance-update", "compliance-owner", "compliance-status"]);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            (await (await AuthedClientAsync("ece.arslan@hrmaster.local")).PatchAsJsonAsync(path + "/owner", new { ownerName = "Yetkisiz" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await (await AuthedClientAsync("globex-admin@globex.local")).PutAsJsonAsync(path, Details("Başka kiracı"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
            // The first form request committed; retirement rejects the independent owner request.
            (await author.PutAsJsonAsync(path, Details("Son kayıt"))).StatusCode.Should().Be(HttpStatusCode.OK);
            correction = await ChangeAsync(reviewer, correction, "retire", "Uzman içeriği yeniden inceleyecek");
            (await author.PatchAsJsonAsync(path + "/owner", new { ownerName = "Başarısız sorumlu", guidanceContentRevisionId = correction.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var saved = (await author.GetFromJsonAsync<List<ComplianceDocumentDto>>("/api/compliance/documents"))!.Single(x => x.Id == document.Id);
            saved.Document.Should().Be("Son kayıt"); saved.Owner.Should().Be("Yeni sorumlu");
            (await OwnOperationsAsync(author)).Should().HaveCount(5);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            db.ComplianceDocuments.Remove(await db.ComplianceDocuments.SingleAsync(x => x.Id == document.Id));
            await db.SaveChangesAsync(CancellationToken.None);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
        }
        finally { foreach (var key in keys) await CleanupAsync(key); }
    }

    private static async Task<EmployeeDetailDto> CreateProofEmployeeAsync(HttpClient client)
    {
        var department = (await (await client.PostAsJsonAsync("/api/departments", new { name = "İşlem kanıtı ekibi" })).Content.ReadFromJsonAsync<DepartmentDto>())!;
        var response = await client.PostAsJsonAsync("/api/employees", new { firstName = "Gerçek", lastName = "Kayıt", title = "Uzman", departmentId = department.Id, hireDate = "2026-01-01", contractType = "indefinite" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
    }
}
