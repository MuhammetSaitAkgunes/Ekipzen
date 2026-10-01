using System.Net;
using System.Net.Http.Json;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Import;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Infrastructure.Persistence.Migrations;
using Ekipzen.Tests.Integration.Tenancy;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Ekipzen.Tests.Integration.Personnel;

[Collection(ApiCollection.Name)]
public sealed class EmployeeImportSafetyTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    private async Task<(HttpClient Client, int TenantId, int DepartmentId)> CompanyAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"import-{suffix}@example.test";
        var tenant = await ProvisionAndActivateAsync("Import " + suffix[..8], email);
        var department = new Department { Name = "Yazılım" };
        await SeedInTenantAsync(tenant.TenantId, db => { db.Departments.Add(department); return Task.CompletedTask; });
        return (await AuthedClientAsync(email), tenant.TenantId, department.Id);
    }

    private static byte[] File(string? nationalId = null, bool partial = false)
    {
        using var stream = new MemoryStream(EmployeeImportTemplate.Olustur());
        using var book = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = book.Worksheet(1);
        sheet.Cell(2, 1).Value = "Aktarım";
        sheet.Cell(2, 6).Value = nationalId ?? "";
        sheet.Cell(2, 8).Value = "";
        sheet.Cell(2, 9).Value = "";
        sheet.Cell(2, 10).Value = "";
        if (partial) { sheet.Cell(4, 2).Value = "Eksik"; sheet.Cell(4, 4).Value = "Yazılım"; sheet.Cell(4, 5).Value = "01.03.2026"; }
        using var bytes = new MemoryStream(); book.SaveAs(bytes); return bytes.ToArray();
    }

    private static MultipartFormDataContent Body(byte[] file, Guid? key = null, string? token = null)
    {
        var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(file), "file", "personel.xlsx");
        if (key.HasValue) body.Add(new StringContent(key.Value.ToString()), "importRequestId");
        if (token is not null) body.Add(new StringContent(token), "expectedPreviewToken");
        return body;
    }

    private static async Task<ImportPreviewDto> Preview(HttpClient client, byte[] file)
    {
        using var body = Body(file);
        var response = await client.PostAsync("/api/employees/import/preview", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ImportPreviewDto>())!;
    }

    private static async Task<HttpResponseMessage> Import(HttpClient client, byte[] file, Guid key, string token)
    {
        using var body = Body(file, key, token);
        return await client.PostAsync("/api/employees/import", body);
    }

    [Fact]
    public async Task SameBlankIdentityBatch_ConcurrentRetriesReturnSameIdsAndOwnedReceipt()
    {
        var team = await CompanyAsync(); var bytes = File(partial: true); var key = Guid.NewGuid();
        var preview = await Preview(team.Client, bytes);
        preview.Satirlar.Select(x => x.SatirNo).Should().Equal(2, 4);
        preview.Satirlar.Select(x => x.Durum).Should().Equal("ready", "invalid");
        await SeedInTenantAsync(team.TenantId, async db => {
            (await db.Employees.CountAsync()).Should().Be(0);
            (await db.EmployeeImportReceipts.CountAsync()).Should().Be(0);
        });
        var responses = await Task.WhenAll(Import(team.Client, bytes, key, preview.PreviewToken!), Import(team.Client, bytes, key, preview.PreviewToken!));
        responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
        var first = (await responses[0].Content.ReadFromJsonAsync<ImportResultDto>())!;
        var second = (await responses[1].Content.ReadFromJsonAsync<ImportResultDto>())!;
        second.Should().BeEquivalentTo(first);
        first.OlusturulanSatir.Should().Be(1); first.AtlananSatir.Should().Be(1);
        first.Satirlar[0].EmployeeId.Should().BeGreaterThan(0);
        first.Satirlar[1].EmployeeId.Should().BeNull();
        (await team.Client.GetFromJsonAsync<ImportResultDto>($"/api/employees/import/results/{key}"))!.Should().BeEquivalentTo(first);
        (await Import(team.Client, bytes, key, new string('B', 64))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Import(team.Client, File("12345678901"), key, preview.PreviewToken!)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var other = await CompanyAsync();
        var hidden = await other.Client.GetAsync($"/api/employees/import/results/{key}");
        (hidden.StatusCode == HttpStatusCode.NoContent || await hidden.Content.ReadAsStringAsync() == "null").Should().BeTrue();
        var otherUserKey = Guid.NewGuid();
        await SeedInTenantAsync(team.TenantId, async db => {
            (await db.Employees.CountAsync()).Should().Be(1);
            (await db.EmployeeEmploymentPeriods.CountAsync()).Should().Be(1);
            (await db.EmployeeCreationEvidences.CountAsync()).Should().Be(0, "aktarım gerçek rehber kanıtı gibi damgalanmaz");
            (await db.EmployeeImportReceipts.CountAsync()).Should().Be(1);
            var stored = await db.EmployeeImportReceipts.SingleAsync();
            stored.ResultJson.Should().NotContain("12345678901").And.NotContain("TR0000");
            // Aynı şirket içinde başka bir kullanıcıya ait sonuç da bulunamaz.
            db.EmployeeImportReceipts.Add(new EmployeeImportReceipt { UserId = "other-user", ImportRequestId = otherUserKey,
                FileFingerprint = stored.FileFingerprint, PreviewToken = stored.PreviewToken, ResultJson = stored.ResultJson });
            var guard = new AddEmployeeImportReceipt().DownOperations.OfType<SqlOperation>().Single().Sql;
            var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(guard));
            error.Number.Should().Be(51000);
        });
        var hiddenUser = await team.Client.GetAsync($"/api/employees/import/results/{otherUserKey}");
        (hiddenUser.StatusCode == HttpStatusCode.NoContent || await hiddenUser.Content.ReadAsStringAsync() == "null").Should().BeTrue();
    }

    [Fact]
    public async Task ChangedSourceOrFileAndMissingKeys_DoNotApplyOldPreview()
    {
        var team = await CompanyAsync(); var bytes = File("12345678901"); var preview = await Preview(team.Client, bytes);
        using var missing = Body(bytes);
        (await team.Client.PostAsync("/api/employees/import", missing)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Import(team.Client, File(), Guid.NewGuid(), preview.PreviewToken!)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await SeedInTenantAsync(team.TenantId, async db => (await db.Departments.SingleAsync()).Name = "Yeni ad");
        (await Import(team.Client, bytes, Guid.NewGuid(), preview.PreviewToken!)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await SeedInTenantAsync(team.TenantId, async db => (await db.Departments.SingleAsync()).Name = "Yazılım");
        var create = await team.Client.PostAsJsonAsync("/api/employees", new { firstName = "Mevcut", lastName = "Kişi", title = "Uzman",
            departmentId = team.DepartmentId, hireDate = "2026-01-01", nationalId = "12345678901", creationRequestId = Guid.NewGuid() });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Import(team.Client, bytes, Guid.NewGuid(), preview.PreviewToken!)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var fresh = await Preview(team.Client, bytes); fresh.MukerrerSatir.Should().Be(1);
        fresh.Satirlar.Single().Durum.Should().Be("duplicate");
        await SeedInTenantAsync(team.TenantId, async db => {
            (await db.Employees.CountAsync()).Should().Be(1);
            (await db.EmployeeImportReceipts.CountAsync()).Should().Be(0);
            db.Departments.Add(new Department { Name = " Yazılım" });
        });
        var ambiguous = await Preview(team.Client, File());
        ambiguous.GecerliSatir.Should().Be(0);
        ambiguous.Sorunlar.Should().Contain(x => x.Mesaj.Contains("birden fazla departman"));
    }

    [Fact]
    public async Task ReceiptWriteFailure_RollsBackEmployeesAndEmploymentTogether()
    {
        var team = await CompanyAsync(); var bytes = File(); var preview = await Preview(team.Client, bytes);
        await SeedInTenantAsync(team.TenantId, async db =>
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [EmployeeImportReceipts] ADD CONSTRAINT [CK_ImportRollback_Test] CHECK ([UserId] = N'blocked-import-user')"));
        (await Import(team.Client, bytes, Guid.NewGuid(), preview.PreviewToken!)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await SeedInTenantAsync(team.TenantId, async db => {
            (await db.Employees.CountAsync()).Should().Be(0);
            (await db.EmployeeEmploymentPeriods.CountAsync()).Should().Be(0);
            (await db.EmployeeImportReceipts.CountAsync()).Should().Be(0);
        });
    }
}
