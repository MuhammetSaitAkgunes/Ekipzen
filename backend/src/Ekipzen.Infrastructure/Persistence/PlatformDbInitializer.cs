using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// Platform veritabanını migrate eder. Platform DB küçüktür ve onsuz hiçbir
/// kiracı çözülemez; bu yüzden açılışta migrate edilir. KİRACI veritabanları
/// açılışta migrate EDİLMEZ — her kiracının şeması kendi provizyonunda uygulanır
/// (bkz. <see cref="Ekipzen.Application.Common.Interfaces.ITenantSchemaInitializer"/>).
/// </summary>
public sealed class PlatformDbInitializer(PlatformDbContext context, ILogger<PlatformDbInitializer> logger)
{
    public async Task InitialiseAsync()
    {
        logger.LogInformation("Platform veritabanı migrate ediliyor.");
        await context.Database.MigrateAsync();
    }
}
