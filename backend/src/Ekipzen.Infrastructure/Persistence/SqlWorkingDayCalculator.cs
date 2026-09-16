using Ekipzen.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// dbo.fn_WorkingDays SQL fonksiyonunu çağırır (tatil tablosu DB'de tek kaynak).
/// Multi-tenant: fonksiyon Holidays'i kiracıya göre filtreler; aktif kiracı param geçilir.
/// </summary>
public sealed class SqlWorkingDayCalculator(AppDbContext context, ICurrentTenant currentTenant)
    : IWorkingDayCalculator
{
    public async Task<int> GetWorkingDaysAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var tenantId = currentTenant.TenantIdOrThrow();
        return await context.Database
            .SqlQuery<int>($"SELECT dbo.fn_WorkingDays({start}, {end}, {tenantId}) AS [Value]")
            .SingleAsync(cancellationToken);
    }
}
