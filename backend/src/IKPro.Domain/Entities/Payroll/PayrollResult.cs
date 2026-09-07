using IKPro.Domain.Common;

namespace IKPro.Domain.Entities.Payroll;

/// <summary>
/// Bir bordro girdisi için hesaplanmış brüt→net sonucu (snapshot). IPayrollEngine
/// çıktısıyla birebir alanlar. Onay anında kalıcılaştırılır.
/// </summary>
public class PayrollResult : AuditableEntity
{
    public int PayrollEmployeeId { get; set; }
    public PayrollEmployee? PayrollEmployee { get; set; }

    public decimal HourlyRate { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal AdditionalPay { get; set; }
    public decimal BaseGross { get; set; }
    public decimal GrossEarnings { get; set; }

    public decimal SgkBase { get; set; }
    public decimal SgkEmployee { get; set; }
    public decimal UnemploymentEmployee { get; set; }

    public decimal IncomeTaxBase { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal StampTax { get; set; }

    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public decimal EmployerSgk { get; set; }
    public decimal EmployerUnemployment { get; set; }
    public decimal EmployerCost { get; set; }

    public decimal SgkExemptMealAllowance { get; set; }
    public decimal IncomeTaxExemptMealAllowance { get; set; }
    public decimal MinimumWageIncomeTaxExemption { get; set; }
    public decimal MinimumWageStampTaxExemption { get; set; }
    public decimal AppliedEmployerSgkRate { get; set; }
    public decimal DisabilityIncomeTaxBaseDeduction { get; set; }

    /// <summary>Uyarı bayrakları (JSON dizi olarak saklanır).</summary>
    public string? WarningsJson { get; set; }

    public DateTime CalculatedAtUtc { get; set; }
}
