using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Recruitment;

/// <summary>Aday değerlendirme kriteri ve puanı (ör. Teknik Yeterlilik 4/5).</summary>
public class CandidateEvaluation : BaseEntity
{
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public string Criterion { get; set; } = string.Empty;
    public int Score { get; set; }
    public int MaxScore { get; set; } = 5;
}
