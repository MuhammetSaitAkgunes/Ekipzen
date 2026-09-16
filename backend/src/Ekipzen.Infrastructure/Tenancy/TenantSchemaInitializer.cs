using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Constants;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ekipzen.Infrastructure.Tenancy;

/// <summary>
/// <see cref="ITenantSchemaInitializer"/>'ın EF uygulaması.
///
/// Kapsam <see cref="ITenantScopeFactory"/> ile açılır: <c>AppDbContext</c>'in
/// bağlantısı kapsam kurulurken çözülür, dolayısıyla kiracının katalog adı kapsam
/// DÖNMEDEN ÖNCE sabitlenmiş olmalıdır. (Bu yüzden çağıranın, kiracı satırını
/// <c>DatabaseName</c> dolu olarak kaydetmiş ve kütüğü düşürmüş olması gerekir.)
/// </summary>
public sealed class TenantSchemaInitializer(
    ITenantScopeFactory tenantScopeFactory,
    ILogger<TenantSchemaInitializer> logger) : ITenantSchemaInitializer
{
    public async Task PrepareAsync(int tenantId, CancellationToken cancellationToken)
    {
        using var kapsam = tenantScopeFactory.Create(tenantId);

        var context = kapsam.Services.GetRequiredService<AppDbContext>();
        logger.LogInformation(
            "Kiracı {TenantId} veritabanına migration uygulanıyor: {Katalog}",
            tenantId, context.Database.GetDbConnection().Database);
        await context.Database.MigrateAsync(cancellationToken);

        // Roller olmadan admin'e rol atanamaz ve atama SESSİZCE düşer — kullanıcı
        // oluşur, giriş yapar, hiçbir yetkisi olmaz. Bu yüzden şema hazırlığının
        // ayrılmaz parçası.
        var roller = kapsam.Services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in Roles.All)
        {
            if (!await roller.RoleExistsAsync(rol))
            {
                await roller.CreateAsync(new IdentityRole(rol));
            }
        }
    }

    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(
        int tenantId, CancellationToken cancellationToken)
    {
        using var kapsam = tenantScopeFactory.Create(tenantId);
        var context = kapsam.Services.GetRequiredService<AppDbContext>();
        return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
    }
}
