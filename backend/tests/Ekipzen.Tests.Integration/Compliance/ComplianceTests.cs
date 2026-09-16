using FluentAssertions;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Compliance;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Common.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Compliance;

/// <summary>
/// Faz 9 uçtan uca: belge oluşturma (mükerrer 409), durum iş akışı (aynı durum 409,
/// tamamlanınca seviye düşer), owner atama, filtreli liste + rol kapsamı
/// (manager yalnız ekibi, employee 403) ve hazırlık skoru view paritesi.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ComplianceTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task DocumentLifecycle_CreateStatusFlowAndOwnerAssignment()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ahmetId = await EmployeeIdAsync(admin, "Ahmet Yılmaz");

        // --- oluştur ---
        var createResponse = await admin.PostAsJsonAsync("/api/compliance/documents", new
        {
            employeeId = ahmetId,
            documentName = "Faz9 Sözleşme Zeyilnamesi",
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(4),
            status = "Eksik",
            level = "high",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = (await createResponse.Content.ReadFromJsonAsync<ComplianceDocumentDto>())!;

        document.Employee.Should().Be("Ahmet Yılmaz");
        document.Status.Should().Be("Eksik");
        document.Level.Should().Be("high");
        document.DueLabel.Should().Be("4 gün");

        // Aynı personel için aynı adlı açık belge → 409.
        (await admin.PostAsJsonAsync("/api/compliance/documents", new
        {
            employeeId = ahmetId,
            documentName = "Faz9 Sözleşme Zeyilnamesi",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // --- durum iş akışı: Eksik → İncelemede → Tamamlandı ---
        var inReview = await PatchAsync(admin,
            $"/api/compliance/documents/{document.Id}/status", new { status = "İncelemede" });
        inReview.Status.Should().Be("İncelemede");

        // Aynı duruma tekrar geçiş → 409.
        (await admin.PatchAsJsonAsync($"/api/compliance/documents/{document.Id}/status",
            new { status = "İncelemede" })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // --- owner atama ---
        var owned = await PatchAsync(admin,
            $"/api/compliance/documents/{document.Id}/owner", new { ownerName = "İK Operasyon" });
        owned.Owner.Should().Be("İK Operasyon");

        // --- tamamla: seviye low'a düşer, son tarih etiketi "Tamamlandı" olur ---
        var completed = await PatchAsync(admin,
            $"/api/compliance/documents/{document.Id}/status", new { status = "Tamamlandı" });
        completed.Level.Should().Be("low");
        completed.DueLabel.Should().Be("Tamamlandı");

        // --- güncelleme: ad/son tarih/seviye ---
        var updateResponse = await admin.PutAsJsonAsync($"/api/compliance/documents/{document.Id}", new
        {
            documentName = "Faz9 Sözleşme Zeyilnamesi (rev)",
            dueDate = (DateOnly?)null,
            level = "medium",
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updateResponse.Content.ReadFromJsonAsync<ComplianceDocumentDto>())!
            .Document.Should().Be("Faz9 Sözleşme Zeyilnamesi (rev)");
    }

    [Fact]
    public async Task Documents_FilterByStatusLevelAndSearch()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var missing = await GetAsync<List<ComplianceDocumentDto>>(
            admin, "/api/compliance/documents?status=Eksik");
        missing.Should().OnlyContain(d => d.Status == "Eksik");

        var high = await GetAsync<List<ComplianceDocumentDto>>(
            admin, "/api/compliance/documents?level=high");
        high.Should().OnlyContain(d => d.Level == "high");

        var searched = await GetAsync<List<ComplianceDocumentDto>>(
            admin, "/api/compliance/documents?search=KVKK");
        searched.Should().NotBeEmpty();
        searched.Should().OnlyContain(d => d.Document.Contains("KVKK"));
    }

    [Theory]
    [InlineData("/api/compliance/documents?status=completed", "Status")]
    [InlineData("/api/compliance/documents?level=urgent", "Level")]
    public async Task Documents_InvalidEnumFilters_ReturnValidationProblem(
        string url, string property)
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        await AssertValidationProblemAsync(await admin.GetAsync(url), property);
    }

    [Fact]
    public async Task Documents_InvalidEnumCommands_ReturnValidationProblem()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var ahmetId = await EmployeeIdAsync(admin, "Ahmet Yılmaz");

        await AssertValidationProblemAsync(await admin.PostAsJsonAsync(
            "/api/compliance/documents", new
            {
                employeeId = ahmetId,
                documentName = "Geçersiz enum testi",
                status = "completed",
                level = "urgent",
            }), "Status", "Level");

        await AssertValidationProblemAsync(await admin.PutAsJsonAsync(
            "/api/compliance/documents/1", new
            {
                documentName = "Geçersiz enum testi",
                level = "urgent",
            }), "Level");

        await AssertValidationProblemAsync(await admin.PatchAsJsonAsync(
            "/api/compliance/documents/1/status", new { status = "completed" }), "Status");
    }

    [Fact]
    public async Task Documents_ManagerSeesOnlyOwnTeam_EmployeeForbidden()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var docs = await GetAsync<List<ComplianceDocumentDto>>(manager, "/api/compliance/documents");

        docs.Should().NotBeEmpty();
        docs.Should().OnlyContain(
            d => d.Employee == "Ece Arslan" || d.Employee == "Ahmet Yılmaz" || d.Employee == "Selin Koç",
            "manager yalnız kendi ekibinin (ve kendisinin) evrakını görür");

        // Mutasyonlar manager'a kapalı.
        (await manager.PostAsJsonAsync("/api/compliance/documents", new
        {
            employeeId = 1,
            documentName = "Yetkisiz deneme",
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Employee hiçbir uyum ucunu göremez.
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (await employee.GetAsync("/api/compliance/documents")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await employee.GetAsync("/api/compliance/readiness")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Readiness_MatchesViewFormulaAndBuildsChecklist()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var documents = await GetAsync<List<ComplianceDocumentDto>>(admin, "/api/compliance/documents");
        var readiness = await GetAsync<ComplianceReadinessDto>(admin, "/api/compliance/readiness");

        // View paritesi: sayımlar liste ucuyla, skorlar formülle birebir tutmalı.
        readiness.TotalCount.Should().Be(documents.Count);
        readiness.CompletedCount.Should().Be(documents.Count(d => d.Status == "Tamamlandı"));
        readiness.MissingCount.Should().Be(documents.Count(d => d.Status == "Eksik"));
        readiness.DueSoonCount.Should().Be(documents.Count(d => d.Status == "Süresi Yaklaşıyor"));
        readiness.InReviewCount.Should().Be(documents.Count(d => d.Status == "İncelemede"));

        readiness.DocumentComplianceScore.Should().Be((int)Math.Round(
            100.0 * readiness.CompletedCount / readiness.TotalCount, MidpointRounding.AwayFromZero));
        readiness.ReadinessScore.Should().Be(Math.Clamp(
            100 - readiness.MissingCount * 6 - readiness.DueSoonCount * 3 - readiness.InReviewCount * 2,
            0, 100));

        readiness.AuditChecklist.Should().HaveCount(4);
        readiness.AuditChecklist.Select(c => c.Label).Should().Contain("Sorumlu atama netliği");
        readiness.AuditChecklist.Should().OnlyContain(c => c.Value >= 0 && c.Value <= 100);
        readiness.RecommendedActions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Readiness_IsScopedForManager()
    {
        // Hazırlık KPI'ları da belge tablosuyla aynı kapsamda olmalı (manager yalnız ekibi).
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");

        var adminReadiness = await GetAsync<ComplianceReadinessDto>(admin, "/api/compliance/readiness");
        var managerDocs = await GetAsync<List<ComplianceDocumentDto>>(manager, "/api/compliance/documents");
        var managerReadiness = await GetAsync<ComplianceReadinessDto>(manager, "/api/compliance/readiness");

        managerReadiness.TotalCount.Should().Be(managerDocs.Count,
            "manager hazırlık sayımı yalnız kendi kapsamındaki belgeleri yansıtmalı");
        managerReadiness.TotalCount.Should().BeLessThan(adminReadiness.TotalCount,
            "manager kapsamı şirket genelinden dar olmalı");
        managerReadiness.CompletedCount.Should().Be(managerDocs.Count(d => d.Status == "Tamamlandı"));
    }

    // --- yardımcılar ---

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

    private static async Task<int> EmployeeIdAsync(HttpClient client, string fullName)
    {
        var directory = await GetAsync<PagedResult<EmployeeListItemDto>>(
            client, $"/api/employees?search={Uri.EscapeDataString(fullName)}");
        return directory.Items.First().Id;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url} → {body}");
        return System.Text.Json.JsonSerializer.Deserialize<T>(
            body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    }

    private static async Task<ComplianceDocumentDto> PatchAsync(
        HttpClient client, string url, object payload)
    {
        var response = await client.PatchAsJsonAsync(url, payload);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"PATCH {url} → {body}");
        return (await response.Content.ReadFromJsonAsync<ComplianceDocumentDto>())!;
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response, params string[] properties)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var errors = problem.GetProperty("errors").EnumerateObject().Select(error => error.Name).ToList();
        foreach (var property in properties)
        {
            errors.Should().Contain(
                error => error.Equals(property, StringComparison.OrdinalIgnoreCase));
        }
    }
}
