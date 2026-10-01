using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Kişisel anlatım tercihi; öğrenme başarısı veya içerik sürümü kanıtı değildir.</summary>
public sealed class GuidanceHelpPreference : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string ProcessKey { get; set; } = string.Empty;
    public string Mode { get; set; } = "detailed";
    public int Revision { get; set; }
}
