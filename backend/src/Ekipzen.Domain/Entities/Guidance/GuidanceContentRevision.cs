namespace Ekipzen.Domain.Entities.Guidance;

// Merkezî ürün içeriği: müşteri/personel/öğrenme verisi değildir; PlatformDbContext'te saklanır.
public class GuidanceContentRevision
{
    public int Id { get; set; }
    public string ContentKey { get; set; } = string.Empty;
    public string ProcessKey { get; set; } = string.Empty;
    public int ContentVersion { get; set; }
    public int Revision { get; set; } = 1;
    public int? BasedOnId { get; set; }
    public string State { get; set; } = "draft";
    public string BodyJson { get; set; } = string.Empty;
    public string ContributorsJson { get; set; } = "[]";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastEditedAtUtc { get; set; }
    public string? ExpertName { get; set; }
    public string? ExpertRole { get; set; }
    public string? ExpertReference { get; set; }
    public DateOnly? ReviewedOn { get; set; }
    public DateOnly? ReviewDueOn { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public ICollection<GuidanceContentEvent> Events { get; set; } = new List<GuidanceContentEvent>();
}

public class GuidanceContentEvent
{
    public int Id { get; set; }
    public int ContentId { get; set; }
    public GuidanceContentRevision? Content { get; set; }
    public int Revision { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime AtUtc { get; set; }
}
