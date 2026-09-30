using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Recruitment;

public sealed class CandidateEvidenceRevision : BaseEntity
{
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public int? InterviewNoteId { get; set; }
    public InterviewNote? InterviewNote { get; set; }
    public int? EvaluationId { get; set; }
    public CandidateEvaluation? Evaluation { get; set; }
    public int Version { get; set; }
    public int FromCandidateVersion { get; set; }
    public int ToCandidateVersion { get; set; }
    public string BeforeJson { get; set; } = string.Empty;
    public string AfterJson { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ChangedByUserId { get; set; } = string.Empty;
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
