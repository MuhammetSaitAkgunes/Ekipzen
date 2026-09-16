using FluentAssertions;
using Ekipzen.Infrastructure.Email;
using Xunit;

namespace Ekipzen.Tests.Unit.Email;

public class SmtpOptionsTests
{
    private static SmtpOptions Valid() => new()
    {
        Host = "smtp.example.com",
        From = "noreply@ekipzen.example",
        User = "noreply@ekipzen.example",
        Password = "secret",
    };

    [Fact]
    public void Validate_FullyConfigured_DoesNotThrow()
        => FluentActions.Invoking(() => Valid().Validate()).Should().NotThrow();

    [Fact]
    public void Validate_MissingHost_Throws()
    {
        var options = Valid();
        options.Host = "";
        FluentActions.Invoking(() => options.Validate())
            .Should().Throw<InvalidOperationException>().WithMessage("*Smtp:Host*");
    }

    [Fact]
    public void Validate_MissingFrom_Throws()
    {
        var options = Valid();
        options.From = "";
        FluentActions.Invoking(() => options.Validate())
            .Should().Throw<InvalidOperationException>().WithMessage("*Smtp:From*");
    }

    [Fact]
    public void Defaults_Port587_StartTlsTrue()
    {
        var options = new SmtpOptions();
        options.Port.Should().Be(587);
        options.UseStartTls.Should().BeTrue();
    }
}
