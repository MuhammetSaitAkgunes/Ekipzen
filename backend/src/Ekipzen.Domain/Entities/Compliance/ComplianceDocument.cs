using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Compliance;

/// <summary>
/// Uyum/özlük belgesi takibi (KVKK açık rıza, İSG yenileme vb.). Durum iş akışı ve
/// son tarih ile denetim hazırlığını besler (complianceMetrics kaynağı).
/// </summary>
public class ComplianceDocument : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    public DateOnly? DueDate { get; set; }
    public ComplianceStatus Status { get; set; } = ComplianceStatus.Missing;
    public RiskLevel Level { get; set; } = RiskLevel.Medium;
}
