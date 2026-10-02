using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Common.Models;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Employees.Offboarding;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Leaves;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.ReadModels;
using Ekipzen.Domain.Services;
using Ekipzen.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Personnel;

/// <summary>
/// Faz 3 uçtan uca: directory (rol kapsamı + filtre + sayfalama), personel kartı CRUD,
/// bulk-deactivate/status, departman CRUD ve dosya (foto + evrak) akışları.
/// Demo hesaplar: hr-admin ik@, manager ece.arslan@ (ekibi: Ahmet + Selin), employee ahmet.yilmaz@.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PersonnelTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";
    private readonly HttpClient _client = factory.CreateClient();

    // --- directory: kapsam ---

    [Fact]
    public async Task Directory_AsHrAdmin_ReturnsAllEmployeesPaged()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var page = await GetAsync<PagedResult<EmployeeListItemDto>>(client, "/api/employees?page=1&pageSize=50");

        page.Total.Should().BeGreaterThanOrEqualTo(4, "seed'de 4 personel var");
        page.Items.Select(i => i.Name).Should().Contain(["Ahmet Yılmaz", "Ayşe Demir", "Selin Koç", "Ece Arslan"]);

        var ahmet = page.Items.Single(i => i.Name == "Ahmet Yılmaz");
        ahmet.NationalIdMasked.Should().Be("123*****", "directory TC maskeli döner");
        ahmet.Department.Should().Be("Yazılım");
        ahmet.Status.Should().Be("active");
        ahmet.Initials.Should().Be("AY");
    }

    [Fact]
    public async Task Directory_AsManager_ReturnsOnlyOwnTeamAndSelf()
    {
        var client = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var page = await GetAsync<PagedResult<EmployeeListItemDto>>(client, "/api/employees?page=1&pageSize=50");

        page.Items.Select(i => i.Name).Should().BeEquivalentTo(
            ["Ece Arslan", "Ahmet Yılmaz", "Selin Koç"],
            "manager yalnız kendi ekibini + kendisini görür (Ayşe Demir İK'da, kapsam dışı)");
    }

    [Fact]
    public async Task Directory_AsEmployee_Returns403()
    {
        var client = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var response = await client.GetAsync("/api/employees");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "routes.js: personnel hr-admin+manager");
    }

    [Fact]
    public async Task Directory_FiltersBySearchAndStatus()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");

        var searched = await GetAsync<PagedResult<EmployeeListItemDto>>(client, "/api/employees?search=Ahmet");
        searched.Items.Should().OnlyContain(i => i.Name.Contains("Ahmet"));

        var passives = await GetAsync<PagedResult<EmployeeListItemDto>>(client, "/api/employees?status=passive&pageSize=100");
        passives.Items.Should().OnlyContain(i => i.Status == "passive");
        passives.Items.Select(i => i.Name).Should().Contain("Selin Koç");
    }

    // --- personel kartı: kapsam + TC maskeleme ---

    [Fact]
    public async Task GetEmployee_ManagerOutsideTeam_Returns403_InsideTeam_MasksTc()
    {
        var client = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var all = await AuthedClientAsync("ik@hrmaster.local");
        var directory = await GetAsync<PagedResult<EmployeeListItemDto>>(all, "/api/employees?search=Ayşe Demir");
        var ayseId = directory.Items.Single().Id;

        (await client.GetAsync($"/api/employees/{ayseId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var team = await GetAsync<PagedResult<EmployeeListItemDto>>(client, "/api/employees?search=Ahmet");
        var detail = await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{team.Items.Single().Id}");
        detail.NationalId.Should().Be("123*****", "TC yalnız hr-admin'e açık döner");
    }

    [Fact]
    public async Task GetEmployee_AsHrAdmin_ReturnsFullCard()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var directory = await GetAsync<PagedResult<EmployeeListItemDto>>(client, "/api/employees?search=Ahmet");

        var detail = await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{directory.Items.Single().Id}");

        detail.NationalId.Should().Be("12345678901");
        detail.Department.Should().Be("Yazılım");
        detail.ManagerName.Should().Be("Ece Arslan");
        detail.Profile.EmploymentType.Should().Be("Tam Zamanlı");
    }

    // --- CRUD ---

    [Fact]
    public async Task CreateUpdateEmployee_FullCard_PersistsProfileGroups()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(client, "/api/departments");
        var yazilim = departments.Single(d => d.Name == "Yazılım");

        var createResponse = await client.CreateSafeEmployeeAsync("/api/employees", new
        {
            firstName = "Mert",
            lastName = "Can",
            title = "Backend Developer",
            departmentId = yazilim.Id,
            hireDate = "2024-06-01",
            nationalId = "45678901234",
            status = "active",
            profile = new
            {
                gender = "Erkek",
                bloodType = "A Rh+",
                mobilePhone = "(532) 111 22 33",
                personalEmail = "mert.can@ornek.local",
                employmentType = "Uzaktan",
                iban = "TR12 0006 4000 0011 2345 6789 01",
                salaryType = "Net",
                pensionStatus = "Otomatik Katılım",
                tshirtSize = "L",
                canWorkNightShift = true,
                healthNotes = "Bilinen rahatsızlığı yok.",
            },
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;

        created.Name.Should().Be("Mert Can");
        created.Initials.Should().Be("MC");
        created.Profile.EmploymentType.Should().Be("Uzaktan");
        created.Profile.Iban.Should().Be("TR120006400000112345678901", "IBAN boşluksuz normalize edilir");
        (await createResponse.Content.ReadAsStringAsync()).Should().NotContain("canWorkNightShift");

        var updateResponse = await client.PutAsJsonAsync($"/api/employees/{created.Id}", new
        {
            firstName = "Mert",
            lastName = "Can",
            title = "Senior Backend Developer",
            departmentId = yazilim.Id,
            hireDate = "2024-06-01",
            nationalId = "45678901234",
            status = "active",
            profile = new { employmentType = "Tam Zamanlı", salaryType = "Brüt" },
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;

        updated.Title.Should().Be("Senior Backend Developer");
        updated.Profile.EmploymentType.Should().Be("Tam Zamanlı");
    }

    [Fact]
    public async Task CreateEmployee_WithInvalidTc_Returns400_WithDuplicateTc_Returns409()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(client, "/api/departments");
        var deptId = departments[0].Id;

        var invalid = await client.CreateSafeEmployeeAsync("/api/employees", new
        {
            firstName = "Hatalı",
            lastName = "Tc",
            title = "Test",
            departmentId = deptId,
            hireDate = "2024-01-01",
            nationalId = "123",
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var duplicate = await client.CreateSafeEmployeeAsync("/api/employees", new
        {
            firstName = "Kopya",
            lastName = "Tc",
            title = "Test",
            departmentId = deptId,
            hireDate = "2024-01-01",
            nationalId = "12345678901", // seed'deki Ahmet Yılmaz'ın TC'si
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EmploymentFoundation_VersionsCompensationWithoutRewritingHistory()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(client, "/api/departments"))[0].Id;
        var createdResponse = await client.CreateSafeEmployeeAsync("/api/employees", new
        {
            firstName = "Ücret",
            lastName = $"Geçmişi{Guid.NewGuid():N}",
            title = "Test Personeli",
            departmentId,
            hireDate = "2024-01-01",
            contractType = "fixed-term",
        });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createdResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
        created.ContractType.Should().Be("fixed-term");

        var initial = await GetAsync<EmployeeEmploymentFoundationDto>(
            client, $"/api/employees/{created.Id}/employment-foundation");
        initial.EmploymentPeriods.Should().ContainSingle();
        initial.EmploymentPeriods[0].StartDate.Should().Be(new DateOnly(2024, 1, 1));
        initial.EmploymentPeriods[0].ContractType.Should().Be("fixed-term");

        (await client.PostAsJsonAsync($"/api/employees/{created.Id}/compensation-packages", new
        {
            effectiveFrom = "2024-01-01",
            monthlyGrossSalary = 40000m,
            regularPremium = 2500m,
            roadAllowance = 1000m,
            mealAllowance = 2000m,
            otherRegularBenefits = 500m,
            sourceReference = "Ücret yazısı-2024",
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync(
            $"/api/employees/{created.Id}/compensation-packages", new
            {
                effectiveFrom = "2025-01-01",
                monthlyGrossSalary = 50000m,
                regularPremium = 3000m,
                roadAllowance = 1200m,
                mealAllowance = 2500m,
                otherRegularBenefits = 800m,
                sourceReference = "Ücret yazısı-2025",
            });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var foundation = (await secondResponse.Content
            .ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!;
        foundation.CompensationPackages.Should().HaveCount(2);
        foundation.CompensationPackages.Should().OnlyContain(
            package => package.EmploymentPeriodId == initial.EmploymentPeriods[0].Id);
        foundation.CompensationPackages[0].EffectiveTo.Should().BeNull();
        foundation.CompensationPackages[0].MonthlyRecurringTotal.Should().Be(57500m);
        foundation.CompensationPackages[1].EffectiveTo.Should().Be(new DateOnly(2024, 12, 31));

        (await client.PostAsJsonAsync($"/api/employees/{created.Id}/compensation-packages", new
        {
            effectiveFrom = "2025-01-01",
            monthlyGrossSalary = 60000m,
            sourceReference = "Mükerrer",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var draft = await SaveOffboardingDraftAsync(
            client, created.Id, new DateOnly(2025, 6, 30));
        var completed = await CompleteOffboardingAsync(client, created.Id, draft);
        completed.TerminationReason.Should().Be("employee-resignation");
        completed.NoticeHandling.Should().Be("not-applicable");
        completed.ContractTypeSnapshot.Should().Be("fixed-term");
        completed.MonthlyGrossSalarySnapshot.Should().Be(50000m);
        completed.CompensationPackageId.Should().Be(foundation.CompensationPackages[0].Id);
        completed.EmploymentPeriodId.Should().NotBeNull();

        var closed = await GetAsync<EmployeeEmploymentFoundationDto>(
            client, $"/api/employees/{created.Id}/employment-foundation");
        closed.EmploymentPeriods[0].EndDate.Should().Be(new DateOnly(2025, 6, 30));
        closed.CompensationPackages[0].EffectiveTo.Should().Be(new DateOnly(2025, 6, 30));
        var correctionUrl = $"/api/employees/{created.Id}/compensation-packages/{completed.CompensationPackageId}/correction-context";
        var locked = await GetAsync<CompensationPackageCorrectionContextDto>(client, correctionUrl);
        locked.CanCorrect.Should().BeFalse();
        locked.BlockingReasons.Should().Contain(reason => reason.Contains("işten ayrılış geçmişinde"));

        (await client.PutAsJsonAsync($"/api/employees/{created.Id}", new
        {
            firstName = created.FirstName,
            lastName = created.LastName,
            title = created.Title,
            departmentId,
            hireDate = "2024-02-01",
            status = "passive",
            contractType = "indefinite",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "pasif kart güncellemesi kapalı istihdam dönemini değiştirmemeli");

        var stillClosed = await GetAsync<EmployeeEmploymentFoundationDto>(
            client, $"/api/employees/{created.Id}/employment-foundation");
        stillClosed.EmploymentPeriods.Should().ContainSingle(period =>
            period.EndDate == new DateOnly(2025, 6, 30));

        var reversed = await ReverseOffboardingAsync(
            client, created.Id, completed, "İstihdam dönemi geri alma testi");
        reversed.Status.Should().Be("reversed");
        var reopened = await GetAsync<EmployeeEmploymentFoundationDto>(
            client, $"/api/employees/{created.Id}/employment-foundation");
        reopened.EmploymentPeriods[0].EndDate.Should().BeNull();
        reopened.CompensationPackages[0].EffectiveTo.Should().BeNull();
        var stillLocked = await GetAsync<CompensationPackageCorrectionContextDto>(client, correctionUrl);
        stillLocked.CanCorrect.Should().BeFalse("tamamlanan ayrılışın tarihsel ücret görüntüsü geri alma sonrasında da korunur");
    }

    [Fact]
    public async Task CompensationCorrection_VersionsUnusedPackageAndLocksPayrollSnapshot()
    {
        var auth = await LoginAsync("ik@hrmaster.local");
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var employeeId = await CreateMinimalEmployeeAsync(
            admin, departmentId, "ÜcretDüzeltme", $"Deneme{Guid.NewGuid():N}");
        var createResponse = await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/compensation-packages", new
            {
                effectiveFrom = "2024-01-01", monthlyGrossSalary = 40000m,
                regularPremium = 2000m, roadAllowance = 1000m,
                mealAllowance = 1500m, otherRegularBenefits = 500m,
                sourceReference = "İlk ücret yazısı",
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var foundation = (await createResponse.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!;
        var package = foundation.CompensationPackages.Single();
        package.Version.Should().Be(1);
        var url = $"/api/employees/{employeeId}/compensation-packages/{package.Id}";
        var context = await GetAsync<CompensationPackageCorrectionContextDto>(admin, $"{url}/correction-context");
        context.CanCorrect.Should().BeTrue();
        context.History.Should().BeEmpty();

        var correction = new
        {
            expectedVersion = 1, monthlyGrossSalary = 42000m,
            regularPremium = 2000m, roadAllowance = 1000m,
            mealAllowance = 1500m, otherRegularBenefits = 500m,
            sourceReference = "Düzeltilmiş ücret yazısı", reason = "İlk brüt tutar yanlış girildi",
        };
        (await manager.GetAsync($"{url}/correction-context"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.PutAsJsonAsync(url, correction))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var correctedResponse = await admin.PutAsJsonAsync(url, correction);
        correctedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var corrected = (await correctedResponse.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!;
        corrected.CompensationPackages.Single().Version.Should().Be(2);
        corrected.CompensationPackages.Single().EffectiveFrom.Should().Be(new DateOnly(2024, 1, 1));
        corrected.CompensationPackages.Single().MonthlyGrossSalary.Should().Be(42000m);
        context = await GetAsync<CompensationPackageCorrectionContextDto>(admin, $"{url}/correction-context");
        context.History.Should().ContainSingle();
        context.History[0].Before.MonthlyGrossSalary.Should().Be(40000m);
        context.History[0].After.MonthlyGrossSalary.Should().Be(42000m);
        context.History[0].Reason.Should().Be("İlk brüt tutar yanlış girildi");
        context.History[0].ActorUserId.Should().NotBeNullOrWhiteSpace();
        (await admin.PutAsJsonAsync(url, correction)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var settingsId = await db.PayrollSettings.Select(item => item.Id).FirstAsync();
            var payrollPeriod = new PayrollPeriod
            {
                Name = $"Düzeltme kontrolü {Guid.NewGuid():N}",
                Year = 2024, Month = 1, PayrollSettingsId = settingsId,
            };
            payrollPeriod.Employees.Add(new PayrollEmployee { EmployeeId = employeeId });
            db.PayrollPeriods.Add(payrollPeriod);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        context = await GetAsync<CompensationPackageCorrectionContextDto>(admin, $"{url}/correction-context");
        context.CanCorrect.Should().BeFalse();
        context.BlockingReasons.Should().Contain(reason => reason.Contains("bordro satırı"));
        var blocked = await admin.PutAsJsonAsync(url, new
        {
            expectedVersion = 2, monthlyGrossSalary = 43000m,
            regularPremium = 2000m, roadAllowance = 1000m,
            mealAllowance = 1500m, otherRegularBenefits = 500m,
            sourceReference = "Üçüncü yazı", reason = "Tekrar düzeltme",
        });
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var unchanged = await GetAsync<CompensationPackageCorrectionContextDto>(admin, $"{url}/correction-context");
        unchanged.Package.Version.Should().Be(2);
        unchanged.History.Should().ContainSingle();
    }

    [Fact]
    public async Task CompensationRemediation_ReviewsImpactsWithoutChangingHistoricalPayroll()
    {
        var auth = await LoginAsync("ik@hrmaster.local");
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var employeeId = await CreateMinimalEmployeeAsync(
            admin, departmentId, "Telafi", $"Inceleme{Guid.NewGuid():N}");
        var createPackage = await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/compensation-packages", new
            {
                effectiveFrom = "2039-01-01", monthlyGrossSalary = 40000m,
                regularPremium = 1000m, roadAllowance = 0m, mealAllowance = 0m,
                otherRegularBenefits = 0m, sourceReference = "İlk karar",
            });
        createPackage.StatusCode.Should().Be(HttpStatusCode.OK);
        var packageId = (await createPackage.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!
            .CompensationPackages.Single().Id;
        int payrollRowId, payrollPeriodId;
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var settingsId = await db.PayrollSettings.Select(item => item.Id).FirstAsync();
            var period = new PayrollPeriod
            {
                Name = $"Telafi inceleme {Guid.NewGuid():N}",
                Year = 2040, Month = 1, PayrollSettingsId = settingsId,
            };
            var row = new PayrollEmployee { EmployeeId = employeeId, GrossSalary = 40000m };
            period.Employees.Add(row);
            db.PayrollPeriods.Add(period);
            await db.SaveChangesAsync(CancellationToken.None);
            payrollRowId = row.Id;
            payrollPeriodId = period.Id;
        }

        var url = $"/api/employees/{employeeId}/compensation-packages/{packageId}/remediation-case";
        (await manager.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var before = await GetAsync<CompensationRemediationContextDto>(admin, url);
        before.CanOpen.Should().BeTrue();
        before.CurrentImpact.PotentialPayrollRows.Should().ContainSingle(row => row.RowId == payrollRowId);
        var proposal = new
        {
            expectedPackageVersion = 1, expectedCaseVersion = (int?)null,
            issueDescription = "İlk ücret kararındaki brüt tutar yanlış okundu.",
            proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 42000m, regularPremium = 1000m,
            roadAllowance = 0m, mealAllowance = 0m, otherRegularBenefits = 0m,
            sourceReference = "Uzman inceleme belgesi",
        };
        var openedResponse = await admin.PostAsJsonAsync(url, proposal);
        openedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var opened = (await openedResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        opened.Status.Should().Be("draft");
        opened.ImpactSnapshot.PotentialPayrollRows.Should().ContainSingle();
        (await admin.PostAsJsonAsync(url, proposal)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.PostAsJsonAsync(url, proposal)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var updatedResponse = await admin.PutAsJsonAsync($"{url}/{opened.Id}", new
        {
            expectedPackageVersion = 1, expectedCaseVersion = (int?)opened.Version,
            issueDescription = "Kaynak belgeyle karşılaştırılan brüt tutar hatası.",
            proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 43000m, regularPremium = 1000m,
            roadAllowance = 0m, mealAllowance = 0m, otherRegularBenefits = 0m,
            sourceReference = "Uzman inceleme belgesi 2",
        });
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        updated.Version.Should().Be(2);
        updated.ProposedValues.MonthlyGrossSalary.Should().Be(43000m);
        (await admin.PostAsJsonAsync($"{url}/{updated.Id}/approve", new
        {
            expectedCaseVersion = updated.Version, expertReviewReference = "Uzman raporu 42",
            reviewNotes = "Kaynak ve bordro satırı incelendi.", confirmExpertReview = false,
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var row = await db.PayrollEmployees.SingleAsync(item => item.Id == payrollRowId);
            row.GrossSalary = 41000m;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        (await admin.PostAsJsonAsync($"{url}/{updated.Id}/approve", new
        {
            expectedCaseVersion = updated.Version, expertReviewReference = "Uzman raporu 42",
            reviewNotes = "Kaynak ve bordro satırı incelendi.", confirmExpertReview = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "etki görüntüsü değişirse uzman incelemesi yenilenmelidir");
        var refreshedResponse = await admin.PutAsJsonAsync($"{url}/{updated.Id}", new
        {
            expectedPackageVersion = 1, expectedCaseVersion = (int?)updated.Version,
            issueDescription = updated.IssueDescription, proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 43000m, regularPremium = 1000m,
            roadAllowance = 0m, mealAllowance = 0m, otherRegularBenefits = 0m,
            sourceReference = "Uzman inceleme belgesi 2",
        });
        refreshedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = (await refreshedResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        refreshed.ImpactSnapshot.PotentialPayrollRows.Single().RecordedGrossSalary.Should().Be(41000m);
        var approvedResponse = await admin.PostAsJsonAsync($"{url}/{updated.Id}/approve", new
        {
            expectedCaseVersion = refreshed.Version, expertReviewReference = "Uzman raporu 42",
            reviewNotes = "Kaynak ve bordro satırı incelendi.", confirmExpertReview = true,
        });
        approvedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = (await approvedResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        approved.Status.Should().Be("approved");
        approved.ReviewedByUserId.Should().Be(opened.OpenedByUserId,
            "kullanıcı kararı aynı İK yöneticisinin uzman referansıyla onayına izin verir");
        (await admin.PutAsJsonAsync($"{url}/{approved.Id}", proposal))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var foundation = await GetAsync<EmployeeEmploymentFoundationDto>(
            admin, $"/api/employees/{employeeId}/employment-foundation");
        foundation.CompensationPackages.Single().MonthlyGrossSalary.Should().Be(40000m);
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.PayrollEmployees.SingleAsync(row => row.Id == payrollRowId))
                .GrossSalary.Should().Be(41000m);
        }
        var newCaseResponse = await admin.PostAsJsonAsync(url, new
        {
            expectedPackageVersion = 1, expectedCaseVersion = (int?)null,
            issueDescription = "Sonradan bulunan yeni ücret hatası.",
            proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 44000m, regularPremium = 1000m,
            roadAllowance = 0m, mealAllowance = 0m,
            otherRegularBenefits = 0m, sourceReference = "Yeni uzman belgesi",
        });
        newCaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var historicalPreviewResponse = await admin.GetAsync(
            $"{url}/{approved.Id}/payroll-periods/{payrollPeriodId}/rows/{payrollRowId}/preview");
        historicalPreviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var historicalPreview = JsonDocument.Parse(
            await historicalPreviewResponse.Content.ReadAsStringAsync());
        historicalPreview.RootElement.GetProperty("blockingReasons").EnumerateArray()
            .Select(item => item.GetString()).Should().Contain(reason =>
                reason!.Contains("yeni uzman inceleme dosyası"));
    }

    [Fact]
    public async Task CompensationRemediation_AppliesOnlyReviewedUnapprovedPayrollRowAfterFreshPreview()
    {
        var payrollJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        payrollJsonOptions.Converters.Add(new JsonStringEnumConverter());
        var auth = await LoginAsync("ik@hrmaster.local");
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var employeeId = await CreateMinimalEmployeeAsync(
            admin, departmentId, "Telafi", $"Bordro{Guid.NewGuid():N}");
        var packageResponse = await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/compensation-packages", new
            {
                effectiveFrom = "2039-01-01", monthlyGrossSalary = 40000m,
                regularPremium = 1000m, roadAllowance = 200m, mealAllowance = 300m,
                otherRegularBenefits = 400m, sourceReference = "İlk ücret kararı",
            });
        packageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var packageId = (await packageResponse.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!
            .CompensationPackages.Single().Id;
        int periodId, rowId, approvedPeriodId, approvedRowId;
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var settingsId = await db.PayrollSettings.Select(item => item.Id).FirstAsync();
            var period = new PayrollPeriod
            {
                Name = $"Telafi bordro {Guid.NewGuid():N}", Year = 2040, Month = 2,
                PayrollSettingsId = settingsId,
            };
            var row = new PayrollEmployee
            {
                EmployeeId = employeeId, GrossSalary = 40000m, PremiumPay = 1000m,
                RoadAllowance = 200m, MealAllowance = 300m, BenefitPay = 400m,
            };
            period.Employees.Add(row);
            db.PayrollPeriods.Add(period);
            var approvedPeriod = new PayrollPeriod
            {
                Name = $"Onaylı telafi bordro {Guid.NewGuid():N}", Year = 2040, Month = 3,
                PayrollSettingsId = settingsId, Status = PayrollPeriodStatus.Approved,
            };
            var approvedRow = new PayrollEmployee
            {
                EmployeeId = employeeId, GrossSalary = 40000m, PremiumPay = 1000m,
                RoadAllowance = 200m, MealAllowance = 300m, BenefitPay = 400m,
                ApprovalStatus = PayrollApprovalStatus.Approved,
                StatutoryProfile = PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA,
                Result = new PayrollResult
                {
                    GrossEarnings = 41900m, IncomeTaxBase = 35000m,
                    IncomeTax = 5250m, NetPay = 30000m, EmployerCost = 51000m,
                    CalculatedAtUtc = DateTime.UtcNow,
                },
            };
            approvedPeriod.Employees.Add(approvedRow);
            db.PayrollPeriods.Add(approvedPeriod);
            var laterPeriod = new PayrollPeriod
            {
                Name = $"Sonraki telafi bordro {Guid.NewGuid():N}", Year = 2040, Month = 4,
                PayrollSettingsId = settingsId, Status = PayrollPeriodStatus.Approved,
            };
            laterPeriod.Employees.Add(new PayrollEmployee
            {
                EmployeeId = employeeId, GrossSalary = 40000m,
                ApprovalStatus = PayrollApprovalStatus.Approved,
            });
            db.PayrollPeriods.Add(laterPeriod);
            await db.SaveChangesAsync(CancellationToken.None);
            periodId = period.Id;
            rowId = row.Id;
            approvedPeriodId = approvedPeriod.Id;
            approvedRowId = approvedRow.Id;
        }
        var caseUrl = $"/api/employees/{employeeId}/compensation-packages/{packageId}/remediation-case";
        var openedResponse = await admin.PostAsJsonAsync(caseUrl, new
        {
            expectedPackageVersion = 1, expectedCaseVersion = (int?)null,
            issueDescription = "Kaynak karardaki brüt ücret yanlış okundu.",
            proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 42000m, regularPremium = 1000m,
            roadAllowance = 200m, mealAllowance = 300m,
            otherRegularBenefits = 400m, sourceReference = "Uzman kararı",
        });
        openedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var opened = (await openedResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        var rowUrl = $"{caseUrl}/{opened.Id}/payroll-periods/{periodId}/rows/{rowId}";
        var approvedRowUrl = $"{caseUrl}/{opened.Id}/payroll-periods/{approvedPeriodId}/rows/{approvedRowId}";
        (await admin.GetAsync($"{approvedRowUrl}/approved-impact"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var draftPreview = (await admin.GetFromJsonAsync<CompensationPayrollPreviewDto>(
            $"{rowUrl}/preview", payrollJsonOptions))!;
        draftPreview.CanApply.Should().BeFalse();
        (await manager.GetAsync($"{rowUrl}/preview")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var approvalResponse = await admin.PostAsJsonAsync($"{caseUrl}/{opened.Id}/approve", new
        {
            expectedCaseVersion = opened.Version, expertReviewReference = "Uzman raporu 43",
            reviewNotes = "Satır ve ilk ücret kararı karşılaştırıldı.", confirmExpertReview = true,
        });
        approvalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var impact = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            $"{approvedRowUrl}/approved-impact", payrollJsonOptions))!;
        impact.CanEstimate.Should().BeTrue();
        impact.Recorded.NetPay.Should().Be(30000m);
        impact.Hypothetical.Should().NotBeNull();
        impact.LaterSameYearRows.Should().ContainSingle(item => item.Month == 4);
        (await manager.GetAsync($"{approvedRowUrl}/approved-impact"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var stored = await db.PayrollEmployees.Include(item => item.Result)
                .SingleAsync(item => item.Id == approvedRowId);
            stored.GrossSalary.Should().Be(40000m);
            stored.Result!.NetPay.Should().Be(30000m);
            stored.GrossSalary = 41000m;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var changedImpact = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            $"{approvedRowUrl}/approved-impact", payrollJsonOptions))!;
        changedImpact.CanEstimate.Should().BeFalse();
        changedImpact.ReviewWarnings.Should().Contain(reason => reason.Contains("değişti"));
        var approvedPreview = (await admin.GetFromJsonAsync<CompensationPayrollPreviewDto>(
            $"{approvedRowUrl}/preview", payrollJsonOptions))!;
        approvedPreview.CanApply.Should().BeFalse();
        approvedPreview.BlockingReasons.Should().Contain(reason => reason.Contains("Onaylanmış"));
        (await admin.PostAsJsonAsync($"{approvedRowUrl}/apply", new
        {
            expectedPreviewToken = approvedPreview.PreviewToken, confirmSourceLink = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var preview = (await admin.GetFromJsonAsync<CompensationPayrollPreviewDto>(
            $"{rowUrl}/preview", payrollJsonOptions))!;
        preview.CanApply.Should().BeTrue();
        preview.Before.GrossSalary.Should().Be(40000m);
        preview.After!.GrossSalary.Should().Be(42000m);
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var row = await db.PayrollEmployees.SingleAsync(item => item.Id == rowId);
            row.GrossSalary = 41000m;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var changedPreview = (await admin.GetFromJsonAsync<CompensationPayrollPreviewDto>(
            $"{rowUrl}/preview", payrollJsonOptions))!;
        changedPreview.CanApply.Should().BeFalse();
        changedPreview.BlockingReasons.Should().Contain(reason => reason.Contains("eşleşmiyor"));
        (await admin.PostAsJsonAsync($"{rowUrl}/apply", new
        {
            expectedPreviewToken = preview.PreviewToken, confirmSourceLink = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var row = await db.PayrollEmployees.SingleAsync(item => item.Id == rowId);
            row.GrossSalary = 40000m;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var refreshedPreview = (await admin.GetFromJsonAsync<CompensationPayrollPreviewDto>(
            $"{rowUrl}/preview", payrollJsonOptions))!;
        refreshedPreview.CanApply.Should().BeTrue();
        refreshedPreview.PreviewToken.Should().NotBe(preview.PreviewToken);
        (await admin.PostAsJsonAsync($"{rowUrl}/apply", new
        {
            expectedPreviewToken = preview.PreviewToken, confirmSourceLink = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"{rowUrl}/apply", new
        {
            expectedPreviewToken = refreshedPreview.PreviewToken, confirmSourceLink = false,
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync($"{rowUrl}/apply", new
        {
            expectedPreviewToken = new string('A', 64), confirmSourceLink = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await manager.PostAsJsonAsync($"{rowUrl}/apply", new
        {
            expectedPreviewToken = preview.PreviewToken, confirmSourceLink = true,
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var appliedResponse = await admin.PostAsJsonAsync($"{rowUrl}/apply", new
        {
            expectedPreviewToken = refreshedPreview.PreviewToken, confirmSourceLink = true,
        });
        appliedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var applied = (await appliedResponse.Content.ReadFromJsonAsync<CompensationPayrollApplicationDto>(
            payrollJsonOptions))!;
        applied.Before.GrossSalary.Should().Be(40000m);
        applied.After.GrossSalary.Should().Be(42000m);
        applied.After.ApprovalStatus.Should().NotBe("Onaylandı");
        (await admin.PostAsJsonAsync($"{rowUrl}/apply", new
        {
            expectedPreviewToken = preview.PreviewToken, confirmSourceLink = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var afterPreview = (await admin.GetFromJsonAsync<CompensationPayrollPreviewDto>(
            $"{rowUrl}/preview", payrollJsonOptions))!;
        afterPreview.CanApply.Should().BeFalse();
        afterPreview.Application!.Id.Should().Be(applied.Id);
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.PayrollEmployees.SingleAsync(item => item.Id == rowId)).GrossSalary
                .Should().Be(42000m);
            (await db.EmployeeCompensationPayrollApplications.CountAsync(item =>
                item.PayrollRowId == rowId)).Should().Be(1);
            (await db.EmployeeCompensationPackages.SingleAsync(item => item.Id == packageId))
                .MonthlyGrossSalary.Should().Be(40000m);
        }
    }

    [Fact]
    public async Task ApprovedPayrollChain_AppliesAllMonthsAtomicallyAndKeepsSeparateEvidence()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        var auth = await LoginAsync("ik@hrmaster.local");
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var employeeId = await CreateMinimalEmployeeAsync(admin, departmentId,
            "Zincir", $"Bordro{Guid.NewGuid():N}");
        var packageResponse = await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/compensation-packages", new
            {
                effectiveFrom = "2042-01-01", monthlyGrossSalary = 50000m,
                regularPremium = 0m, roadAllowance = 0m, mealAllowance = 0m,
                otherRegularBenefits = 0m, sourceReference = "İlk ücret kararı",
            });
        packageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var packageId = (await packageResponse.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!
            .CompensationPackages.Single().Id;
        int periodId, rowId, laterPeriodId, laterRowId;
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var settings = await db.PayrollSettings.Include(item => item.TaxBrackets)
                .FirstAsync(item => item.Status == PayrollSettingsStatus.Approved);
            var engine = scope.Services.GetRequiredService<IPayrollEngine>();
            var first = new PayrollPeriod
            {
                Name = $"Zincir ilk {Guid.NewGuid():N}", Year = 2043, Month = 1,
                PayrollSettingsId = settings.Id, Status = PayrollPeriodStatus.Approved,
            };
            var source = new PayrollEmployee
            {
                EmployeeId = employeeId, GrossSalary = 50000m,
                StatutoryProfile = PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA,
                WorkedDays = settings.DefaultWorkedDays, SgkPremiumDaysConfirmed = true,
                IbanComplete = true, TimesheetComplete = true,
                DisabilityReliefType = PayrollDisabilityReliefType.None,
                OtherIncomeTaxBaseDeductionsConfirmedNone = true,
                CumulativeIncomeTaxBaseConfirmed = true,
                ApprovalStatus = PayrollApprovalStatus.Approved,
            };
            var firstCalculation = engine.Calculate(source.ToEngineInput(0),
                settings.ToEngineParameters());
            source.Result = new PayrollResult
            {
                GrossEarnings = firstCalculation.GrossEarnings,
                IncomeTaxBase = firstCalculation.IncomeTaxBase,
                IncomeTax = firstCalculation.IncomeTax,
                NetPay = firstCalculation.NetPay,
                EmployerCost = firstCalculation.EmployerCost,
                CalculatedAtUtc = DateTime.UtcNow,
            };
            first.Employees.Add(source);
            db.PayrollPeriods.Add(first);
            var second = new PayrollPeriod
            {
                Name = $"Zincir ikinci {Guid.NewGuid():N}", Year = 2043, Month = 2,
                PayrollSettingsId = settings.Id, Status = PayrollPeriodStatus.Approved,
            };
            var later = new PayrollEmployee
            {
                EmployeeId = employeeId, GrossSalary = 50000m,
                StatutoryProfile = PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA,
                WorkedDays = settings.DefaultWorkedDays, SgkPremiumDaysConfirmed = true,
                IbanComplete = true, TimesheetComplete = true,
                DisabilityReliefType = PayrollDisabilityReliefType.None,
                OtherIncomeTaxBaseDeductionsConfirmedNone = true,
                CumulativeIncomeTaxBaseConfirmed = true,
                PreviousTaxBase = firstCalculation.IncomeTaxBase,
                ApprovalStatus = PayrollApprovalStatus.Approved,
            };
            var laterCalculation = engine.Calculate(later.ToEngineInput(0),
                settings.ToEngineParameters());
            later.Result = new PayrollResult
            {
                GrossEarnings = laterCalculation.GrossEarnings,
                IncomeTaxBase = laterCalculation.IncomeTaxBase,
                IncomeTax = laterCalculation.IncomeTax,
                NetPay = laterCalculation.NetPay,
                EmployerCost = laterCalculation.EmployerCost,
                CalculatedAtUtc = DateTime.UtcNow,
            };
            second.Employees.Add(later);
            db.PayrollPeriods.Add(second);
            await db.SaveChangesAsync(CancellationToken.None);
            periodId = first.Id; rowId = source.Id;
            laterPeriodId = second.Id; laterRowId = later.Id;
        }
        var caseUrl = $"/api/employees/{employeeId}/compensation-packages/{packageId}/remediation-case";
        var openedResponse = await admin.PostAsJsonAsync(caseUrl, new
        {
            expectedPackageVersion = 1, expectedCaseVersion = (int?)null,
            issueDescription = "Zincirleme ücret hatası.", proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 52000m, regularPremium = 0m,
            roadAllowance = 0m, mealAllowance = 0m,
            otherRegularBenefits = 0m, sourceReference = "Uzman ücret kararı",
        });
        openedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var opened = (await openedResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        (await admin.PostAsJsonAsync($"{caseUrl}/{opened.Id}/approve", new
        {
            expectedCaseVersion = opened.Version, expertReviewReference = "Zincir raporu",
            reviewNotes = "Kaynak ve dönemler doğrulandı.", confirmExpertReview = true,
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        var impactUrl = $"{caseUrl}/{opened.Id}/payroll-periods/{periodId}/rows/{rowId}/approved-impact";
        var impact = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            impactUrl, jsonOptions))!;
        impact.CanApplyChain.Should().BeTrue();
        var body = new
        {
            expectedPreviewToken = impact.ChainImpact.PreviewToken,
            expertReviewReference = "Zincir son onayı", reviewNotes = "İki ay birlikte incelendi.",
            confirmSourceLink = true, confirmChainImpact = true,
            confirmExternalReconciliation = true,
        };
        (await manager.PostAsJsonAsync($"{impactUrl}/chain/apply", body))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"{impactUrl}/chain/apply", new
        {
            expectedPreviewToken = new string('0', 64),
            expertReviewReference = "Zincir son onayı", reviewNotes = "Bayat önizleme.",
            confirmSourceLink = true, confirmChainImpact = true,
            confirmExternalReconciliation = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.PayrollApprovedRowRevisions.CountAsync(item =>
                item.PayrollRowId == rowId || item.PayrollRowId == laterRowId)).Should().Be(0);
        }
        var response = await admin.PostAsJsonAsync($"{impactUrl}/chain/apply", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var batch = (await response.Content.ReadFromJsonAsync<ApprovedPayrollRevisionBatchDto>(jsonOptions))!;
        batch.Rows.Should().HaveCount(2);
        batch.Rows.Should().OnlyContain(item => item.Revision.BatchId == batch.Id);
        batch.Rows.Select(item => item.PeriodId).Should().BeEquivalentTo([periodId, laterPeriodId]);
        var sourceRevision = batch.Rows.Single(item => item.Revision.PayrollRowId == rowId).Revision;
        var laterRevision = batch.Rows.Single(item => item.Revision.PayrollRowId == laterRowId).Revision;
        sourceRevision.After.GrossSalary.Should().Be(52000m);
        laterRevision.After.PreviousTaxBase.Should().Be(sourceRevision.After.IncomeTaxBase);
        (await admin.PostAsJsonAsync($"{impactUrl}/chain/apply", body))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var reopened = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            impactUrl, jsonOptions))!;
        reopened.BatchRevisions.Should().HaveCount(2);
        var laterDetail = (await admin.GetFromJsonAsync<PayrollPeriodDetailDto>(
            $"/api/payroll/periods/{laterPeriodId}", jsonOptions))!;
        laterDetail.Rows.Single(item => item.Id == laterRowId).NetPay
            .Should().Be(laterRevision.After.NetPay);
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            (await db.PayrollEmployees.SingleAsync(item => item.Id == rowId))
                .GrossSalary.Should().Be(50000m);
            (await db.PayrollEmployees.SingleAsync(item => item.Id == laterRowId))
                .PreviousTaxBase.Should().Be(sourceRevision.Before.IncomeTaxBase);
            (await db.PayrollApprovedRowRevisions.CountAsync(item => item.BatchId == batch.Id))
                .Should().Be(2);
        }
        foreach (var item in batch.Rows)
        {
            var evidenceUrl = $"{caseUrl}/{opened.Id}/payroll-periods/{item.PeriodId}"
                + $"/rows/{item.Revision.PayrollRowId}/revisions/{item.Revision.Id}/external-reconciliation";
            var evidence = (await admin.GetFromJsonAsync<PayrollRevisionExternalReconciliationDto>(
                evidenceUrl, jsonOptions))!;
            evidence.Payment.Should().BeNull();
            evidence.Notification.Should().BeNull();
        }
        var paymentKey = $"payroll-revision-external-payment:{sourceRevision.Id}";
        var laterPaymentKey = $"payroll-revision-external-payment:{laterRevision.Id}";
        var tasks = await GetAsync<List<DailyGuidanceTaskDto>>(admin, "/api/guidance/daily-tasks");
        tasks.Should().ContainSingle(item => item.Key == paymentKey);
        tasks.Should().ContainSingle(item => item.Key == laterPaymentKey);
        tasks.Should().ContainSingle(item => item.Key
            == $"payroll-revision-external-notification:{sourceRevision.Id}");
        tasks.Should().ContainSingle(item => item.Key
            == $"payroll-revision-external-notification:{laterRevision.Id}");
        var firstEvidenceUrl = $"{caseUrl}/{opened.Id}/payroll-periods/{periodId}"
            + $"/rows/{rowId}/revisions/{sourceRevision.Id}/external-reconciliation";
        (await admin.PostAsJsonAsync(firstEvidenceUrl, new
        {
            kind = "payment", outcome = "not-required", responsibleName = "Muhasebe",
            evidenceReference = "Banka uzlaştırma kaydı", actionDate = (string?)null,
            recordedAmount = (decimal?)null, notes = "Bu ay ek ödeme gerekmiyor.",
            expectedLatestRecordId = (int?)null, confirmRecordedStatus = true,
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        var remaining = await GetAsync<List<DailyGuidanceTaskDto>>(admin, "/api/guidance/daily-tasks");
        remaining.Should().NotContain(item => item.Key == paymentKey);
        remaining.Should().ContainSingle(item => item.Key == laterPaymentKey);
    }

    [Fact]
    public async Task ApprovedPayrollRevision_PreservesOriginalAndBlocksLaterApprovedRows()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        var auth = await LoginAsync("ik@hrmaster.local");
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var employeeId = await CreateMinimalEmployeeAsync(
            admin, departmentId, "Revizyon", $"Bordro{Guid.NewGuid():N}");
        var packageResponse = await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/compensation-packages", new
            {
                effectiveFrom = "2039-01-01", monthlyGrossSalary = 50000m,
                regularPremium = 0m, roadAllowance = 0m, mealAllowance = 0m,
                otherRegularBenefits = 0m, sourceReference = "İlk ücret kararı",
            });
        packageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var packageId = (await packageResponse.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!
            .CompensationPackages.Single().Id;

        int periodId, rowId, laterPeriodId, laterRowId, finalRowId;
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var settings = await db.PayrollSettings.Include(item => item.TaxBrackets)
                .FirstAsync(item => item.Status == PayrollSettingsStatus.Approved);
            var period = new PayrollPeriod
            {
                Name = $"Revizyon bordro {Guid.NewGuid():N}", Year = 2041, Month = 8,
                PayrollSettingsId = settings.Id, Status = PayrollPeriodStatus.Approved,
            };
            var row = new PayrollEmployee
            {
                EmployeeId = employeeId, GrossSalary = 50000m,
                StatutoryProfile = PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA,
                WorkedDays = settings.DefaultWorkedDays, SgkPremiumDaysConfirmed = true,
                IbanComplete = true, TimesheetComplete = true,
                DisabilityReliefType = PayrollDisabilityReliefType.None,
                OtherIncomeTaxBaseDeductionsConfirmedNone = true,
                CumulativeIncomeTaxBaseConfirmed = true,
                ApprovalStatus = PayrollApprovalStatus.Approved,
                Result = new PayrollResult
                {
                    GrossEarnings = 50000m, IncomeTaxBase = 42500m,
                    IncomeTax = 6375m, TotalDeductions = 13000m,
                    NetPay = 37000m, EmployerCost = 61000m,
                    CalculatedAtUtc = DateTime.UtcNow,
                },
            };
            period.Employees.Add(row);
            db.PayrollPeriods.Add(period);
            var later = new PayrollPeriod
            {
                Name = $"Revizyon sonrası {Guid.NewGuid():N}", Year = 2041, Month = 9,
                PayrollSettingsId = settings.Id, Status = PayrollPeriodStatus.Approved,
            };
            var laterRow = new PayrollEmployee
            {
                EmployeeId = employeeId, GrossSalary = 50000m,
                StatutoryProfile = PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA,
                WorkedDays = settings.DefaultWorkedDays, SgkPremiumDaysConfirmed = true,
                IbanComplete = true, TimesheetComplete = true,
                DisabilityReliefType = PayrollDisabilityReliefType.None,
                OtherIncomeTaxBaseDeductionsConfirmedNone = true,
                CumulativeIncomeTaxBaseConfirmed = true,
                PreviousTaxBase = 42500m,
                ApprovalStatus = PayrollApprovalStatus.Approved,
            };
            var engine = scope.Services.GetRequiredService<IPayrollEngine>();
            var laterCalculation = engine.Calculate(laterRow.ToEngineInput(0),
                settings.ToEngineParameters());
            laterRow.Result = new PayrollResult
            {
                GrossEarnings = laterCalculation.GrossEarnings,
                IncomeTaxBase = laterCalculation.IncomeTaxBase,
                IncomeTax = laterCalculation.IncomeTax,
                NetPay = laterCalculation.NetPay,
                EmployerCost = laterCalculation.EmployerCost,
                CalculatedAtUtc = DateTime.UtcNow,
            };
            later.Employees.Add(laterRow);
            db.PayrollPeriods.Add(later);
            var final = new PayrollPeriod
            {
                Name = $"Revizyon zinciri {Guid.NewGuid():N}", Year = 2041, Month = 10,
                PayrollSettingsId = settings.Id, Status = PayrollPeriodStatus.Approved,
            };
            var finalRow = new PayrollEmployee
            {
                EmployeeId = employeeId, GrossSalary = 50000m,
                StatutoryProfile = PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA,
                WorkedDays = settings.DefaultWorkedDays, SgkPremiumDaysConfirmed = true,
                IbanComplete = true, TimesheetComplete = true,
                DisabilityReliefType = PayrollDisabilityReliefType.None,
                OtherIncomeTaxBaseDeductionsConfirmedNone = true,
                CumulativeIncomeTaxBaseConfirmed = true,
                PreviousTaxBase = 42500m + laterCalculation.IncomeTaxBase,
                ApprovalStatus = PayrollApprovalStatus.Approved,
            };
            var finalCalculation = engine.Calculate(finalRow.ToEngineInput(0),
                settings.ToEngineParameters());
            finalRow.Result = new PayrollResult
            {
                GrossEarnings = finalCalculation.GrossEarnings,
                IncomeTaxBase = finalCalculation.IncomeTaxBase,
                IncomeTax = finalCalculation.IncomeTax,
                NetPay = finalCalculation.NetPay,
                EmployerCost = finalCalculation.EmployerCost,
                CalculatedAtUtc = DateTime.UtcNow,
            };
            final.Employees.Add(finalRow);
            db.PayrollPeriods.Add(final);
            await db.SaveChangesAsync(CancellationToken.None);
            periodId = period.Id;
            rowId = row.Id;
            laterPeriodId = later.Id;
            laterRowId = laterRow.Id;
            finalRowId = finalRow.Id;
        }
        var caseUrl = $"/api/employees/{employeeId}/compensation-packages/{packageId}/remediation-case";
        var openedResponse = await admin.PostAsJsonAsync(caseUrl, new
        {
            expectedPackageVersion = 1, expectedCaseVersion = (int?)null,
            issueDescription = "Onaylı bordro brütü hatalıydı.",
            proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 52000m, regularPremium = 0m,
            roadAllowance = 0m, mealAllowance = 0m,
            otherRegularBenefits = 0m, sourceReference = "Uzman ücret kararı",
        });
        openedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var opened = (await openedResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        var approval = await admin.PostAsJsonAsync($"{caseUrl}/{opened.Id}/approve", new
        {
            expectedCaseVersion = opened.Version, expertReviewReference = "Uzman raporu 51",
            reviewNotes = "Kaynak ücret belgesi karşılaştırıldı.", confirmExpertReview = true,
        });
        approval.StatusCode.Should().Be(HttpStatusCode.OK);
        var impactUrl = $"{caseUrl}/{opened.Id}/payroll-periods/{periodId}/rows/{rowId}/approved-impact";
        var blocked = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            impactUrl, jsonOptions))!;
        blocked.CanApply.Should().BeFalse();
        blocked.BlockingReasons.Should().Contain(reason => reason.Contains("sonraki onaylı"));
        blocked.ChainImpact.CanEstimate.Should().BeTrue();
        blocked.ChainImpact.LaterApprovedRows.Should().HaveCount(2);
        blocked.ChainImpact.LaterApprovedRows[0].Should().Match<ApprovedPayrollChainRowDto>(item =>
                item.RowId == laterRowId && item.RecordedPreviousTaxBase == 42500m
                && item.ProjectedPreviousTaxBase == blocked.Hypothetical!.IncomeTaxBase
                && item.Projected != null);
        blocked.ChainImpact.LaterApprovedRows[1].Should().Match<ApprovedPayrollChainRowDto>(item =>
            item.RowId == finalRowId && item.ProjectedPreviousTaxBase
                == blocked.ChainImpact.LaterApprovedRows[0].ProjectedPreviousTaxBase
                    + blocked.ChainImpact.LaterApprovedRows[0].Projected!.IncomeTaxBase
                && item.Projected != null);
        (await manager.GetAsync(impactUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var later = await db.PayrollEmployees.SingleAsync(item => item.Id == laterRowId);
            later.PreviousTaxBase = 42501m;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var inconsistent = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            impactUrl, jsonOptions))!;
        inconsistent.ChainImpact.CanEstimate.Should().BeFalse();
        inconsistent.ChainImpact.BlockingReasons.Should().Contain(reason =>
            reason.Contains("önceki vergi matrahı"));
        inconsistent.ChainImpact.LaterApprovedRows.Should().OnlyContain(item => item.Projected == null);

        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var later = await db.PayrollEmployees.SingleAsync(item => item.Id == laterRowId);
            later.ApprovalStatus = PayrollApprovalStatus.PreCalc;
            var final = await db.PayrollEmployees.SingleAsync(item => item.Id == finalRowId);
            final.ApprovalStatus = PayrollApprovalStatus.PreCalc;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        var preview = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            impactUrl, jsonOptions))!;
        preview.CanApply.Should().BeTrue();
        (await admin.PostAsJsonAsync($"{impactUrl}/apply", new
        {
            expectedPreviewToken = blocked.PreviewToken,
            expertReviewReference = "Uzman sonuç 52", reviewNotes = "Hesap kontrol edildi.",
            confirmSourceLink = true, confirmExternalReconciliation = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var applyResponse = await admin.PostAsJsonAsync($"{impactUrl}/apply", new
        {
            expectedPreviewToken = preview.PreviewToken,
            expertReviewReference = "Uzman sonuç 52", reviewNotes = "Hesap kontrol edildi.",
            confirmSourceLink = true, confirmExternalReconciliation = true,
        });
        applyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var revision = (await applyResponse.Content.ReadFromJsonAsync<ApprovedPayrollRevisionDto>(
            jsonOptions))!;
        revision.Before.GrossSalary.Should().Be(50000m);
        revision.After.GrossSalary.Should().Be(52000m);
        revision.After.NetPay.Should().NotBe(revision.Before.NetPay);
        (await admin.PostAsJsonAsync($"{impactUrl}/apply", new
        {
            expectedPreviewToken = preview.PreviewToken,
            expertReviewReference = "Uzman sonuç 52", reviewNotes = "Tekrar",
            confirmSourceLink = true, confirmExternalReconciliation = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var current = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            impactUrl, jsonOptions))!;
        current.Revision!.Id.Should().Be(revision.Id);
        current.CanApply.Should().BeFalse();
        var detail = (await admin.GetFromJsonAsync<PayrollPeriodDetailDto>(
            $"/api/payroll/periods/{periodId}", jsonOptions))!;
        detail.Rows.Single(item => item.Id == rowId).NetPay.Should().Be(revision.After.NetPay);
        detail.Rows.Single(item => item.Id == rowId).RevisionNumber.Should().Be(1);
        var summary = (await admin.GetFromJsonAsync<PayrollPeriodSummary>(
            $"/api/payroll/periods/{periodId}/summary", jsonOptions))!;
        summary.TotalNet.Should().Be(revision.After.NetPay);
        var laterDetail = (await admin.GetFromJsonAsync<PayrollPeriodDetailDto>(
            $"/api/payroll/periods/{laterPeriodId}", jsonOptions))!;
        laterDetail.Rows.Single(item => item.Id == laterRowId).PreviousTaxBase
            .Should().Be(revision.After.IncomeTaxBase);
        var originalPdf = await admin.GetAsync($"/api/payroll/periods/{periodId}/rows/{rowId}/payslip?original=true");
        originalPdf.StatusCode.Should().Be(HttpStatusCode.OK);
        originalPdf.Content.Headers.ContentDisposition!.FileName.Should().Contain("ilk-onay");
        (await admin.GetAsync($"/api/payroll/periods/{periodId}/rows/{rowId}/payslip"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var externalUrl = $"{caseUrl}/{opened.Id}/payroll-periods/{periodId}/rows/{rowId}"
            + $"/revisions/{revision.Id}/external-reconciliation";
        (await manager.GetAsync(externalUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync(externalUrl.Replace($"/rows/{rowId}/", "/rows/999999/")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var initialExternal = await GetAsync<PayrollRevisionExternalReconciliationDto>(admin, externalUrl);
        initialExternal.Payment.Should().BeNull();
        initialExternal.Notification.Should().BeNull();
        initialExternal.CalculatedNetDifference.Should().Be(revision.After.NetPay - revision.Before.NetPay);
        var paymentKey = $"payroll-revision-external-payment:{revision.Id}";
        var notificationKey = $"payroll-revision-external-notification:{revision.Id}";
        var tasks = await GetAsync<List<DailyGuidanceTaskDto>>(admin, "/api/guidance/daily-tasks");
        tasks.Should().ContainSingle(item => item.Key == paymentKey);
        tasks.Should().ContainSingle(item => item.Key == notificationKey);
        (await GetAsync<List<DailyGuidanceTaskDto>>(manager, "/api/guidance/daily-tasks"))
            .Should().NotContain(item => item.Key == paymentKey || item.Key == notificationKey);

        var invalid = await admin.PostAsJsonAsync(externalUrl, new {
            kind = "payment", outcome = "completed", responsibleName = "Muhasebe",
            evidenceReference = "", actionDate = "2041-09-01", recordedAmount = 1200m,
            notes = "Ödeme kontrolü", expectedLatestRecordId = (int?)null,
            confirmRecordedStatus = true,
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var pendingResponse = await admin.PostAsJsonAsync(externalUrl, new {
            kind = "payment", outcome = "pending", responsibleName = "Muhasebe",
            evidenceReference = "", actionDate = (string?)null, recordedAmount = (decimal?)null,
            notes = "Banka teyidi bekleniyor", expectedLatestRecordId = (int?)null,
            confirmRecordedStatus = true,
        });
        pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = (await pendingResponse.Content.ReadFromJsonAsync<PayrollRevisionExternalActionDto>())!;
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, "/api/guidance/daily-tasks"))
            .Should().Contain(item => item.Key == paymentKey);
        var completedResponse = await admin.PostAsJsonAsync(externalUrl, new {
            kind = "payment", outcome = "completed", responsibleName = "Muhasebe",
            evidenceReference = "Banka-2041-08", actionDate = "2041-09-01",
            recordedAmount = 1200m, notes = "Gerçek ödeme bankada teyit edildi",
            expectedLatestRecordId = pending.Id, confirmRecordedStatus = true,
        });
        completedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = (await completedResponse.Content.ReadFromJsonAsync<PayrollRevisionExternalActionDto>())!;
        completed.PreviousRecordId.Should().Be(pending.Id);
        (await admin.PostAsJsonAsync(externalUrl, new {
            kind = "payment", outcome = "pending", responsibleName = "Muhasebe",
            evidenceReference = "", actionDate = (string?)null, recordedAmount = (decimal?)null,
            notes = "Eski ekran", expectedLatestRecordId = pending.Id,
            confirmRecordedStatus = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var afterPayment = await GetAsync<List<DailyGuidanceTaskDto>>(admin, "/api/guidance/daily-tasks");
        afterPayment.Should().NotContain(item => item.Key == paymentKey);
        afterPayment.Should().ContainSingle(item => item.Key == notificationKey);
        var notificationResponse = await admin.PostAsJsonAsync(externalUrl, new {
            kind = "notification", outcome = "not-required", responsibleName = "Mali müşavir",
            evidenceReference = "Uzman görüşü-2041-08", actionDate = (string?)null,
            recordedAmount = (decimal?)null, notes = "Bildirim gerekmeme gerekçesi incelendi",
            expectedLatestRecordId = (int?)null, confirmRecordedStatus = true,
        });
        notificationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reconciled = await GetAsync<PayrollRevisionExternalReconciliationDto>(admin, externalUrl);
        reconciled.History.Should().HaveCount(3);
        reconciled.Payment!.Id.Should().Be(completed.Id);
        reconciled.Notification!.Outcome.Should().Be("not-required");
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, "/api/guidance/daily-tasks"))
            .Should().NotContain(item => item.Key == paymentKey || item.Key == notificationKey);
        using var verifyScope = factory.Services.GetRequiredService<ITenantScopeFactory>()
            .Create(auth.User.TenantId);
        var verifyDb = verifyScope.Services.GetRequiredService<IApplicationDbContext>();
        (await verifyDb.PayrollEmployees.Include(item => item.Result)
            .SingleAsync(item => item.Id == rowId)).Result!.NetPay.Should().Be(37000m);
        (await verifyDb.PayrollApprovedRowRevisions.CountAsync(item => item.PayrollRowId == rowId))
            .Should().Be(1);

        var nextContext = await GetAsync<CompensationRemediationContextDto>(admin, caseUrl);
        nextContext.CanOpen.Should().BeTrue();
        nextContext.History.Should().ContainSingle(item => item.Id == opened.Id);
        nextContext.CurrentImpact.PotentialPayrollRows.Single(item => item.RowId == rowId)
            .RecordedGrossSalary.Should().Be(52000m);
        var nextCaseResponse = await admin.PostAsJsonAsync(caseUrl, new
        {
            expectedPackageVersion = nextContext.Package.Version,
            expectedCaseVersion = (int?)null,
            issueDescription = "İkinci uzman düzeltmesi.",
            proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 54000m, regularPremium = 0m,
            roadAllowance = 0m, mealAllowance = 0m,
            otherRegularBenefits = 0m, sourceReference = "İkinci ücret kararı",
        });
        nextCaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var nextCase = (await nextCaseResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        nextCase.Id.Should().NotBe(opened.Id);
        var historicalImpact = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            impactUrl, jsonOptions))!;
        historicalImpact.BlockingReasons.Should().Contain(reason =>
            reason.Contains("yeni uzman inceleme dosyası"));
        (await admin.PostAsJsonAsync($"{caseUrl}/{nextCase.Id}/approve", new
        {
            expectedCaseVersion = nextCase.Version,
            expertReviewReference = "İkinci uzman raporu",
            reviewNotes = "Geçerli sürüm yeniden incelendi.", confirmExpertReview = true,
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        var nextImpactUrl = $"{caseUrl}/{nextCase.Id}/payroll-periods/{periodId}/rows/{rowId}/approved-impact";
        var nextImpact = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            nextImpactUrl, jsonOptions))!;
        nextImpact.CanApply.Should().BeFalse();
        nextImpact.CanApplyNext.Should().BeTrue();
        nextImpact.Revision!.Id.Should().Be(revision.Id);
        nextImpact.RevisionHistory.Should().ContainSingle(item => item.Id == revision.Id);
        nextImpact.Recorded.NetPay.Should().Be(revision.After.NetPay);
        nextImpact.Hypothetical!.NetPay.Should().NotBe(revision.After.NetPay);
        var nextBody = new
        {
            expectedCurrentRevisionId = revision.Id,
            expectedPreviewToken = nextImpact.PreviewToken,
            expertReviewReference = "İkinci son onay",
            reviewNotes = "Yeni tutar ve dış etkiler kontrol edildi.",
            confirmSourceLink = true, confirmExternalReconciliation = true,
        };
        (await manager.PostAsJsonAsync($"{nextImpactUrl}/revise", nextBody))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"{nextImpactUrl}/revise", new
        {
            expectedCurrentRevisionId = revision.Id,
            expectedPreviewToken = new string('0', 64),
            expertReviewReference = "İkinci son onay", reviewNotes = "Bayat önizleme.",
            confirmSourceLink = true, confirmExternalReconciliation = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await verifyDb.PayrollApprovedRowRevisions.CountAsync(item => item.PayrollRowId == rowId))
            .Should().Be(1);
        var nextApplyResponse = await admin.PostAsJsonAsync($"{nextImpactUrl}/revise", nextBody);
        nextApplyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var nextRevision = (await nextApplyResponse.Content.ReadFromJsonAsync<ApprovedPayrollRevisionDto>(
            jsonOptions))!;
        nextRevision.RevisionNumber.Should().Be(2);
        nextRevision.BasedOnRevisionId.Should().Be(revision.Id);
        nextRevision.RemediationCaseId.Should().Be(nextCase.Id);
        nextRevision.Before.NetPay.Should().Be(revision.After.NetPay);
        nextRevision.Before.GrossSalary.Should().Be(52000m);
        nextRevision.After.GrossSalary.Should().Be(54000m);
        (await admin.PostAsJsonAsync($"{nextImpactUrl}/revise", nextBody))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetAsync<PayrollRevisionExternalReconciliationDto>(admin, externalUrl))
            .IsCurrent.Should().BeFalse();
        var nextExternalUrl = $"{caseUrl}/{nextCase.Id}/payroll-periods/{periodId}/rows/{rowId}"
            + $"/revisions/{nextRevision.Id}/external-reconciliation";
        var newExternal = await GetAsync<PayrollRevisionExternalReconciliationDto>(
            admin, nextExternalUrl);
        newExternal.IsCurrent.Should().BeTrue();
        newExternal.Payment.Should().BeNull();
        newExternal.Notification.Should().BeNull();
        newExternal.OriginalNetPay.Should().Be(revision.After.NetPay);
        var nextTasks = await GetAsync<List<DailyGuidanceTaskDto>>(
            admin, "/api/guidance/daily-tasks");
        nextTasks.Should().NotContain(item => item.Key == paymentKey || item.Key == notificationKey);
        nextTasks.Should().ContainSingle(item => item.Key
            == $"payroll-revision-external-payment:{nextRevision.Id}");
        nextTasks.Should().ContainSingle(item => item.Key
            == $"payroll-revision-external-notification:{nextRevision.Id}");
        var nextDetail = (await admin.GetFromJsonAsync<PayrollPeriodDetailDto>(
            $"/api/payroll/periods/{periodId}", jsonOptions))!;
        nextDetail.Rows.Single(item => item.Id == rowId).NetPay.Should().Be(nextRevision.After.NetPay);
        nextDetail.Rows.Single(item => item.Id == rowId).RevisionNumber.Should().Be(2);
        var refreshedLater = (await admin.GetFromJsonAsync<PayrollPeriodDetailDto>(
            $"/api/payroll/periods/{laterPeriodId}", jsonOptions))!;
        refreshedLater.Rows.Single(item => item.Id == laterRowId).PreviousTaxBase
            .Should().Be(nextRevision.After.IncomeTaxBase);
        (await verifyDb.PayrollEmployees.Include(item => item.Result)
            .SingleAsync(item => item.Id == rowId)).Result!.NetPay.Should().Be(37000m);

        var laterApprovedRow = await verifyDb.PayrollEmployees
            .SingleAsync(item => item.Id == laterRowId);
        laterApprovedRow.ApprovalStatus = PayrollApprovalStatus.Approved;
        await verifyDb.SaveChangesAsync(CancellationToken.None);
        var thirdContext = await GetAsync<CompensationRemediationContextDto>(admin, caseUrl);
        thirdContext.History.Should().HaveCount(2);
        var thirdResponse = await admin.PostAsJsonAsync(caseUrl, new
        {
            expectedPackageVersion = thirdContext.Package.Version,
            expectedCaseVersion = (int?)null,
            issueDescription = "Sonraki onaylı ay bulunan üçüncü düzeltme.",
            proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 56000m, regularPremium = 0m,
            roadAllowance = 0m, mealAllowance = 0m,
            otherRegularBenefits = 0m, sourceReference = "Üçüncü ücret kararı",
        });
        thirdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var thirdCase = (await thirdResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        (await admin.PostAsJsonAsync($"{caseUrl}/{thirdCase.Id}/approve", new
        {
            expectedCaseVersion = thirdCase.Version,
            expertReviewReference = "Üçüncü uzman raporu",
            reviewNotes = "Sonraki ay ayrıca incelenecek.", confirmExpertReview = true,
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        var thirdImpactUrl = $"{caseUrl}/{thirdCase.Id}/payroll-periods/{periodId}/rows/{rowId}/approved-impact";
        var thirdImpact = (await admin.GetFromJsonAsync<ApprovedCompensationPayrollImpactDto>(
            thirdImpactUrl, jsonOptions))!;
        thirdImpact.CanApplyNext.Should().BeFalse();
        thirdImpact.BlockingReasons.Should().Contain(reason => reason.Contains("sonraki onaylı"));
        (await admin.PostAsJsonAsync($"{thirdImpactUrl}/revise", new
        {
            expectedCurrentRevisionId = nextRevision.Id,
            expectedPreviewToken = thirdImpact.PreviewToken,
            expertReviewReference = "Üçüncü son onay", reviewNotes = "Kilitli kapsam.",
            confirmSourceLink = true, confirmExternalReconciliation = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await verifyDb.PayrollApprovedRowRevisions.CountAsync(item => item.PayrollRowId == rowId))
            .Should().Be(2);
    }

    [Fact]
    public async Task CompletedOffboarding_SettlementRevisionPreservesExitAndOriginalCalculation()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var employeeId = await CreateMinimalEmployeeAsync(
            admin, departmentId, "Ayrilis", $"Hesap{Guid.NewGuid():N}");
        var packageResponse = await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/compensation-packages", new
            {
                effectiveFrom = "2025-01-01", monthlyGrossSalary = 50000m,
                regularPremium = 1000m, roadAllowance = 100m, mealAllowance = 200m,
                otherRegularBenefits = 300m, sourceReference = "İlk ücret kararı",
            });
        packageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var packageId = (await packageResponse.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!
            .CompensationPackages.Single().Id;
        var draft = await SaveOffboardingDraftAsync(admin, employeeId,
            new DateOnly(2025, 6, 30), unusedLeaveDays: 4m,
            unusedLeavePayment: 500m, severancePayment: 1000m, noticePayment: 0m);
        var completed = await CompleteOffboardingAsync(admin, employeeId, draft);
        var caseUrl = $"/api/employees/{employeeId}/compensation-packages/{packageId}/remediation-case";
        var context = await GetAsync<CompensationRemediationContextDto>(admin, caseUrl);
        context.CurrentImpact.OffboardingCases.Should().ContainSingle(item => item.CaseId == completed.Id);
        var openedResponse = await admin.PostAsJsonAsync(caseUrl, new
        {
            expectedPackageVersion = context.Package.Version, expectedCaseVersion = (int?)null,
            issueDescription = "Ayrılışta kullanılan aylık brüt ücret yanlış.",
            proposedEffectiveFrom = (string?)null, monthlyGrossSalary = 52000m,
            regularPremium = 1000m, roadAllowance = 100m, mealAllowance = 200m,
            otherRegularBenefits = 300m, sourceReference = "Düzeltilmiş ücret kararı",
        });
        openedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var review = (await openedResponse.Content.ReadFromJsonAsync<CompensationRemediationCaseDto>())!;
        var revisionUrl = $"{caseUrl}/{review.Id}/offboarding/{completed.Id}/settlement-revisions";
        var beforeApproval = await GetAsync<OffboardingSettlementRevisionContextDto>(admin, revisionUrl);
        beforeApproval.CanOpen.Should().BeFalse();
        (await manager.GetAsync(revisionUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var approval = await admin.PostAsJsonAsync($"{caseUrl}/{review.Id}/approve", new
        {
            expectedCaseVersion = review.Version, expertReviewReference = "Uzman ücret raporu",
            reviewNotes = "Ayrılış ve kaynak ücret belgesi karşılaştırıldı.",
            confirmExpertReview = true,
        });
        approval.StatusCode.Should().Be(HttpStatusCode.OK);
        var openResponse = await admin.PostAsJsonAsync(revisionUrl,
            new { expectedOffboardingVersion = completed.Version });
        openResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var financialDraft = (await openResponse.Content
            .ReadFromJsonAsync<OffboardingSettlementRevisionDto>())!;
        financialDraft.Status.Should().Be("draft");
        financialDraft.Values.MonthlyGrossSalarySnapshot.Should().Be(52000m);
        financialDraft.Values.UnusedLeavePayment.Should().Be(500m);
        (await admin.PostAsJsonAsync(revisionUrl,
            new { expectedOffboardingVersion = completed.Version }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var saveResponse = await admin.PutAsJsonAsync($"{revisionUrl}/{financialDraft.Id}", new
        {
            expectedRevisionVersion = financialDraft.Version,
            unusedLeaveDays = 4m, unusedLeavePayment = 700m,
            severancePayment = 1300m, noticePayment = 0m,
            reason = "Düzeltilmiş ücretle uzman hesaplaması",
            settlementReference = "Mali müşavir hesap belgesi 44",
            settlementNotes = "Dış ödeme farkı ayrıca uzlaştırılacak.",
        });
        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = (await saveResponse.Content.ReadFromJsonAsync<OffboardingSettlementRevisionDto>())!;
        (await admin.PutAsJsonAsync($"{revisionUrl}/{saved.Id}", new
        {
            expectedRevisionVersion = financialDraft.Version,
            unusedLeaveDays = 4m, unusedLeavePayment = 701m,
            severancePayment = 1300m, noticePayment = 0m,
            reason = "Eski sürüm", settlementReference = "Kaynak",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"{revisionUrl}/{saved.Id}/approve", new
        {
            expectedRevisionVersion = saved.Version,
            expertReviewReference = "Uzman son kontrolü", reviewNotes = "Tutar farkı incelendi.",
            confirmFinancialReview = true, confirmExternalReconciliation = false,
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var approveResponse = await admin.PostAsJsonAsync($"{revisionUrl}/{saved.Id}/approve", new
        {
            expectedRevisionVersion = saved.Version,
            expertReviewReference = "Uzman son kontrolü", reviewNotes = "Tutar farkı incelendi.",
            confirmFinancialReview = true, confirmExternalReconciliation = true,
        });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = (await approveResponse.Content
            .ReadFromJsonAsync<OffboardingSettlementRevisionDto>())!;
        approved.Status.Should().Be("approved");
        var after = await GetAsync<OffboardingSettlementRevisionContextDto>(admin, revisionUrl);
        after.Original.MonthlyGrossSalarySnapshot.Should().Be(50000m);
        after.Original.TotalPayment.Should().Be(1500m);
        after.Effective.MonthlyGrossSalarySnapshot.Should().Be(52000m);
        after.Effective.TotalPayment.Should().Be(2000m);
        after.History.Should().ContainSingle(item => item.Id == approved.Id);
        var offboardingContext = await GetAsync<EmployeeOffboardingContextDto>(
            admin, $"/api/employees/{employeeId}/offboarding-context");
        offboardingContext.CurrentCase!.Status.Should().Be("completed");
        offboardingContext.CurrentCase.TotalPayment.Should().Be(1500m);
        offboardingContext.EffectiveSettlementRevision!.Values.TotalPayment.Should().Be(2000m);
        var employee = await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{employeeId}");
        employee.Status.Should().Be("passive");
        employee.TerminationDate.Should().Be(new DateOnly(2025, 6, 30));
        var reconciliationUrl = $"{revisionUrl}/{approved.Id}/external-reconciliation";
        var pending = await GetAsync<OffboardingExternalReconciliationDto>(admin, reconciliationUrl);
        pending.Payment.Should().BeNull();
        pending.Notification.Should().BeNull();
        pending.CalculationDifference.Should().Be(500m);
        pending.PreviousEffectiveTotal.Should().Be(1500m);
        var pendingTasks = await GetAsync<List<DailyGuidanceTaskDto>>(
            admin, "/api/guidance/daily-tasks");
        pendingTasks.Should().Contain(item => item.Key == $"offboarding-external-payment:{approved.Id}");
        pendingTasks.Should().Contain(item => item.Key == $"offboarding-external-notification:{approved.Id}");
        (await manager.GetAsync(reconciliationUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var assignmentResponse = await admin.PostAsJsonAsync(reconciliationUrl, new
        {
            kind = "payment", outcome = "pending", responsibleName = "Bordro uzmanı",
            evidenceReference = "", notes = "Ödeme belgesi bekleniyor.",
            expectedLatestRecordId = (int?)null, confirmRecordedStatus = true,
        });
        assignmentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignment = (await assignmentResponse.Content.ReadFromJsonAsync<OffboardingExternalActionDto>())!;
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, "/api/guidance/daily-tasks"))
            .Should().Contain(item => item.Key == $"offboarding-external-payment:{approved.Id}");
        var paymentResponse = await admin.PostAsJsonAsync(reconciliationUrl, new
        {
            kind = "payment", outcome = "completed", responsibleName = "Bordro uzmanı",
            evidenceReference = "Banka dekontu 123", actionDate = "2025-07-02",
            recordedAmount = 420m, notes = "Net ödeme belgesi; hesap farkıyla aynı bazda değildir.",
            expectedLatestRecordId = assignment.Id, confirmRecordedStatus = true,
        });
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = (await paymentResponse.Content.ReadFromJsonAsync<OffboardingExternalActionDto>())!;
        (await admin.PostAsJsonAsync(reconciliationUrl, new
        {
            kind = "payment", outcome = "not-required", responsibleName = "Bordro uzmanı",
            evidenceReference = "Eski önizleme", notes = "Hatalı tekrar",
            expectedLatestRecordId = (int?)null, confirmRecordedStatus = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var notificationResponse = await admin.PostAsJsonAsync(reconciliationUrl, new
        {
            kind = "notification", outcome = "not-required", responsibleName = "İK yöneticisi",
            evidenceReference = "SGK inceleme notu 55", notes = "Düzeltme bildirimi gerekmiyor.",
            expectedLatestRecordId = (int?)null, confirmRecordedStatus = true,
        });
        notificationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var correctedPaymentResponse = await admin.PostAsJsonAsync(reconciliationUrl, new
        {
            kind = "payment", outcome = "completed", responsibleName = "Bordro uzmanı",
            evidenceReference = "Düzeltilmiş banka dekontu 124", actionDate = "2025-07-03",
            recordedAmount = 425m, notes = "Dekont referansı düzeltildi.",
            expectedLatestRecordId = payment.Id, confirmRecordedStatus = true,
        });
        correctedPaymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var correctedPayment = (await correctedPaymentResponse.Content
            .ReadFromJsonAsync<OffboardingExternalActionDto>())!;
        var reconciled = await GetAsync<OffboardingExternalReconciliationDto>(admin, reconciliationUrl);
        reconciled.Payment!.PreviousRecordId.Should().Be(payment.Id);
        reconciled.Payment.RecordedAmount.Should().Be(425m);
        reconciled.Notification!.Outcome.Should().Be("not-required");
        reconciled.History.Should().HaveCount(4);
        reconciled.History.Should().Contain(item => item.Id == payment.Id);
        reconciled.IsCurrent.Should().BeTrue();
        var closedTasks = await GetAsync<List<DailyGuidanceTaskDto>>(
            admin, "/api/guidance/daily-tasks");
        closedTasks.Should().NotContain(item => item.Key == $"offboarding-external-payment:{approved.Id}");
        closedTasks.Should().NotContain(item => item.Key == $"offboarding-external-notification:{approved.Id}");
        var reopenResponse = await admin.PostAsJsonAsync(reconciliationUrl, new
        {
            kind = "payment", outcome = "pending", responsibleName = "Bordro uzmanı",
            evidenceReference = "", notes = "Dekont bankaca geri çevrildi; yeni işlem bekleniyor.",
            expectedLatestRecordId = correctedPayment.Id, confirmRecordedStatus = true,
        });
        reopenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync<List<DailyGuidanceTaskDto>>(admin, "/api/guidance/daily-tasks"))
            .Should().Contain(item => item.Key == $"offboarding-external-payment:{approved.Id}");
        var secondDraftResponse = await admin.PostAsJsonAsync(revisionUrl,
            new { expectedOffboardingVersion = completed.Version });
        secondDraftResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondDraft = (await secondDraftResponse.Content
            .ReadFromJsonAsync<OffboardingSettlementRevisionDto>())!;
        secondDraft.BasedOnApprovedRevisionId.Should().Be(approved.Id);
        var secondSavedResponse = await admin.PutAsJsonAsync($"{revisionUrl}/{secondDraft.Id}", new
        {
            expectedRevisionVersion = secondDraft.Version,
            unusedLeaveDays = 4m, unusedLeavePayment = 800m,
            severancePayment = 1400m, noticePayment = 0m,
            reason = "Sonradan bulunan ek ücret farkı",
            settlementReference = "Mali müşavir ek hesap 45",
        });
        secondSavedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondSaved = (await secondSavedResponse.Content
            .ReadFromJsonAsync<OffboardingSettlementRevisionDto>())!;
        var secondApproval = await admin.PostAsJsonAsync(
            $"{revisionUrl}/{secondSaved.Id}/approve", new
            {
                expectedRevisionVersion = secondSaved.Version,
                expertReviewReference = "Ek uzman kontrolü",
                reviewNotes = "İkinci fark doğrulandı.",
                confirmFinancialReview = true,
                confirmExternalReconciliation = true,
            });
        secondApproval.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldEvidence = await GetAsync<OffboardingExternalReconciliationDto>(
            admin, reconciliationUrl);
        oldEvidence.IsCurrent.Should().BeFalse();
        oldEvidence.History.Should().HaveCount(5);
        (await admin.PostAsJsonAsync(reconciliationUrl, new
        {
            kind = "payment", outcome = "not-required", responsibleName = "İK yöneticisi",
            evidenceReference = "Eski sürüm", notes = "Eski sürüme kayıt denemesi",
            expectedLatestRecordId = oldEvidence.Payment?.Id,
            confirmRecordedStatus = true,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var currentEvidence = await GetAsync<OffboardingExternalReconciliationDto>(
            admin, $"{revisionUrl}/{secondSaved.Id}/external-reconciliation");
        currentEvidence.IsCurrent.Should().BeTrue();
        currentEvidence.PreviousEffectiveTotal.Should().Be(2000m);
        currentEvidence.CalculationDifference.Should().Be(200m);
        currentEvidence.Payment.Should().BeNull();
        currentEvidence.Notification.Should().BeNull();
        var renewedTasks = await GetAsync<List<DailyGuidanceTaskDto>>(
            admin, "/api/guidance/daily-tasks");
        renewedTasks.Should().Contain(item => item.Key == $"offboarding-external-payment:{secondSaved.Id}");
        renewedTasks.Should().Contain(item => item.Key == $"offboarding-external-notification:{secondSaved.Id}");
        (await admin.ApplyReviewedOffboardingAsync($"/api/employees/{employeeId}/offboarding/{completed.Id}/reverse",
            new { expectedVersion = completed.Version, reason = "Yanlış geri alma" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var freshReview = await GetAsync<CompensationRemediationContextDto>(admin, caseUrl);
        freshReview.CanOpen.Should().BeTrue();
        var laterCaseResponse = await admin.PostAsJsonAsync(caseUrl, new
        {
            expectedPackageVersion = freshReview.Package.Version,
            expectedCaseVersion = (int?)null,
            issueDescription = "Ayrılış için yeni ücret incelemesi.",
            proposedEffectiveFrom = (string?)null,
            monthlyGrossSalary = 53000m, regularPremium = 1000m,
            roadAllowance = 100m, mealAllowance = 200m,
            otherRegularBenefits = 300m, sourceReference = "Yeni uzman kararı",
        });
        laterCaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldSettlementContext = await GetAsync<OffboardingSettlementRevisionContextDto>(
            admin, revisionUrl);
        oldSettlementContext.CanOpen.Should().BeFalse();
        oldSettlementContext.BlockingReasons.Should().Contain(reason =>
            reason.Contains("yeni uzman inceleme dosyası"));
    }

    [Fact]
    public async Task CompensationDateCorrection_UpdatesLastPackageAndBlocksAffectedHistory()
    {
        var auth = await LoginAsync("ik@hrmaster.local");
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var employeeId = await CreateMinimalEmployeeAsync(
            admin, departmentId, "Tarih", $"Duzeltme{Guid.NewGuid():N}");
        foreach (var (date, gross) in new[] { ("2038-01-01", 40000m), ("2038-07-01", 50000m) })
        {
            (await admin.PostAsJsonAsync($"/api/employees/{employeeId}/compensation-packages", new
            {
                effectiveFrom = date, monthlyGrossSalary = gross,
                regularPremium = 0m, roadAllowance = 0m, mealAllowance = 0m,
                otherRegularBenefits = 0m, sourceReference = $"Karar {date}",
            })).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var foundation = await GetAsync<EmployeeEmploymentFoundationDto>(
            admin, $"/api/employees/{employeeId}/employment-foundation");
        var latest = foundation.CompensationPackages.Single(item => item.EffectiveFrom == new DateOnly(2038, 7, 1));
        var previous = foundation.CompensationPackages.Single(item => item.EffectiveFrom == new DateOnly(2038, 1, 1));
        previous.Version.Should().Be(2);
        var previewUrl = $"/api/employees/{employeeId}/compensation-packages/{latest.Id}/date-correction-preview";
        var correctionUrl = $"/api/employees/{employeeId}/compensation-packages/{latest.Id}/effective-date";
        (await manager.PostAsJsonAsync(previewUrl, new { proposedEffectiveFrom = "2038-08-01" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var previewResponse = await admin.PostAsJsonAsync(previewUrl,
            new { proposedEffectiveFrom = "2038-08-01" });
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await previewResponse.Content.ReadFromJsonAsync<CompensationDateCorrectionPreviewDto>())!;
        preview.CanApply.Should().BeTrue();
        preview.ProposedPreviousEffectiveTo.Should().Be(new DateOnly(2038, 7, 31));
        var body = new
        {
            proposedEffectiveFrom = "2038-08-01", expectedPackageVersion = latest.Version,
            expectedPreviousPackageId = (int?)previous.Id,
            expectedPreviousPackageVersion = (int?)previous.Version,
            reason = "Karardaki başlangıç ayı yanlış kaydedildi",
        };
        (await manager.PutAsJsonAsync(correctionUrl, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var correctedResponse = await admin.PutAsJsonAsync(correctionUrl, body);
        correctedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var corrected = (await correctedResponse.Content.ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!;
        corrected.CompensationPackages.Single(item => item.Id == latest.Id)
            .EffectiveFrom.Should().Be(new DateOnly(2038, 8, 1));
        corrected.CompensationPackages.Single(item => item.Id == previous.Id)
            .EffectiveTo.Should().Be(new DateOnly(2038, 7, 31));
        corrected.CompensationPackages.Single(item => item.Id == latest.Id).Version.Should().Be(latest.Version + 1);
        corrected.CompensationPackages.Single(item => item.Id == previous.Id).Version.Should().Be(previous.Version + 1);
        (await admin.PutAsJsonAsync(correctionUrl, body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        preview = (await (await admin.PostAsJsonAsync(previewUrl,
            new { proposedEffectiveFrom = "2038-07-01" }))
            .Content.ReadFromJsonAsync<CompensationDateCorrectionPreviewDto>())!;
        preview.History.Should().ContainSingle(item => item.Reason == body.reason
            && item.OldEffectiveFrom == new DateOnly(2038, 7, 1)
            && item.NewEffectiveFrom == new DateOnly(2038, 8, 1));

        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var settingsId = await db.PayrollSettings.Select(item => item.Id).FirstAsync();
            foreach (var month in new[] { 7, 8 })
            {
                var period = new PayrollPeriod
                {
                    Name = $"Tarih düzeltme kontrol {month} {Guid.NewGuid():N}",
                    Year = 2038, Month = month, PayrollSettingsId = settingsId,
                };
                period.Employees.Add(new PayrollEmployee { EmployeeId = employeeId });
                db.PayrollPeriods.Add(period);
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
        preview = (await (await admin.PostAsJsonAsync(previewUrl,
            new { proposedEffectiveFrom = "2038-07-01" }))
            .Content.ReadFromJsonAsync<CompensationDateCorrectionPreviewDto>())!;
        preview.CanApply.Should().BeFalse();
        preview.BlockingReasons.Should().Contain(reason => reason.Contains("Değişecek yürürlük aralığında bordro"));
        preview.BlockingReasons.Should().Contain(reason => reason.Contains("Son paket bordro"));
    }

    [Fact]
    public async Task Rehire_StartsNewEmploymentPeriodWithoutOldLeaveBalanceLeak()
    {
        var adminAuth = await LoginAsync("ik@hrmaster.local");
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.Token);
        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var createdResponse = await admin.CreateSafeEmployeeAsync("/api/employees", new
        {
            firstName = "Yeniden",
            lastName = $"Giris{Guid.NewGuid():N}",
            title = "Test Personeli",
            departmentId,
            hireDate = "2024-01-01",
            contractType = "indefinite",
        });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createdResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
        var initial = await GetAsync<EmployeeEmploymentFoundationDto>(
            admin, $"/api/employees/{created.Id}/employment-foundation");
        var initialPeriodId = initial.EmploymentPeriods.Single().Id;

        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(adminAuth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            db.LeaveLedgerEntries.Add(new LeaveLedgerEntry
            {
                EmploymentPeriodId = initialPeriodId,
                OccurredOn = new DateOnly(2024, 1, 1),
                Type = LeaveLedgerEntryType.LegacyOpening,
                Days = 24,
                SourceKey = $"rehire-test:{created.Id}",
                SourceReference = "Rehire izolasyon testi",
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await CompleteOffboardingAsync(admin, created.Id, new DateOnly(2025, 12, 31));
        var rehireResponse = await admin.PostAsJsonAsync($"/api/employees/{created.Id}/rehire", new
        {
            rehireDate = "2026-02-01",
            contractType = "fixed-term",
        });
        rehireResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rehired = (await rehireResponse.Content
            .ReadFromJsonAsync<EmployeeEmploymentFoundationDto>())!;
        rehired.EmploymentPeriods.Should().HaveCount(2);
        var currentPeriod = rehired.EmploymentPeriods.Single(period => period.EndDate is null);
        currentPeriod.Id.Should().NotBe(initialPeriodId);
        currentPeriod.StartDate.Should().Be(new DateOnly(2026, 2, 1));
        currentPeriod.ContractType.Should().Be("fixed-term");

        using var verifyScope = factory.Services.GetRequiredService<ITenantScopeFactory>()
            .Create(adminAuth.User.TenantId);
        var verifyDb = verifyScope.Services.GetRequiredService<IApplicationDbContext>();
        (await verifyDb.LeaveLedgerEntries.CountAsync(
            item => item.EmploymentPeriodId == initialPeriodId)).Should().Be(1);
        (await verifyDb.LeaveLedgerEntries.CountAsync(
            item => item.EmploymentPeriodId == currentPeriod.Id)).Should().Be(0,
            "eski dönem bakiyesi yeni işe girişe otomatik taşınmamalı");
    }

    [Fact]
    public async Task StatusChanges_MustUseOffboardingWorkflow()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(client, "/api/departments"))[0].Id;
        var id = await CreateMinimalEmployeeAsync(client, departmentId, "Durum", $"Kapisi{Guid.NewGuid():N}");

        (await client.PutAsJsonAsync($"/api/employees/{id}", new
        {
            firstName = "Durum",
            lastName = "Kapisi",
            title = "Degistirildi",
            departmentId,
            hireDate = "2024-01-01",
            status = "passive",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await client.PatchAsJsonAsync($"/api/employees/{id}/status", new
        {
            status = "passive",
            terminationDate = "2026-08-15",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var unchanged = await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{id}");
        unchanged.Status.Should().Be("active");
        unchanged.TerminationDate.Should().BeNull();
    }
    [Fact]
    public async Task CreateEmployee_AsManager_Returns403()
    {
        var client = await AuthedClientAsync("ece.arslan@hrmaster.local");

        var response = await client.CreateSafeEmployeeAsync("/api/employees", new
        {
            firstName = "Yetkisiz",
            lastName = "Deneme",
            title = "Test",
            departmentId = 1,
            hireDate = "2024-01-01",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BulkDeactivate_CannotBypassPerEmployeeClosure()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(client, "/api/departments"))[0].Id;
        var id = await CreateMinimalEmployeeAsync(client, departmentId, "Toplu", "Pasif");

        (await client.PostAsJsonAsync(
            "/api/employees/bulk-deactivate", new { ids = new[] { id } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.PostAsJsonAsync("/api/employees/bulk-deactivate", new
        {
            ids = new[] { id },
            terminationDate = "2026-08-15",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{id}"))
            .Status.Should().Be("active");
    }
    [Fact]
    public async Task CompletedOffboarding_RevokesAccess_AndReverseRequiresNewSession()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var auth = await LoginAsync("ahmet.yilmaz@hrmaster.local");
        var employeeId = auth.User.EmployeeId!.Value;
        var oldSession = factory.CreateClient();
        oldSession.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        EmployeeOffboardingCaseDto? completed = null;
        try
        {
            completed = await CompleteOffboardingAsync(
                admin, employeeId, DateOnly.FromDateTime(DateTime.UtcNow));

            (await oldSession.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "ahmet.yilmaz@hrmaster.local",
                password = DemoPassword,
            })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await _client.PostAsJsonAsync("/api/auth/refresh", new
            {
                refreshToken = auth.RefreshToken,
            })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await admin.PatchAsJsonAsync(
                $"/api/employees/{employeeId}/status", new { status = "active" }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            if (completed is not null)
            {
                await ReverseOffboardingAsync(admin, employeeId, completed, "Test temizliği");
            }
        }

        (await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = auth.RefreshToken,
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "ahmet.yilmaz@hrmaster.local",
            password = DemoPassword,
        })).StatusCode.Should().Be(HttpStatusCode.OK);
    }
    [Fact]
    public async Task ManagerAssignment_GrantsAndRevokesRole_RejectsStaleTokens()
    {
        var adminAuth = await LoginAsync("ik@hrmaster.local");
        var ahmetAuth = await LoginAsync("ahmet.yilmaz@hrmaster.local");
        var ahmetId = ahmetAuth.User.EmployeeId!.Value;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminAuth.Token);
        var staleEmployeeClient = factory.CreateClient();
        staleEmployeeClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ahmetAuth.Token);

        var departmentId = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0].Id;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        int? createdEmployeeId = null;

        try
        {
            var createResponse = await admin.CreateSafeEmployeeAsync("/api/employees", new
            {
                firstName = "Rol",
                lastName = $"Takibi{suffix}",
                title = "Test Personeli",
                departmentId,
                managerId = ahmetId,
                hireDate = "2024-01-01",
                status = "active",
            });
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = (await createResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
            createdEmployeeId = created.Id;

            (await staleEmployeeClient.GetAsync("/api/me")).StatusCode.Should().Be(
                HttpStatusCode.Unauthorized,
                "terfi öncesi employee tokenı yeni organizasyon rolünü atlayamamalı");

            var managerAuth = await LoginAsync("ahmet.yilmaz@hrmaster.local");
            managerAuth.User.Role.Should().Be("manager");
            var staleManagerClient = factory.CreateClient();
            staleManagerClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", managerAuth.Token);

            var updateResponse = await admin.PutAsJsonAsync($"/api/employees/{created.Id}", new
            {
                firstName = "Rol",
                lastName = $"Takibi{suffix}",
                title = "Test Personeli",
                departmentId,
                managerId = (int?)null,
                hireDate = "2024-01-01",
                status = "active",
            });
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            (await staleManagerClient.GetAsync("/api/me")).StatusCode.Should().Be(
                HttpStatusCode.Unauthorized,
                "son ekibini kaybeden yöneticinin eski tokenı yetkiyi korumamalı");

            var employeeAuth = await LoginAsync("ahmet.yilmaz@hrmaster.local");
            employeeAuth.User.Role.Should().Be("employee");
        }
        finally
        {
            using var tenantScope = factory.Services
                .GetRequiredService<ITenantScopeFactory>()
                .Create(adminAuth.User.TenantId);
            var db = tenantScope.Services.GetRequiredService<IApplicationDbContext>();
            if (createdEmployeeId is int employeeId)
            {
                var created = await db.Employees
                    .FirstOrDefaultAsync(employee => employee.Id == employeeId);
                if (created is not null)
                {
                    created.ManagerId = null;
                    await db.SaveChangesAsync(CancellationToken.None);
                }
            }

            var identity = tenantScope.Services.GetRequiredService<IIdentityService>();
            await identity.SetEmployeeManagerRoleAsync(ahmetId, false, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ManagerOffboarding_DraftCompleteAndReverse_PreserveOwnershipAndRoles()
    {
        var adminAuth = await LoginAsync("ik@hrmaster.local");
        var eceId = (await LoginAsync("ece.arslan@hrmaster.local")).User.EmployeeId!.Value;
        var ahmetId = (await LoginAsync("ahmet.yilmaz@hrmaster.local")).User.EmployeeId!.Value;
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.Token);
        var directory = await GetAsync<PagedResult<EmployeeListItemDto>>(
            admin, "/api/employees?page=1&pageSize=50");
        var selinId = directory.Items.Single(item => item.Name == "Selin Koç").Id;

        int leaveRequestId;
        using (var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                   .Create(adminAuth.User.TenantId))
        {
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var request = new LeaveRequest
            {
                EmployeeId = selinId,
                EmploymentPeriodId = await db.EmployeeEmploymentPeriods
                    .Where(item => item.EmployeeId == selinId)
                    .Select(item => item.Id).SingleAsync(),
                LeaveTypeId = await db.LeaveTypes.Select(item => item.Id).FirstAsync(),
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                Days = 1,
                Status = LeaveStatus.Pending,
            };
            db.LeaveRequests.Add(request);
            await db.SaveChangesAsync(CancellationToken.None);
            leaveRequestId = request.Id;
        }

        EmployeeOffboardingCaseDto? completed = null;
        try
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var incomplete = await SaveOffboardingDraftAsync(admin, eceId, date);
            (await admin.ApplyReviewedOffboardingAsync(
                $"/api/employees/{eceId}/offboarding/{incomplete.Id}/complete",
                new { expectedVersion = incomplete.Version }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);

            var ready = await SaveOffboardingDraftAsync(
                admin, eceId, date, ahmetId, incomplete.Version,
                unusedLeaveDays: 4.5m, unusedLeavePayment: 12500m,
                severancePayment: 80000m, noticePayment: 25000m);
            (await admin.ApplyReviewedOffboardingAsync(
                $"/api/employees/{eceId}/offboarding/{ready.Id}/complete",
                new { expectedVersion = incomplete.Version }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict,
                    "eski taslak sürümü tamamlamayı kazanamamalı");
            completed = await CompleteOffboardingAsync(admin, eceId, ready);
            completed.TotalPayment.Should().Be(117500m);
            completed.SettlementSource.Should().Be("external-expert");

            var departing = await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{eceId}");
            (await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{ahmetId}"))
                .ManagerId.Should().Be(departing.ManagerId);
            (await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{selinId}"))
                .ManagerId.Should().Be(ahmetId);
            (await LoginAsync("ahmet.yilmaz@hrmaster.local")).User.Role.Should().Be("manager");

            var promoted = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
            (await GetAsync<List<LeaveRequestDto>>(promoted, "/api/leaves/pending"))
                .Should().Contain(item => item.Id == leaveRequestId);

            using (var driftScope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                       .Create(adminAuth.User.TenantId))
            {
                var driftDb = driftScope.Services.GetRequiredService<IApplicationDbContext>();
                (await driftDb.Employees.FirstAsync(item => item.Id == selinId)).ManagerId = null;
                await driftDb.SaveChangesAsync(CancellationToken.None);
            }

            (await admin.ApplyReviewedOffboardingAsync(
                $"/api/employees/{eceId}/offboarding/{completed.Id}/reverse",
                new { expectedVersion = completed.Version, reason = "Sapma testi" }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict,
                    "sonradan değişen organizasyon bağı sessizce ezilmemeli");

            using (var repairScope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                       .Create(adminAuth.User.TenantId))
            {
                var repairDb = repairScope.Services.GetRequiredService<IApplicationDbContext>();
                (await repairDb.Employees.FirstAsync(item => item.Id == selinId)).ManagerId = ahmetId;
                await repairDb.SaveChangesAsync(CancellationToken.None);
            }

            completed = await ReverseOffboardingAsync(
                admin, eceId, completed, "Hatalı çıkış tarihi");
            completed.Status.Should().Be("reversed");
            (await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{eceId}"))
                .Status.Should().Be("active");
            (await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{ahmetId}"))
                .ManagerId.Should().Be(eceId);
            (await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{selinId}"))
                .ManagerId.Should().Be(eceId);
            (await LoginAsync("ahmet.yilmaz@hrmaster.local")).User.Role.Should().Be("employee");
        }
        finally
        {
            using var scope = factory.Services.GetRequiredService<ITenantScopeFactory>()
                .Create(adminAuth.User.TenantId);
            var db = scope.Services.GetRequiredService<IApplicationDbContext>();
            var request = await db.LeaveRequests.FirstOrDefaultAsync(item => item.Id == leaveRequestId);
            if (request is not null) db.LeaveRequests.Remove(request);
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TerminationCalculationRule_RequiresApproval_IsImmutable_AndGeneratesTraceableDraft()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var employeeClient = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (await employeeClient.GetAsync("/api/termination-calculation-rules"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var createRule = await admin.PostAsJsonAsync("/api/termination-calculation-rules", new
        {
            effectiveFrom,
            dailyWageDivisor = 30m,
            severanceDaysPerYear = 30m,
            minimumSeveranceServiceDays = 365,
            severanceCeiling = 73729.87m,
            noticeDaysUnderSixMonths = 14,
            noticeDaysSixToEighteenMonths = 28,
            noticeDaysEighteenToThirtySixMonths = 42,
            noticeDaysOverThirtySixMonths = 56,
            noticeFirstThresholdMonths = 6,
            noticeSecondThresholdMonths = 18,
            noticeThirdThresholdMonths = 36,
            includeRegularPremiumInDressedWage = true,
            includeRoadAllowanceInDressedWage = true,
            includeMealAllowanceInDressedWage = true,
            includeOtherRegularBenefitsInDressedWage = true,
            sourceReference = "CSGB 2026 + uzman doğrulama TEST",
            reviewNotes = "Entegrasyon testi",
        });
        createRule.StatusCode.Should().Be(HttpStatusCode.Created);
        var rule = (await createRule.Content.ReadFromJsonAsync<TerminationCalculationRuleDto>())!;
        rule.Status.Should().Be("draft");

        var departments = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        var employeeId = await CreateMinimalEmployeeAsync(
            admin, departments[0].Id, "Hesap", $"Taslak{Guid.NewGuid():N}"[..12]);
        var terminationDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var foundation = await GetAsync<EmployeeEmploymentFoundationDto>(
            admin, $"/api/employees/{employeeId}/employment-foundation");
        var packageResponse = await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/compensation-packages", new
            {
                effectiveFrom = foundation.EmploymentPeriods.Single().StartDate,
                monthlyGrossSalary = 60000m,
                regularPremium = 3000m,
                roadAllowance = 1000m,
                mealAllowance = 2000m,
                otherRegularBenefits = 1000m,
                sourceReference = "Test ücret paketi",
            });
        packageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = await SaveOffboardingDraftAsync(
            admin, employeeId, terminationDate,
            unusedLeaveDays: 3m);

        (await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/offboarding/{draft.Id}/calculation-draft", new
            {
                expectedVersion = draft.Version,
                confirmSeveranceEligibility = true,
                confirmNoticeEligibility = false,
                expertReviewReference = "Uzman inceleme TEST-1",
            })).StatusCode.Should().Be(HttpStatusCode.Conflict,
                "taslak kural hesapta kullanılamamalı");

        var approveResponse = await admin.PostAsJsonAsync(
            $"/api/termination-calculation-rules/{rule.Id}/approve",
            new { expectedVersion = rule.Version });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = (await approveResponse.Content
            .ReadFromJsonAsync<TerminationCalculationRuleDto>())!;
        approved.Status.Should().Be("approved");

        (await admin.PutAsJsonAsync($"/api/termination-calculation-rules/{approved.Id}", new
        {
            expectedVersion = approved.Version,
            effectiveFrom,
            dailyWageDivisor = 30m,
            severanceDaysPerYear = 30m,
            minimumSeveranceServiceDays = 365,
            severanceCeiling = 99999m,
            noticeDaysUnderSixMonths = 14,
            noticeDaysSixToEighteenMonths = 28,
            noticeDaysEighteenToThirtySixMonths = 42,
            noticeDaysOverThirtySixMonths = 56,
            noticeFirstThresholdMonths = 6,
            noticeSecondThresholdMonths = 18,
            noticeThirdThresholdMonths = 36,
            includeRegularPremiumInDressedWage = true,
            includeRoadAllowanceInDressedWage = true,
            includeMealAllowanceInDressedWage = true,
            includeOtherRegularBenefitsInDressedWage = true,
            sourceReference = "Değiştirme denemesi",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "onaylı sürüm değiştirilememeli");

        var calculationResponse = await admin.PostAsJsonAsync(
            $"/api/employees/{employeeId}/offboarding/{draft.Id}/calculation-draft", new
            {
                expectedVersion = draft.Version,
                confirmSeveranceEligibility = true,
                confirmNoticeEligibility = false,
                expertReviewReference = "Uzman inceleme TEST-1",
            });
        calculationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var calculated = (await calculationResponse.Content
            .ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
        calculated.SettlementSource.Should().Be("approved-rule-draft");
        calculated.CalculationRuleId.Should().Be(approved.Id);
        calculated.CalculationRuleVersion.Should().Be(approved.Version);
        calculated.CalculationGeneratedAtUtc.Should().NotBeNull();
        calculated.UnusedLeavePayment.Should().Be(6000m);
        calculated.SeverancePayment.Should().BeGreaterThan(0m);
        calculated.NoticePayment.Should().Be(0m);
    }
    // --- departmanlar ---

    [Fact]
    public async Task Departments_CrudAndGuards()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        (await GetAsync<List<DepartmentDto>>(manager, "/api/departments"))
            .Select(d => d.Name).Should().Contain(["Yazılım", "İnsan Kaynakları", "Tasarım", "Satış"]);

        (await manager.PostAsJsonAsync("/api/departments", new { name = "Yetkisiz Departman" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "mutasyon yalnız hr-admin");

        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var createResponse = await admin.PostAsJsonAsync("/api/departments", new { name = "Pazarlama", code = "PZR" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<DepartmentDto>())!;

        (await admin.PostAsJsonAsync("/api/departments", new { name = "Pazarlama" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updateResponse = await admin.PutAsJsonAsync($"/api/departments/{created.Id}", new { name = "Pazarlama & Büyüme", code = "PZR" });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var yazilim = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments")).Single(d => d.Name == "Yazılım");
        (await admin.DeleteAsync($"/api/departments/{yazilim.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "personeli olan departman silinemez");

        (await admin.DeleteAsync($"/api/departments/{created.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- dosyalar ---

    [Fact]
    public async Task Documents_UploadListDownload_AndPhotoUpload()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(client, "/api/departments");
        var id = await CreateMinimalEmployeeAsync(client, departments[0].Id, "Evrak", "Sahibi");

        // Uzantı beyaz listesi dışı → 400.
        (await UploadDocumentAsync(client, id, "zararli.exe", "Nüfus Cüzdanı"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var uploadResponse = await UploadDocumentAsync(client, id, "nufus-cuzdani.pdf", "Nüfus Cüzdanı");
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = (await uploadResponse.Content.ReadFromJsonAsync<EmployeeDocumentDto>())!;
        uploaded.DocumentType.Should().Be("Nüfus Cüzdanı");

        var documents = await GetAsync<List<EmployeeDocumentDto>>(client, $"/api/employees/{id}/documents");
        documents.Should().ContainSingle(d => d.FileName == "nufus-cuzdani.pdf");

        var downloadResponse = await client.GetAsync($"/api/employees/{id}/documents/{uploaded.Id}");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await downloadResponse.Content.ReadAsStringAsync()).Should().Be("test-evrak-icerigi");

        // Foto yükleme profil PhotoPath'ine yazar.
        using var photoContent = new MultipartFormDataContent
        {
            { new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]), "file", "vesikalik.png" },
        };
        var photoResponse = await client.PostAsync($"/api/employees/{id}/photo", photoContent);
        photoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{id}")).Profile.PhotoPath
            .Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Documents_WithdrawalRequiresReasonAndBlocksNormalAccessWhileKeepingAuditMetadata()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        var employeeId = await CreateMinimalEmployeeAsync(
            admin, departments[0].Id, "GeriÇekme", $"Deneme{Guid.NewGuid():N}");
        var otherEmployeeId = await CreateMinimalEmployeeAsync(
            admin, departments[0].Id, "Başka", $"Kişi{Guid.NewGuid():N}");
        var upload = await UploadDocumentAsync(admin, employeeId, "yanlis.pdf", "Kimlik");
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = (await upload.Content.ReadFromJsonAsync<EmployeeDocumentDto>())!;
        var url = $"/api/employees/{employeeId}/documents/{document.Id}";

        (await manager.PostAsJsonAsync($"{url}/withdraw", new { reason = "Yanlış dosya" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"/api/employees/{otherEmployeeId}/documents/{document.Id}/withdraw",
            new { reason = "Yanlış kişi" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsJsonAsync($"{url}/withdraw", new { reason = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var withdrawnResponse = await admin.PostAsJsonAsync($"{url}/withdraw",
            new { reason = "  Yanlış kişiye ait belge  " });
        withdrawnResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var withdrawn = (await withdrawnResponse.Content.ReadFromJsonAsync<WithdrawnEmployeeDocumentDto>())!;
        withdrawn.WithdrawalReason.Should().Be("Yanlış kişiye ait belge");
        withdrawn.WithdrawnByUserId.Should().NotBeNullOrWhiteSpace();
        withdrawn.WithdrawnAtUtc.Should().BeAfter(document.CreatedAtUtc);

        (await GetAsync<List<EmployeeDocumentDto>>(admin, $"/api/employees/{employeeId}/documents"))
            .Should().NotContain(d => d.Id == document.Id);
        (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{employeeId}"))
            .Documents.Should().NotContain(d => d.Id == document.Id);
        var history = await GetAsync<List<WithdrawnEmployeeDocumentDto>>(
            admin, $"/api/employees/{employeeId}/documents/withdrawn");
        history.Should().ContainSingle(d => d.Id == document.Id &&
            d.WithdrawalReason == "Yanlış kişiye ait belge");
        (await manager.GetAsync($"/api/employees/{employeeId}/documents/withdrawn"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"{url}/withdraw", new { reason = "İkinci gerekçe" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var replacementResponse = await UploadDocumentAsync(admin, employeeId, "dogru.pdf", "Kimlik");
        replacementResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var replacement = (await replacementResponse.Content.ReadFromJsonAsync<EmployeeDocumentDto>())!;
        (await GetAsync<List<EmployeeDocumentDto>>(admin, $"/api/employees/{employeeId}/documents"))
            .Should().ContainSingle(d => d.Id == replacement.Id);
        (await GetAsync<List<WithdrawnEmployeeDocumentDto>>(
            admin, $"/api/employees/{employeeId}/documents/withdrawn"))
            .Should().ContainSingle(d => d.Id == document.Id);
    }

    // --- yardımcılar ---

    private static async Task<EmployeeOffboardingCaseDto> SaveOffboardingDraftAsync(
        HttpClient client, int employeeId, DateOnly terminationDate,
        int? successorManagerId = null, int? expectedVersion = null,
        decimal unusedLeaveDays = 0m, decimal unusedLeavePayment = 0m,
        decimal severancePayment = 0m, decimal noticePayment = 0m)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/employees/{employeeId}/offboarding-draft", new
            {
                expectedVersion,
                terminationDate,
                exitCode = "TEST-03",
                exitReason = "Entegrasyon testi",
                terminationReason = "employee-resignation",
                noticeHandling = "not-applicable",
                successorManagerId,
                unusedLeaveDays,
                unusedLeavePayment,
                severancePayment,
                noticePayment,
                settlementReference = "Test uzman hesap belgesi",
                settlementNotes = "Otomatik mevzuat hesabı değildir.",
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
    }

    private static async Task<EmployeeOffboardingCaseDto> CompleteOffboardingAsync(
        HttpClient client, int employeeId, DateOnly terminationDate)
        => await CompleteOffboardingAsync(
            client, employeeId,
            await SaveOffboardingDraftAsync(client, employeeId, terminationDate));

    private static async Task<EmployeeOffboardingCaseDto> CompleteOffboardingAsync(
        HttpClient client, int employeeId, EmployeeOffboardingCaseDto draft)
    {
        var response = await client.ApplyReviewedOffboardingAsync(
            $"/api/employees/{employeeId}/offboarding/{draft.Id}/complete",
            new { expectedVersion = draft.Version });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
    }

    private static async Task<EmployeeOffboardingCaseDto> ReverseOffboardingAsync(
        HttpClient client, int employeeId, EmployeeOffboardingCaseDto completed, string reason)
    {
        var response = await client.ApplyReviewedOffboardingAsync(
            $"/api/employees/{employeeId}/offboarding/{completed.Id}/reverse",
            new { expectedVersion = completed.Version, reason });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
    }
    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"demo hesap girişi başarısız: {email}");
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<int> CreateMinimalEmployeeAsync(
        HttpClient client, int departmentId, string firstName, string lastName)
    {
        var response = await client.CreateSafeEmployeeAsync("/api/employees", new
        {
            firstName,
            lastName,
            title = "Test Personeli",
            departmentId,
            hireDate = "2024-01-01",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!.Id;
    }

    [Fact]
    public async Task Documents_SahteContentType_DogrulanmisUzantidanTuretilir()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(client, "/api/departments");
        var id = await CreateMinimalEmployeeAsync(client, departments[0].Id, "Mime", "Sahibi");

        // İstemci PDF'i "text/html" diye beyan ediyor. MIME tipi kullanıcı girdisidir;
        // saklanan ve indirmede geri verilen değer, doğrulanmış uzantıdan türetilmeli.
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 test"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        content.Add(file, "file", "sozlesme.pdf");
        content.Add(new StringContent("Sözleşme"), "documentType");

        var response = await client.PostAsync($"/api/employees/{id}/documents", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = (await response.Content.ReadFromJsonAsync<EmployeeDocumentDto>())!;
        uploaded.ContentType.Should().Be("application/pdf");
    }

    private static Task<HttpResponseMessage> UploadDocumentAsync(
        HttpClient client, int employeeId, string fileName, string documentType)
    {
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes("test-evrak-icerigi")), "file", fileName },
            { new StringContent(documentType), "documentType" },
        };
        return client.PostAsync($"/api/employees/{employeeId}/documents", content);
    }
}
