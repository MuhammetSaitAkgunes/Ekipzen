namespace Ekipzen.Domain.Common;

/// <summary>
/// Entities implementing this are stamped with create/update audit metadata by the
/// EF Core SaveChanges interceptor (see Infrastructure).
/// </summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    string? UpdatedBy { get; set; }
}
