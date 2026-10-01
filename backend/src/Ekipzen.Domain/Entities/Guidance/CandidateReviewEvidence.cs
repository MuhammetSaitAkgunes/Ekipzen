using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Recruitment;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Yeni adayın ilk aşama kararıyla aynı kayıtta üretilen kullanıcı kanıtı.</summary>
public sealed class CandidateReviewEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public CandidateStatus DecisionStatus { get; set; }
    public string? GuidanceSnapshotJson { get; set; }
}
