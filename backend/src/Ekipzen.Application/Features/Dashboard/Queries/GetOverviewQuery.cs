using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Dashboard.Queries;

/// <summary>
/// Genel durum KPI'ları (dashboard.js OverviewDashboard karşılığı). Tüm roller erişir;
/// bekleyen onaylar rol kapsamına göre daralır (manager → ekibi, employee → kendisi).
/// </summary>
public sealed record GetOverviewQuery : IRequest<OverviewDto>;

public sealed class GetOverviewQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetOverviewQuery, OverviewDto>
{
    public async Task<OverviewDto> Handle(
        GetOverviewQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var activeEmployees = await context.Employees
            .CountAsync(e => e.Status == EmployeeStatus.Active, cancellationToken);

        var scopedEmployeeIds = context.Employees.ScopeFor(currentUser).Select(e => e.Id);
        var pendingApprovals = await context.LeaveRequests.CountAsync(
            l => l.Status == LeaveStatus.Pending && scopedEmployeeIds.Contains(l.EmployeeId),
            cancellationToken);

        var openPositions = await context.Positions
            .Where(p => p.IsOpen)
            .SumAsync(p => p.OpenCount, cancellationToken);
        var newApplications = await context.Candidates
            .CountAsync(c => c.Status == CandidateStatus.New, cancellationToken);

        var inOfficeToday = await context.AttendanceRecords.CountAsync(
            a => a.WorkDate == today && a.Status != AttendanceStatus.Absent,
            cancellationToken);
        var onLeaveToday = await context.LeaveRequests.CountAsync(
            l => l.Status == LeaveStatus.Approved && l.StartDate <= today && l.EndDate >= today,
            cancellationToken);

        var latestPulses = await context.EngagementMetrics
            .GroupBy(m => m.DepartmentId)
            .Select(g => g.OrderByDescending(m => m.PeriodDate).First().PulseScore)
            .ToListAsync(cancellationToken);
        var pulseScore = latestPulses.Count == 0
            ? 0 : (int)Math.Round(latestPulses.Average());

        var departmentGroups = await context.Employees
            .Where(e => e.Status == EmployeeStatus.Active)
            .GroupBy(e => e.Department!.Name)
            .Select(g => new { Dept = g.Key, Count = g.Count() })
            .OrderByDescending(d => d.Count)
            .ToListAsync(cancellationToken);
        var departmentDistribution = departmentGroups
            .Select(d => new DepartmentCountDto(d.Dept, d.Count))
            .ToList();

        var funnel = await context.Candidates
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        int CountOf(CandidateStatus status) => funnel.FirstOrDefault(f => f.Status == status)?.Count ?? 0;

        return new OverviewDto(
            activeEmployees,
            pendingApprovals,
            openPositions,
            newApplications,
            inOfficeToday,
            onLeaveToday,
            pulseScore,
            departmentDistribution,
            new RecruitmentFunnelSliceDto(
                funnel.Sum(f => f.Count),
                CountOf(CandidateStatus.New),
                CountOf(CandidateStatus.Interview),
                CountOf(CandidateStatus.Offer),
                CountOf(CandidateStatus.Rejected),
                CountOf(CandidateStatus.Hired)));
    }
}
