using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Dashboard.Queries;

/// <summary>
/// Risk merkezi ana yükü (dashboard.js getDashboardMetrics karşılığı).
/// hr-admin → tüm şirket, manager → kendi ekibi kapsamında hesaplanır.
/// </summary>
public sealed record GetDashboardMetricsQuery : IRequest<DashboardMetricsDto>;

public sealed class GetDashboardMetricsQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    public async Task<DashboardMetricsDto> Handle(
        GetDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        var riskRows = await context.EmployeeRiskMetrics
            .ScopeFor(currentUser)
            .OrderByDescending(m => m.RiskScore)
            .ToListAsync(cancellationToken);

        var employees = riskRows.Select(m => m.ToDto()).ToList();
        var riskScore = employees.Count == 0
            ? 0
            : (int)Math.Round(employees.Average(e => e.RiskScore));

        var scopedEmployeeIds = riskRows.Select(m => m.EmployeeId).ToList();
        var riskTrend = await RiskTrendAsync(scopedEmployeeIds, cancellationToken);

        var departmentRisk = await DepartmentRiskAsync(cancellationToken);

        var managerLoads = await ManagerLoadCalculator.CalculateAsync(
            context, currentUser, cancellationToken);
        var managerLoadIndex = managerLoads.Count == 0
            ? 0
            : (int)Math.Round(managerLoads.Average(m => m.Load));

        var criticalActions = await context.GlobalActions
            .CountAsync(a => a.Status != ActionStatus.Done, cancellationToken);

        var pulseScore = await LatestPulseAverageAsync(cancellationToken);

        // İşe alım sağlığı: pipeline'da ilerleyen adayların oranı (huni dönüşümü).
        var totalCandidates = await context.Candidates.CountAsync(cancellationToken);
        var progressedCandidates = await context.Candidates.CountAsync(
            c => c.Status != CandidateStatus.New && c.Status != CandidateStatus.Rejected,
            cancellationToken);
        var hiringHealth = totalCandidates == 0
            ? 100
            : (int)Math.Round(100.0 * progressedCandidates / totalCandidates);

        // Beceri açığı: hiç adayı olmayan açık pozisyon oranı (yetenek arzı sinyali).
        var openPositions = await context.Positions
            .Where(p => p.IsOpen)
            .Select(p => new { p.Id, HasCandidates = p.Candidates.Any() })
            .ToListAsync(cancellationToken);
        var skillGap = openPositions.Count == 0
            ? 0
            : (int)Math.Round(100.0 * openPositions.Count(p => !p.HasCandidates) / openPositions.Count);

        var criticalRoleRisk = employees.Count(e => e.RoleCriticality > 85);

        var talentCapacity = new List<TalentCapacityItemDto>
        {
            new("İşe Alım Sağlığı", hiringHealth,
                $"{openPositions.Count} açık pozisyon, {totalCandidates} aday",
                hiringHealth >= 80 ? "low" : hiringHealth >= 60 ? "medium" : "high"),
            new("Beceri Açığı", skillGap,
                "Adayı olmayan açık pozisyon oranı",
                skillGap >= 35 ? "high" : skillGap >= 20 ? "medium" : "low"),
            new("Kritik Rol Riski", criticalRoleRisk,
                "Kritiklik seviyesi 85 üzeri rol",
                criticalRoleRisk > 0 ? "high" : "low"),
            new("Kültür / Nabız", pulseScore,
                "Son nabız ölçümü ortalaması",
                pulseScore >= 70 ? "low" : pulseScore >= 60 ? "medium" : "high"),
        };

        return new DashboardMetricsDto(
            riskScore,
            managerLoadIndex,
            employees.Count(e => e.AttritionRisk == "high"),
            employees.Count(e => e.BurnoutRisk == "high"),
            criticalActions,
            pulseScore,
            hiringHealth,
            skillGap,
            criticalRoleRisk,
            riskTrend,
            departmentRisk,
            talentCapacity,
            employees);
    }

    /// <summary>Dönem bazlı ortalama risk serisi (son 12 dönem, eskiden yeniye).</summary>
    private async Task<IReadOnlyList<int>> RiskTrendAsync(
        IReadOnlyCollection<int> employeeIds, CancellationToken cancellationToken)
    {
        var series = await context.EmployeeMetricSnapshots
            .Where(s => employeeIds.Contains(s.EmployeeId))
            .GroupBy(s => s.PeriodDate)
            .Select(g => new
            {
                Period = g.Key,
                Score = g.Average(s =>
                    s.AbsencePct * 0.18 + s.LatenessPct * 0.14 + s.OvertimePct * 0.20 +
                    s.UnusedLeavePct * 0.15 + (100 - s.Pulse) * 0.18 +
                    (100 - s.Performance) * 0.15),
            })
            .OrderByDescending(g => g.Period)
            .Take(12)
            .ToListAsync(cancellationToken);

        return series
            .OrderBy(g => g.Period)
            .Select(g => (int)Math.Round(g.Score))
            .ToList();
    }

    private async Task<IReadOnlyList<DepartmentRiskDto>> DepartmentRiskAsync(
        CancellationToken cancellationToken)
    {
        if (currentUser.Roles.Contains(Domain.Constants.Roles.HrAdmin))
        {
            return await context.DepartmentRiskSummaries
                .OrderByDescending(d => d.RiskScore)
                .Select(d => new DepartmentRiskDto(
                    d.DepartmentId, d.DepartmentName, d.RiskScore, d.EmployeeCount,
                    d.HighAttritionCount, d.HighBurnoutCount))
                .ToListAsync(cancellationToken);
        }

        // Manager: view yerine kendi ekibinin satırlarından aynı agregasyon.
        var rows = await context.EmployeeRiskMetrics
            .ScopeFor(currentUser)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(m => new { m.DepartmentId, m.DepartmentName })
            .Select(g => new DepartmentRiskDto(
                g.Key.DepartmentId,
                g.Key.DepartmentName,
                (int)Math.Round(g.Average(m => (double)m.RiskScore)),
                g.Count(),
                g.Count(m => m.AttritionRisk == "high"),
                g.Count(m => m.BurnoutRisk == "high")))
            .OrderByDescending(d => d.Risk)
            .ToList();
    }

    private async Task<int> LatestPulseAverageAsync(CancellationToken cancellationToken)
    {
        var latestPulses = await context.EngagementMetrics
            .GroupBy(m => m.DepartmentId)
            .Select(g => g.OrderByDescending(m => m.PeriodDate).First().PulseScore)
            .ToListAsync(cancellationToken);

        return latestPulses.Count == 0 ? 0 : (int)Math.Round(latestPulses.Average());
    }
}
