using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Payroll;
using IKPro.Domain.Services;

namespace IKPro.Application.Features.Payroll.Periods;

/// <summary>Bordro satırının puantaj ve parasal onay kapılarını tek yerde tutar.</summary>
public static class PayrollRowReadiness
{
    public static async Task RefreshTimesheetCompletenessAsync(
        IApplicationDbContext context,
        PayrollPeriod period,
        IReadOnlyCollection<PayrollEmployee> rows,
        CancellationToken cancellationToken)
    {
        var employmentPeriods = rows.ToDictionary(
            row => row.EmployeeId,
            row => row.Employee is { } employee
                ? new PayrollEmploymentPeriod(employee.HireDate, employee.TerminationDate)
                : throw new InvalidOperationException(
                    "Puantaj kontrolü için çalışan bilgisi yüklenmelidir."));
        var coverage = await PayrollTimesheetCoverage.CalculateAsync(
            context, employmentPeriods, period.Year, period.Month, cancellationToken);

        foreach (var row in rows)
        {
            row.TimesheetComplete = coverage.GetValueOrDefault(row.EmployeeId);
        }
    }

    public static bool IsReady(PayrollEmployee row, PayrollCalculation calculation)
        => row.IbanComplete
           && row.TimesheetComplete
           && row.GrossSalary > 0m
           && calculation.NetPay > 0m;

    public static void EnsureApprovable(PayrollEmployee row, PayrollCalculation calculation)
    {
        if (!row.IbanComplete || !row.TimesheetComplete)
        {
            throw new ConflictException("Eksik verili satır onaylanamaz (IBAN/puantaj tamamlanmalı).");
        }

        if (row.GrossSalary <= 0m)
        {
            throw new ConflictException("Brüt maaşı sıfır olan bordro satırı onaylanamaz.");
        }

        if (calculation.NetPay <= 0m)
        {
            throw new ConflictException("Net ödemesi sıfır veya negatif olan bordro satırı onaylanamaz.");
        }
    }
}
