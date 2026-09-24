using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Bir kullanıcının rehberde bıraktığı yer; öğrenme başarısı değildir.</summary>
public sealed class GuidanceBookmark : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string ProcessKey { get; set; } = string.Empty;
    public string StepKey { get; set; } = string.Empty;
    public int ContentVersion { get; set; }
}
