using Ekipzen.Domain.Common;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Organization;

/// <summary>
/// İşten çıkışın kalıcı süreç kaydı. Parasal kalemler E-5A'da uzman/harici
/// kaynaktan alınır; otomatik hukuki hesap izlenimi verilmez.
/// </summary>
public sealed class EmployeeOffboardingCase : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public EmployeeOffboardingStatus Status { get; set; } = EmployeeOffboardingStatus.Draft;
    public int Version { get; set; } = 1;

    public DateOnly TerminationDate { get; set; }
    public string ExitCode { get; set; } = string.Empty;
    public string ExitReason { get; set; } = string.Empty;
    public int? SuccessorManagerId { get; set; }
    public TerminationReason TerminationReason { get; set; } = TerminationReason.Other;
    public NoticeHandling NoticeHandling { get; set; } = NoticeHandling.NotApplicable;
    public EmploymentContractType ContractTypeSnapshot { get; set; }
    public int? EmploymentPeriodId { get; set; }
    public int? CompensationPackageId { get; set; }
    public DateOnly? CompensationPackagePreviousEffectiveTo { get; set; }
    public decimal MonthlyGrossSalarySnapshot { get; set; }
    public decimal RegularPremiumSnapshot { get; set; }
    public decimal RoadAllowanceSnapshot { get; set; }
    public decimal MealAllowanceSnapshot { get; set; }
    public decimal OtherRegularBenefitsSnapshot { get; set; }

    public decimal UnusedLeaveDays { get; set; }
    public decimal UnusedLeavePayment { get; set; }
    public decimal SeverancePayment { get; set; }
    public decimal NoticePayment { get; set; }
    public OffboardingSettlementSource SettlementSource { get; set; }
        = OffboardingSettlementSource.ExternalExpert;
    public string SettlementReference { get; set; } = string.Empty;
    public string? SettlementNotes { get; set; }
    public int? CalculationRuleId { get; set; }
    public TerminationCalculationRule? CalculationRule { get; set; }
    public int? CalculationRuleVersion { get; set; }
    public DateTime? CalculationGeneratedAtUtc { get; set; }
    public string? CalculationGeneratedByUserId { get; set; }

    /// <summary>Geri almada personel profilini güvenle eski haline döndürmek için snapshot.</summary>
    public string? PreviousExitCode { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletedByUserId { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversedByUserId { get; set; }
    public string? ReversalReason { get; set; }

    public ICollection<EmployeeOffboardingTeamTransfer> TeamTransfers { get; set; }
        = new List<EmployeeOffboardingTeamTransfer>();
}

/// <summary>
/// Tamamlama sırasında değiştirilen her yönetici bağının önce/sonra snapshot'ı.
/// Geri alma yalnız bağ hâlâ beklenen son durumda ise uygulanır.
/// </summary>
public sealed class EmployeeOffboardingTeamTransfer : BaseEntity
{
    public int OffboardingCaseId { get; set; }
    public EmployeeOffboardingCase? OffboardingCase { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int? PreviousManagerId { get; set; }
    public int? AssignedManagerId { get; set; }
}

/// <summary>Tamamlanmış ayrılışın tarihsel kaydını değiştirmeden geçerli parasal hesabı sürümler.</summary>
public sealed class EmployeeOffboardingSettlementRevision : BaseEntity
{
    public int OffboardingCaseId { get; set; }
    public EmployeeOffboardingCase? OffboardingCase { get; set; }
    public int RemediationCaseId { get; set; }
    public EmployeeCompensationRemediationCase? RemediationCase { get; set; }
    public int RevisionNumber { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "draft";
    public int? BasedOnApprovedRevisionId { get; set; }
    public decimal MonthlyGrossSalarySnapshot { get; set; }
    public decimal RegularPremiumSnapshot { get; set; }
    public decimal RoadAllowanceSnapshot { get; set; }
    public decimal MealAllowanceSnapshot { get; set; }
    public decimal OtherRegularBenefitsSnapshot { get; set; }
    public decimal UnusedLeaveDays { get; set; }
    public decimal UnusedLeavePayment { get; set; }
    public decimal SeverancePayment { get; set; }
    public decimal NoticePayment { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SettlementReference { get; set; } = string.Empty;
    public string? SettlementNotes { get; set; }
    public string OpenedByUserId { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ExpertReviewReference { get; set; }
    public string? ReviewNotes { get; set; }
}

/// <summary>
/// Onaylı parasal sürümün uygulama dışı ödeme/bildirim sonucuna ilişkin değişmez kanıt olayı.
/// Yeni kayıt önceki sonucu geçersiz kılar; eski kanıt silinmez.
/// </summary>
public sealed class EmployeeOffboardingExternalActionRecord : BaseEntity
{
    public int SettlementRevisionId { get; set; }
    public EmployeeOffboardingSettlementRevision? SettlementRevision { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int? PreviousRecordId { get; set; }
    public string ResponsibleName { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public DateOnly? ActionDate { get; set; }
    public decimal? RecordedAmount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}
