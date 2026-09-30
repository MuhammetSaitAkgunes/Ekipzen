using FluentAssertions;
using Ekipzen.API.Services;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Common.Models;
using Ekipzen.Infrastructure.Email;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Leaves;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Recruitment;
using Ekipzen.Application.Features.Recruitment.Commands;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ekipzen.Tests.Integration.Recruitment;

/// <summary>
/// Faz 7 uçtan uca: pozisyon + aday oluşturma, pipeline geçişleri (history kaydıyla),
/// not/değerlendirme ekleme, hire→Employee dönüşümü (kontenjan düşer, kart directory'de
/// görünür), funnel sayıları ve rol korumaları (yalnız hr-admin).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RecruitmentTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task Pipeline_EndToEnd_FromApplicationToHire()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        var yazilim = departments.Single(d => d.Name == "Yazılım");

        // --- pozisyon (kontenjan 1) ---
        var positionResponse = await admin.PostAsJsonAsync("/api/positions", new
        {
            title = "Senior Frontend Developer",
            departmentId = yazilim.Id,
            openCount = 1,
        });
        positionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var position = (await positionResponse.Content.ReadFromJsonAsync<PositionDto>())!;

        // --- aday (recruitment.js Burak Yılmaz kartı) ---
        var candidateResponse = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Burak Yılmaz",
            appliedRole = "Senior Frontend Developer",
            positionId = position.Id,
            score = 92,
            location = "İstanbul",
            experienceYears = 5,
            summary = "React ve modern JavaScript konusunda 5 yıllık deneyime sahip güçlü bir aday.",
            skills = new[] { "React.js", "Next.js", "TypeScript" },
            experiences = new[]
            {
                new { title = "Senior Frontend Developer", company = "TechSolutions A.Ş.", period = "2021 - Günümüz" },
            },
        });
        candidateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var candidate = (await candidateResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!;

        candidate.Status.Should().Be("Yeni");
        candidate.Initials.Should().Be("BY");
        candidate.Skills.Select(s => s.Name).Should().Contain("TypeScript");
        candidate.History.Should().Contain(h => h.Event == "Başvuru alındı");

        // --- pipeline: Yeni → Mülakat → Teklif (geçişler history'ye düşer) ---
        var interview = await PatchStatusAsync(admin, candidate.Id, "Mülakat");
        interview.Status.Should().Be("Mülakat");
        interview.History.Should().Contain(h => h.Event.Contains("Yeni → Mülakat"));

        // --- mülakat notu + değerlendirme ---
        var noteResponse = await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/notes", new
        {
            noteType = "Teknik Mülakat",
            text = "React derinliği ve mimari yaklaşımı güçlü.",
        });
        noteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await noteResponse.Content.ReadFromJsonAsync<InterviewNoteDto>())!
            .AuthorName.Should().Be("İK Yöneticisi");

        (await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/evaluations", new
        {
            criterion = "Teknik Yeterlilik",
            score = 4,
            maxScore = 5,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        await PatchStatusAsync(admin, candidate.Id, "Teklif");

        // --- hire → Employee dönüşümü (tam provizyon: login + çalışma dönemi) ---
        var hireEmail = $"burak.yilmaz-{Guid.NewGuid():N}@hrmaster.local";
        (await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire", new
        {
            departmentId = yazilim.Id,
            email = hireEmail,
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest, "eski istemcinin eksik sözleşme türü varsayılmamalı");
        (await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire", new
        {
            departmentId = yazilim.Id,
            email = hireEmail,
            contractType = "unknown",
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetAsync<CandidateDetailDto>(admin, $"/api/candidates/{candidate.Id}"))
            .Status.Should().Be("Teklif", "geçersiz istek adayı işe alınmış saymamalı");

        var hireResponse = await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire", new
        {
            departmentId = yazilim.Id,
            email = hireEmail,
            contractType = "fixed-term",
        });
        hireResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var hired = (await hireResponse.Content.ReadFromJsonAsync<HireResultDto>())!;
        hired.EmployeeName.Should().Be("Burak Yılmaz");
        var employee = await GetAsync<EmployeeDetailDto>(admin, $"/api/employees/{hired.EmployeeId}");
        employee.ContractType.Should().Be("fixed-term");
        var foundation = await GetAsync<EmployeeEmploymentFoundationDto>(
            admin, $"/api/employees/{hired.EmployeeId}/employment-foundation");
        foundation.ContractType.Should().Be("fixed-term");
        foundation.EmploymentPeriods.Should().ContainSingle()
            .Which.ContractType.Should().Be("fixed-term");

        // İşe alınan kişi davetini kabul eder (şifre belirler), login olur.
        // Yeni dönemde peşin bakiye verilmez; ilk hak ediş çalışma yıldönümündedir.
        await AcceptInviteAsync(hireEmail);
        var hiredClient = await AuthedClientAsync(hireEmail);
        var hiredBalance = await GetAsync<LeaveBalanceDto>(
            hiredClient, $"/api/leaves/balance?year={DateTime.UtcNow.Year}");
        hiredBalance.EntitledDays.Should().Be(0);
        hiredBalance.NextAccrualDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1));

        // Personel kartı directory'de görünür.
        var directory = await GetAsync<PagedResult<EmployeeListItemDto>>(
            admin, "/api/employees?search=Burak Yılmaz");
        directory.Items.Should().Contain(e => e.Id == hired.EmployeeId && e.Status == "active");

        // Aday kilitlenir, pozisyon kontenjanı kapanır.
        var afterHire = await GetAsync<CandidateDetailDto>(admin, $"/api/candidates/{candidate.Id}");
        afterHire.Status.Should().Be("İşe Alındı");
        afterHire.History.Should().Contain(h => h.Event.Contains("İşe alındı"));

        (await admin.PatchAsJsonAsync($"/api/candidates/{candidate.Id}/status", new { status = "Yeni" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire",
                new { departmentId = yazilim.Id, email = $"tekrar-{Guid.NewGuid():N}@hrmaster.local", contractType = "fixed-term" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var positions = await GetAsync<List<PositionDto>>(admin, "/api/positions");
        var closedPosition = positions.Single(p => p.Id == position.Id);
        closedPosition.OpenCount.Should().Be(0);
        closedPosition.IsOpen.Should().BeFalse("tek kontenjan dolunca ilan kapanır");
    }

    [Fact]
    public async Task PositionCorrection_PreservesCandidates_AndEnforcesClosureAcrossCreateAndHire()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var department = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))
            .Single(item => item.Name == "Yazılım");
        var positionResponse = await admin.PostAsJsonAsync("/api/positions", new
        {
            title = "QA Uzmanı", departmentId = department.Id, openCount = 2,
        });
        positionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var position = (await positionResponse.Content.ReadFromJsonAsync<PositionDto>())!;
        position.Version.Should().Be(1);
        var candidateResponse = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Deniz Korkmaz", appliedRole = position.Title,
            positionId = position.Id, score = 75,
        });
        candidateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var candidate = (await candidateResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        var correctionUrl = $"/api/positions/{position.Id}";
        var closeBody = new
        {
            expectedVersion = position.Version, title = "Kalite Güvence Uzmanı",
            departmentId = department.Id, openCount = 2, isOpen = false,
            reason = "İşe alım geçici olarak durduruldu.",
        };
        (await manager.PutAsJsonAsync(correctionUrl, closeBody))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var closeResponse = await admin.PutAsJsonAsync(correctionUrl, closeBody);
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = (await closeResponse.Content.ReadFromJsonAsync<PositionDto>())!;
        closed.Version.Should().Be(2);
        closed.IsOpen.Should().BeFalse();
        closed.CandidateCount.Should().Be(1);
        (await admin.PutAsJsonAsync(correctionUrl, closeBody))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Yeni Başvuran", appliedRole = closed.Title,
            positionId = position.Id, score = 70,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire", new
        {
            departmentId = department.Id,
            email = $"closed-position-{Guid.NewGuid():N}@hrmaster.local",
            contractType = "indefinite",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetAsync<CandidateDetailDto>(admin, $"/api/candidates/{candidate.Id}"))
            .PositionId.Should().Be(position.Id);
        var history = await GetAsync<List<PositionCorrectionDto>>(admin,
            $"{correctionUrl}/corrections");
        history.Should().ContainSingle();
        history[0].Before.Title.Should().Be(position.Title);
        history[0].After.Title.Should().Be(closed.Title);
        history[0].Reason.Should().Be(closeBody.reason);

        var reopenResponse = await admin.PutAsJsonAsync(correctionUrl, new
        {
            expectedVersion = closed.Version, title = closed.Title,
            departmentId = department.Id, openCount = 1, isOpen = true,
            reason = "Yeni onaylı kadro açıldı.",
        });
        reopenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopened = (await reopenResponse.Content.ReadFromJsonAsync<PositionDto>())!;
        reopened.Version.Should().Be(3);
        var hireResponse = await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire", new
        {
            departmentId = department.Id,
            email = $"reopened-position-{Guid.NewGuid():N}@hrmaster.local",
            contractType = "indefinite",
        });
        hireResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterHire = (await GetAsync<List<PositionDto>>(admin, "/api/positions"))
            .Single(item => item.Id == position.Id);
        afterHire.OpenCount.Should().Be(0);
        afterHire.IsOpen.Should().BeFalse();
        afterHire.Version.Should().Be(4);
        (await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Kontenjan Sonrası", appliedRole = afterHire.Title,
            positionId = position.Id, score = 70,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RejectedCandidate_CannotBeHired()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var departments = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");

        var candidateResponse = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Ayşe Vural",
            appliedRole = "İK Uzmanı",
            score = 45,
        });
        var candidate = (await candidateResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!;

        await PatchStatusAsync(admin, candidate.Id, "Red");

        (await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire",
                new { departmentId = departments[0].Id, email = $"red-{Guid.NewGuid():N}@hrmaster.local", contractType = "indefinite" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Hire_TransientInviteFailure_RemainsRecoverableAndRetryCompletes()
    {
        var hireEmail = $"retry-{Guid.NewGuid():N}@hrmaster.local";
        using var retryFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(provider =>
                    new FailCountEmailSender(
                        hireEmail,
                        failureCount: 2,
                        new FileOutboxEmailSender(
                            provider.GetRequiredService<IConfiguration>(),
                            provider.GetRequiredService<ILogger<FileOutboxEmailSender>>())));
            }));

        var anonymous = retryFactory.CreateClient();
        var login = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { email = "ik@hrmaster.local", password = DemoPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        var admin = retryFactory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var departments = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        var candidateResponse = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Retry Aday",
            appliedRole = "Platform Uzmanı",
            score = 82,
        });
        candidateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var candidate = (await candidateResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!;

        var hireResponse = await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire", new
        {
            departmentId = departments[0].Id,
            email = hireEmail,
            contractType = "indefinite",
        });
        hireResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "gecici e-posta hatasi atomik ise alim kaydini kaybettirmemeli");
        var hired = (await hireResponse.Content.ReadFromJsonAsync<HireResultDto>())!;
        hired.Provisioning.Status.Should().Be("pending");
        hired.Provisioning.AttemptCount.Should().Be(1);

        var pending = await GetAsync<CandidateDetailDto>(admin, $"/api/candidates/{candidate.Id}");
        pending.Status.Should().Be("İşe Alındı");
        pending.Provisioning!.Status.Should().Be("pending");
        pending.Provisioning.LastError.Should().NotBeNullOrWhiteSpace();

        var retryResponse = await admin.PostAsJsonAsync(
            $"/api/candidates/{candidate.Id}/provisioning/retry", new { email = (string?)null });
        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var stillPending = (await retryResponse.Content.ReadFromJsonAsync<IdentityProvisioningDto>())!;
        stillPending.Status.Should().Be("pending");
        stillPending.AttemptCount.Should().Be(2);
        stillPending.LastError.Should().NotBeNullOrWhiteSpace();

        using (var tenantScope = retryFactory.Services
                   .GetRequiredService<ITenantScopeFactory>()
                   .Create(auth.User.TenantId))
        {
            var db = tenantScope.Services.GetRequiredService<IApplicationDbContext>();
            var updated = await db.IdentityProvisioningOperations
                .Where(operation => operation.CandidateId == candidate.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(operation => operation.NextAttemptAtUtc,
                        DateTime.UtcNow.AddSeconds(-1)));
            updated.Should().Be(1);
        }

        var completedCount = await retryFactory.Services
            .GetRequiredService<IdentityProvisioningRetryService>()
            .RunOnceAsync(CancellationToken.None);
        completedCount.Should().BeGreaterThan(0);

        var completedCandidate = await GetAsync<CandidateDetailDto>(
            admin, $"/api/candidates/{candidate.Id}");
        completedCandidate.Provisioning!.Status.Should().Be("completed");
        completedCandidate.Provisioning.AttemptCount.Should().Be(3);
        completedCandidate.Provisioning.LastError.Should().BeNull();

        await AcceptInviteAsync(hireEmail);
        _ = await AuthedClientAsync(hireEmail);

        (await admin.PostAsJsonAsync(
            $"/api/candidates/{candidate.Id}/provisioning/retry", new { email = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict,
                "tamamlanan idempotent is yeniden kuyruga alinmamali");
    }

    [Fact]
    public async Task Funnel_AndPoolFilters_ReflectCandidates()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var created = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Mert Demir",
            appliedRole = "Backend Developer",
            score = 78,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var pool = await GetAsync<List<CandidateListItemDto>>(admin, "/api/candidates?status=Yeni");
        pool.Should().OnlyContain(c => c.Status == "Yeni");
        pool.Should().Contain(c => c.Name == "Mert Demir");

        var searched = await GetAsync<List<CandidateListItemDto>>(admin, "/api/candidates?search=Backend");
        searched.Should().OnlyContain(c => c.AppliedRole.Contains("Backend"));

        var funnel = await GetAsync<FunnelDto>(admin, "/api/recruitment/funnel");
        funnel.Total.Should().BeGreaterThanOrEqualTo(1);
        funnel.New.Should().BeGreaterThanOrEqualTo(1);
        (funnel.New + funnel.Interview + funnel.Offer + funnel.Rejected + funnel.Hired)
            .Should().Be(funnel.Total);
    }

    [Fact]
    public async Task CandidatePool_InvalidStatusFilter_ReturnsValidationProblem()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var response = await admin.GetAsync("/api/candidates?status=completed");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("errors").EnumerateObject()
            .Should().Contain(error => error.Name.Equals("Status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Recruitment_IsHrAdminOnly()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        (await manager.GetAsync("/api/candidates")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (await employee.GetAsync("/api/recruitment/funnel")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CandidateCorrection_PreservesEvidence_AndRequiresCurrentVersionsAndRoleReview()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var department = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0];
        async Task<PositionDto> CreatePosition(string title)
        {
            var response = await admin.PostAsJsonAsync("/api/positions", new
            { title, departmentId = department.Id, openCount = 2 });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<PositionDto>())!;
        }
        var source = await CreatePosition("Kaynak Rol");
        var target = await CreatePosition("Yeni Rol");
        var created = await admin.PostAsJsonAsync("/api/candidates", new
        {
            name = "Deniz Yanlış", appliedRole = source.Title, positionId = source.Id, score = 60,
            skills = new[] { "SQL" },
            experiences = new[] { new { title = "Uzman", company = "Önceki Şirket" } },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var candidate = (await created.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        candidate.Version.Should().Be(1);
        var url = $"/api/candidates/{candidate.Id}";
        var interviewed = await PatchStatusAsync(admin, candidate.Id, "Mülakat");
        interviewed.Version.Should().Be(2);
        (await admin.PostAsJsonAsync($"{url}/notes", new
        { noteType = "Teknik Mülakat", text = "Kaynak role ilişkin gözlem." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var afterNote = await GetAsync<CandidateDetailDto>(admin, url);
        afterNote.Version.Should().Be(3);
        (await admin.PostAsJsonAsync($"{url}/evaluations", new
        { criterion = "SQL", score = 4, maxScore = 5 })).StatusCode.Should().Be(HttpStatusCode.Created);
        var current = await GetAsync<CandidateDetailDto>(admin, url);
        current.Version.Should().Be(4);
        object Body(int version, bool confirm = true, int? targetVersion = null) => new
        {
            expectedVersion = version, name = "Deniz Doğru", appliedRole = target.Title,
            positionId = target.Id, expectedPositionVersion = targetVersion ?? target.Version,
            score = 75, location = "Ankara", experienceYears = 3, summary = "Belgeden doğrulandı.",
            reason = "Başvuru belgesindeki isim ve rol doğrulandı.", confirmExistingEvidenceReview = confirm,
        };
        (await manager.PutAsJsonAsync(url, Body(current.Version)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.GetAsync($"{url}/corrections")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PutAsJsonAsync(url, Body(interviewed.Version)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "not ve değerlendirme eski düzeltme formunu geçersiz kılar");
        (await admin.PutAsJsonAsync(url, Body(afterNote.Version))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PutAsJsonAsync(url, Body(current.Version, false)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync(url, Body(current.Version, targetVersion: 999)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var correctedResponse = await admin.PutAsJsonAsync(url, Body(current.Version));
        correctedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var corrected = (await correctedResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        corrected.Version.Should().Be(5);
        corrected.Status.Should().Be("Mülakat");
        corrected.AppliedAtUtc.Should().Be(candidate.AppliedAtUtc);
        corrected.PositionId.Should().Be(target.Id);
        corrected.Notes.Should().BeEquivalentTo(current.Notes);
        corrected.Evaluations.Should().BeEquivalentTo(current.Evaluations);
        corrected.Skills.Should().BeEquivalentTo(candidate.Skills);
        corrected.Experiences.Should().BeEquivalentTo(candidate.Experiences);
        var history = await GetAsync<List<CandidateCorrectionDto>>(admin, $"{url}/corrections");
        history.Should().ContainSingle();
        history[0].Before.Name.Should().Be(candidate.Name);
        history[0].Before.PositionId.Should().Be(source.Id);
        history[0].After.Name.Should().Be("Deniz Doğru");
        history[0].After.Status.Should().Be("Mülakat");
        history[0].EvidenceReviewConfirmed.Should().BeTrue();
        history[0].ChangedByName.Should().Be("İK Yöneticisi");
        history[0].ChangedByUserId.Should().NotBeNullOrEmpty();
        history[0].Reason.Should().Be("Başvuru belgesindeki isim ve rol doğrulandı.");
        (await admin.PutAsJsonAsync(url, Body(current.Version))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var positions = await GetAsync<List<PositionDto>>(admin, "/api/positions");
        positions.Single(item => item.Id == source.Id).CandidateCount.Should().Be(0);
        positions.Single(item => item.Id == target.Id).CandidateCount.Should().Be(1);
        positions.Where(item => item.Id == source.Id || item.Id == target.Id)
            .Should().OnlyContain(item => item.OpenCount == 2 && item.Version == 1);

        var hired = await admin.PostAsJsonAsync($"{url}/hire", new
        { departmentId = department.Id, email = $"corrected-{Guid.NewGuid():N}@hrmaster.local", contractType = "indefinite" });
        hired.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterHire = await GetAsync<CandidateDetailDto>(admin, url);
        afterHire.Version.Should().Be(6);
        (await admin.PutAsJsonAsync(url, Body(afterHire.Version))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetAsync<List<CandidateCorrectionDto>>(admin, $"{url}/corrections")).Should().ContainSingle();
    }

    [Fact]
    public async Task CandidateCorrection_ClosedTargetCannotBeAttached_ButExistingClosedLinkCanBeKeptOrRemoved()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var createdPosition = await admin.PostAsJsonAsync("/api/positions", new
        { title = "Kapanacak Rol", openCount = 1 });
        var position = (await createdPosition.Content.ReadFromJsonAsync<PositionDto>())!;
        var createdCandidate = await admin.PostAsJsonAsync("/api/candidates", new
        { name = "Mevcut Aday", appliedRole = position.Title, positionId = position.Id, score = 70 });
        var linked = (await createdCandidate.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        var unlinkedResponse = await admin.PostAsJsonAsync("/api/candidates", new
        { name = "Bağlantısız Aday", appliedRole = position.Title, score = 70 });
        var unlinked = (await unlinkedResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        var journey = await GetAsync<RecruitmentJourneyDto>(admin, "/api/guidance/recruitment/journey");
        var closedResponse = await admin.PutAsJsonAsync($"/api/positions/{position.Id}", new
        { expectedVersion = position.Version, title = position.Title, openCount = 1, isOpen = false, reason = "Alım durdu." });
        closedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = (await closedResponse.Content.ReadFromJsonAsync<PositionDto>())!;
        object Body(CandidateDetailDto item, int? positionId, string name, string reason = "Belgeden düzeltildi.") => new
        {
            expectedVersion = item.Version, name, appliedRole = item.AppliedRole, positionId,
            expectedPositionVersion = closed.Version, score = item.Score,
            experienceYears = item.ExperienceYears, reason, confirmExistingEvidenceReview = true,
        };
        (await admin.PutAsJsonAsync($"/api/candidates/{unlinked.Id}", Body(unlinked, position.Id, "Yeni Ad")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PutAsJsonAsync($"/api/candidates/{linked.Id}", Body(linked, position.Id, "Yeni Ad", " ")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var renameResponse = await admin.PutAsJsonAsync($"/api/candidates/{linked.Id}", Body(linked, position.Id, "Doğru Ad"));
        renameResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = (await renameResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        renamed.PositionId.Should().Be(position.Id);
        renamed.Status.Should().Be("Yeni");
        (await admin.PutAsJsonAsync($"/api/candidates/{linked.Id}", Body(renamed, position.Id, renamed.Name)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "değişikliksiz kayıt geçmiş üretmez");
        var detachedResponse = await admin.PutAsJsonAsync($"/api/candidates/{linked.Id}", Body(renamed, null, renamed.Name));
        detachedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detachedResponse.Content.ReadFromJsonAsync<CandidateDetailDto>())!.PositionId.Should().BeNull();
        var history = await GetAsync<List<CandidateCorrectionDto>>(admin, $"/api/candidates/{linked.Id}/corrections");
        history.Should().HaveCount(2);
        history[0].EvidenceReviewConfirmed.Should().BeTrue();
        history[1].EvidenceReviewConfirmed.Should().BeFalse();
        (await GetAsync<RecruitmentJourneyDto>(admin, "/api/guidance/recruitment/journey"))
            .VerifiedFirstReviews.Should().Be(journey.VerifiedFirstReviews,
                "başvuru düzeltmesi ilk aşama kararı veya yeni öğrenme kanıtı değildir");
    }

    [Fact]
    public async Task EvidenceRevisions_KeepOriginals_ProjectCurrentVersions_AndDoNotCreateLearningEvidence()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var created = await admin.PostAsJsonAsync("/api/candidates", new
        { name = "Kanıt Sürüm Adayı", appliedRole = "Uzman", score = 66 });
        var candidate = (await created.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        var url = $"/api/candidates/{candidate.Id}";
        var noteResponse = await admin.PostAsJsonAsync($"{url}/notes", new
        { noteType = "İK Görüşmesi", text = "İlk not metni." });
        var note = (await noteResponse.Content.ReadFromJsonAsync<InterviewNoteDto>())!;
        var evaluationResponse = await admin.PostAsJsonAsync($"{url}/evaluations", new
        { criterion = "SQL", score = 4, maxScore = 5 });
        var evaluation = (await evaluationResponse.Content.ReadFromJsonAsync<CandidateEvaluationDto>())!;
        var current = await GetAsync<CandidateDetailDto>(admin, url);
        var journey = await GetAsync<RecruitmentJourneyDto>(admin, "/api/guidance/recruitment/journey");
        var noteUrl = $"{url}/notes/{note.Id}/revisions";
        var evalUrl = $"{url}/evaluations/{evaluation.Id}/revisions";
        object NoteBody(int candidateVersion, int recordVersion, string text = "Doğrulanmış teknik gözlem.", string reason = "Görüşme kaynağıyla doğrulandı.") => new
        { expectedCandidateVersion = candidateVersion, expectedRecordVersion = recordVersion,
            noteType = "Teknik Mülakat", text, reason };
        (await manager.PostAsJsonAsync(noteUrl, NoteBody(current.Version, 1)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.GetAsync(noteUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync(noteUrl, NoteBody(candidate.Version, 1)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync(noteUrl, NoteBody(current.Version, 99)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync(noteUrl, NoteBody(current.Version, 1, reason: " ")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var firstRevision = await admin.PostAsJsonAsync(noteUrl, NoteBody(current.Version, 1));
        firstRevision.StatusCode.Should().Be(HttpStatusCode.OK);
        current = (await firstRevision.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        var currentNote = current.Notes.Single(item => item.Id == note.Id);
        currentNote.Version.Should().Be(2);
        currentNote.Text.Should().Be("Doğrulanmış teknik gözlem.");
        currentNote.NoteType.Should().Be("Teknik Mülakat");
        currentNote.AuthorName.Should().Be(note.AuthorName);
        currentNote.CreatedAtUtc.Should().Be(note.CreatedAtUtc);
        var secondRevision = await admin.PostAsJsonAsync(noteUrl,
            NoteBody(current.Version, currentNote.Version, "İkinci doğrulanmış gözlem."));
        secondRevision.StatusCode.Should().Be(HttpStatusCode.OK);
        current = (await secondRevision.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        (await admin.PostAsJsonAsync(noteUrl, NoteBody(current.Version, 3, "İkinci doğrulanmış gözlem.")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "değişikliksiz istek sürüm üretmez");
        var noteHistory = await GetAsync<CandidateEvidenceHistoryDto>(admin, noteUrl);
        noteHistory.Original.Text.Should().Be(note.Text);
        noteHistory.Original.NoteType.Should().Be(note.NoteType);
        noteHistory.Revisions.Select(item => item.Version).Should().Equal(3, 2);
        noteHistory.Revisions[0].Before.Text.Should().Be("Doğrulanmış teknik gözlem.");
        noteHistory.Revisions[1].Before.Should().Be(noteHistory.Original);
        noteHistory.Revisions.Should().OnlyContain(item => item.ChangedByName == "İK Yöneticisi"
            && item.ToCandidateVersion == item.FromCandidateVersion + 1 && item.ChangedByUserId != "");
        (await admin.PostAsJsonAsync(evalUrl, new
        { expectedCandidateVersion = current.Version, expectedRecordVersion = 1, criterion = "SQL",
            score = 6, maxScore = 5, reason = "Yanlış puan" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var evalRevision = await admin.PostAsJsonAsync(evalUrl, new
        { expectedCandidateVersion = current.Version, expectedRecordVersion = 1, criterion = "SQL uygulaması",
            score = 4, maxScore = 10, reason = "Belgedeki puan ölçeği düzeltildi." });
        evalRevision.StatusCode.Should().Be(HttpStatusCode.OK);
        current = (await evalRevision.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        var effectiveEvaluation = current.Evaluations.Single(item => item.Id == evaluation.Id);
        effectiveEvaluation.Version.Should().Be(2);
        effectiveEvaluation.Criterion.Should().Be("SQL uygulaması");
        effectiveEvaluation.MaxScore.Should().Be(10);
        current.Status.Should().Be("Yeni");
        current.Score.Should().Be(66);
        current.AppliedAtUtc.Should().Be(candidate.AppliedAtUtc);
        (await GetAsync<RecruitmentJourneyDto>(admin, "/api/guidance/recruitment/journey"))
            .VerifiedFirstReviews.Should().Be(journey.VerifiedFirstReviews);
        var evaluationHistory = await GetAsync<CandidateEvidenceHistoryDto>(admin, evalUrl);
        evaluationHistory.Original.Score.Should().Be(4);
        evaluationHistory.Original.MaxScore.Should().Be(5);
        evaluationHistory.Revisions.Should().ContainSingle().Which.After.MaxScore.Should().Be(10);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        { email = "ik@hrmaster.local", password = DemoPassword });
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        using var scope = factory.Services.GetRequiredService<ITenantScopeFactory>().Create(auth.User.TenantId);
        var db = scope.Services.GetRequiredService<IApplicationDbContext>();
        var storedNote = await db.InterviewNotes.AsNoTracking().SingleAsync(item => item.Id == note.Id);
        storedNote.Text.Should().Be(note.Text, "ilk kaynak satırı değişmeden kalmalı");
        storedNote.NoteType.Should().Be(note.NoteType);
        var storedEvaluation = await db.CandidateEvaluations.AsNoTracking().SingleAsync(item => item.Id == evaluation.Id);
        storedEvaluation.Criterion.Should().Be(evaluation.Criterion);
        storedEvaluation.MaxScore.Should().Be(evaluation.MaxScore);
    }

    [Fact]
    public async Task EvidenceRevisions_RejectWrongCandidate_ConcurrentWrites_AndHiredHistoryMutation()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        async Task<CandidateDetailDto> CreateCandidate()
        {
            var response = await admin.PostAsJsonAsync("/api/candidates", new
            { name = "Kilit Testi " + Guid.NewGuid().ToString("N"), appliedRole = "Uzman", score = 70 });
            return (await response.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
        }
        var candidate = await CreateCandidate();
        var other = await CreateCandidate();
        var url = $"/api/candidates/{candidate.Id}";
        var noteResponse = await admin.PostAsJsonAsync($"{url}/notes", new
        { noteType = "İK Görüşmesi", text = "İlk gözlem." });
        var note = (await noteResponse.Content.ReadFromJsonAsync<InterviewNoteDto>())!;
        var evalResponse = await admin.PostAsJsonAsync($"{url}/evaluations", new
        { criterion = "İletişim", score = 3, maxScore = 5 });
        var evaluation = (await evalResponse.Content.ReadFromJsonAsync<CandidateEvaluationDto>())!;
        var current = await GetAsync<CandidateDetailDto>(admin, url);
        var body = new { expectedCandidateVersion = current.Version, expectedRecordVersion = 1,
            noteType = "Teknik Mülakat", text = "Kontrol edilmiş gözlem.", reason = "Kaynağı kontrol edildi." };
        (await admin.PostAsJsonAsync($"/api/candidates/{other.Id}/notes/{note.Id}/revisions",
            body with { expectedCandidateVersion = other.Version })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync($"/api/candidates/{other.Id}/notes/{note.Id}/revisions"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var noteUrl = $"{url}/notes/{note.Id}/revisions";
        var responses = await Task.WhenAll(admin.PostAsJsonAsync(noteUrl, body), admin.PostAsJsonAsync(noteUrl, body));
        responses.Select(item => item.StatusCode).Should().BeEquivalentTo(
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        (await GetAsync<CandidateEvidenceHistoryDto>(admin, noteUrl)).Revisions.Should().ContainSingle();
        current = await GetAsync<CandidateDetailDto>(admin, url);
        var department = (await GetAsync<List<DepartmentDto>>(admin, "/api/departments"))[0];
        (await admin.PostAsJsonAsync($"{url}/hire", new
        { departmentId = department.Id, email = $"evidence-lock-{Guid.NewGuid():N}@hrmaster.local",
            contractType = "indefinite" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var hired = await GetAsync<CandidateDetailDto>(admin, url);
        (await admin.PostAsJsonAsync(noteUrl, body with { expectedCandidateVersion = hired.Version, expectedRecordVersion = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"{url}/evaluations/{evaluation.Id}/revisions", new
        { expectedCandidateVersion = hired.Version, expectedRecordVersion = 1, criterion = "İletişim",
            score = 4, maxScore = 5, reason = "Sonradan düzeltme" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"{url}/notes", new
        { noteType = "İK Görüşmesi", text = "Düzeltme yerine yeni kayıt." })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync($"{url}/evaluations", new
        { criterion = "İletişim", score = 4, maxScore = 5 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetAsync<CandidateDetailDto>(admin, url)).Version.Should().Be(hired.Version);
        (await GetAsync<CandidateEvidenceHistoryDto>(admin, noteUrl)).Revisions.Should().ContainSingle();
    }

    // --- yardımcılar ---

    private sealed class FailCountEmailSender(
        string targetEmail,
        int failureCount,
        IEmailSender inner) : IEmailSender
    {
        private int _attempts;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (string.Equals(message.To, targetEmail, StringComparison.OrdinalIgnoreCase)
                && Interlocked.Increment(ref _attempts) <= failureCount)
            {
                throw new IOException("Test transient e-posta hatası.");
            }

            return inner.SendAsync(message, cancellationToken);
        }
    }

    private sealed record FunnelDto(int Total, int New, int Interview, int Offer, int Rejected, int Hired);

    // İşe alınan personel şifresiz oluşur; davet e-postasındaki token ile şifre belirler.
    private async Task AcceptInviteAsync(string email)
    {
        var outbox = Path.Combine(factory.StorageRoot, "outbox");
        string? token = null;
        foreach (var file in Directory.EnumerateFiles(outbox, "*.json")
                     .OrderByDescending(File.GetCreationTimeUtc))
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(file));
            var root = doc.RootElement;
            if (!string.Equals(root.GetProperty("to").GetString(), email, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var match = Regex.Match(root.GetProperty("body").GetString() ?? "", @"DAVET-KODU:\s*(?<token>.+)");
            if (match.Success)
            {
                token = match.Groups["token"].Value.Trim();
                break;
            }
        }
        token.Should().NotBeNull($"'{email}' için davet e-postası outbox'ta bulunmalı");

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/accept-invite", new { email, token, newPassword = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, $"davet kabulü başarısız: {email}");
    }

    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"demo giriş başarısız: {email}");
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<CandidateDetailDto> PatchStatusAsync(HttpClient client, int id, string status)
    {
        var response = await client.PatchAsJsonAsync($"/api/candidates/{id}/status", new { status });
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"durum geçişi: {status}");
        return (await response.Content.ReadFromJsonAsync<CandidateDetailDto>())!;
    }
}
