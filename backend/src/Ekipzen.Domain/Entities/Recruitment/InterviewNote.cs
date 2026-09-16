using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Recruitment;

/// <summary>Aday için mülakat/görüşme notu (Teknik Mülakat | İK Görüşmesi).</summary>
public class InterviewNote : AuditableEntity
{
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public string? AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string NoteType { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
