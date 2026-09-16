using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Dashboard.Queries;

/// <summary>
/// Yönetici yükü hesabı (dashboard.js managers şekli). Yük endeksi formülü:
///   load = clamp( ekip*1.5 + bekleyenOnay*4 + açıkAksiyon*3
///               + ortMesai*0.35 + (100-ortNabız)*0.35 , 0..100 )
/// Bekleyen onay = ekibin pending izin talepleri; açık aksiyon = yöneticinin
/// üzerindeki tamamlanmamış GlobalAction kayıtları.
/// </summary>
public static class ManagerLoadCalculator
{
    public static async Task<IReadOnlyList<ManagerLoadItemDto>> CalculateAsync(
        IApplicationDbContext context, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var managersQuery = context.Employees
            .Where(e => context.Employees.Any(r => r.ManagerId == e.Id));

        if (!currentUser.Roles.Contains(Roles.HrAdmin))
        {
            var selfId = currentUser.EmployeeId ?? -1;
            managersQuery = managersQuery.Where(e => e.Id == selfId);
        }

        var managers = await managersQuery
            .Select(e => new
            {
                e.Id,
                Name = e.FirstName + " " + e.LastName,
                TeamSize = context.Employees.Count(r => r.ManagerId == e.Id),
                PendingApprovals = context.LeaveRequests.Count(l =>
                    l.Status == LeaveStatus.Pending &&
                    l.Employee!.ManagerId == e.Id),
                OpenActions = context.GlobalActions.Count(a =>
                    a.Status != ActionStatus.Done &&
                    a.Owner == e.FirstName + " " + e.LastName),
            })
            .ToListAsync(cancellationToken);

        var teamAverages = await context.EmployeeRiskMetrics
            .Where(m => m.ManagerId != null)
            .GroupBy(m => m.ManagerId!.Value)
            .Select(g => new
            {
                ManagerId = g.Key,
                Overtime = g.Average(m => (double)m.OvertimePct),
                Pulse = g.Average(m => (double)m.Pulse),
            })
            .ToListAsync(cancellationToken);
        var averagesByManager = teamAverages.ToDictionary(a => a.ManagerId);

        return managers
            .Select(m =>
            {
                // Metrik verisi yoksa nabız katkısı nötr kabul edilir (100 → 0 katkı).
                var overtime = averagesByManager.TryGetValue(m.Id, out var avg)
                    ? (int)Math.Round(avg.Overtime) : 0;
                var pulse = averagesByManager.TryGetValue(m.Id, out avg)
                    ? (int)Math.Round(avg.Pulse) : 100;

                var load = (int)Math.Round(
                    m.TeamSize * 1.5 + m.PendingApprovals * 4 + m.OpenActions * 3 +
                    overtime * 0.35 + (100 - pulse) * 0.35);

                return new ManagerLoadItemDto(
                    m.Id, m.Name, m.TeamSize, m.PendingApprovals, m.OpenActions,
                    overtime, pulse, Math.Clamp(load, 0, 100));
            })
            .OrderByDescending(m => m.Load)
            .ToList();
    }
}
