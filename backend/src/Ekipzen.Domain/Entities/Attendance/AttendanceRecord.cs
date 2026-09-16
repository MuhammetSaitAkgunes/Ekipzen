using Ekipzen.Domain.Common;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Attendance;

/// <summary>
/// Çalışan-gün bazlı puantaj kaydı. Canlı yoklama panosu = bugünün kayıtları;
/// aylık puantaj = ilgili aya ait kayıtlar. Fazla mesai bordroya beslenir.
/// </summary>
public class AttendanceRecord : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateOnly WorkDate { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public int BreakMinutes { get; set; }

    /// <summary>Net çalışılan dakika (çıkış - giriş - mola).</summary>
    public int WorkedMinutes { get; set; }
    public int OvertimeMinutes { get; set; }

    public TimesheetType Type { get; set; } = TimesheetType.Full;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.OnTime;
    public string? Note { get; set; }
}
