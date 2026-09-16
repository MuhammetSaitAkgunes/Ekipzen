using FluentAssertions;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Application.Features.Setup;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Entities.Settings;
using Ekipzen.Infrastructure.Persistence;
using Ekipzen.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ekipzen.Tests.Integration.Tenancy;

[Collection(ApiCollection.Name)]
public sealed class TenantSetupTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task FreshTenant_SetupThenTenEmployeesAndFirstPayroll_UsesNoDemoData()
    {
        var email = $"pilot-{Guid.NewGuid():N}@example.test";
        var tenant = await ProvisionAndActivateAsync("Pilot Muhasebe", email);
        var admin = await AuthedClientAsync(email);
        var before = await GetAsync<TenantSetupStatusDto>(admin, "/api/setup");
        before.NeedsInitialization.Should().BeTrue();
        before.MissingLeaveTypeCount.Should().Be(4);
        before.MissingFullDayHolidayCount.Should().Be(14);
        (await admin.GetAsync("/api/payroll/settings")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var initialized = await admin.PostAsync("/api/setup", null);
        initialized.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = (await initialized.Content.ReadFromJsonAsync<TenantSetupStatusDto>())!;
        status.NeedsInitialization.Should().BeFalse();
        status.HasEffectiveApprovedPayrollSettings.Should().BeFalse();
        var settings = await GetAsync<JsonObject>(admin, "/api/settings");
        settings["company"]!["name"]!.GetValue<string>().Should().Be("Pilot Muhasebe");

        using (var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(tenant.TenantId))
        {
            var db = scope.Services.GetRequiredService<AppDbContext>();
            (await db.Employees.CountAsync()).Should().Be(0);
            (await db.Departments.CountAsync()).Should().Be(0);
            (await db.EmployeeMetricSnapshots.CountAsync()).Should().Be(0);
            (await db.NotificationSettings.CountAsync()).Should().Be(0);
        }

        var drafts = await GetAsync<List<PayrollSettingsDto>>(admin, "/api/payroll/settings/drafts");
        var draft = drafts.Should().ContainSingle().Subject;
        draft.Status.Should().Be("draft");
        draft.Version.Should().Be(1);
        draft.ApprovedAtUtc.Should().BeNull();
        draft.ApprovedByUserId.Should().BeNull();
        draft.ReviewNotes.Should().BeNull();
        draft.TaxBrackets.Should().HaveCount(5);
        (await admin.PostAsJsonAsync($"/api/payroll/settings/{draft.Id}/approve",
            new { id = draft.Id, expectedVersion = draft.Version })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "taslak hesaplara giremez");

        var body = JsonSerializer.SerializeToNode(draft, JsonOptions)!.AsObject();
        body["expectedVersion"] = draft.Version;
        body["reviewNotes"] = "TEST: bağımsız uzman kabulünün API akışı; gerçek müşteri onayı değildir.";
        var savedResponse = await admin.PutAsJsonAsync("/api/payroll/settings", body);
        savedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = (await savedResponse.Content.ReadFromJsonAsync<PayrollSettingsDto>())!;
        (await admin.PostAsJsonAsync($"/api/payroll/settings/{saved.Id}/approve",
            new { id = saved.Id, expectedVersion = saved.Version })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<TenantSetupStatusDto>(admin, "/api/setup"))
            .HasEffectiveApprovedPayrollSettings.Should().BeTrue();

        var departmentResponse = await admin.PostAsJsonAsync("/api/departments", new { name = "Muhasebe", code = "MUH" });
        departmentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var department = (await departmentResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        for (var i = 1; i <= 10; i++)
        {
            var response = await admin.PostAsJsonAsync("/api/employees", new
            {
                firstName = $"Personel{i}", lastName = "Pilot", title = "Muhasebe Personeli",
                departmentId = department["id"]!.GetValue<int>(), hireDate = "2026-01-01",
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        var periodResponse = await admin.PostAsJsonAsync("/api/payroll/periods", new { year = 2026, month = 1 });
        periodResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var period = (await periodResponse.Content.ReadFromJsonAsync<PayrollPeriodDetailDto>(JsonOptions))!;
        period.Status.Should().Be("draft");
        period.Rows.Should().HaveCount(10);
    }

    [Fact]
    public async Task RepeatedAndConcurrentSetup_PreservesCustomRecordsAndExistingDraft()
    {
        var email = $"repeat-{Guid.NewGuid():N}@example.test";
        var tenant = await ProvisionAndActivateAsync("Korunan Büro", email);
        await SeedInTenantAsync(tenant.TenantId, db =>
        {
            db.CompanyProfiles.Add(new CompanyProfile { Name = "Müşterinin özel adı", Phone = "2125550101" });
            db.LeaveTypes.Add(new LeaveType { Code = "Yıllık", Name = "Özel izin adı", DeductsFromAnnualBalance = false });
            db.Holidays.Add(new Holiday { Date = new DateOnly(2026, 1, 1), Name = "Özel takvim adı", IsOfficial = false });
            var draft = TenantBaseline2026.CreatePayrollDraft();
            draft.OvertimeMultiplier = 2m;
            draft.ReviewNotes = "Korunacak inceleme";
            db.PayrollSettings.Add(draft);
            return Task.CompletedTask;
        });
        var admin = await AuthedClientAsync(email);
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => admin.PostAsync("/api/setup", null)));
        responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
        (await admin.PostAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(tenant.TenantId);
        var db = scope.Services.GetRequiredService<AppDbContext>();
        (await db.CompanyProfiles.SingleAsync()).Name.Should().Be("Müşterinin özel adı");
        (await db.LeaveTypes.CountAsync()).Should().Be(4);
        (await db.LeaveTypes.SingleAsync(x => x.Code == "Yıllık")).DeductsFromAnnualBalance.Should().BeFalse();
        (await db.Holidays.CountAsync()).Should().Be(14);
        (await db.Holidays.SingleAsync(x => x.Date == new DateOnly(2026, 1, 1))).Name.Should().Be("Özel takvim adı");
        var preserved = await db.PayrollSettings.SingleAsync();
        preserved.OvertimeMultiplier.Should().Be(2m);
        preserved.ReviewNotes.Should().Be("Korunacak inceleme");
        preserved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Setup_OnlyChangesAuthenticatedTenant()
    {
        var emailA = $"a-{Guid.NewGuid():N}@example.test";
        var emailB = $"b-{Guid.NewGuid():N}@example.test";
        await ProvisionAndActivateAsync("Büro A", emailA);
        var tenantB = await ProvisionAndActivateAsync("Büro B", emailB);
        var a = await AuthedClientAsync(emailA);
        (await a.PostAsJsonAsync("/api/setup", new { tenantId = tenantB.TenantId })).StatusCode.Should().Be(HttpStatusCode.OK);
        var b = await AuthedClientAsync(emailB);
        var status = await GetAsync<TenantSetupStatusDto>(b, "/api/setup");
        status.CompanyProfileExists.Should().BeFalse();
        status.PayrollSettingsExists.Should().BeFalse();
        status.MissingLeaveTypeCount.Should().Be(4);
    }

    [Theory]
    [InlineData("ahmet.yilmaz@hrmaster.local")]
    [InlineData("ece.arslan@hrmaster.local")]
    public async Task Setup_RejectsNonHrAdmins(string email)
    {
        var client = await AuthedClientAsync(email);
        (await client.GetAsync("/api/setup")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Setup_RequiresAuthentication()
    {
        var client = Factory.CreateClient();
        (await client.GetAsync("/api/setup")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/setup", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
