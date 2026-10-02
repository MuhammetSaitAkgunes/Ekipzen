using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Departments.DeleteDepartment;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Upsert;
using Ekipzen.Application.Features.Settings;
using Ekipzen.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    private static MultipartFormDataContent HelperImage(string? token = null, int? source = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]), "file", "image.png");
        if (token is not null) form.Add(new StringContent(token), "expectedRecordToken");
        if (source is not null) form.Add(new StringContent(source.Value.ToString()), "guidanceContentRevisionId");
        return form;
    }

    [Fact]
    public async Task CompanyHelpers_DeletionChecksReviewedDependenciesAndPreservesOwnHistory()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "company", "departments");
            var department = (await (await author.PostAsJsonAsync("/api/departments", new { name = "Silinebilir birim" })).Content.ReadFromJsonAsync<DepartmentDto>())!;
            var path = $"/api/departments/{department.Id}";
            var preview = (await author.GetFromJsonAsync<DepartmentDeletionPreviewDto>(path + "/deletion-preview"))!;
            preview.CanDelete.Should().BeTrue(); (await OwnOperationsAsync(author)).Should().BeEmpty();
            (await author.DeleteAsync(path + $"?guidanceContentRevisionId={source.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await author.PostAsJsonAsync("/api/positions", new { title = "Bağlı pozisyon", departmentId = department.Id, openCount = 1 })).StatusCode.Should().Be(HttpStatusCode.Created);
            (await author.DeleteAsync(path + $"?expectedRecordToken={preview.RecordToken}&guidanceContentRevisionId={source.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var blocked = (await author.GetFromJsonAsync<DepartmentDeletionPreviewDto>(path + "/deletion-preview"))!;
            blocked.CanDelete.Should().BeFalse(); blocked.PositionCount.Should().Be(1);
            (await author.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
            (await manager.GetAsync(path + "/deletion-preview")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var outsider = await AuthedClientAsync("globex-admin@globex.local");
            var outsideResponse = await outsider.GetAsync(path + "/deletion-preview");
            outsideResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
            // Ayrı kiracı veritabanlarında aynı sayısal kimlik bulunabilir; bu durumda yalnız kendi kaydı okunur.
            if (outsideResponse.IsSuccessStatusCode)
                (await outsideResponse.Content.ReadFromJsonAsync<DepartmentDeletionPreviewDto>())!.Name.Should().NotBe(department.Name);
            var empty = (await (await author.PostAsJsonAsync("/api/departments", new { name = "Gerçekten boş birim" })).Content.ReadFromJsonAsync<DepartmentDto>())!;
            var emptyPath = $"/api/departments/{empty.Id}";
            var emptyPreview = (await author.GetFromJsonAsync<DepartmentDeletionPreviewDto>(emptyPath + "/deletion-preview"))!;
            var delete = emptyPath + $"?expectedRecordToken={emptyPreview.RecordToken}&guidanceContentRevisionId={source.Content.Id}";
            var results = await Task.WhenAll(author.DeleteAsync(delete), author.DeleteAsync(delete));
            results.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound });
            var proof = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject.Evidence;
            proof.OperationKey.Should().Be("department-delete"); proof.Outcome.Should().Be("deleted"); proof.ResourceId.Should().Be(empty.Id);
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            using var export = JsonDocument.Parse(await (await author.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceOperations").GetArrayLength().Should().Be(1);
            await ChangeAsync(reviewer, source, "retire", "Kaynak güncelleniyor");
            (await OwnOperationsAsync(author)).Should().OnlyContain(x => !x.IsCurrent);
        }
        finally { await CleanupAsync("company-departments"); }
    }

    [Fact]
    public async Task CompanyHelpers_NotificationNoOpStaleReplayAndWrongSourceNeverInventProof()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "company", "company-profile");
            var wrong = await PublishOperationSourceAsync(author, reviewer, "company", "departments");
            var current = (await author.GetFromJsonAsync<SettingsDto>("/api/settings"))!.Notifications;
            var noOp = new { current.NewPersonnelEmail, current.LeaveRequestEmail, weeklyReportEmail = false, expectedRecordToken = current.RecordToken, guidanceContentRevisionId = source.Content.Id };
            (await author.PutAsJsonAsync("/api/settings/notifications", noOp)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
            var command = new { newPersonnelEmail = !current.NewPersonnelEmail, current.LeaveRequestEmail, weeklyReportEmail = false, expectedRecordToken = current.RecordToken, guidanceContentRevisionId = source.Content.Id };
            (await author.PutAsJsonAsync("/api/settings/notifications", new { newPersonnelEmail = false, leaveRequestEmail = false, weeklyReportEmail = false, guidanceContentRevisionId = source.Content.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await author.PutAsJsonAsync("/api/settings/notifications", new { newPersonnelEmail = false, leaveRequestEmail = false, weeklyReportEmail = false, expectedRecordToken = current.RecordToken, guidanceContentRevisionId = wrong.Content.Id })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var results = await Task.WhenAll(author.PutAsJsonAsync("/api/settings/notifications", command), author.PutAsJsonAsync("/api/settings/notifications", command));
            results.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            var proof = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject.Evidence;
            proof.OperationKey.Should().Be("company-notifications-update"); proof.PreparationChanges!.ChangedFields.Should().BeEquivalentTo("Yeni personel e-posta tercihi");
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            var updated = (await author.GetFromJsonAsync<SettingsDto>("/api/settings"))!.Notifications;
            updated.NewPersonnelEmail.Should().Be(!current.NewPersonnelEmail);
            (await author.PutAsJsonAsync("/api/settings/notifications", command)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally { await CleanupAsync("company-company-profile"); await CleanupAsync("company-departments"); }
    }

    [Fact]
    public async Task CompanyHelpers_ImageAndDeletionProofFailureRollBackBusinessAndKeepPreviousFiles()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        try
        {
            var company = await PublishOperationSourceAsync(author, reviewer, "company", "company-profile");
            var photo = await PublishOperationSourceAsync(author, reviewer, "personnel-entry", "tab-kimlik");
            var departmentSource = await PublishOperationSourceAsync(author, reviewer, "company", "departments");
            var employee = await CreateProofEmployeeAsync(author);
            using (var file = HelperImage()) (await author.PostAsync("/api/settings/company/logo", file)).StatusCode.Should().Be(HttpStatusCode.OK);
            using (var file = HelperImage()) (await author.PostAsync($"/api/employees/{employee.Id}/photo", file)).StatusCode.Should().Be(HttpStatusCode.OK);
            var settings = (await author.GetFromJsonAsync<SettingsDto>("/api/settings"))!;
            employee = (await author.GetFromJsonAsync<EmployeeDetailDto>($"/api/employees/{employee.Id}"))!;
            var department = (await (await author.PostAsJsonAsync("/api/departments", new { name = "Korunacak birim" })).Content.ReadFromJsonAsync<DepartmentDto>())!;
            var preview = (await author.GetFromJsonAsync<DepartmentDeletionPreviewDto>($"/api/departments/{department.Id}/deletion-preview"))!;
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var directory = Path.Combine(Factory.StorageRoot, $"tenant-{team.TenantId}");
            var filesBefore = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePreparationEvidences] WITH NOCHECK ADD CONSTRAINT [CK_Test_HelperPreparation] CHECK ([Id] < 0)");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePersonnelEvidences] WITH NOCHECK ADD CONSTRAINT [CK_Test_HelperPersonnel] CHECK ([Id] < 0)");
            try
            {
                using (var file = HelperImage(settings.Company.LogoRecordToken, company.Content.Id)) (await author.PostAsync("/api/settings/company/logo", file)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                using (var file = HelperImage(employee.Profile.PhotoRecordToken, photo.Content.Id)) (await author.PostAsync($"/api/employees/{employee.Id}/photo", file)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await author.DeleteAsync($"/api/departments/{department.Id}?expectedRecordToken={preview.RecordToken}&guidanceContentRevisionId={departmentSource.Content.Id}")).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await author.PutAsJsonAsync("/api/settings/notifications", new { newPersonnelEmail = !settings.Notifications.NewPersonnelEmail, settings.Notifications.LeaveRequestEmail, weeklyReportEmail = false,
                    expectedRecordToken = settings.Notifications.RecordToken, guidanceContentRevisionId = company.Content.Id })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
                (await author.GetFromJsonAsync<SettingsDto>("/api/settings"))!.Should().BeEquivalentTo(settings);
                (await author.GetFromJsonAsync<EmployeeDetailDto>($"/api/employees/{employee.Id}"))!.Profile.PhotoPath.Should().Be(employee.Profile.PhotoPath);
                (await author.GetAsync($"/api/departments/{department.Id}/deletion-preview")).StatusCode.Should().Be(HttpStatusCode.OK);
                Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Should().BeEquivalentTo(filesBefore);
                (await OwnOperationsAsync(author)).Should().BeEmpty();
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePreparationEvidences] DROP CONSTRAINT [CK_Test_HelperPreparation]");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePersonnelEvidences] DROP CONSTRAINT [CK_Test_HelperPersonnel]");
            }
            using (var file = HelperImage(settings.Company.LogoRecordToken, company.Content.Id)) (await author.PostAsync("/api/settings/company/logo", file)).StatusCode.Should().Be(HttpStatusCode.OK);
            using (var file = HelperImage(employee.Profile.PhotoRecordToken, photo.Content.Id)) (await author.PostAsync($"/api/employees/{employee.Id}/photo", file)).StatusCode.Should().Be(HttpStatusCode.OK);
            var photoRead = await author.GetAsync($"/api/employees/{employee.Id}/photo");
            photoRead.StatusCode.Should().Be(HttpStatusCode.OK);
            photoRead.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
            (await photoRead.Content.ReadAsByteArrayAsync()).Should().Equal(137, 80, 78, 71, 13, 10, 26, 10);
            var outside = await AuthedClientAsync("globex-admin@globex.local");
            (await outside.GetAsync($"/api/employees/{employee.Id}/photo")).IsSuccessStatusCode.Should().BeFalse();
            var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
            (await manager.GetAsync($"/api/employees/{employee.Id}/photo")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Should().HaveCount(2).And.NotIntersectWith(filesBefore);
            using (var file = HelperImage(settings.Company.LogoRecordToken, company.Content.Id)) (await author.PostAsync("/api/settings/company/logo", file)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            using (var file = HelperImage(employee.Profile.PhotoRecordToken, photo.Content.Id)) (await author.PostAsync($"/api/employees/{employee.Id}/photo", file)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            var proofs = await OwnOperationsAsync(author);
            proofs.Should().HaveCount(2).And.OnlyContain(x => x.Evidence.Outcome == "uploaded");
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            using var export = JsonDocument.Parse(await (await author.GetAsync("/api/me/data-export")).Content.ReadAsStringAsync());
            export.RootElement.GetProperty("guidanceOperations").GetArrayLength().Should().Be(2);
        }
        finally { await CleanupAsync("company-company-profile"); await CleanupAsync("company-departments"); await CleanupAsync("personnel-entry-tab-kimlik"); }
    }

    [Fact]
    public async Task CompanyHelpers_RemovedAptitudeFieldsAreAbsentAndExistingValuesSurviveCardUpdate()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var employee = await CreateProofEmployeeAsync(author);
        using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
        var db = scope.Services.GetRequiredService<AppDbContext>();
        var profile = await db.EmployeeProfiles.SingleAsync(x => x.EmployeeId == employee.Id);
        profile.CanWorkAtHeight = true; profile.CanWorkNightShift = true; profile.CanLiftHeavyLoads = true;
        await db.SaveChangesAsync();
        employee = (await author.GetFromJsonAsync<EmployeeDetailDto>($"/api/employees/{employee.Id}"))!;
        (await author.PutAsJsonAsync($"/api/employees/{employee.Id}", PersonnelBody(employee) with { Profile = new EmployeeProfileUpsertModel(HealthNotes: "Korunan sağlık notu") })).StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await (await author.GetAsync($"/api/employees/{employee.Id}")).Content.ReadAsStringAsync();
        json.Should().NotContain("canWorkAtHeight").And.NotContain("canWorkNightShift").And.NotContain("canLiftHeavyLoads");
        await db.Entry(profile).ReloadAsync();
        profile.CanWorkAtHeight.Should().BeTrue(); profile.CanWorkNightShift.Should().BeTrue(); profile.CanLiftHeavyLoads.Should().BeTrue();
        profile.HealthNotes.Should().Be("Korunan sağlık notu");
    }
}
