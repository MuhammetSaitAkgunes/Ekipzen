using FluentAssertions;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Dashboard;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Dashboard;

/// <summary>
/// Faz 8 uçtan uca: risk metrikleri (dashboard.js formül paritesi), rol kapsamı
/// (manager → yalnız ekibi, employee → 403), detay uçları (attrition/burnout/
/// manager-load/employee-voice/compliance) ve overview KPI'ları.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task Metrics_RiskScores_MatchDashboardJsFormula()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var metrics = await GetAsync<DashboardMetricsDto>(admin, "/api/dashboard/metrics");

        // dashboard.js riskScoreFor paritesi — seed girdileriyle beklenen skorlar:
        // Ahmet (18,22,74,82,52,68,92) → round(46.86) = 47, attrition high, burnout high
        var ahmet = metrics.Employees.Single(e => e.Name == "Ahmet Yılmaz");
        ahmet.RiskScore.Should().Be(47);
        ahmet.AttritionRisk.Should().Be("high", "pulse<55 ve kritiklik>85");
        ahmet.BurnoutRisk.Should().Be("high", "mesai>65 ve kullanılmayan izin>65");
        ahmet.Trend.Should().Be("Son 30 günde fazla mesai +%18");

        // Selin (8,12,66,71,61,74,76) → round(37.89) = 38, attrition medium, burnout high
        var selin = metrics.Employees.Single(e => e.Name == "Selin Koç");
        selin.RiskScore.Should().Be(38);
        selin.AttritionRisk.Should().Be("medium");
        selin.BurnoutRisk.Should().Be("high");

        // Ece (6,9,34,42,78,84,58) → round(21.80) = 22, low/low
        var ece = metrics.Employees.Single(e => e.Name == "Ece Arslan");
        ece.RiskScore.Should().Be(22);
        ece.AttritionRisk.Should().Be("low");
        ece.BurnoutRisk.Should().Be("low");

        // Genel skor = satır skorlarının ortalaması (JS ile aynı agregasyon).
        var expectedAverage = (int)Math.Round(metrics.Employees.Average(e => e.RiskScore));
        metrics.RiskScore.Should().Be(expectedAverage);

        metrics.AttritionHigh.Should().Be(metrics.Employees.Count(e => e.AttritionRisk == "high"));
        metrics.BurnoutRisk.Should().Be(metrics.Employees.Count(e => e.BurnoutRisk == "high"));
        metrics.CriticalRoleRisk.Should().Be(metrics.Employees.Count(e => e.RoleCriticality > 85));

        // 12 aylık seed → 12 noktalı yükselen trend serisi; son nokta bugünkü skor.
        metrics.RiskTrend.Should().HaveCount(12);
        metrics.RiskTrend[^1].Should().BeGreaterThan(metrics.RiskTrend[0], "seed geçmişe doğru hafifler");

        metrics.DepartmentRisk.Should().NotBeEmpty();
        metrics.DepartmentRisk.Select(d => d.Dept).Should().Contain("Yazılım");
        metrics.TalentCapacity.Should().HaveCount(4);
        metrics.CriticalActions.Should().BeGreaterThan(0, "seed açık aksiyonlar içerir");
    }

    [Fact]
    public async Task Metrics_ManagerSeesOnlyOwnTeam()
    {
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var metrics = await GetAsync<DashboardMetricsDto>(manager, "/api/dashboard/metrics");

        // Ece'nin ekibi: Ahmet + Selin (+ kendisi). İK'daki Ayşe kapsam dışıdır.
        metrics.Employees.Select(e => e.Name).Should()
            .BeEquivalentTo("Ece Arslan", "Ahmet Yılmaz", "Selin Koç");
        metrics.DepartmentRisk.Select(d => d.Dept).Should().NotContain("İnsan Kaynakları");
    }

    [Fact]
    public async Task RiskEndpoints_AreForbiddenForEmployee_ButOverviewIsOpen()
    {
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");

        foreach (var url in new[]
                 {
                     "/api/dashboard/metrics", "/api/dashboard/attrition", "/api/dashboard/burnout",
                     "/api/dashboard/manager-load", "/api/dashboard/employee-voice",
                     "/api/dashboard/compliance",
                 })
        {
            (await employee.GetAsync(url)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, $"employee rolü {url} göremez");
        }

        var overview = await GetAsync<OverviewDto>(employee, "/api/dashboard/overview");
        overview.ActiveEmployees.Should().BeGreaterThanOrEqualTo(3);
        overview.DepartmentDistribution.Should().NotBeEmpty();
        overview.PulseScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AttritionAndBurnoutDetails_SortAndCountConsistently()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var attrition = await GetAsync<RiskDetailDto>(admin, "/api/dashboard/attrition");
        attrition.Employees.Should().BeInDescendingOrder(e => e.RiskScore);
        attrition.HighCount.Should().Be(attrition.Employees.Count(e => e.AttritionRisk == "high"));
        attrition.CriticalRoleCount.Should().Be(attrition.Employees.Count(e => e.RoleCriticality > 85));

        var burnout = await GetAsync<RiskDetailDto>(admin, "/api/dashboard/burnout");
        burnout.Employees.Should().Equal(
            burnout.Employees.OrderByDescending(e => e.Overtime + e.UnusedLeave).ToList());
        burnout.HighCount.Should().Be(burnout.Employees.Count(e => e.BurnoutRisk == "high"));
    }

    [Fact]
    public async Task ManagerLoad_ListsManagersWithTeamCounts()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var load = await GetAsync<ManagerLoadDto>(admin, "/api/dashboard/manager-load");

        var ece = load.Managers.Single(m => m.Name == "Ece Arslan");
        ece.Team.Should().Be(2, "seed'de Ahmet ve Selin, Ece'ye bağlıdır");
        ece.Load.Should().BeInRange(0, 100);
        load.ManagerLoadIndex.Should().BeInRange(0, 100);

        // Manager kendi satırını görür, diğer yöneticileri göremez.
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        var own = await GetAsync<ManagerLoadDto>(manager, "/api/dashboard/manager-load");
        own.Managers.Should().OnlyContain(m => m.Name == "Ece Arslan");
    }

    [Fact]
    public async Task EmployeeVoice_ReportsDecliningTeamsFromEngagementHistory()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var voice = await GetAsync<EmployeeVoiceDto>(admin, "/api/dashboard/employee-voice");

        voice.Departments.Should().HaveCountGreaterThanOrEqualTo(4);
        voice.Departments.Should().BeInAscendingOrder(d => d.Pulse, "en düşük nabız önce gelir");

        // Seed: Yazılım 65→58 ve Tasarım 72→69 geriledi → decliningTeams = 2.
        voice.DecliningTeams.Should().Be(2);

        var yazilim = voice.Departments.Single(d => d.Dept == "Yazılım");
        yazilim.Pulse.Should().Be(58);
        yazilim.Level.Should().Be("high", "nabız < 60");
        voice.Signals.Should().Contain(s => s.Contains("Yazılım"));
    }

    [Fact]
    public async Task ComplianceRisk_ComputesScoresFromDocuments()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var compliance = await GetAsync<ComplianceRiskDto>(admin, "/api/dashboard/compliance");

        // Skorlar dönen kayıtlardan yeniden türetilir (test sırasından bağımsız):
        // uyum = tamamlanan/toplam; hazırlık = 100 - eksik*6 - yaklaşan*3 - incelemede*2.
        var total = compliance.Records.Count;
        var completed = compliance.Records.Count(r => r.Status == "Tamamlandı");
        var missing = compliance.Records.Count(r => r.Status == "Eksik");
        var dueSoon = compliance.Records.Count(r => r.Status == "Süresi Yaklaşıyor");
        var inReview = compliance.Records.Count(r => r.Status == "İncelemede");

        total.Should().BeGreaterThanOrEqualTo(5, "seed 5 uyum evrağı içerir");
        compliance.DocumentComplianceScore.Should().Be(
            (int)Math.Round(100.0 * completed / total, MidpointRounding.AwayFromZero));
        compliance.MissingDocuments.Should().Be(missing);
        compliance.UpcomingDocuments.Should().Be(dueSoon);

        var expectedReadiness = Math.Clamp(100 - missing * 6 - dueSoon * 3 - inReview * 2, 0, 100);
        compliance.AuditReadinessScore.Should().Be(expectedReadiness);
        compliance.AuditReadinessRisk.Should().Be(
            expectedReadiness >= 80 ? "Düşük" : expectedReadiness >= 60 ? "Orta" : "Yüksek");

        var kvkk = compliance.Records.Single(r => r.Document == "KVKK açık rıza eki");
        kvkk.DueDate.Should().Be("Bugün");
        kvkk.Status.Should().Be("Eksik");
        kvkk.Level.Should().Be("high");

        compliance.Deadlines.Should().Contain(d => d.Title == "KVKK açık rıza eki" && d.Level == "high");
        compliance.Records.Single(r => r.Document == "Personel dosyası kontrolü")
            .DueDate.Should().Be("Tamamlandı");
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
}
