using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Şirket/işe alım hazırlığında yapılan kaynaklı işlem. Kimlik tarihsel referanstır; kaynak kaydı silinince yeniden atfedilmez.</summary>
public sealed class GuidancePreparationEvidence : AuditableEntity, IGuidanceOperationEvidence
{
    public string UserId { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public string ResourceKind { get; set; } = string.Empty;
    public int ResourceId { get; set; }
    public string ChangedFieldsJson { get; set; } = "[]";
    public string? GuidanceSnapshotJson { get; set; }
}
