using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Import;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Organization;
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
    private static byte[] ImportLearningFile(bool invalidOnly = false)
    {
        using var book = new XLWorkbook(new MemoryStream(EmployeeImportTemplate.Olustur()));
        var sheet = book.Worksheet(1); sheet.Rows(2, 10).Clear();
        for (var row = 2; row <= 4; row++)
        {
            sheet.Cell(row, 1).Value = row == 4 || invalidOnly ? "" : "Aktarım";
            sheet.Cell(row, 2).Value = "Kişi " + row; sheet.Cell(row, 3).Value = "Uzman";
            sheet.Cell(row, 4).Value = "Aktarım departmanı";
            sheet.Cell(row, 5).Value = GuidanceContentCatalog.Today.ToString("dd.MM.yyyy");
        }
        using var bytes = new MemoryStream(); book.SaveAs(bytes); return bytes.ToArray();
    }
    private static MultipartFormDataContent ImportLearningBody(byte[] bytes, Guid? request = null, string? token = null, int? source = null)
    {
        var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(bytes), "file", "personel.xlsx");
        if (request.HasValue) body.Add(new StringContent(request.Value.ToString()), "importRequestId");
        if (token is not null) body.Add(new StringContent(token), "expectedPreviewToken");
        if (source.HasValue) body.Add(new StringContent(source.Value.ToString()), "guidanceContentRevisionId");
        return body;
    }
    private static async Task<ImportPreviewDto> ImportLearningPreview(HttpClient client, byte[] file)
    {
        using var body = ImportLearningBody(file);
        var response = await client.PostAsync("/api/employees/import/preview", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ImportPreviewDto>())!;
    }
    private static async Task<HttpResponseMessage> ImportLearningSend(HttpClient client, byte[] file, Guid request, string token, int? source = null)
    {
        using var body = ImportLearningBody(file, request, token, source);
        return await client.PostAsync("/api/employees/import", body);
    }
    private async Task ImportLearningDepartment(int tenantId) => await SeedInTenantAsync(tenantId, db => {
        db.Departments.Add(new Department { Name = "Aktarım departmanı" }); return Task.CompletedTask;
    });

    [Fact]
    public async Task ImportEvidence_ConcurrentReplayAndRetiredSourceKeepOneOwnedHistoricalProof()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1]; const string key = "personnel-import-review";
        try
        {
            await ImportLearningDepartment(team.TenantId);
            var source = await PublishOperationSourceAsync(author, reviewer, "personnel", "import-review");
            var file = ImportLearningFile(); var request = Guid.NewGuid(); var preview = await ImportLearningPreview(author, file);
            preview.GecerliSatir.Should().Be(2); preview.HataliSatir.Should().Be(1);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            var responses = await Task.WhenAll(ImportLearningSend(author, file, request, preview.PreviewToken!, source.Content.Id),
                ImportLearningSend(author, file, request, preview.PreviewToken!, source.Content.Id));
            responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
            var first = (await responses[0].Content.ReadFromJsonAsync<ImportResultDto>())!;
            (await responses[1].Content.ReadFromJsonAsync<ImportResultDto>())!.Should().BeEquivalentTo(first);
            first.GuidanceContent!.ContentRevisionId.Should().Be(source.Content.Id);
            var own = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject;
            own.Evidence.OperationKey.Should().Be("employee-import"); own.IsCurrent.Should().BeTrue();
            var changes = own.Evidence.ImportChanges!;
            changes.ImportRequestId.Should().Be(request); changes.CreatedCount.Should().Be(2); changes.SkippedCount.Should().Be(1);
            changes.CreatedEmployeeIds.Should().BeEquivalentTo(first.Satirlar.Where(x => x.EmployeeId.HasValue).Select(x => x.EmployeeId!.Value));
            changes.SkippedRows.Should().ContainSingle().Which.RowNumber.Should().Be(4);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            var outsider = await AuthedClientAsync("globex-admin@globex.local");
            (await OwnOperationsAsync(outsider)).Should().BeEmpty();
            foreach (var client in new[] { reviewer, outsider })
            {
                var hidden = await client.GetAsync($"/api/employees/import/results/{request}");
                (hidden.StatusCode == HttpStatusCode.NoContent || await hidden.Content.ReadAsStringAsync() == "null").Should().BeTrue();
            }
            using var export = JsonDocument.Parse(await (await author.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceOperations").GetArrayLength().Should().Be(1);
            export.RootElement.GetProperty("guidanceOperations")[0].GetProperty("importChanges").GetProperty("createdCount").GetInt32().Should().Be(2);
            var tasks = (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!;
            foreach (var id in changes.CreatedEmployeeIds) tasks.Should().Contain(x => x.Key == $"employee-compensation:{id}");
            await ChangeAsync(reviewer, source, "retire", "Test sürümü geri çekildi");
            var replay = await ImportLearningSend(author, file, request, preview.PreviewToken!, source.Content.Id);
            replay.StatusCode.Should().Be(HttpStatusCode.OK); (await replay.Content.ReadFromJsonAsync<ImportResultDto>())!.Should().BeEquivalentTo(first);
            (await ImportLearningSend(author, file, request, preview.PreviewToken!)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ImportLearningSend(author, file, Guid.NewGuid(), preview.PreviewToken!, source.Content.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var historical = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject;
            historical.IsCurrent.Should().BeFalse(); historical.Evidence.Should().BeEquivalentTo(own.Evidence);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            (await db.EmployeeImportReceipts.CountAsync()).Should().Be(1); (await db.Employees.CountAsync()).Should().Be(2);
            var sql = new AddEmployeeImportGuidanceSnapshot().DownOperations.OfType<SqlOperation>().Single().Sql;
            (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(sql))).Number.Should().Be(51000);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task ImportEvidence_WrongSourceOrdinaryReplayAndZeroCreatedCannotEarnLearningCredit()
    {
        var team = await TeamAsync(); await using var host = team.Host; var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            await ImportLearningDepartment(team.TenantId);
            var source = await PublishOperationSourceAsync(author, reviewer, "personnel", "import-review");
            var wrong = await PublishOperationSourceAsync(author, reviewer, "personnel", "record-review");
            var file = ImportLearningFile(); var preview = await ImportLearningPreview(author, file); var request = Guid.NewGuid();
            (await ImportLearningSend(author, file, request, preview.PreviewToken!, wrong.Content.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ImportLearningSend(author, file, request, preview.PreviewToken!, 0)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ImportLearningSend(author, file, request, new string('B', 64), source.Content.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            (await ImportLearningSend(author, file, request, preview.PreviewToken!)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            (await ImportLearningSend(author, file, request, preview.PreviewToken!, source.Content.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict, "olağan makbuz sonradan damgalanmaz");
            (await ImportLearningSend(author, file, request, preview.PreviewToken!)).StatusCode.Should().Be(HttpStatusCode.OK);
            var invalid = ImportLearningFile(invalidOnly: true); var noOp = await ImportLearningPreview(author, invalid);
            var response = await ImportLearningSend(author, invalid, Guid.NewGuid(), noOp.PreviewToken!, source.Content.Id);
            response.StatusCode.Should().Be(HttpStatusCode.OK); (await response.Content.ReadFromJsonAsync<ImportResultDto>())!.OlusturulanSatir.Should().Be(0);
            (await OwnOperationsAsync(author)).Should().BeEmpty("yeni personel oluşmadıysa gerçek işlem kanıtı yoktur");
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            (await db.Employees.CountAsync()).Should().Be(2); (await db.EmployeeImportReceipts.CountAsync()).Should().Be(2);
        }
        finally { await CleanupAsync("personnel-import-review"); await CleanupAsync("personnel-record-review"); }
    }

    [Fact]
    public async Task ImportEvidence_SnapshotInsertFailureRollsBackCardsPeriodsAndReceipt()
    {
        var team = await TeamAsync(); await using var host = team.Host; const string key = "personnel-import-review";
        try
        {
            await ImportLearningDepartment(team.TenantId);
            var source = await PublishOperationSourceAsync(team.Clients[0], team.Clients[1], "personnel", "import-review");
            var file = ImportLearningFile(); var preview = await ImportLearningPreview(team.Clients[0], file);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [EmployeeImportReceipts] WITH NOCHECK ADD CONSTRAINT [CK_ImportGuidance_Test] CHECK ([GuidanceSnapshotJson] IS NULL)");
            try
            {
                (await ImportLearningSend(team.Clients[0], file, Guid.NewGuid(), preview.PreviewToken!, source.Content.Id)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await db.Employees.CountAsync()).Should().Be(0); (await db.EmployeeEmploymentPeriods.CountAsync()).Should().Be(0);
                (await db.EmployeeImportReceipts.CountAsync()).Should().Be(0); (await OwnOperationsAsync(team.Clients[0])).Should().BeEmpty();
            }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE [EmployeeImportReceipts] DROP CONSTRAINT [CK_ImportGuidance_Test]"); }
        }
        finally { await CleanupAsync(key); }
    }
}
