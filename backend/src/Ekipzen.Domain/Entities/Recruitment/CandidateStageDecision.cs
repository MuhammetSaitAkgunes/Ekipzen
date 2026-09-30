using Ekipzen.Domain.Common;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Recruitment;

public sealed class CandidateStageDecision : BaseEntity
{
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public CandidateStatus FromStatus { get; set; }
    public CandidateStatus ToStatus { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "[]";
    public string DecidedByUserId { get; set; } = string.Empty;
    public string DecidedByName { get; set; } = string.Empty;
    public DateTime DecidedAtUtc { get; set; }
}
