using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Actions;

/// <summary>
/// Append-only denetim kaydı. Mutasyon yapan işlemlerce (interceptor + SQL trigger)
/// doldurulur; aksiyon merkezindeki denetim zaman çizelgesini besler.
/// </summary>
public class AuditLog : BaseEntity
{
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
