namespace Ekipzen.Domain.Common;

/// <summary>
/// Convenience base combining an identity key with audit metadata.
/// </summary>
public abstract class AuditableEntity : BaseEntity, IAuditable
{
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
