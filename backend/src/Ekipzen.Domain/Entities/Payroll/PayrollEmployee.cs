using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Payroll;

/// <summary>
/// Bir dönemdeki bir çalışanın bordro girdisi. Brüt→net sonucu IPayrollEngine ile
/// hesaplanır (Faz 6); onaylandığında sonuç <see cref="PayrollResult"/> olarak saklanır.
/// </summary>
public class PayrollEmployee : AuditableEntity
{
    public int PayrollPeriodId { get; set; }
    public PayrollPeriod? Period { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public decimal GrossSalary { get; set; }
    public PayrollStatutoryProfile StatutoryProfile { get; set; }
        = PayrollStatutoryProfile.Unspecified;
    public int WorkedDays { get; set; }
    public bool SgkPremiumDaysConfirmed { get; set; }
    public int OvertimeHours { get; set; }
    public bool OvertimeTreatmentConfirmed { get; set; }
    public decimal PremiumPay { get; set; }
    public decimal RoadAllowance { get; set; }
    public decimal MealAllowance { get; set; }
    public int MealDays { get; set; }
    public PayrollMealTreatment MealTreatment { get; set; }
        = PayrollMealTreatment.Unspecified;
    public decimal BenefitPay { get; set; }
    public decimal SpecialDeductions { get; set; }

    /// <summary>Önceki dönemlere ait, sunucu tarafından üretilen kümülatif gelir vergisi matrahı.</summary>
    public decimal PreviousTaxBase { get; set; }

    /// <summary>
    /// Takvim yılı içinde işveren değişikliğinde, çalışanın talebi ve işverenin kabulüyle
    /// ilk Ekipzen bordrosuna taşınan önceki işveren gelir vergisi matrahı.
    /// </summary>
    public decimal PreviousEmployerIncomeTaxBase { get; set; }
    public string? PreviousEmployerIncomeTaxBaseReference { get; set; }
    public bool CumulativeIncomeTaxBaseConfirmed { get; set; }

    public bool ApplyMinimumWageTaxExemption { get; set; } = true;
    public EmployerSgkDiscountType EmployerSgkDiscount { get; set; }
        = EmployerSgkDiscountType.None;
    public string? EmployerSgkDiscountReference { get; set; }
    public bool BenefitPayFullyTaxableConfirmed { get; set; }
    public PayrollDisabilityReliefType DisabilityReliefType { get; set; }
        = PayrollDisabilityReliefType.Unspecified;
    public string? DisabilityReliefReference { get; set; }
    public bool OtherIncomeTaxBaseDeductionsConfirmedNone { get; set; }

    public bool IbanComplete { get; set; }
    public bool TimesheetComplete { get; set; }
    public string? ReviewedTimesheetRevision { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? CurrentTimesheetRevision { get; set; }

    public PayrollApprovalStatus ApprovalStatus { get; set; } = PayrollApprovalStatus.PreCalc;
    public string? Notes { get; set; }

    public PayrollResult? Result { get; set; }
}
