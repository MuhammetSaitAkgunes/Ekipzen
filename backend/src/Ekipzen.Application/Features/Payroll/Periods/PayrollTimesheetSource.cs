using System.Security.Cryptography;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Periods;

/// <summary>Puantaj, onaylı izin, takvim ve istihdam kaynaklarını tutarları otomatik değiştirmeden izler.</summary>
public static class PayrollTimesheetSource
{
    public static async Task<IReadOnlyDictionary<int, string>> ReadAsync(IApplicationDbContext db,
        IReadOnlyCollection<int> employeeIds, int year, int month, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return new Dictionary<int, string>();
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.WorkDate >= start && x.WorkDate <= end)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.EmployeeId, x.WorkDate, x.CheckIn, x.CheckOut,
                x.BreakMinutes, x.WorkedMinutes, x.OvertimeMinutes, x.Type, x.Status }).ToListAsync(ct);
        var leaves = await db.LeaveRequests.AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.Status == LeaveStatus.Approved
                && x.StartDate <= end && x.EndDate >= start)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.EmployeeId, x.StartDate, x.EndDate }).ToListAsync(ct);
        var holidays = await db.Holidays.AsNoTracking().Where(x => x.Date >= start && x.Date <= end)
            .OrderBy(x => x.Date).Select(x => x.Date).ToListAsync(ct);
        var employees = await db.Employees.AsNoTracking().Where(x => employeeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.HireDate, x.TerminationDate }).ToListAsync(ct);
        return employees.ToDictionary(x => x.Id, x => Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(new { x.HireDate, x.TerminationDate, holidays,
                Records = records.Where(r => r.EmployeeId == x.Id), Leaves = leaves.Where(l => l.EmployeeId == x.Id) }))));
    }
}
