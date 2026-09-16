using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Domain.Entities.Analytics;

/// <summary>
/// Çalışan bazlı dönemsel risk girdileri (dashboard getDashboardMetrics kaynağı).
/// Risk skoru / attrition / burnout bu girdilerden SQL view/SP ile türetilir (Faz 8).
/// </summary>
public class EmployeeMetricSnapshot : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateOnly PeriodDate { get; set; }

    public int AbsencePct { get; set; }
    public int LatenessPct { get; set; }
    public int OvertimePct { get; set; }
    public int UnusedLeavePct { get; set; }

    public int Pulse { get; set; }
    public int Performance { get; set; }
    public int RoleCriticality { get; set; }

    /// <summary>Son sinyal özeti (dashboard.js employee.trend karşılığı).</summary>
    public string? TrendNote { get; set; }

    /// <summary>Önerilen takip aksiyonu (dashboard.js employee.action karşılığı).</summary>
    public string? RecommendedAction { get; set; }
}
