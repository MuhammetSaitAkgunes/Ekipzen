using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Guidance;

namespace Ekipzen.Domain.Entities.Organization;

/// <summary>Aynı kullanıcının aynı aktarımının tekrarında kayıt açılmasını önler.</summary>
public sealed class EmployeeImportReceipt : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public Guid ImportRequestId { get; set; }
    public string FileFingerprint { get; set; } = string.Empty;
    public string PreviewToken { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public string? GuidanceSnapshotJson { get; set; }
}
