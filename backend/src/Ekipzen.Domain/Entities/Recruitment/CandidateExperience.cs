using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Recruitment;

/// <summary>Adayın iş geçmişi kaydı.</summary>
public class CandidateExperience : BaseEntity
{
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Period { get; set; }
    public string? Description { get; set; }
}
