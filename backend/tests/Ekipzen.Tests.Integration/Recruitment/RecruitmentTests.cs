using FluentAssertions;
using Ekipzen.API.Services;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Common.Models;
using Ekipzen.Infrastructure.Email;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Leaves;
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
        var hireResponse = await admin.PostAsJsonAsync($"/api/candidates/{candidate.Id}/hire", new
        {
            departmentId = yazilim.Id,
            email = hireEmail,
        });
        hireResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var hired = (await hireResponse.Content.ReadFromJsonAsync<HireResultDto>())!;
        hired.EmployeeName.Should().Be("Burak Yılmaz");

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
                new { departmentId = yazilim.Id, email = $"tekrar-{Guid.NewGuid():N}@hrmaster.local" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var positions = await GetAsync<List<PositionDto>>(admin, "/api/positions");
        var closedPosition = positions.Single(p => p.Id == position.Id);
        closedPosition.OpenCount.Should().Be(0);
        closedPosition.IsOpen.Should().BeFalse("tek kontenjan dolunca ilan kapanır");
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
                new { departmentId = departments[0].Id, email = $"red-{Guid.NewGuid():N}@hrmaster.local" }))
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
