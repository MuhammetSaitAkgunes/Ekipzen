using FluentAssertions;
using Ekipzen.API.Services;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Zamanlanmış temizlik: BackgroundService'in tek geçişi (RunOnceAsync) yarım kalmış
/// (Provisioning'de takılı) eski kiracıları siler, aktif/doğrulanmış kiracıları korur.
/// Timer'dan bağımsız test.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ScheduledCleanupTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task RunOnce_PurgesUnverified_KeepsActive()
    {
        // Yarım kalmış provizyon: kiracı satırı açıldı, admin daveti hiç kabul edilmedi.
        // Self-servis kayıt kaldırıldığından bu durum artık ancak provizyon ORTASINDA
        // kesilmekle (ör. veritabanı oluşturma adımı çökerse) oluşur; testte de tam
        // olarak o durum kurulur — provizyonla + Provisioning'e geri al + tarihi eskit.
        var unverified = await ProvisionTenantAsync("Zamanlı Temizlik A.Ş.", $"sched-{Guid.NewGuid():N}@cleanup.local");

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var t = await platform.Tenants.FirstAsync(x => x.Id == unverified.TenantId);
            t.Status = TenantStatus.Provisioning;
            t.CreatedAtUtc = DateTime.UtcNow.AddDays(-40);
            await platform.SaveChangesAsync(CancellationToken.None);
        }
        var unverifiedId = unverified.TenantId;

        var kept = await ProvisionAndActivateAsync("Zamanlı Aktif A.Ş.", $"k-{Guid.NewGuid():N}@cleanup.local");

        var options = Options.Create(new UnverifiedTenantCleanupOptions
        {
            Enabled = true,
            OlderThanDays = 7,
        });
        var service = new UnverifiedTenantCleanupService(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<UnverifiedTenantCleanupService>.Instance);

        await service.RunOnceAsync(CancellationToken.None);

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            (await platform.Tenants.AnyAsync(x => x.Id == unverifiedId)).Should().BeFalse("doğrulanmamış temizlenmeli");
            (await platform.Tenants.AnyAsync(x => x.Id == kept.TenantId)).Should().BeTrue("aktif kiracı korunmalı");
        }
    }

    [Fact]
    public async Task Start_WithNonPositiveInterval_DoesNotThrow()
    {
        // Yanlış yapılandırma (IntervalHours=0) host'u çökertmemeli — servis sessizce devre dışı kalır.
        var options = Options.Create(new UnverifiedTenantCleanupOptions
        {
            Enabled = true,
            IntervalHours = 0,
            OlderThanDays = 7,
        });
        var service = new UnverifiedTenantCleanupService(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<UnverifiedTenantCleanupService>.Instance);

        var start = async () =>
        {
            await ((IHostedService)service).StartAsync(CancellationToken.None);
            await ((IHostedService)service).StopAsync(CancellationToken.None);
        };
        await start.Should().NotThrowAsync("IntervalHours<=0 yapılandırması BackgroundService'i çökertmemeli");
    }
}
