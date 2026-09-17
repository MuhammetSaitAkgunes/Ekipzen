using Ekipzen.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace Ekipzen.Tests.Integration.DemoRequests;

[Collection(ApiCollection.Name)]
public sealed class DemoRequestTests(EkipzenApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object ValidBody(string company, string? website = null) => new
    {
        fullName = "Ayşe Demir",
        email = "ayse@firma.example",
        company,
        companySize = "51-200",
        phone = "+90 555 000 00 00",
        message = "Bordro modülünü görmek istiyoruz.",
        kvkkConsent = true,
        website,
    };

    private IEnumerable<JsonElement> OutboxMessagesFor(string company)
    {
        var outbox = Path.Combine(factory.StorageRoot, "outbox");
        if (!Directory.Exists(outbox)) return [];
        return Directory.EnumerateFiles(outbox, "*.json")
            .Select(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement)
            .Where(root => root.GetProperty("to").GetString() == EkipzenApiFactory.DemoNotifyEmail
                && (root.GetProperty("body").GetString() ?? "").Contains(company))
            .ToList();
    }

    [Fact]
    public async Task GecerliTalep_204Doner_VeBildirimEpostasiGonderir()
    {
        var company = $"Firma-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", ValidBody(company));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var message = OutboxMessagesFor(company).Should().ContainSingle().Subject;
        message.GetProperty("subject").GetString().Should().Be($"Demo talebi: {company}");
        var body = message.GetProperty("body").GetString();
        body.Should().Contain("Ayşe Demir").And.Contain("ayse@firma.example").And.Contain("51-200").And.Contain("KVKK onayı: Evet");
    }

    [Fact]
    public async Task HoneypotDoluysa_204Doner_AmaEpostaGondermez()
    {
        var company = $"Bot-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", ValidBody(company, website: "http://spam.example"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        OutboxMessagesFor(company).Should().BeEmpty();
    }

    [Fact]
    public async Task KvkkOnayiYoksa_400Doner()
    {
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", new
        {
            fullName = "Ali", email = "ali@firma.example", company = "Firma", companySize = "1-10", kvkkConsent = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("KvkkConsent");
    }

    [Fact]
    public async Task GecersizAlanlar_400Doner()
    {
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", new
        {
            fullName = "", email = "gecersiz", company = new string('x', 151), companySize = "999", kvkkConsent = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("FullName").And.Contain("Email").And.Contain("Company").And.Contain("CompanySize");
    }

    [Fact]
    public async Task KonuSatiriYeniSatirIcermez()
    {
        var company = $"Satir-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", ValidBody($"{company}\r\nBcc: x@y.z"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var subject = OutboxMessagesFor(company).Single().GetProperty("subject").GetString();
        subject.Should().NotContain("\r").And.NotContain("\n");
    }

    [Fact]
    public void Uc_AnonimVeRateLimitli()
    {
        var method = typeof(PublicController).GetMethod(nameof(PublicController.SubmitDemoRequest))!;
        method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be("public-form");
        typeof(PublicController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>().Should().NotBeNull();
    }
}
