using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Leaves;

/// <summary>
/// İzin talebi. Çalışan oluşturur (pending), yönetici onaylar/reddeder.
/// </summary>
public class LeaveRequest : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int EmploymentPeriodId { get; set; }
    public EmployeeEmploymentPeriod? EmploymentPeriod { get; set; }

    public int LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Days { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public string? Description { get; set; }

    /// <summary>Yerine bakacak kişi.</summary>
    public int? SubstituteEmployeeId { get; set; }
    public Employee? SubstituteEmployee { get; set; }

    // Onay bilgisi
    public string? DecisionByUserId { get; set; }
    public DateTime? DecisionAtUtc { get; set; }
    public string? DecisionNote { get; set; }

    public ICollection<LeaveLedgerEntry> LedgerEntries { get; set; } = new List<LeaveLedgerEntry>();
}
