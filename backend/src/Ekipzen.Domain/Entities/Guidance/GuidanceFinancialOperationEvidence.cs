using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Domain.Entities.Guidance;

public sealed class GuidancePayrollPeriodEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;
    public string OperationKey { get; set; } = string.Empty;
    public string? GuidanceSnapshotJson { get; set; }
}
public sealed class GuidancePayrollRowEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int PayrollEmployeeId { get; set; }
    public PayrollEmployee PayrollEmployee { get; set; } = null!;
    public string? GuidanceSnapshotJson { get; set; }
}
public sealed class GuidanceOffboardingEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int OffboardingCaseId { get; set; }
    public EmployeeOffboardingCase OffboardingCase { get; set; } = null!;
    public int CaseVersion { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public string? GuidanceSnapshotJson { get; set; }
}
