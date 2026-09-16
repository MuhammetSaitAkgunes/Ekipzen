using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ekipzen.Infrastructure.Persistence.Interceptors;

/// <summary>
/// IAuditable entity'lerine create/update zaman damgası ve aktör bilgisini yazar.
/// Kritik tablolardaki SQL audit trigger'ları (AuditTriggers migration) aktörü
/// buradaki CreatedBy/UpdatedBy kolonlarından okur.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentUser currentUser, ICurrentTenant currentTenant)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;
        var actor = currentUser.UserName ?? currentUser.UserId ?? "system";

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = actor;
            }
        }

        // Multi-tenant: eklenen her kiracı-kapsamlı kayıt aktif kiracıyla damgalanır.
        // TenantId önceden atanmışsa (ör. platform işlemi) korunur.
        var tenantId = currentTenant.TenantId;
        if (tenantId is { } tid)
        {
            foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == 0)
                {
                    entry.Entity.TenantId = tid;
                }
            }
        }
    }
}
