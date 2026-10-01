using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Leaves;
using Ekipzen.Application.Features.Recruitment;
using Ekipzen.Domain.Constants;
using Ekipzen.Infrastructure.Persistence;
using Ekipzen.Infrastructure.Persistence.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    private const string Operations = "/api/guidance/operations";

    [Fact]
    public async Task OperationEvidence_EmployeeCreatePinsSourceAndKeepsOwnHistoricalSnapshot()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        const string key = "personnel-entry-tab-is";
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "personnel-entry", "tab-is");
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            var departmentResponse = await author.PostAsJsonAsync("/api/departments", new { name = "Kanıt departmanı" });
            departmentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var department = (await departmentResponse.Content.ReadFromJsonAsync<DepartmentDto>())!;
            object Body(int? version, int departmentId) => new { firstName = "Gerçek", lastName = "İşlem", title = "Uzman",
                departmentId, hireDate = GuidanceContentCatalog.Today, contractType = "indefinite", guidanceContentRevisionId = version };
            (await author.CreateSafeEmployeeAsync("/api/employees", Body(0, department.Id))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await author.CreateSafeEmployeeAsync("/api/employees", Body(source.Content.Id, int.MaxValue))).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            var without = await author.CreateSafeEmployeeAsync("/api/employees", Body(null, department.Id));
            without.StatusCode.Should().Be(HttpStatusCode.Created);
            (await OwnOperationsAsync(author)).Should().BeEmpty("geçmiş işlemlere kaynak tahmin edilmez");
            var created = await author.CreateSafeEmployeeAsync("/api/employees", Body(source.Content.Id, department.Id));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var employee = (await created.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
            var own = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject;
            own.Evidence.ResourceId.Should().Be(employee.Id);
            own.Evidence.OperationKey.Should().Be("employee-create"); own.Evidence.Outcome.Should().Be("created");
            own.Evidence.Content.ContentRevisionId.Should().Be(source.Content.Id); own.IsCurrent.Should().BeTrue();
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            (await OwnOperationsAsync(await AuthedClientAsync("globex-admin@globex.local"))).Should().BeEmpty();
            using var export = JsonDocument.Parse(await (await author.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceOperations").GetArrayLength().Should().Be(1);
            export.RootElement.GetProperty("guidanceContentReadings").GetArrayLength().Should().Be(0);
            export.RootElement.GetProperty("guidancePublishedPractices").GetArrayLength().Should().Be(0);
            var body = CompleteBody("Yeni açıklama") with { Audience = [Roles.HrAdmin], StepKey = "tab-is" };
            var nextResponse = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = key, processKey = "personnel-entry", basedOnId = source.Content.Id, body });
            var next = (await nextResponse.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
            next = await ChangeAsync(author, next, "submit");
            (await PublishAsync(reviewer, next)).StatusCode.Should().Be(HttpStatusCode.OK);
            var historical = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject;
            historical.IsCurrent.Should().BeFalse(); historical.Evidence.Should().BeEquivalentTo(own.Evidence);
            (await author.CreateSafeEmployeeAsync("/api/employees", Body(source.Content.Id, department.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(author)).Should().ContainSingle();
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.Employees.CountAsync()).Should().Be(2, "eski sürüm reddi çalışan oluşturmaz");
            var rollbackSql = new AddGuidanceOperationEvidence().DownOperations.OfType<SqlOperation>().Single().Sql;
            Func<Task> rollbackGuard = () => scope.Services.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(rollbackSql);
            (await rollbackGuard.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
            (await OwnOperationsAsync(author)).Should().ContainSingle("geri alma koruması kayıtları silmez");
            db.Employees.Remove(await db.Employees.SingleAsync(x => x.Id == employee.Id));
            await db.SaveChangesAsync(CancellationToken.None);
            (await OwnOperationsAsync(author)).Should().BeEmpty("kaynak silinince bağlı kanıt cascade ile silinir");
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task OperationEvidence_CandidateNoOpAndConcurrentStaleDecisionCannotDuplicateEvidence()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        const string key = "recruitment-first-review";
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "recruitment", "first-review");
            var created = await author.PostAsJsonAsync("/api/candidates", new { name = "İlk karar", appliedRole = "Uzman" });
            var candidate = (await created.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            var path = $"/api/candidates/{candidate.Id}/status";
            (await author.PatchAsJsonAsync(path, new { status = "Yeni", expectedVersion = candidate.Version,
                reason = "Aşama korundu", guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            var input = new { status = "Mülakat", expectedVersion = candidate.Version,
                reason = "Başvuru incelendi", guidanceContentRevisionId = source.Content.Id };
            var responses = await Task.WhenAll(author.PatchAsJsonAsync(path, input), author.PatchAsJsonAsync(path, input));
            responses.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var row = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject.Evidence;
            row.OperationKey.Should().Be("candidate-first-review"); row.ResourceId.Should().Be(candidate.Id); row.Outcome.Should().Be("Interview");
            (await author.PatchAsJsonAsync(path, new { status = "Teklif", expectedVersion = candidate.Version + 1,
                reason = "İleri aşama", guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(author)).Should().ContainSingle();
            (await author.PatchAsJsonAsync(path, new { status = "Yeni", expectedVersion = candidate.Version + 1, reason = "Yeniden inceleme" })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await author.PatchAsJsonAsync(path, new { status = "Red", expectedVersion = candidate.Version + 2,
                reason = "Tekrar incelendi", guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().ContainSingle().Which.Evidence.Should().BeEquivalentTo(row, "ilk kanıt yeni incelemeyle ezilmez");
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
        }
        finally { await CleanupAsync(key); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OperationEvidence_LeaveDecisionRespectsScopeAndSerializesDuplicateDecisions(bool approve)
    {
        var team = await TeamAsync(); await using var host = team.Host;
        const string key = "leave-decision-effect";
        try
        {
            var source = await PublishOperationSourceAsync(team.Clients[0], team.Clients[1], "leave", "decision-effect");
            var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
            var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
            var hr = await AuthedClientAsync("ik@hrmaster.local");
            var outsider = await AuthedClientAsync("globex-admin@globex.local");
            var before = await OwnOperationsAsync(manager);
            var types = (await employee.GetFromJsonAsync<List<LeaveTypeDto>>("/api/leaves/types"))!;
            var start = GuidanceContentCatalog.Today.AddDays(1200 + (approve ? 0 : 7));
            while (start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) start = start.AddDays(1);
            var created = await employee.PostAsJsonAsync("/api/leaves", new { leaveTypeId = types.Single(x => x.Code == "Mazeret").Id,
                startDate = start, endDate = start, description = "Sürümlü karar testi" });
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var leave = (await created.Content.ReadFromJsonAsync<LeaveRequestDto>())!;
            var path = $"/api/leaves/{leave.Id}/{(approve ? "approve" : "reject")}";
            var input = new { note = "Gerçek talep incelendi", guidanceContentRevisionId = source.Content.Id };
            (await outsider.PostAsJsonAsync(path, input)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await employee.PostAsJsonAsync(path, input)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await employee.GetAsync(Operations)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Factory.CreateClient().GetAsync(Operations)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var responses = await Task.WhenAll(manager.PostAsJsonAsync(path, input), manager.PostAsJsonAsync(path, input));
            responses.Select(x => x.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var after = await OwnOperationsAsync(manager);
            after.Count.Should().Be(before.Count + 1);
            var item = after.Single(x => x.Evidence.ResourceId == leave.Id && x.Evidence.OperationKey == "leave-decision");
            item.Evidence.Outcome.Should().Be(approve ? "Approved" : "Rejected");
            item.Evidence.Content.ContentRevisionId.Should().Be(source.Content.Id);
            (await OwnOperationsAsync(hr)).Should().NotContain(x => x.Evidence.ResourceId == leave.Id && x.Evidence.OperationKey == "leave-decision");
            using var export = JsonDocument.Parse(await (await manager.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceOperations").EnumerateArray().Should().Contain(x =>
                x.GetProperty("resourceId").GetInt32() == leave.Id && x.GetProperty("content").GetProperty("contentRevisionId").GetInt32() == source.Content.Id);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task OperationEvidence_RejectsWrongUnpublishedRetiredOrExpiredSourceBeforeWriting()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        const string key = "recruitment-first-review";
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "recruitment", "first-review");
            var candidate = (await (await author.PostAsJsonAsync("/api/candidates", new { name = "Kaynak sınırı", appliedRole = "Uzman" })).Content.ReadFromJsonAsync<CandidateDetailDto>())!;
            var input = new { status = "Mülakat", expectedVersion = candidate.Version, reason = "Kontrol", guidanceContentRevisionId = source.Content.Id };
            (await author.CreateSafeEmployeeAsync("/api/employees", new { firstName = "Yanlış", lastName = "Adım", title = "Uzman", departmentId = 1,
                hireDate = GuidanceContentCatalog.Today, guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            using (var scope = Factory.Services.CreateScope())
            {
                var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
                var row = await platform.GuidanceContentRevisions.SingleAsync(x => x.Id == source.Content.Id);
                foreach (var state in new[] { "draft", "retired", "published" })
                {
                    row.State = state;
                    if (state == "published") row.ReviewDueOn = GuidanceContentCatalog.Today.AddDays(-1);
                    await platform.SaveChangesAsync(CancellationToken.None);
                    var response = await author.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status", input);
                    response.StatusCode.Should().Be(state == "draft" ? HttpStatusCode.NotFound : HttpStatusCode.Conflict);
                }
            }
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            (await author.GetFromJsonAsync<CandidateDetailDto>($"/api/candidates/{candidate.Id}"))!.Version.Should().Be(candidate.Version);
        }
        finally { await CleanupAsync(key); }
    }

    private static async Task<List<GuidanceOperationHistoryDto>> OwnOperationsAsync(HttpClient client)
    {
        var response = await client.GetAsync(Operations);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<List<GuidanceOperationHistoryDto>>())!;
    }
    private static async Task<GuidanceEditorialDto> PublishOperationSourceAsync(HttpClient author, HttpClient reviewer, string process, string step)
    {
        var body = CompleteBody("Kaynağı doğrulayarak gerçek işlem") with { Audience = [Roles.HrAdmin, Roles.Manager], StepKey = step };
        var response = await author.PostAsJsonAsync($"{Root}/editorial", new { contentKey = $"{process}-{step}", processKey = process, body });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var item = (await response.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
        item = await ChangeAsync(author, item, "submit");
        var published = await PublishAsync(reviewer, item);
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        return (await published.Content.ReadFromJsonAsync<GuidanceEditorialDto>())!;
    }
}
