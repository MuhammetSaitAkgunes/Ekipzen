using FluentAssertions;
using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Settings;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ekipzen.Tests.Integration.Settings;

/// <summary>
/// Faz 11 uçtan uca: ayarların birleşik görünümü (seed paritesi), profil/bildirim/
/// güvenlik güncellemeleri, logo yükleme-indirme (herkese görünür), rol koruması
/// ve bildirim tetikleyicilerinin toggle'lara uyması (dosya outbox doğrulaması).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SettingsTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    [Fact]
    public async Task GetSettings_ReturnsSeededSingletons()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var settings = await GetAsync<SettingsDto>(admin, "/api/settings");

        // settings.js form değerleri + plan banner'ı (seed paritesi).
        settings.Company.Name.Should().Be("HR Master Teknoloji A.Ş.");
        settings.Company.Website.Should().Be("www.hrmaster.com");
        settings.Company.SystemEmail.Should().Be("info@hrmaster.com");

        settings.Notifications.NewPersonnelEmail.Should().BeTrue();
        settings.Notifications.LeaveRequestEmail.Should().BeTrue();
        settings.Notifications.WeeklyReportEmail.Should().BeFalse();

        settings.Subscription.Plan.Should().Be("PRO");
        settings.Subscription.PlanName.Should().Be("HR Master Kurumsal");
        settings.Subscription.Price.Should().Be(12000m);
        settings.Subscription.PaymentMethodMasked.Should().Be("•••• •••• •••• 4582");
    }

    [Fact]
    public async Task UpdateCompanyNotificationsAndSecurity_RoundTrips()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        var companyResponse = await admin.PutAsJsonAsync("/api/settings/company", new
        {
            name = "HR Master Teknoloji A.Ş.",
            website = "www.hrmaster.com",
            systemEmail = "info@hrmaster.com",
            phone = "+90 212 555 11 11",
            headquartersAddress = "Maslak Mah. Büyükdere Cad. No:123 Sarıyer/İstanbul",
        });
        companyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await companyResponse.Content.ReadFromJsonAsync<CompanyProfileDto>())!
            .Phone.Should().Be("+90 212 555 11 11");

        // Geçersiz e-posta → 400 (FluentValidation).
        (await admin.PutAsJsonAsync("/api/settings/company", new
        {
            name = "HR Master",
            systemEmail = "gecersiz-eposta",
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var securityResponse = await admin.PutAsJsonAsync("/api/settings/security", new
        {
            twoFactorSmsEnabled = true,
        });
        securityResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync("/api/settings/security", new { twoFactorSmsEnabled = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PutAsJsonAsync("/api/settings/notifications", new
        {
            newPersonnelEmail = true, leaveRequestEmail = true, weeklyReportEmail = true,
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var settings = await GetAsync<SettingsDto>(admin, "/api/settings");
        settings.Company.Phone.Should().Be("+90 212 555 11 11");
        settings.Security.TwoFactorSmsEnabled.Should().BeFalse();

        // Ayarlar yalnız hr-admin: manager ve employee 403 alır.
        var manager = await AuthedClientAsync("ece.arslan@hrmaster.local");
        (await manager.GetAsync("/api/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        (await employee.PutAsJsonAsync("/api/settings/notifications", new
        {
            newPersonnelEmail = false, leaveRequestEmail = false, weeklyReportEmail = false,
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompanyLogo_UploadByAdmin_VisibleToAllRoles()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");

        // Logo yüklenmeden indirme 404 döner.
        (await admin.GetAsync("/api/settings/company/logo"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Geçersiz uzantı reddedilir (PNG/JPG, maks 2 MB).
        (await UploadLogoAsync(admin, "logo.gif", [1, 2, 3]))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var upload = await UploadLogoAsync(admin, "logo.png", [137, 80, 78, 71, 13, 10, 26, 10]);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        // Header logosu tüm roller tarafından görüntülenebilir.
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var logoResponse = await employee.GetAsync("/api/settings/company/logo");
        logoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        logoResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task EmailTriggers_FollowNotificationToggles()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local");
        var employee = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var mazeretId = await LeaveTypeIdAsync(employee, "Mazeret");

        async Task SetLeaveToggleAsync(bool enabled)
            => (await admin.PutAsJsonAsync("/api/settings/notifications", new
            {
                newPersonnelEmail = true,
                leaveRequestEmail = enabled,
                weeklyReportEmail = false,
            })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Toggle AÇIK: izin talebi outbox'a e-posta düşürür.
        await SetLeaveToggleAsync(true);
        var before = OutboxFileCount();
        (await CreateLeaveAsync(employee, mazeretId, "2026-09-21", "2026-09-21"))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        OutboxFileCount().Should().Be(before + 1, "toggle açıkken her izin talebi e-posta üretir");

        // Toggle KAPALI: yeni talep e-posta üretmez.
        await SetLeaveToggleAsync(false);
        var afterDisable = OutboxFileCount();
        (await CreateLeaveAsync(employee, mazeretId, "2026-09-23", "2026-09-23"))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        OutboxFileCount().Should().Be(afterDisable, "toggle kapalıyken e-posta üretilmez");

        // Diğer testler için varsayılanı geri yükle.
        await SetLeaveToggleAsync(true);
    }

    // --- yardımcılar ---

    private static int OutboxFileCount()
    {
        var storageRoot = Environment.GetEnvironmentVariable("Storage__Root")!;
        var outbox = Path.Combine(storageRoot, "outbox");
        return Directory.Exists(outbox) ? Directory.GetFiles(outbox, "*.json").Length : 0;
    }

    private static async Task<HttpResponseMessage> UploadLogoAsync(
        HttpClient client, string fileName, byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        return await client.PostAsync("/api/settings/company/logo", content);
    }

    private static async Task<int> LeaveTypeIdAsync(HttpClient client, string code)
    {
        var types = await GetAsync<List<System.Text.Json.JsonElement>>(client, "/api/leaves/types");
        return types.Single(t => t.GetProperty("code").GetString() == code).GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> CreateLeaveAsync(
        HttpClient client, int leaveTypeId, string start, string end)
        => client.PostAsJsonAsync("/api/leaves", new
        {
            leaveTypeId,
            startDate = start,
            endDate = end,
            description = "Faz 11 bildirim testi",
        });

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
