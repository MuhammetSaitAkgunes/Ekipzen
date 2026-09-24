using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Domain.Entities.Guidance;

/// <summary>Çalışan oluşturma işlemiyle aynı kayıtta üretilen kullanıcı kanıtı.</summary>
public sealed class EmployeeCreationEvidence : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
}
