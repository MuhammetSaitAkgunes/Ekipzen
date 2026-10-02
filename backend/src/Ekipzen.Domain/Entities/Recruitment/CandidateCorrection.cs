using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Guidance;

namespace Ekipzen.Domain.Entities.Recruitment;

public sealed class CandidateCorrection : BaseEntity, IGuidanceOperationEvidence
{
    public string? GuidanceSnapshotJson { get; set; }
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public string BeforeJson { get; set; } = string.Empty;
    public string AfterJson { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool EvidenceReviewConfirmed { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
