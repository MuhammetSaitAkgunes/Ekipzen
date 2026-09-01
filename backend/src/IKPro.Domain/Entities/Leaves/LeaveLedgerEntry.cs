using IKPro.Domain.Common;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Enums;

namespace IKPro.Domain.Entities.Leaves;

/// <summary>
/// Yıllık izin için değiştirilmeyen, imzalı hareket kaydı. Pozitif günler hak
/// kazandırır, negatif günler kullanımı gösterir; bakiye hareketlerin toplamıdır.
/// </summary>
public sealed class LeaveLedgerEntry : AuditableEntity
{
    public int EmploymentPeriodId { get; set; }
    public EmployeeEmploymentPeriod? EmploymentPeriod { get; set; }

    public int? LeaveRequestId { get; set; }
    public LeaveRequest? LeaveRequest { get; set; }

    public DateOnly OccurredOn { get; set; }
    public LeaveLedgerEntryType Type { get; set; }
    public int Days { get; set; }
    public int? ServiceYear { get; set; }

    /// <summary>Tenant içinde idempotans anahtarı.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>Karar, talep veya geçiş kaynağını açıklayan okunabilir referans.</summary>
    public string SourceReference { get; set; } = string.Empty;
}
