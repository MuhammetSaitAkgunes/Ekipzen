using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.ReadModels;

/// <summary>
/// Aylık puantaj özeti okuma modeli — vw_MonthlyAttendanceSummary SQL view'ına eşlenir.
/// Fazla mesai toplamı bordro motoruna (Faz 6) girdi olur.
/// </summary>
public class MonthlyAttendanceSummary : ITenantScoped
{
    /// <summary>Sahip kiracı (view TenantId kolonundan; global filtre bununla izole eder).</summary>
    public int TenantId { get; set; }

    public int EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    public int TotalDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }

    public int TotalWorkedMinutes { get; set; }
    public int TotalOvertimeMinutes { get; set; }
}
