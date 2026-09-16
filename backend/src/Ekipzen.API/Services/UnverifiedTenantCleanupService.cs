using Ekipzen.Application.Features.Tenancy.Commands;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ekipzen.API.Services;

/// <summary>
/// Doğrulanmamış (hiç etkinleşmemiş) eski self-servis kiracıları uygulama-içi periyodik
/// olarak temizler — dış cron sistemine gerek yok. Silme mantığı yeniden yazılmaz;
/// mevcut <see cref="CleanupUnverifiedTenantsCommand"/> çağrılır. Varsayılan kapalı.
/// </summary>
public sealed class UnverifiedTenantCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<UnverifiedTenantCleanupOptions> options,
    ILogger<UnverifiedTenantCleanupService> logger) : BackgroundService
{
    private readonly UnverifiedTenantCleanupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Doğrulanmamış kiracı temizliği kapalı (Cleanup:UnverifiedTenants:Enabled=false).");
            return;
        }

        // Yanlış yapılandırmada (IntervalHours<=0) PeriodicTimer ctor'u fırlatır; bu, host'u
        // çökertmesin diye burada yakalanır — temizlik devre dışı kalır, uygulama ayakta kalır.
        if (_options.IntervalHours <= 0)
        {
            logger.LogError(
                "Cleanup:UnverifiedTenants:IntervalHours pozitif olmalı (verilen: {Value}); temizlik devre dışı.",
                _options.IntervalHours);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.IntervalHours));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // uygulama kapanıyor
            }
            catch (Exception ex)
            {
                // Bir geçiş hatası döngüyü öldürmesin; sonraki tetiklemede yeniden dener.
                logger.LogError(ex, "Doğrulanmamış kiracı temizliği geçişi başarısız.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Tek temizlik geçişi (deterministik test için ayrık; scope açıp komutu gönderir).</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(
            new CleanupUnverifiedTenantsCommand(_options.OlderThanDays), cancellationToken);

        if (result.PurgedCount > 0)
        {
            logger.LogInformation(
                "Doğrulanmamış kiracı temizliği: {Count} kiracı silindi.", result.PurgedCount);
        }
    }
}
