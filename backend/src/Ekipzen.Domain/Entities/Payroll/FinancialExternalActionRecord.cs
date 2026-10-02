using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Domain.Entities.Payroll;

/// <summary>İlk onaylı finansal sonucun eklemeli dış ödeme/bildirim kanıtı.</summary>
public sealed class FinancialExternalActionRecord : BaseEntity
{
    public int? PayrollRowId { get; set; }
    public PayrollEmployee? PayrollRow { get; set; }
    public int? OffboardingCaseId { get; set; }
    public EmployeeOffboardingCase? OffboardingCase { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int? PreviousRecordId { get; set; }
    public string ResponsibleName { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public DateOnly? ActionDate { get; set; }
    public decimal? RecordedAmount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}
