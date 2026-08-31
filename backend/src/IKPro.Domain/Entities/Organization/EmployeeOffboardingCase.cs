using IKPro.Domain.Common;
using IKPro.Domain.Enums;

namespace IKPro.Domain.Entities.Organization;

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

    public decimal UnusedLeaveDays { get; set; }
    public decimal UnusedLeavePayment { get; set; }
    public decimal SeverancePayment { get; set; }
    public decimal NoticePayment { get; set; }
    public OffboardingSettlementSource SettlementSource { get; set; }
        = OffboardingSettlementSource.ExternalExpert;
    public string SettlementReference { get; set; } = string.Empty;
    public string? SettlementNotes { get; set; }

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
