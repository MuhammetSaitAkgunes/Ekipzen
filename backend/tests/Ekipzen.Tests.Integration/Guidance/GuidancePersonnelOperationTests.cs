using System.Net;
using System.Net.Http.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Upsert;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Infrastructure.Persistence;
using Ekipzen.Infrastructure.Identity;
using Ekipzen.Infrastructure.Persistence.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Guidance;

public sealed partial class GuidanceCatalogTests
{
    [Fact]
    public async Task PersonnelEvidence_FailedHistoryInsertRollsBackCardUploadAndWithdrawal()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var employee = await CreateProofEmployeeAsync(author);
        using var first = new MultipartFormDataContent();
        first.Add(new ByteArrayContent("existing file"u8.ToArray()), "file", "existing.pdf");
        first.Add(new StringContent("Kontrol"), "documentType");
        var document = (await (await author.PostAsync($"/api/employees/{employee.Id}/documents", first)).Content.ReadFromJsonAsync<EmployeeDocumentDto>())!;
        using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
        var db = scope.Services.GetRequiredService<AppDbContext>();
        // Yalnız bu testin yeni oluşturulmuş şirket veritabanındaki sonraki INSERT'ler başarısız olur.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePersonnelEvidences] WITH NOCHECK ADD CONSTRAINT [CK_Test_PersonnelEvidenceFailure] CHECK ([Id] < 0)");
        try
        {
            (await author.PutAsJsonAsync($"/api/employees/{employee.Id}", PersonnelBody(employee) with { Title = "Kaydedilmemeli" })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await author.GetFromJsonAsync<EmployeeDetailDto>($"/api/employees/{employee.Id}"))!.Title.Should().Be(employee.Title);
            using var failed = new MultipartFormDataContent();
            failed.Add(new ByteArrayContent("new file"u8.ToArray()), "file", "new.pdf"); failed.Add(new StringContent("Kontrol"), "documentType");
            (await author.PostAsync($"/api/employees/{employee.Id}/documents", failed)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await author.GetFromJsonAsync<List<EmployeeDocumentDto>>($"/api/employees/{employee.Id}/documents"))!.Should().ContainSingle().Which.Id.Should().Be(document.Id);
            (await author.PostAsJsonAsync($"/api/employees/{employee.Id}/documents/{document.Id}/withdraw", new { reason = "Hata denemesi" })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await db.EmployeeDocuments.AsNoTracking().SingleAsync(x => x.Id == document.Id)).WithdrawnAtUtc.Should().BeNull();
            (await db.GuidancePersonnelEvidences.CountAsync()).Should().Be(1);
            (await OwnOperationsAsync(author)).Should().BeEmpty();
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE [GuidancePersonnelEvidences] DROP CONSTRAINT [CK_Test_PersonnelEvidenceFailure]"); }
    }

    private static EmployeeUpsertModel PersonnelBody(EmployeeDetailDto employee) => new(
        employee.FirstName, employee.LastName, employee.Title, employee.DepartmentId, employee.HireDate,
        employee.NationalId, employee.ManagerId, employee.Status, employee.ContractType,
        ExpectedRecordToken: employee.RecordToken);

    [Fact]
    public async Task PersonnelEvidence_CardNoOpStaleAndConcurrentChangesKeepActualPersonalProof()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        const string key = "personnel-record-review";
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "personnel", "record-review");
            var employee = await CreateProofEmployeeAsync(author);
            var body = PersonnelBody(employee) with { GuidanceContentRevisionId = source.Content.Id, CorrectionReason = "Belgeyle kontrol" };
            (await author.PutAsJsonAsync($"/api/employees/{employee.Id}", body)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await OwnOperationsAsync(author)).Should().BeEmpty("aynı değerleri kaydetmek düzeltme değildir");
            (await author.PutAsJsonAsync($"/api/employees/{employee.Id}", body with { CorrectionReason = null })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            body = body with { Title = "Doğrulanmış unvan" };
            var responses = await Task.WhenAll(author.PutAsJsonAsync($"/api/employees/{employee.Id}", body), author.PutAsJsonAsync($"/api/employees/{employee.Id}", body));
            responses.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            var proof = (await OwnOperationsAsync(author)).Should().ContainSingle().Subject;
            proof.IsCurrent.Should().BeTrue(); proof.Evidence.OperationKey.Should().Be("employee-update");
            proof.Evidence.PersonnelChanges!.EmployeeId.Should().Be(employee.Id);
            proof.Evidence.PersonnelChanges.ChangedFields.Should().BeEquivalentTo("İş / Unvan");
            proof.Evidence.PersonnelChanges.Reason.Should().Be("Belgeyle kontrol");
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            var history = (await author.GetFromJsonAsync<List<EmployeePersonnelHistoryDto>>($"/api/employees/{employee.Id}/personnel-history"))!;
            history.Should().ContainSingle().Which.ChangedFields.Should().BeEquivalentTo("İş / Unvan");
            history[0].OccurredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
            var fresh = (await author.GetFromJsonAsync<EmployeeDetailDto>($"/api/employees/{employee.Id}"))!;
            fresh.RecordToken.Should().NotBe(employee.RecordToken);
            (await author.PutAsJsonAsync($"/api/employees/{employee.Id}", body with { Title = "Eski form" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await author.GetFromJsonAsync<EmployeeDetailDto>($"/api/employees/{employee.Id}"))!.Title.Should().Be("Doğrulanmış unvan");
            var otherEmail = $"personnel-other-{Guid.NewGuid():N}@example.test";
            await ProvisionAndActivateAsync("Başka personel şirketi", otherEmail);
            var outsider = await AuthedClientAsync(otherEmail);
            (await outsider.GetAsync($"/api/employees/{employee.Id}/personnel-history")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var sql = new AddGuidancePersonnelEvidence().DownOperations.OfType<SqlOperation>().Single().Sql;
            Func<Task> guard = () => db.Database.ExecuteSqlRawAsync(sql);
            (await guard.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(51000);
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task PersonnelEvidence_DocumentSourceWithdrawalAndDailyInventoryFollowRealRecords()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var reviewer = team.Clients[1];
        const string key = "personnel-entry-tab-evrak";
        try
        {
            var source = await PublishOperationSourceAsync(author, reviewer, "personnel-entry", "tab-evrak");
            var employee = await CreateProofEmployeeAsync(author);
            var dailyKey = $"employee-documents:{employee.Id}";
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().Contain(x => x.Key == dailyKey && x.DueDate == null);
            using var upload = new MultipartFormDataContent();
            upload.Add(new ByteArrayContent("test file"u8.ToArray()), "file", "belge.pdf");
            upload.Add(new StringContent("Kontrol belgesi"), "documentType");
            upload.Add(new StringContent(source.Content.Id.ToString()), "guidanceContentRevisionId");
            var response = await author.PostAsync($"/api/employees/{employee.Id}/documents", upload);
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            var document = (await response.Content.ReadFromJsonAsync<EmployeeDocumentDto>())!;
            (await OwnOperationsAsync(author)).Should().ContainSingle().Which.Evidence.PersonnelChanges!.DocumentId.Should().Be(document.Id);
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().NotContain(x => x.Key == dailyKey);
            var body = new { reason = "Yanlış sürüm; doğru dosya ayrıca yüklenecek", guidanceContentRevisionId = source.Content.Id };
            var responses = await Task.WhenAll(author.PostAsJsonAsync($"/api/employees/{employee.Id}/documents/{document.Id}/withdraw", body), author.PostAsJsonAsync($"/api/employees/{employee.Id}/documents/{document.Id}/withdraw", body));
            responses.Select(x => x.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            var proofs = await OwnOperationsAsync(author);
            proofs.Should().HaveCount(2); proofs.Should().ContainSingle(x => x.Evidence.OperationKey == "employee-document-withdraw");
            (await OwnOperationsAsync(reviewer)).Should().BeEmpty();
            (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().Contain(x => x.Key == dailyKey);
            (await author.GetAsync($"/api/employees/{employee.Id}/documents/{document.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
            var db = scope.Services.GetRequiredService<AppDbContext>();
            var stored = await db.EmployeeDocuments.AsNoTracking().SingleAsync(x => x.Id == document.Id);
            await using var file = await scope.Services.GetRequiredService<IFileStorage>().OpenReadAsync(stored.FilePath, CancellationToken.None);
            file.Length.Should().Be(9, "geri çekme fiziksel dosyayı silmez");
        }
        finally { await CleanupAsync(key); }
    }

    [Fact]
    public async Task PersonnelEvidence_OrdinaryContactClearsTaskWithoutLearningCreditAndManagerChoicesAreScoped()
    {
        var team = await TeamAsync(); await using var host = team.Host;
        var author = team.Clients[0]; var employee = await CreateProofEmployeeAsync(author);
        var key = $"employee-contact:{employee.Id}";
        var tasks = (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!;
        tasks.Should().Contain(x => x.Key == key && x.TargetPath.EndsWith("tab=tab-iletisim") && x.DueDate == null);
        tasks.Should().NotContain(x => x.Key == $"employee-manager:{employee.Id}", "yönetici ataması zorunlu değil");
        var response = await author.PutAsJsonAsync($"/api/employees/{employee.Id}", PersonnelBody(employee) with { Profile = new EmployeeProfileUpsertModel(MobilePhone: "05320000000") });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await author.GetFromJsonAsync<List<DailyGuidanceTaskDto>>("/api/guidance/daily-tasks"))!.Should().NotContain(x => x.Key == key);
        (await OwnOperationsAsync(author)).Should().BeEmpty("olağan işlem kendiliğinden rehbere bağlanmaz");
        (await author.GetFromJsonAsync<List<EmployeePersonnelHistoryDto>>($"/api/employees/{employee.Id}/personnel-history"))!.Should().ContainSingle().Which.ChangedFields.Should().BeEquivalentTo("İletişim / Cep telefonu");
        var choices = (await author.GetFromJsonAsync<List<EmployeeManagerChoiceDto>>("/api/employees/manager-choices"))!;
        choices.Should().NotContain(x => x.Id == employee.Id, "hesap bağlı olmayan çalışan yönetici adayı değil");
        using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(team.TenantId);
        var db = scope.Services.GetRequiredService<AppDbContext>();
        var stored = await db.Employees.SingleAsync(x => x.Id == employee.Id);
        var managerUser = await db.Users.SingleAsync(x => x.Email!.StartsWith("editor-3-"));
        stored.UserId = managerUser.Id; await db.SaveChangesAsync();
        (await author.GetFromJsonAsync<List<EmployeeManagerChoiceDto>>("/api/employees/manager-choices"))!.Should().Contain(x => x.Id == employee.Id);
        (await author.GetFromJsonAsync<List<EmployeeManagerChoiceDto>>($"/api/employees/manager-choices?exceptEmployeeId={employee.Id}"))!.Should().NotContain(x => x.Id == employee.Id);
        var users = scope.Services.GetRequiredService<UserManager<ApplicationUser>>();
        managerUser.EmployeeId = employee.Id;
        (await users.UpdateAsync(managerUser)).Succeeded.Should().BeTrue();
        (await users.RemoveFromRoleAsync(managerUser, Roles.HrAdmin)).Succeeded.Should().BeTrue();
        (await users.AddToRoleAsync(managerUser, Roles.Manager)).Succeeded.Should().BeTrue();
        var subordinate = new Employee { FirstName = "Ekip", LastName = "Test", Title = "Uzman", DepartmentId = employee.DepartmentId,
            HireDate = employee.HireDate, ManagerId = employee.Id };
        db.Employees.Add(subordinate); await db.SaveChangesAsync();
        var manager = await AuthedClientAsync(managerUser.Email!);
        (await manager.GetFromJsonAsync<EmployeeDetailDto>($"/api/employees/{subordinate.Id}"))!.RecordToken.Should().BeNull();
        (await manager.GetAsync("/api/employees/manager-choices")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.GetAsync($"/api/employees/{employee.Id}/personnel-history")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var otherEmail = $"manager-other-{Guid.NewGuid():N}@example.test";
        await ProvisionAndActivateAsync("Başka yönetici şirketi", otherEmail);
        (await (await AuthedClientAsync(otherEmail)).GetFromJsonAsync<List<EmployeeManagerChoiceDto>>("/api/employees/manager-choices"))!.Should().BeEmpty();
    }
}
