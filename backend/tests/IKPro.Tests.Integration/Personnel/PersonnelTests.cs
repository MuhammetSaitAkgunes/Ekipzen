using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Common.Models;
using IKPro.Application.Features.Auth;
using IKPro.Application.Features.Departments;
using IKPro.Application.Features.Employees;
using IKPro.Application.Features.Employees.Employment;
using IKPro.Application.Features.Employees.Offboarding;
using IKPro.Application.Features.Leaves;
using IKPro.Domain.Entities.Leaves;
using IKPro.Domain.Enums;
using IKPro.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace IKPro.Tests.Integration.Personnel;

/// <summary>
/// Faz 3 uçtan uca: directory (rol kapsamı + filtre + sayfalama), personel kartı CRUD,
/// bulk-deactivate/status, departman CRUD ve dosya (foto + evrak) akışları.
/// Demo hesaplar: hr-admin ik@, manager ece.arslan@ (ekibi: Ahmet + Selin), employee ahmet.yilmaz@.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PersonnelTests(IKProApiFactory factory)
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

        var createResponse = await client.PostAsJsonAsync("/api/employees", new
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
        created.Profile.CanWorkNightShift.Should().BeTrue();

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

        var invalid = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Hatalı",
            lastName = "Tc",
            title = "Test",
            departmentId = deptId,
            hireDate = "2024-01-01",
            nationalId = "123",
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var duplicate = await client.PostAsJsonAsync("/api/employees", new
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
        var createdResponse = await client.PostAsJsonAsync("/api/employees", new
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
    }

    [Fact]
    public async Task StatusChanges_MustUseOffboardingWorkflow()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departmentId = (await GetAsync<List<DepartmentDto>>(client, "/api/departments"))[0].Id;
        var id = await CreateMinimalEmployeeAsync(client, departmentId, "Durum", $"Kapisi{Guid.NewGuid():N}");

        (await client.PutAsJsonAsync($"/api/employees/{id}", new
        {
            firstName = "Durum", lastName = "Kapisi", title = "Degistirildi",
            departmentId, hireDate = "2024-01-01", status = "passive",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await client.PatchAsJsonAsync($"/api/employees/{id}/status", new
        {
            status = "passive", terminationDate = "2026-08-15",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var unchanged = await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{id}");
        unchanged.Status.Should().Be("active");
        unchanged.TerminationDate.Should().BeNull();
    }
    [Fact]
    public async Task CreateEmployee_AsManager_Returns403()
    {
        var client = await AuthedClientAsync("ece.arslan@hrmaster.local");

        var response = await client.PostAsJsonAsync("/api/employees", new
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
            ids = new[] { id }, terminationDate = "2026-08-15",
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
                email = "ahmet.yilmaz@hrmaster.local", password = DemoPassword,
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
            email = "ahmet.yilmaz@hrmaster.local", password = DemoPassword,
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
            var createResponse = await admin.PostAsJsonAsync("/api/employees", new
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
            (await admin.PostAsJsonAsync(
                $"/api/employees/{eceId}/offboarding/{incomplete.Id}/complete",
                new { expectedVersion = incomplete.Version }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);

            var ready = await SaveOffboardingDraftAsync(
                admin, eceId, date, ahmetId, incomplete.Version,
                unusedLeaveDays: 4.5m, unusedLeavePayment: 12500m,
                severancePayment: 80000m, noticePayment: 25000m);
            (await admin.PostAsJsonAsync(
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

            (await admin.PostAsJsonAsync(
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
                expectedVersion, terminationDate, exitCode = "TEST-03",
                exitReason = "Entegrasyon testi",
                terminationReason = "employee-resignation", noticeHandling = "not-applicable",
                successorManagerId,
                unusedLeaveDays, unusedLeavePayment, severancePayment, noticePayment,
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
        var response = await client.PostAsJsonAsync(
            $"/api/employees/{employeeId}/offboarding/{draft.Id}/complete",
            new { expectedVersion = draft.Version });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EmployeeOffboardingCaseDto>())!;
    }

    private static async Task<EmployeeOffboardingCaseDto> ReverseOffboardingAsync(
        HttpClient client, int employeeId, EmployeeOffboardingCaseDto completed, string reason)
    {
        var response = await client.PostAsJsonAsync(
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
        var response = await client.PostAsJsonAsync("/api/employees", new
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
