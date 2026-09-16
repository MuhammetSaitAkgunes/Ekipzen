using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Recruitment;

/// <summary>Aday becerisi (ör. React.js, TypeScript).</summary>
public class CandidateSkill : BaseEntity
{
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public string Name { get; set; } = string.Empty;
}
