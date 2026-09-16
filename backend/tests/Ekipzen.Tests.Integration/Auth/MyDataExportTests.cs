using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ekipzen.Tests.Integration.Auth;

/// <summary>
/// Self-servis veri dışa aktarımı (KVKK taşınabilirlik): oturum açmış kullanıcı kendi
/// verisini JSON olarak indirir. Yalnız kendi verisi; başka çalışanınki sızmaz.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MyDataExportTests(EkipzenApiFactory factory)
{
    private const string DemoPassword = "demo123";

    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await response.Content.ReadFromJsonAsync<Ekipzen.Application.Features.Auth.AuthResponse>())!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    [Fact]
    public async Task Export_ReturnsOwnData_NotOthers()
    {
        var ahmet = await AuthedClientAsync("ahmet.yilmaz@hrmaster.local");
        var response = await ahmet.GetAsync("/api/me/data-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentDisposition!.FileName.Should().NotBeNullOrEmpty();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("account").GetProperty("email").GetString().Should().Be("ahmet.yilmaz@hrmaster.local");
        root.GetProperty("employee").ValueKind.Should().NotBe(JsonValueKind.Null);
        // Kendi dışında personel adı içermemeli.
        root.ToString().Should().NotContain("Ece Arslan");
    }

    [Fact]
    public async Task Export_ForUserWithoutEmployeeLink_DoesNotCrash()
    {
        var admin = await AuthedClientAsync("ik@hrmaster.local"); // hr-admin, EmployeeId'siz seed'lenir
        var response = await admin.GetAsync("/api/me/data-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("employee").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
