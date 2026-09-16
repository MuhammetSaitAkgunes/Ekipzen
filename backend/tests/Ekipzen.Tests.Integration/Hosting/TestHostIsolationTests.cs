using FluentAssertions;
using Ekipzen.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ekipzen.Tests.Integration.Hosting;

[Collection(ApiCollection.Name)]
public sealed class TestHostIsolationTests(EkipzenApiFactory factory)
{
    [Fact]
    public void AutomaticBackgroundReaders_DoNotRacePerTestDatabaseCleanup()
    {
        // API testleri her testin sonunda katalog düşürür. Zamanlanmış okuyucular
        // silinmekte olan test veritabanına yeni bağlantı açmamalı.
        factory.Services.GetRequiredService<IOptions<IdentityProvisioningRetryOptions>>()
            .Value.Enabled.Should().BeFalse();
        factory.Services.GetServices<IHostedService>()
            .Should().NotContain(service => service is TenantSchemaCheckService);

        // Timer kapalı olsa da iş mantığı DI'da kalır; RecruitmentTests retry'ı
        // RunOnceAsync ile açıkça çalıştırıp sonucunu doğrular.
        factory.Services.GetRequiredService<IdentityProvisioningRetryService>()
            .Should().NotBeNull();
    }
}
