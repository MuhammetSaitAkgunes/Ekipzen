using Ekipzen.Domain.Common;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Actions;

/// <summary>
/// Aksiyon merkezindeki birleşik görev (risk/bordro/uyum/çalışan deneyimi kaynaklı).
/// </summary>
public class GlobalAction : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? SourceRoute { get; set; }
    public string Owner { get; set; } = string.Empty;

    /// <summary>Son tarih etiketi (Bugün | Bu hafta | Tamamlandı).</summary>
    public string? Due { get; set; }

    public ActionPriority Priority { get; set; } = ActionPriority.Medium;
    public ActionStatus Status { get; set; } = ActionStatus.Open;

    /// <summary>Önerilen aksiyon metni.</summary>
    public string? RecommendedAction { get; set; }
}
