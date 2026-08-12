using IKPro.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IKPro.Infrastructure.Persistence;

/// <summary>
/// Platform veritabanını migrate eder ve varsayılan (demo) kiracının varlığını
/// güvence altına alır. Platform DB küçüktür ve onsuz hiçbir kiracı çözülemez;
/// bu yüzden açılışta migrate edilir. KİRACI veritabanları açılışta migrate
/// EDİLMEZ (bkz. tasarım belgesi, migration orkestrasyonu).
/// </summary>
public sealed class PlatformDbInitializer(PlatformDbContext context, ILogger<PlatformDbInitializer> logger)
{
    public async Task InitialiseAsync()
    {
        logger.LogInformation("Platform veritabanı migrate ediliyor.");
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Varsayılan kiracıyı garanti eder ve kimliğini döner.
    ///
    /// Neden BURADA: kiracı satırı platform verisidir ve uygulama seed'inin
    /// ÖNKOŞULUDUR — seed'in hangi kiracıya yazacağı, seed başlamadan bilinmelidir.
    /// Daha önce bu iş <c>AppDbContextInitializer.SeedDefaultTenantAsync</c>
    /// içindeydi ve orada kiracıyı <c>ICurrentTenant.Impersonate</c> ile ambient
    /// olarak sabitliyordu; ama o sınıfın <c>AppDbContext</c>'i DI tarafından
    /// sınıf çözülürken çoktan kurulmuş oluyordu, yani impersonation bağlantıyı
    /// artık etkilemiyordu — "sıraya güvenen", sessizce yanlış çalışabilen bir
    /// düzenek. Kiracı artık kapsamla birlikte gelir (bkz. Program.cs).
    /// </summary>
    public async Task<int> EnsureDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await context.Tenants.OrderBy(t => t.Id).FirstOrDefaultAsync(cancellationToken);
        if (tenant is not null)
        {
            return tenant.Id;
        }

        tenant = new Tenant
        {
            Name = "Demo Şirket",
            Slug = "demo",
            Status = TenantStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
        };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Varsayılan kiracı oluşturuldu: {TenantId}", tenant.Id);
        return tenant.Id;
    }
}
