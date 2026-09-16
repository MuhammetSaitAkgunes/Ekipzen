using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Domain.Entities.Analytics;

/// <summary>
/// Departman bazlı çalışan nabzı / eNPS ölçümü (employeeVoiceMetrics kaynağı).
/// </summary>
public class EngagementMetric : AuditableEntity
{
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    public DateOnly PeriodDate { get; set; }

    public int PulseScore { get; set; }
    public int ENps { get; set; }
    public int ParticipationRate { get; set; }
    public string? Mood { get; set; }
    public string? Driver { get; set; }
}
