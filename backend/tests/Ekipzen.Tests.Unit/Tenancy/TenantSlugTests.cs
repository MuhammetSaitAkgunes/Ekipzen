using FluentAssertions;
using Ekipzen.Application.Features.Tenancy;
using Xunit;

namespace Ekipzen.Tests.Unit.Tenancy;

public class TenantSlugTests
{
    [Theory]
    [InlineData("Acme Teknoloji A.Ş.", "acme-teknoloji-a-s")]
    [InlineData("Globex   Bilişim", "globex-bilisim")]
    [InlineData("Ekipzen İK", "ekipzen-ik")]
    [InlineData("!!!", "sirket")]
    [InlineData("", "sirket")]
    public void From_ProducesSlug(string input, string expected)
        => TenantSlug.From(input).Should().Be(expected);

    [Fact]
    public void From_Truncates_To64()
        => TenantSlug.From(new string('a', 100)).Length.Should().BeLessThanOrEqualTo(64);
}
