using Ekipzen.Domain.Common;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Recruitment;

/// <summary>
/// İşe alım adayı. Pipeline: Yeni → Mülakat → Teklif → Red/İşe Alındı.
/// </summary>
public class Candidate : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string AppliedRole { get; set; } = string.Empty;

    public int? PositionId { get; set; }
    public Position? Position { get; set; }

    public CandidateStatus Status { get; set; } = CandidateStatus.New;
    public int Version { get; set; } = 1;

    /// <summary>İK kullanıcısının elle kaydettiği aday puanı (0-100).</summary>
    public int Score { get; set; }

    public DateTime AppliedAtUtc { get; set; }
    public string? Location { get; set; }
    public int ExperienceYears { get; set; }
    public string? Summary { get; set; }

    public ICollection<CandidateSkill> Skills { get; set; } = new List<CandidateSkill>();
    public ICollection<CandidateExperience> Experiences { get; set; } = new List<CandidateExperience>();
    public ICollection<InterviewNote> Notes { get; set; } = new List<InterviewNote>();
    public ICollection<CandidateEvaluation> Evaluations { get; set; } = new List<CandidateEvaluation>();
    public ICollection<CandidateHistory> History { get; set; } = new List<CandidateHistory>();
}
