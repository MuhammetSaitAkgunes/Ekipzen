using FluentAssertions;
using Ekipzen.Application.Features.Actions;
using Ekipzen.Application.Features.Auth;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Actions;

/// <summary>
/// Faz 10 uçtan uca: aksiyon CRUD + ileri yönlü durum akışı (open→week→done,
/// geri dönüş/aynı durum 409), filtreler, rozet sayacı, denetim izi (trigger'ların
/// doldurduğu append-only AuditLogs) ve rol kapsamlı birleşik arama.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ActionsTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task ActionLifecycle_CreateForwardStatusFlowAndDelete()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        // --- oluştur (open başlar) ---
        var createResponse = await admin.PostAsJsonAsync("/api/actions", new
        {
            title = "Faz10 test aksiyonu: vardiya planını gözden geçir",
            source = "Puantaj",
            sourceRoute = "attendance",
            owner = "İK Operasyon",
            due = "Bugün",
            priority = "high",
            recommendedAction = "Vardiya çizelgesini haftalık kontrol et.",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var action = (await createResponse.Content.ReadFromJsonAsync<GlobalActionDto>())!;
        action.Status.Should().Be("open");
        action.Priority.Should().Be("high");

        // --- ileri yönlü akış: open → week → done ---
        var week = await PatchStatusAsync(admin, action.Id, "week");
        week.Status.Should().Be("week");

        var done = await PatchStatusAsync(admin, action.Id, "done");
        done.Status.Should().Be("done");
        done.Due.Should().Be("Tamamlandı", "tamamlanan aksiyonun son tarihi etiketlenir");

        // Aynı durum ve geri dönüş → 409.
        (await admin.PatchAsJsonAsync($"/api/actions/{action.Id}/status", new { status = "done" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PatchAsJsonAsync($"/api/actions/{action.Id}/status", new { status = "open" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // --- güncelle + sil ---
        var updateResponse = await admin.PutAsJsonAsync($"/api/actions/{action.Id}", new
        {
            title = "Faz10 test aksiyonu (rev)",
            source = "Puantaj",
            owner = "Ece Arslan",
            sourceRoute = "attendance",
            due = "Tamamlandı",
            priority = "medium",
            recommendedAction = (string?)null,
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updateResponse.Content.ReadFromJsonAsync<GlobalActionDto>())!
            .Owner.Should().Be("Ece Arslan");

        (await admin.DeleteAsync($"/api/actions/{action.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterDelete = await GetAsync<List<GlobalActionDto>>(admin, "/api/actions");
        afterDelete.Should().NotContain(a => a.Id == action.Id);
    }

    [Fact]
    public async Task Actions_FiltersAndBadge_AreConsistent()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var all = await GetAsync<List<GlobalActionDto>>(admin, "/api/actions");
        all.Should().NotBeEmpty("seed aksiyonları içerir");

        var high = await GetAsync<List<GlobalActionDto>>(admin, "/api/actions?priority=high");
        high.Should().OnlyContain(a => a.Priority == "high");

        var open = await GetAsync<List<GlobalActionDto>>(admin, "/api/actions?status=open");
        open.Should().OnlyContain(a => a.Status == "open");

        var source = all[0].Source;
        var bySource = await GetAsync<List<GlobalActionDto>>(
            admin, $"/api/actions?source={Uri.EscapeDataString(source)}");
        bySource.Should().OnlyContain(a => a.Source == source);

        // Rozet = tamamlanmamış aksiyon sayısı (listeden türetilir, sıradan bağımsız).
        var badge = await GetAsync<ActionBadgeDto>(admin, "/api/actions/badge");
        badge.OpenCount.Should().Be(all.Count(a => a.Status != "done"));
    }

    [Theory]
    [InlineData("/api/actions?priority=urgent", "Priority")]
    [InlineData("/api/actions?status=closed", "Status")]
    public async Task Actions_InvalidEnumFilters_ReturnValidationProblem(
        string url, string property)
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        await AssertValidationProblemAsync(await admin.GetAsync(url), property);
    }

    [Fact]
    public async Task Actions_InvalidEnumCommands_ReturnValidationProblem()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        await AssertValidationProblemAsync(await admin.PostAsJsonAsync("/api/actions", new
        {
            title = "Geçersiz öncelik testi",
            source = "Kalite",
            owner = "İK Operasyon",
            priority = "urgent",
        }), "Priority");

        await AssertValidationProblemAsync(await admin.PutAsJsonAsync("/api/actions/1", new
        {
            title = "Geçersiz öncelik testi",
            source = "Kalite",
            owner = "İK Operasyon",
            priority = "urgent",
        }), "Priority");

        await AssertValidationProblemAsync(await admin.PatchAsJsonAsync(
            "/api/actions/1/status", new { status = "closed" }), "Status");
    }

    [Fact]
    public async Task Actions_RoleMatrix_EmployeeReadsButCannotMutate()
    {
        // routes.js: /actions tüm rollere açık → employee listeyi ve rozeti görür.
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (await employee.GetAsync("/api/actions")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await employee.GetAsync("/api/actions/badge")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await employee.PostAsJsonAsync("/api/actions", new
        {
            title = "Yetkisiz", source = "X", owner = "Y",
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var open = await GetAsync<List<GlobalActionDto>>(admin, "/api/actions?status=open");
        (await employee.PatchAsJsonAsync($"/api/actions/{open[0].Id}/status", new { status = "week" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Denetim izi yönetsel: employee 403, manager 200.
        (await employee.GetAsync("/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        (await manager.GetAsync("/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuditLogs_AreAppendOnlyTrail_FilterableByModule()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        // Trigger'lı bir tabloda mutasyon üret → denetim izine düşmeli.
        var employees = await GetAsync<System.Text.Json.JsonElement>(admin, "/api/employees?search=Ahmet");
        var ahmetId = employees.GetProperty("items")[0].GetProperty("id").GetInt32();
        (await admin.PostAsJsonAsync("/api/compliance/documents", new
        {
            employeeId = ahmetId,
            documentName = $"Faz10 denetim izi belgesi {Guid.NewGuid():N}",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var logs = await GetAsync<List<AuditLogDto>>(admin, "/api/audit-logs?module=Uyum&take=10");
        logs.Should().NotBeEmpty("uyum belgesi mutasyonu trigger ile iz bırakır");
        logs.Should().OnlyContain(l => l.Module == "Uyum");
        logs[0].Action.Should().Be("insert");
        logs[0].Actor.Should().Be("İK Yöneticisi",
            "aktör, interceptor'ın CreatedBy kolonuna yazdığı JWT name claim'inden (DisplayName) okunur");

        // En yeni kayıt önce gelir.
        logs.Should().BeInDescendingOrder(l => l.CreatedAtUtc);
    }

    [Fact]
    public async Task GlobalSearch_ScopesResultsByRole()
    {
        // hr-admin: personel + aksiyon + aday tipleri döner.
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var adminResults = await GetAsync<List<SearchResultDto>>(admin, "/api/search?q=Ahmet");
        adminResults.Should().Contain(r => r.Type == "Personel" && r.Label == "Ahmet Yılmaz");

        var actionResults = await GetAsync<List<SearchResultDto>>(admin, "/api/search?q=KVKK");
        actionResults.Should().Contain(r => r.Type == "Aksiyon");

        // Manager: İK'daki Ayşe ekip kapsamı dışında → personel sonucu dönmez.
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var managerResults = await GetAsync<List<SearchResultDto>>(manager, "/api/search?q=Ayşe");
        managerResults.Should().NotContain(r => r.Type == "Personel");

        // Employee: başka personeli göremez, aday sonuçları hiç dönmez.
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var employeeResults = await GetAsync<List<SearchResultDto>>(employee, "/api/search?q=Selin");
        employeeResults.Should().NotContain(r => r.Type == "Personel" || r.Type == "Aday");

        // Kısa sorgu boş döner (min 2 karakter).
        (await GetAsync<List<SearchResultDto>>(admin, "/api/search?q=a")).Should().BeEmpty();
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

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url} → {body}");
        return System.Text.Json.JsonSerializer.Deserialize<T>(
            body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    }

    private static async Task<GlobalActionDto> PatchStatusAsync(HttpClient client, int id, string status)
    {
        var response = await client.PatchAsJsonAsync($"/api/actions/{id}/status", new { status });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"durum geçişi {status} → {body}");
        return (await response.Content.ReadFromJsonAsync<GlobalActionDto>())!;
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response, string property)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("errors").EnumerateObject()
            .Should().Contain(error => error.Name.Equals(property, StringComparison.OrdinalIgnoreCase));
    }
}
