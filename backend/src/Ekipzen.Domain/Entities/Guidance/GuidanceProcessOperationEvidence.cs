using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Attendance;
using Ekipzen.Domain.Entities.Compliance;

namespace Ekipzen.Domain.Entities.Guidance;

public sealed class GuidanceSetupEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int TemplateYear { get; set; }
    public bool AddedCompanyProfile { get; set; }
    public int AddedLeaveTypeCount { get; set; }
    public int AddedHolidayCount { get; set; }
    public bool AddedPayrollSettings { get; set; }
    public string? GuidanceSnapshotJson { get; set; }
}

public sealed class GuidanceAttendanceEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;
    public string OperationKey { get; set; } = string.Empty;
    public string? GuidanceSnapshotJson { get; set; }
}

public sealed class GuidanceComplianceEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int ComplianceDocumentId { get; set; }
    public ComplianceDocument ComplianceDocument { get; set; } = null!;
    public string OperationKey { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? GuidanceSnapshotJson { get; set; }
}
