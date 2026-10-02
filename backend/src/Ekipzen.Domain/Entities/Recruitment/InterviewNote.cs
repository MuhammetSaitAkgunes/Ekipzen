using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Guidance;

namespace Ekipzen.Domain.Entities.Recruitment;

/// <summary>Aday için mülakat/görüşme notu (Teknik Mülakat | İK Görüşmesi).</summary>
public class InterviewNote : AuditableEntity, IGuidanceOperationEvidence
{
    public string? GuidanceSnapshotJson { get; set; }
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public string? AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string NoteType { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
