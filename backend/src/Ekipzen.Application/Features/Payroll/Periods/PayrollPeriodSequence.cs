using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Periods;

/// <summary>Bordro dönemlerinin kronolojik ve kapanışa bağlı açılma kuralı.</summary>
public static class PayrollPeriodSequence
{
    public static async Task EnsureCanCreateAsync(
        IApplicationDbContext context,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var latest = await context.PayrollPeriods
            .AsNoTracking()
            .OrderByDescending(period => period.Year)
            .ThenByDescending(period => period.Month)
            .Select(period => new { period.Year, period.Month, period.Status, period.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return;
        }

        var requestedOrder = year * 12 + month;
        var latestOrder = latest.Year * 12 + latest.Month;
        if (requestedOrder <= latestOrder)
        {
            throw new ConflictException(
                $"{latest.Name} döneminden önce veya aynı aya ait yeni dönem açılamaz.");
        }

        if (latest.Status is not (PayrollPeriodStatus.Approved or PayrollPeriodStatus.Closed))
        {
            throw new ConflictException(
                $"Yeni dönem açılamaz: {latest.Name} dönemi önce onaylanmalı veya kapatılmalı.");
        }
    }
}
