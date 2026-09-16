using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.ReadModels;

/// <summary>
/// Bordro dönemi özeti okuma modeli — vw_PayrollPeriodSummary SQL view'ına eşlenir.
/// Tutarlar onaylı satırların kalıcı sonuçlarından (PayrollResults) toplanır.
/// </summary>
public class PayrollPeriodSummary : ITenantScoped
{
    /// <summary>Sahip kiracı (view TenantId kolonundan; global filtre bununla izole eder).</summary>
    public int TenantId { get; set; }

    public int PayrollPeriodId { get; set; }
    public int EmployeeCount { get; set; }
    public int ApprovedCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalEmployerCost { get; set; }
}
