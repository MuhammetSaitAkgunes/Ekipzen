using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Periods;

public sealed record PayrollEmploymentPeriod(DateOnly HireDate, DateOnly? TerminationDate);

/// <summary>
/// Bordro puantaj yeterliliğini sunucu tarafında hesaplar. Bir çalışanın dönemdeki
/// beklenen iş günlerinin her biri puantaj kaydı veya onaylı izinle karşılanmalıdır;
/// hafta sonları, tatiller, işe girişten önceki ve ayrılıştan sonraki günler dahil edilmez.
/// </summary>
public static class PayrollTimesheetCoverage
{
    public static async Task<IReadOnlyDictionary<int, bool>> CalculateAsync(
        IApplicationDbContext context,
        IReadOnlyDictionary<int, PayrollEmploymentPeriod> employmentPeriods,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (employmentPeriods.Count == 0)
        {
            return new Dictionary<int, bool>();
        }

        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var employeeIds = employmentPeriods.Keys.ToArray();

        var holidays = (await context.Holidays
                .AsNoTracking()
                .Where(h => h.Date >= periodStart && h.Date <= periodEnd)
                .Select(h => h.Date)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var attendanceDates = await context.AttendanceRecords
            .AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId)
                        && a.WorkDate >= periodStart
                        && a.WorkDate <= periodEnd)
            .Select(a => new { a.EmployeeId, a.WorkDate })
            .ToListAsync(cancellationToken);
        var attendanceByEmployee = attendanceDates
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.WorkDate).ToHashSet());

        var approvedLeaves = await context.LeaveRequests
            .AsNoTracking()
            .Where(l => employeeIds.Contains(l.EmployeeId)
                        && l.Status == LeaveStatus.Approved
                        && l.StartDate <= periodEnd
                        && l.EndDate >= periodStart)
            .Select(l => new { l.EmployeeId, l.StartDate, l.EndDate })
            .ToListAsync(cancellationToken);
        var leavesByEmployee = approvedLeaves
            .GroupBy(l => l.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<(DateOnly Start, DateOnly End)>)g
                    .Select(l => (l.StartDate, l.EndDate))
                    .ToList());

        return employmentPeriods.ToDictionary(
            pair => pair.Key,
            pair => IsComplete(
                periodStart,
                periodEnd,
                pair.Value.HireDate,
                pair.Value.TerminationDate,
                holidays,
                attendanceByEmployee.GetValueOrDefault(pair.Key) ?? [],
                leavesByEmployee.GetValueOrDefault(pair.Key) ?? []));
    }

    /// <summary>Saf kapsam kuralı; tatil/izin birleşimi regresyon testlerinde doğrudan doğrulanır.</summary>
    public static bool IsComplete(
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly hireDate,
        DateOnly? terminationDate,
        IReadOnlySet<DateOnly> holidays,
        IReadOnlySet<DateOnly> attendanceDates,
        IReadOnlyList<(DateOnly Start, DateOnly End)> approvedLeaves)
    {
        var effectiveStart = hireDate > periodStart ? hireDate : periodStart;
        var effectiveEnd = terminationDate is { } endDate && endDate < periodEnd
            ? endDate
            : periodEnd;
        if (effectiveStart > effectiveEnd)
        {
            return true;
        }

        for (var date = effectiveStart; date <= effectiveEnd; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidays.Contains(date))
            {
                continue;
            }

            var coveredByLeave = approvedLeaves.Any(l => l.Start <= date && l.End >= date);
            if (!attendanceDates.Contains(date) && !coveredByLeave)
            {
                return false;
            }
        }

        return true;
    }
}
