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
