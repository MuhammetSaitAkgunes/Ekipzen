using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Leaves;

/// <summary>
/// İzin tipi (frontend: Yıllık İzin | Mazeret İzni | Raporlu | Uzaktan).
/// Yıllık bakiyeden düşülüp düşülmediği <see cref="DeductsFromAnnualBalance"/> ile belirlenir.
/// </summary>
public class LeaveType : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool DeductsFromAnnualBalance { get; set; }
    public bool RequiresApproval { get; set; } = true;

    public ICollection<LeaveRequest> Requests { get; set; } = new List<LeaveRequest>();
}
