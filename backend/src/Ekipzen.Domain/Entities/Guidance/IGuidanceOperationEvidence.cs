namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Yalnız gerçek işlem sırasında doğrulanan merkezî içerik sürümünün değişmez özeti.</summary>
public interface IGuidanceOperationEvidence
{
    string? GuidanceSnapshotJson { get; set; }
}
