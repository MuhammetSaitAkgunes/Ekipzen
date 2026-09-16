using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ekipzen.API.Services;

/// <summary>
/// Aktif tenant'ları merkezi platform kataloğundan bulur, her birini kendine sabitlenmiş
/// scope içinde işler. Bir tenant'ın şema/bağlantı hatası diğer tenant'ların retry
/// kuyruğunu durdurmaz.
/// </summary>
public sealed class IdentityProvisioningRetryService(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopeFactory,
    IOptions<IdentityProvisioningRetryOptions> options,
    ILogger<IdentityProvisioningRetryService> logger) : BackgroundService
{
    private readonly IdentityProvisioningRetryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Identity provizyon retry servisi kapalı.");
            return;
        }

        if (_options.IntervalSeconds <= 0 || _options.BatchSize <= 0)
        {
            logger.LogError(
                "IdentityProvisioning IntervalSeconds ve BatchSize pozitif olmalı; servis devre dışı.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));
        try
        {
            // İlk deneme HTTP akışında senkron best-effort yapılır. Worker ilk interval'i
            // bekler; bu hem açılış yükünü hem aynı işe gereksiz yarışmayı önler.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Identity provizyon retry geçişi başarısız.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal uygulama kapanışı.
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        List<int> tenantIds;
        using (var platformScope = scopeFactory.CreateScope())
        {
            var platform = platformScope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            tenantIds = await platform.Tenants
                .AsNoTracking()
                .Where(tenant => tenant.Status == TenantStatus.Active)
                .OrderBy(tenant => tenant.Id)
                .Select(tenant => tenant.Id)
                .ToListAsync(cancellationToken);
        }

        var completed = 0;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                using var tenantScope = tenantScopeFactory.Create(tenantId);
                var processor = tenantScope.Services
                    .GetRequiredService<IIdentityProvisioningProcessor>();
                completed += await processor.ProcessPendingAsync(
                    _options.BatchSize, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Tenant Identity provizyon kuyruğu işlenemedi. Tenant={TenantId}", tenantId);
            }
        }

        if (completed > 0)
        {
            logger.LogInformation("Identity provizyon retry geçişi {Completed} işi tamamladı.", completed);
        }

        return completed;
    }
}
