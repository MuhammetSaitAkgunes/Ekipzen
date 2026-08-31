using IKPro.Domain.Common;
using IKPro.Domain.Enums;

namespace IKPro.Domain.Entities.Identity;

/// <summary>
/// Tenant verisi ile Identity/dizin/e-posta yan etkileri arasındaki kalıcı teslimat kaydı.
/// İşe alım gibi iş akışları bu satırı kendi iş verileriyle aynı SaveChanges içinde
/// oluşturur; ilk deneme veya arka plan retry başarısız olsa bile niyet kaybolmaz.
/// </summary>
public sealed class IdentityProvisioningOperation : BaseEntity
{
    public IdentityProvisioningOperationType Type { get; set; }
    public IdentityProvisioningStatus Status { get; set; } = IdentityProvisioningStatus.Pending;

    public int EmployeeId { get; set; }
    public Organization.Employee? Employee { get; set; }
    public int? CandidateId { get; set; }
    public Recruitment.Candidate? Candidate { get; set; }

    public string? Email { get; set; }
    public string? DisplayName { get; set; }

    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LastError { get; set; }
}
