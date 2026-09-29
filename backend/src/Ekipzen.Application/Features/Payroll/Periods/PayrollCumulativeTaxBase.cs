using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Periods;

/// <summary>
/// Çalışanın yıl içi kümülatif gelir vergisi matrahını yalnız onaylanmış önceki
/// bordro satırlarından üretir. Bordro satırı API'si bu değeri istemciden kabul etmez.
/// </summary>
public static class PayrollCumulativeTaxBase
{
    public static async Task RefreshAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<PayrollEmployee> rows,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var employeeIds = rows.Select(row => row.EmployeeId).Distinct().ToArray();
        var priorRows = await context.PayrollEmployees
            .AsNoTracking()
            .Where(row => employeeIds.Contains(row.EmployeeId)
                          && row.Period!.Year == year
                          && row.Period.Month < month
                          && row.ApprovalStatus == PayrollApprovalStatus.Approved
                          && (row.Period.Status == PayrollPeriodStatus.Approved
                              || row.Period.Status == PayrollPeriodStatus.Closed)
                          && row.Result != null)
            .Select(row => new
            {
                row.Id, row.EmployeeId,
                row.Period!.Month,
                row.PreviousTaxBase,
                IncomeTaxBase = row.Result!.IncomeTaxBase,
            })
            .ToListAsync(cancellationToken);

        var revisions = await ApprovedPayrollRevisionRules.LatestByRowAsync(
            context, priorRows.Select(row => row.Id).ToArray(), cancellationToken);

        var cumulativeByEmployee = priorRows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var latest = group.OrderByDescending(row => row.Month).First();
                    var taxBase = revisions.TryGetValue(latest.Id, out var revision)
                        ? ApprovedPayrollRevisionRules.EffectiveRow(revision).IncomeTaxBase
                        : latest.IncomeTaxBase;
                    return latest.PreviousTaxBase + taxBase;
                });

        foreach (var row in rows)
        {
            if (cumulativeByEmployee.TryGetValue(row.EmployeeId, out var priorPayrollBase))
            {
                row.PreviousTaxBase = priorPayrollBase;
                // Açılış bakiyesi yalnız Ekipzen'deki ilk bordroda kullanılır; sonraki
                // aylarda önceki onaylı snapshot zaten bu bakiyeyi içerir.
                row.PreviousEmployerIncomeTaxBase = 0m;
                row.PreviousEmployerIncomeTaxBaseReference = null;
            }
            else
            {
                row.PreviousTaxBase = row.PreviousEmployerIncomeTaxBase;
            }
        }
    }
}
