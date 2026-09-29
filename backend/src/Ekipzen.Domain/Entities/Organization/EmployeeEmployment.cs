using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Organization;

/// <summary>Bir çalışanın kesintisiz istihdam dönemi; yeniden işe girişler ayrı satırdır.</summary>
public class EmployeeEmploymentPeriod : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Enums.EmploymentContractType ContractType { get; set; }

    /// <summary>Bu dönem için her hizmet yıldönümünde yazılacak şirket politika günü.</summary>
    public int AnnualLeaveEntitlementDays { get; set; } = 24;
    /// <summary>Ledger'a otomatik yıldönümü yazımının başlayacağı geçiş tarihi.</summary>
    public DateOnly? LeaveAccrualManagedFrom { get; set; }

    public ICollection<EmployeeCompensationPackage> CompensationPackages { get; set; }
        = new List<EmployeeCompensationPackage>();
    public ICollection<Ekipzen.Domain.Entities.Leaves.LeaveLedgerEntry> LeaveLedgerEntries { get; set; }
        = new List<Ekipzen.Domain.Entities.Leaves.LeaveLedgerEntry>();
}

/// <summary>
/// Çalışanın tarih etkili, brüt ve düzenli ücret paketi. Bordro bu kaydı doğrudan
/// değiştirmez; dönem satırına snapshot alır.
/// </summary>
public class EmployeeCompensationPackage : AuditableEntity
{
    public int EmploymentPeriodId { get; set; }
    public EmployeeEmploymentPeriod? EmploymentPeriod { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal MonthlyGrossSalary { get; set; }
    public decimal RegularPremium { get; set; }
    public decimal RoadAllowance { get; set; }
    public decimal MealAllowance { get; set; }
    public decimal OtherRegularBenefits { get; set; }
    public string Currency { get; set; } = "TRY";
    public string SourceReference { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
}

/// <summary>Ücret paketi düzeltmesinin silinmeyen önce/sonra kaydı.</summary>
public sealed class EmployeeCompensationPackageCorrection : BaseEntity
{
    public int CompensationPackageId { get; set; }
    public EmployeeCompensationPackage? CompensationPackage { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public string BeforeJson { get; set; } = string.Empty;
    public string AfterJson { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public DateTime CorrectedAtUtc { get; set; }
}

/// <summary>Geçmiş bordro/ayrılış kaydını değiştirmeyen, ayrı uzman inceleme dosyası.</summary>
public sealed class EmployeeCompensationRemediationCase : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int CompensationPackageId { get; set; }
    public EmployeeCompensationPackage? CompensationPackage { get; set; }
    public int PackageVersion { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "draft";
    public string IssueDescription { get; set; } = string.Empty;
    public DateOnly? ProposedEffectiveFrom { get; set; }
    public string ProposedValuesJson { get; set; } = string.Empty;
    public string ImpactSnapshotJson { get; set; } = string.Empty;
    public string OpenedByUserId { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ExpertReviewReference { get; set; }
    public string? ReviewNotes { get; set; }
}

/// <summary>Onaylı telafi dosyasının açık bordro satırına uygulanmış, silinmeyen kaydı.</summary>
public sealed class EmployeeCompensationPayrollApplication : BaseEntity
{
    public int RemediationCaseId { get; set; }
    public EmployeeCompensationRemediationCase? RemediationCase { get; set; }
    public int PayrollRowId { get; set; }
    public Ekipzen.Domain.Entities.Payroll.PayrollEmployee? PayrollRow { get; set; }
    public string BeforeJson { get; set; } = string.Empty;
    public string AfterJson { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
}

/// <summary>Son ücret paketinin yürürlük tarihi değişikliğine ait kalıcı geçmiş.</summary>
public sealed class EmployeeCompensationDateCorrection : BaseEntity
{
    public int CompensationPackageId { get; set; }
    public EmployeeCompensationPackage? CompensationPackage { get; set; }
    public int? PreviousPackageId { get; set; }
    public DateOnly OldEffectiveFrom { get; set; }
    public DateOnly NewEffectiveFrom { get; set; }
    public DateOnly? OldPreviousEffectiveTo { get; set; }
    public DateOnly? NewPreviousEffectiveTo { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public DateTime CorrectedAtUtc { get; set; }
}
