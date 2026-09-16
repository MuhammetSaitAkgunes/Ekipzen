using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Dashboard.Queries;

/// <summary>Ayrılma riski detayı: risk skoruna göre sıralı satırlar + KPI'lar.</summary>
public sealed record GetAttritionDetailQuery : IRequest<RiskDetailDto>;

/// <summary>Tükenmişlik detayı: mesai+izin yüküne göre sıralı satırlar + KPI'lar.</summary>
public sealed record GetBurnoutDetailQuery : IRequest<RiskDetailDto>;

/// <summary>Yönetici yükü detayı (dashboard.js managers tablosu).</summary>
public sealed record GetManagerLoadQuery : IRequest<ManagerLoadDto>;

public sealed class GetAttritionDetailQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetAttritionDetailQuery, RiskDetailDto>
{
    public async Task<RiskDetailDto> Handle(
        GetAttritionDetailQuery request, CancellationToken cancellationToken)
    {
        var rows = await context.EmployeeRiskMetrics
            .ScopeFor(currentUser)
            .OrderByDescending(m => m.RiskScore)
            .ToListAsync(cancellationToken);

        var employees = rows.Select(m => m.ToDto()).ToList();
        return new RiskDetailDto(
            employees.Count(e => e.AttritionRisk == "high"),
            employees.Count(e => e.AttritionRisk == "medium"),
            employees.Count(e => e.RoleCriticality > 85),
            Average(employees, e => e.Pulse),
            Average(employees, e => e.Overtime),
            Average(employees, e => e.UnusedLeave),
            employees);
    }

    internal static int Average(IReadOnlyList<RiskEmployeeDto> employees, Func<RiskEmployeeDto, int> selector)
        => employees.Count == 0 ? 0 : (int)Math.Round(employees.Average(selector));
}

public sealed class GetBurnoutDetailQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetBurnoutDetailQuery, RiskDetailDto>
{
    public async Task<RiskDetailDto> Handle(
        GetBurnoutDetailQuery request, CancellationToken cancellationToken)
    {
        var rows = await context.EmployeeRiskMetrics
            .ScopeFor(currentUser)
            .OrderByDescending(m => m.OvertimePct + m.UnusedLeavePct)
            .ToListAsync(cancellationToken);

        var employees = rows.Select(m => m.ToDto()).ToList();
        return new RiskDetailDto(
            employees.Count(e => e.BurnoutRisk == "high"),
            employees.Count(e => e.BurnoutRisk == "medium"),
            employees.Count(e => e.RoleCriticality > 85),
            GetAttritionDetailQueryHandler.Average(employees, e => e.Pulse),
            GetAttritionDetailQueryHandler.Average(employees, e => e.Overtime),
            GetAttritionDetailQueryHandler.Average(employees, e => e.UnusedLeave),
            employees);
    }
}

public sealed class GetManagerLoadQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetManagerLoadQuery, ManagerLoadDto>
{
    public async Task<ManagerLoadDto> Handle(
        GetManagerLoadQuery request, CancellationToken cancellationToken)
    {
        var managers = await ManagerLoadCalculator.CalculateAsync(
            context, currentUser, cancellationToken);

        var openActions = await context.GlobalActions
            .CountAsync(a => a.Status != ActionStatus.Done, cancellationToken);

        return new ManagerLoadDto(
            managers.Count == 0 ? 0 : (int)Math.Round(managers.Average(m => m.Load)),
            managers.Count(m => m.Load > 70),
            managers.Sum(m => m.Approvals),
            openActions,
            managers);
    }
}
