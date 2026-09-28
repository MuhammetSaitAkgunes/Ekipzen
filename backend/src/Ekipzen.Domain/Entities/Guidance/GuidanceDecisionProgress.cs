using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Kullanıcı ve içerik sürümü bazında son alıştırma yanıtı; işlem kanıtı değildir.</summary>
public sealed class GuidanceDecisionProgress : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string ProcessKey { get; set; } = string.Empty;
    public string StepKey { get; set; } = string.Empty;
    public int ContentVersion { get; set; }
    public int AttemptCount { get; set; }
    public int LastSelectedChoice { get; set; }
    public bool LastCorrect { get; set; }
    public DateTime LastAnsweredAtUtc { get; set; }
}
