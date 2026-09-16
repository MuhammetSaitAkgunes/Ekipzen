using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Dashboard.Queries;

/// <summary>
/// Çalışan nabzı analitiği (dashboard.js employeeVoiceMetrics karşılığı).
/// Departman bazlı son ölçüm + bir önceki ölçümle düşüş karşılaştırması.
/// </summary>
public sealed record GetEmployeeVoiceQuery : IRequest<EmployeeVoiceDto>;

public sealed class GetEmployeeVoiceQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetEmployeeVoiceQuery, EmployeeVoiceDto>
{
    public async Task<EmployeeVoiceDto> Handle(
        GetEmployeeVoiceQuery request, CancellationToken cancellationToken)
    {
        var metrics = await context.EngagementMetrics
            .Include(m => m.Department)
            .OrderBy(m => m.DepartmentId).ThenByDescending(m => m.PeriodDate)
            .ToListAsync(cancellationToken);

        var byDepartment = metrics
            .GroupBy(m => m.DepartmentId)
            .Select(g => new { Latest = g.First(), Previous = g.Skip(1).FirstOrDefault() })
            .ToList();

        var departments = byDepartment
            .Select(d => new VoiceDepartmentDto(
                d.Latest.DepartmentId,
                d.Latest.Department?.Name ?? string.Empty,
                d.Latest.PulseScore,
                d.Latest.ENps,
                d.Latest.ParticipationRate,
                d.Latest.Mood,
                d.Latest.Driver,
                DashboardMappings.PulseLevelOf(d.Latest.PulseScore)))
            .OrderBy(d => d.Pulse)
            .ToList();

        var decliningTeams = byDepartment
            .Count(d => d.Previous is not null && d.Latest.PulseScore < d.Previous.PulseScore);

        var pulseScore = departments.Count == 0
            ? 0 : (int)Math.Round(departments.Average(d => d.Pulse));
        var eNps = departments.Count == 0
            ? 0 : (int)Math.Round(departments.Average(d => d.ENps));
        var participation = departments.Count == 0
            ? 0 : (int)Math.Round(departments.Average(d => d.Participation));

        var sentimentTrend = decliningTeams > 0
            ? $"Son nabız ölçümünde {decliningTeams} ekipte bağlılık geriledi"
            : "Bağlılık son ölçümde stabil seyrediyor";

        // Sinyaller: düşük nabızlı departmanların sürücü metinlerinden üretilir.
        var signals = departments
            .Where(d => d.Level != "low" && !string.IsNullOrWhiteSpace(d.Driver))
            .Select(d => $"{d.Dept} ekibinde {d.Driver} kaynaklı sinyal izleniyor.")
            .ToList();

        var recommendedActions = new List<string>
        {
            "Yönetici ile 1:1 görüşme başlat",
            "Ekip içi iş yükü kontrolü yap",
            "Takip anketi planla",
        };

        return new EmployeeVoiceDto(
            pulseScore, eNps, participation, decliningTeams, sentimentTrend,
            departments, signals, recommendedActions);
    }
}
