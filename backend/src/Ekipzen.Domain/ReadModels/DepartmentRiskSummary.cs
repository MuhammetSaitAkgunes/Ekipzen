using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.ReadModels;

/// <summary>
/// Departman bazlı risk agregasyonu (SQL view: vw_DepartmentRisk).
/// vw_EmployeeRiskMetric üzerinden ortalama risk skoru ve yüksek riskli sayıları toplar
/// (dashboard.js departmentRisk kaynağı).
/// </summary>
public class DepartmentRiskSummary : ITenantScoped
{
    /// <summary>Sahip kiracı (view TenantId kolonundan; global filtre bununla izole eder).</summary>
    public int TenantId { get; set; }

    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int RiskScore { get; set; }
    public int HighAttritionCount { get; set; }
    public int HighBurnoutCount { get; set; }
}
