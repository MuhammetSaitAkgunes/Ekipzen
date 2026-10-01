using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Guidance;

// Kişisel öğrenme kaydı; gerçek işlem veya yetkinlik onayı değildir. Platforma çapraz DB FK kurulmaz.
public sealed class GuidancePublishedPracticeProgress : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public int ContentRevisionId { get; set; }
    public string ContentKey { get; set; } = string.Empty;
    public string ProcessKey { get; set; } = string.Empty;
    public string StepKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int ContentVersion { get; set; }
    public int AttemptCount { get; set; }
    public int LastSelectedChoice { get; set; }
    public bool LastCorrect { get; set; }
    public DateTime LastAnsweredAtUtc { get; set; }
    public Guid LastAttemptId { get; set; }
}
