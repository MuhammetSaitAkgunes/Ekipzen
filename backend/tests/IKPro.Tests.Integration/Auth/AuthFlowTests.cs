using FluentAssertions;
using IKPro.Application.Features.Auth;
using IKPro.Tests.Integration.Tenancy;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IKPro.Tests.Integration.Auth;

/// <summary>
/// Faz 2 auth akışı uçtan uca: login → me → refresh (rotasyon) → logout →
/// change-password. Seed'lenen demo hesaplar yalnız başarı senaryolarında
/// kullanılır; hata senaryoları taze kullanıcılarla çalışır ki lockout
/// sayaçları demo hesapları kilitlemesin.
///
/// <c>POST /api/auth/register</c> KASITLI olarak yoktur (bkz.
/// <see cref="Register_Kaldirildi_404Doner"/> ve AuthController xmldoc) —
/// anonim self-servis kayıt bir kiracı sızıntısı güvenlik açığıydı. Testler
/// için taze kullanıcı üretim akışıyla AYNI meşru yoldan geçer: kiracı
/// provizyonu (<see cref="TenancyTestBase.ProvisionTenantAsync"/>) + davet
/// kabulü (<see cref="TenancyTestBase.AcceptInviteAsync"/>).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthFlowTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    private const string DemoPassword = "demo123";
    private readonly HttpClient _client = factory.CreateClient();

    // --- login ---

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "ik@hrmaster.local", password = DemoPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        auth.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);

        // demoUsers (mockData.js) paritesi.
        auth.User.Email.Should().Be("ik@hrmaster.local");
        auth.User.Name.Should().Be("İK Yöneticisi");
        auth.User.Role.Should().Be("hr-admin");
        auth.User.RoleLabel.Should().Be("İK Admin");
        // "İK Yöneticisi" → "İY". Baş harfler artık ÜRETİM kuralından (DeriveInitials)
        // geliyor: demo admin'i elle yazılmış bir seed satırı değil, gerçek provizyon
        // yolu oluşturuyor. Eskiden "İK" idi çünkü seed değeri elle veriliyordu —
        // yani demo veri, ürünün kendi kuralını atlıyordu.
        auth.User.Initials.Should().Be("İY");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "yok@hrmaster.local", password = "her-neyse-1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var (email, _, _) = await RegisterUniqueUserAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "yanlis-sifre-1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithMissingEmail_Returns400ValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "", password = DemoPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- me ---

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsSessionUser()
    {
        var auth = await LoginAsync("ahmet.yilmaz@hrmaster.local", DemoPassword);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await response.Content.ReadFromJsonAsync<UserDto>();
        me.Should().NotBeNull();
        me!.Role.Should().Be("employee");
        me.RoleLabel.Should().Be("Çalışan");
        me.Initials.Should().Be("AY");
        me.EmployeeId.Should().NotBeNull("çalışan demo kullanıcısı Employee kaydına bağlıdır");
    }

    // --- refresh (rotasyon) ---

    [Fact]
    public async Task Refresh_ReturnsNewPair_AndInvalidatesOldToken()
    {
        var (_, _, auth) = await RegisterUniqueUserAsync();

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        rotated!.RefreshToken.Should().NotBe(auth.RefreshToken, "rotasyon yeni token üretmeli");

        // Eski refresh token tek kullanımlıktır.
        var replayResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithGarbageToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "gecersiz-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- logout ---

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var (_, _, auth) = await RegisterUniqueUserAsync();

        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = auth.RefreshToken });
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- register (kaldırıldı — kiracı sızıntısı güvenlik açığı) ---

    /// <summary>
    /// Güvenlik regresyon testi: <c>POST /api/auth/register</c> anonim
    /// self-servis kayıt ucuydu ve her yeni kullanıcıyı platformdaki EN
    /// DÜŞÜK Id'li kiracıya (üretimde gerçek bir müşteri) bağlayıp erişim
    /// kapısını BİLİNÇLİ ATLAYARAK anında token veriyordu — internetten
    /// herhangi biri kaydolup o müşterinin İK panosunu okuyabiliyordu. Uç
    /// tamamen kaldırıldı; bu test ucun SESSİZCE geri gelmediğini (ör. bir
    /// sonraki değişiklikte yanlışlıkla yeniden eklenmediğini) doğrular.
    /// </summary>
    [Fact]
    public async Task Register_Kaldirildi_404Doner()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { name = "Sızma Denemesi", email = $"kaldirildi-{Guid.NewGuid():N}@test.local", password = "sifre123" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "POST /api/auth/register artık mevcut olmamalı — kiracı sızıntısı güvenlik açığı yüzünden kaldırıldı");
    }

    // --- change-password ---

    [Fact]
    public async Task ChangePassword_AllowsLoginWithNewPassword()
    {
        var (email, password, auth) = await RegisterUniqueUserAsync();
        const string newPassword = "yeni-sifre-42";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = password, newPassword }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.PostAsJsonAsync("/api/auth/login", new { email, password }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "eski şifre artık geçersiz olmalı");

        var newLogin = await LoginAsync(email, newPassword);
        newLogin.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401()
    {
        var (_, _, auth) = await RegisterUniqueUserAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = "yanlis-mevcut-1", newPassword = "yeni-sifre-42" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "a-1", newPassword = "b-234567" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- yardımcılar ---

    private async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    /// <summary>
    /// Taze, tek kullanımlık bir test kullanıcısı üretir. Eskiden <c>POST
    /// /api/auth/register</c>'ı çağırırdı; o uç kiracı sızıntısı güvenlik
    /// açığı yüzünden kaldırıldığı için artık üretimdeki TEK meşru kullanıcı
    /// oluşturma yolunu izler: kiracı provizyonu + davet kabulü (bkz.
    /// <see cref="TenancyTestBase"/>). Bu testlerin hiçbiri rolün
    /// "employee" olmasına bağlı değildir — provizyonun ürettiği hr-admin
    /// rolü login/refresh/logout/change-password akışları için yeterlidir.
    /// </summary>
    private async Task<(string Email, string Password, AuthResponse Auth)> RegisterUniqueUserAsync()
    {
        var email = $"test-{Guid.NewGuid():N}@test.local";
        const string password = "test-sifre-1";

        // Şirket adına ayrıca GUID eklenmez: ProvisionTenantAsync zaten slug'ı kendi
        // GUID'iyle benzersizleştirir (bkz. orada) — ikisi üst üste binince slug 64
        // karakter sınırını aşıp 400 döndürüyordu.
        await ProvisionTenantAsync("Auth Akis Testi", email);
        await AcceptInviteAsync(email, password);

        var auth = await LoginAsync(email, password);
        return (email, password, auth);
    }
}
