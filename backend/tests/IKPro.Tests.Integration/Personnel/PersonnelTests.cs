using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Common.Models;
using IKPro.Application.Features.Auth;
using IKPro.Application.Features.Departments;
using IKPro.Application.Features.Employees;
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
    public async Task StatusChanges_MustUseDedicatedEndpoint()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(client, "/api/departments");
        var departmentId = departments[0].Id;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var passiveCreate = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Dogrudan",
            lastName = $"Pasif{suffix}",
            title = "Test Personeli",
            departmentId,
            hireDate = "2024-01-01",
            status = "passive",
        });
        passiveCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "ayrilis tarihi olmayan pasif personel olusturulamamali");

        var employeeId = await CreateMinimalEmployeeAsync(
            client, departmentId, "Durum", $"Kapisi{suffix}");

        var bypassAttempt = await client.PutAsJsonAsync($"/api/employees/{employeeId}", new
        {
            firstName = "Durum",
            lastName = $"Kapisi{suffix}",
            title = "Degistirildi",
            departmentId,
            hireDate = "2024-01-01",
            status = "passive",
        });
        bypassAttempt.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "genel guncelleme ayrilis tarihi ve Identity erisim esitlemesini atlamamali");

        var unchanged = await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{employeeId}");
        unchanged.Status.Should().Be("active");
        unchanged.TerminationDate.Should().BeNull();
        unchanged.Title.Should().Be("Test Personeli");

        var terminationDate = new DateOnly(2026, 8, 15);
        (await client.PatchAsJsonAsync($"/api/employees/{employeeId}/status", new
        {
            status = "passive",
            terminationDate,
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var passiveEdit = await client.PutAsJsonAsync($"/api/employees/{employeeId}", new
        {
            firstName = "Durum",
            lastName = $"Kapisi{suffix}",
            title = "Pasif Kayit Duzenlendi",
            departmentId,
            hireDate = "2024-01-01",
            status = "passive",
        });
        passiveEdit.StatusCode.Should().Be(HttpStatusCode.OK,
            "pasif personelin ozluk bilgileri ayni durum korunarak duzenlenebilmeli");

        var edited = (await passiveEdit.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
        edited.Status.Should().Be("passive");
        edited.TerminationDate.Should().Be(terminationDate);
        edited.Title.Should().Be("Pasif Kayit Duzenlendi");
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
    public async Task BulkDeactivate_ThenReactivate_UpdatesStatuses()
    {
        var client = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(client, "/api/departments");
        var id = await CreateMinimalEmployeeAsync(client, departments[0].Id, "Toplu", "Pasif");

        (await client.PostAsJsonAsync(
                "/api/employees/bulk-deactivate", new { ids = new[] { id } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "pasife alma etkin işten ayrılma tarihi olmadan yapılamaz");

        var terminationDate = new DateOnly(2026, 8, 15);
        var bulkResponse = await client.PostAsJsonAsync("/api/employees/bulk-deactivate", new
        {
            ids = new[] { id },
            terminationDate,
        });
        bulkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var passive = await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{id}");
        passive.Status.Should().Be("passive");
        passive.TerminationDate.Should().Be(terminationDate);

        var statusResponse = await client.PatchAsJsonAsync($"/api/employees/{id}/status", new { status = "active" });
        statusResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var active = await GetAsync<EmployeeDetailDto>(client, $"/api/employees/{id}");
        active.Status.Should().Be("active");
        active.TerminationDate.Should().BeNull("yeniden aktifleştirmede eski ayrılış tarihi taşınmaz");
    }

    [Fact]
    public async Task PassiveEmployee_LosesCurrentSession_LoginAndRefreshAccess()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var employeeAuth = await LoginAsync("ahmet.yilmaz@hrmaster.local");
        var employeeId = employeeAuth.User.EmployeeId!.Value;

        var employeeClient = factory.CreateClient();
        employeeClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", employeeAuth.Token);

        try
        {
            var deactivate = await admin.PatchAsJsonAsync(
                $"/api/employees/{employeeId}/status", new
                {
                    status = "passive",
                    terminationDate = "2026-08-15",
                });
            deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await employeeClient.GetAsync("/api/me")).StatusCode.Should().Be(
                HttpStatusCode.Unauthorized,
                "pasife alınmadan önceki access token anında kesilmeli");

            (await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "ahmet.yilmaz@hrmaster.local",
                password = DemoPassword,
            })).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "pasif personel yeni oturum açamamalı");

            (await _client.PostAsJsonAsync("/api/auth/refresh", new
            {
                refreshToken = employeeAuth.RefreshToken,
            })).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "pasife alınırken mevcut refresh token iptal edilmeli");
        }
        finally
        {
            var reactivate = await admin.PatchAsJsonAsync(
                $"/api/employees/{employeeId}/status", new { status = "active" });
            reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = employeeAuth.RefreshToken,
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "reaktivasyon eski refresh token'ı yeniden geçerli kılmamalı");

        (await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "ahmet.yilmaz@hrmaster.local",
            password = DemoPassword,
        })).StatusCode.Should().Be(HttpStatusCode.OK,
            "reaktive edilen personel yeni bir oturum açabilmeli");
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
    public async Task ManagerOffboarding_RequiresSuccessor_TransfersTeamLeaveScopeAndRoles()
    {
        var adminAuth = await LoginAsync("ik@hrmaster.local");
        var eceAuth = await LoginAsync("ece.arslan@hrmaster.local");
        var ahmetAuth = await LoginAsync("ahmet.yilmaz@hrmaster.local");
        var eceId = eceAuth.User.EmployeeId!.Value;
        var ahmetId = ahmetAuth.User.EmployeeId!.Value;

        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminAuth.Token);

        var directory = await GetAsync<PagedResult<EmployeeListItemDto>>(
            admin, "/api/employees?page=1&pageSize=50");
        var selinId = directory.Items.Single(employee => employee.Name == "Selin Koç").Id;
        int leaveRequestId;

        using (var tenantScope = factory.Services
                   .GetRequiredService<ITenantScopeFactory>()
                   .Create(adminAuth.User.TenantId))
        {
            var db = tenantScope.Services.GetRequiredService<IApplicationDbContext>();
            var leaveTypeId = await db.LeaveTypes.Select(type => type.Id).FirstAsync();
            var leaveRequest = new LeaveRequest
            {
                EmployeeId = selinId,
                LeaveTypeId = leaveTypeId,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                Days = 1,
                Status = LeaveStatus.Pending,
                Description = "Halef devri kapsam testi",
            };
            db.LeaveRequests.Add(leaveRequest);
            await db.SaveChangesAsync(CancellationToken.None);
            leaveRequestId = leaveRequest.Id;
        }

        try
        {
            var context = await GetAsync<EmployeeOffboardingContextDto>(
                admin, $"/api/employees/{eceId}/offboarding-context");
            context.DirectReportCount.Should().Be(2);
            context.PendingLeaveRequestCount.Should().BeGreaterThanOrEqualTo(1);
            context.Successors.Should().Contain(successor =>
                successor.Id == ahmetId && successor.IsDirectReport);

            var terminationDate = DateOnly.FromDateTime(DateTime.UtcNow);
            (await admin.PatchAsJsonAsync($"/api/employees/{eceId}/status", new
            {
                status = "passive",
                terminationDate,
            })).StatusCode.Should().Be(HttpStatusCode.Conflict,
                "ekibi olan yönetici halef seçilmeden pasife alınamamalı");

            (await admin.PostAsJsonAsync("/api/employees/bulk-deactivate", new
            {
                ids = new[] { eceId },
                terminationDate,
            })).StatusCode.Should().Be(HttpStatusCode.Conflict,
                "yönetici toplu pasife alma yoluyla halef kapısını aşamamalı");

            (await admin.PatchAsJsonAsync($"/api/employees/{eceId}/status", new
            {
                status = "passive",
                terminationDate,
                successorManagerId = ahmetId,
            })).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var departing = await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{eceId}");
            var promoted = await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{ahmetId}");
            var transferred = await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{selinId}");
            departing.Status.Should().Be("passive");
            promoted.ManagerId.Should().Be(departing.ManagerId,
                "terfi eden doğrudan rapor kendi kendisinin yöneticisi olmamalı");
            transferred.ManagerId.Should().Be(ahmetId);

            (await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "ece.arslan@hrmaster.local",
                password = DemoPassword,
            })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var promotedAuth = await LoginAsync("ahmet.yilmaz@hrmaster.local");
            promotedAuth.User.Role.Should().Be("manager");
            var promotedClient = factory.CreateClient();
            promotedClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", promotedAuth.Token);

            var pending = await GetAsync<List<LeaveRequestDto>>(
                promotedClient, "/api/leaves/pending");
            pending.Should().Contain(request => request.Id == leaveRequestId,
                "bekleyen izinler yeni yönetici kapsamına dinamik olarak geçmeli");
            (await promotedClient.PostAsJsonAsync(
                $"/api/leaves/{leaveRequestId}/approve", new { note = "Halef onayı" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            using var tenantScope = factory.Services
                .GetRequiredService<ITenantScopeFactory>()
                .Create(adminAuth.User.TenantId);
            var db = tenantScope.Services.GetRequiredService<IApplicationDbContext>();
            var ece = await db.Employees.FirstAsync(employee => employee.Id == eceId);
            var ahmet = await db.Employees.FirstAsync(employee => employee.Id == ahmetId);
            var selin = await db.Employees.FirstAsync(employee => employee.Id == selinId);
            ece.Status = EmployeeStatus.Active;
            ece.TerminationDate = null;
            ahmet.ManagerId = eceId;
            selin.ManagerId = eceId;

            var leaveRequest = await db.LeaveRequests
                .FirstOrDefaultAsync(request => request.Id == leaveRequestId);
            if (leaveRequest is not null)
            {
                db.LeaveRequests.Remove(leaveRequest);
            }

            await db.SaveChangesAsync(CancellationToken.None);

            var identity = tenantScope.Services.GetRequiredService<IIdentityService>();
            await identity.SetEmployeeManagerRoleAsync(eceId, true, CancellationToken.None);
            await identity.SetEmployeeAccessAsync(eceId, true, CancellationToken.None);
            await identity.SetEmployeeManagerRoleAsync(ahmetId, false, CancellationToken.None);
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
