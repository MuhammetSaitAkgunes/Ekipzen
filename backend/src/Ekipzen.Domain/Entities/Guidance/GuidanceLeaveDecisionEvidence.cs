using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Guidance;

public sealed class GuidanceLeaveDecisionEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int LeaveRequestId { get; set; }
    public LeaveRequest LeaveRequest { get; set; } = null!;
    public LeaveStatus DecisionStatus { get; set; }
    public string? GuidanceSnapshotJson { get; set; }
}
