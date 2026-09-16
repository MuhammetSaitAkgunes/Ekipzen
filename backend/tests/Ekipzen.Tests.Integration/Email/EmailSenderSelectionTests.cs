using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Ekipzen.Tests.Integration.Email;

/// <summary>
/// E-posta göndericisi seçimi: Email:Mode yapılandırılmadığında (test/dev varsayılanı)
/// dosya outbox stub'ı kullanılmalı — davet testleri token'ı outbox'tan okur.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EmailSenderSelectionTests(EkipzenApiFactory factory)
{
    [Fact]
    public void DefaultMode_UsesFileOutbox()
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEmailSender>()
            .Should().BeOfType<FileOutboxEmailSender>("Email:Mode yapılandırılmadığında outbox varsayılandır");
    }
}
