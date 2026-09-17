using FluentAssertions;
using System.Text.Json;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>Davet e-postası hash router'sız temiz yola (/davet) işaret eder.</summary>
[Collection(ApiCollection.Name)]
public sealed class DavetLinkiTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task DavetEpostasi_TemizDavetYolunuKullanir()
    {
        var email = $"link-{Guid.NewGuid():N}@example.test";
        await ProvisionTenantAsync("Link Testi", email);

        var outbox = Path.Combine(Factory.StorageRoot, "outbox");
        var bodies = Directory.EnumerateFiles(outbox, "*.json")
            .Select(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement)
            .Where(root => string.Equals(root.GetProperty("to").GetString(), email, StringComparison.OrdinalIgnoreCase))
            .Select(root => root.GetProperty("body").GetString() ?? "")
            .ToList();

        bodies.Should().ContainSingle();
        bodies[0].Should().Contain("/davet?email=").And.NotContain("/#/");
    }
}
