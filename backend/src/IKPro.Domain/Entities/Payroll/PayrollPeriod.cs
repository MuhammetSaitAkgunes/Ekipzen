using IKPro.Domain.Common;
using IKPro.Domain.Enums;

namespace IKPro.Domain.Entities.Payroll;

/// <summary>
/// Bordro dönemi (ör. "Nisan 2026"). Her dönem kendi çalışan bordro girdilerini
/// ve kullandığı ayar/vergi dilimi setini barındırır.
/// </summary>
public class PayrollPeriod : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Draft;

    /// <summary>Bu dönemde geçerli ayar seti.</summary>
    public int PayrollSettingsId { get; set; }
    public PayrollSettings? Settings { get; set; }

    /// <summary>Dönem ayından önceki takvim aylarının asgari ücret vergi matrahı toplamı.</summary>
    public decimal MinimumWageIncomeTaxBaseBefore { get; set; }

    public ICollection<PayrollEmployee> Employees { get; set; } = new List<PayrollEmployee>();
}
