using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Payroll;
using IKPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Payroll.Periods;

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
                row.EmployeeId,
                row.Period!.Month,
                CumulativeAfter = row.PreviousTaxBase + row.Result!.IncomeTaxBase,
            })
            .ToListAsync(cancellationToken);

        var cumulativeByEmployee = priorRows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.Month).First().CumulativeAfter);

        foreach (var row in rows)
        {
            row.PreviousTaxBase = cumulativeByEmployee.GetValueOrDefault(row.EmployeeId, 0m);
        }
    }
}
