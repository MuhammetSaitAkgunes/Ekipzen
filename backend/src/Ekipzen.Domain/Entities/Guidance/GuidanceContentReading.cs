using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Kişinin belirli yayımlanmış sürümü incelediğine dair beyanı; iş/yeterlilik kanıtı değildir.</summary>
public sealed class GuidanceContentReading : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    // Platform veritabanındaki sürüm kimliği; kiracı veritabanları arasında FK kurulmaz.
    public int ContentRevisionId { get; set; }
    public string ContentKey { get; set; } = string.Empty;
    public string ProcessKey { get; set; } = string.Empty;
    public int ContentVersion { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime FirstReadAtUtc { get; set; }
}
