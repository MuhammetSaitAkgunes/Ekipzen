using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Domain.Entities.Payroll;

/// <summary>Onaylı bordro sonucunun geçmişi silinmeyen parasal düzeltme sürümü.</summary>
public sealed class PayrollApprovedRowRevision : AuditableEntity
{
    public int PayrollRowId { get; set; }
    public PayrollEmployee? PayrollRow { get; set; }
    public int RemediationCaseId { get; set; }
    public EmployeeCompensationRemediationCase? RemediationCase { get; set; }
    public int RevisionNumber { get; set; }
    public string BeforeJson { get; set; } = string.Empty;
    public string AfterJson { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ExpertReviewReference { get; set; } = string.Empty;
    public string ReviewNotes { get; set; } = string.Empty;
    public string ApprovedByUserId { get; set; } = string.Empty;
    public DateTime ApprovedAtUtc { get; set; }
}
