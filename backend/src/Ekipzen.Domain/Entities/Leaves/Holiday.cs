using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Leaves;

/// <summary>
/// Resmi/şirket tatili. İş-günü izin hesabında (SQL function) hariç tutulur.
/// </summary>
public class Holiday : AuditableEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsOfficial { get; set; } = true;
}
