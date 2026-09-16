using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Periods;

/// <summary>
/// Asgari ücret gelir vergisi istisnasının tarife dilimini belirleyen takvim yılı
/// kümülatif matrahını, yürürlükteki onaylı mevzuat setlerinden üretir.
/// </summary>
public static class PayrollMinimumWageTaxBase
{
    public static async Task<decimal> CalculateBeforeAsync(
        IApplicationDbContext context,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (month <= 1)
        {
            return 0m;
        }

        var endExclusive = new DateOnly(year, month, 1);
        var settings = await context.PayrollSettings
            .AsNoTracking()
            .Where(item => item.Status == PayrollSettingsStatus.Approved
                && item.EffectiveFrom < endExclusive)
            .OrderBy(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken);

        decimal total = 0m;
        for (var priorMonth = 1; priorMonth < month; priorMonth++)
        {
            var date = new DateOnly(year, priorMonth, 1);
            var effective = settings.LastOrDefault(item => item.EffectiveFrom <= date)
                ?? throw new ConflictException(
                    $"{date:yyyy-MM-dd} tarihinde onaylı bordro mevzuat seti bulunamadı.");
            total += decimal.Round(
                effective.MinWageGross
                    * (1m - effective.SgkEmployeeRate / 100m
                          - effective.UnemploymentEmployeeRate / 100m),
                2,
                MidpointRounding.AwayFromZero);
        }

        return total;
    }
}
