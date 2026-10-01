using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Gerçek kart/belge değişikliği; eski kişisel değerler kopyalanmaz.</summary>
public sealed class GuidancePersonnelEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int? DocumentId { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public string ChangedFieldsJson { get; set; } = "[]";
    public string? Reason { get; set; }
    public string? GuidanceSnapshotJson { get; set; }
}
