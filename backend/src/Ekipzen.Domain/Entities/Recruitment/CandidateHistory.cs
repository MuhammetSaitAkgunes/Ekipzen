using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Recruitment;

/// <summary>Aday zaman çizelgesi olayı (durum değişikliği, mülakat vb.).</summary>
public class CandidateHistory : BaseEntity
{
    public int CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public string Event { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
