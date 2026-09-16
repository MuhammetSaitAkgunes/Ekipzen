using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.ReadModels;

/// <summary>
/// Çalışan bazlı güncel risk okuma-modeli (SQL view: vw_EmployeeRiskMetric).
/// Her çalışanın EN SON EmployeeMetricSnapshot kaydından türetilir; risk skoru ve
/// seviye eşikleri dashboard.js getDashboardMetrics() formülünün birebir SQL karşılığıdır:
///   riskScore = round(absence*0.18 + lateness*0.14 + overtime*0.20
///             + unusedLeave*0.15 + (100-pulse)*0.18 + (100-performance)*0.15)
///   attrition: pulse&lt;55 veya kritiklik&gt;85 → high; pulse&lt;65 veya kritiklik&gt;75 → medium
///   burnout:   mesai&gt;65 VE izin&gt;65 → high; mesai&gt;55 veya izin&gt;65 → medium
/// </summary>
public class EmployeeRiskMetric : ITenantScoped
{
    /// <summary>Sahip kiracı (view TenantId kolonundan; global filtre bununla izole eder).</summary>
    public int TenantId { get; set; }

    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }

    public DateOnly PeriodDate { get; set; }

    public int AbsencePct { get; set; }
    public int LatenessPct { get; set; }
    public int OvertimePct { get; set; }
    public int UnusedLeavePct { get; set; }
    public int Pulse { get; set; }
    public int Performance { get; set; }
    public int RoleCriticality { get; set; }

    public int RiskScore { get; set; }

    /// <summary>Frontend seviyesi: high | medium | low.</summary>
    public string AttritionRisk { get; set; } = "low";

    /// <summary>Frontend seviyesi: high | medium | low.</summary>
    public string BurnoutRisk { get; set; } = "low";

    public string? TrendNote { get; set; }
    public string? RecommendedAction { get; set; }
}
