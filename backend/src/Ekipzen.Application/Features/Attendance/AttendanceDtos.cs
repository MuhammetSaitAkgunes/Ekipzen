using Ekipzen.Domain.Enums;

namespace Ekipzen.Application.Features.Attendance;

/// <summary>Canlı yoklama kartı — attendance.js dailyMoves şekli (status: ontime|late|absent|early).</summary>
public sealed record LiveBoardCardDto(
    int EmployeeId,
    string Name,
    string Initials,
    string Department,
    TimeOnly? CheckIn,
    string Status);

/// <summary>Aylık puantaj satırı — attendance.js timesheetData şekli.</summary>
public sealed record TimesheetRowDto(
    int Id,
    DateOnly WorkDate,
    string Type,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    int BreakMinutes,
    int WorkedMinutes,
    int OvertimeMinutes,
    string Status,
    string? Note);

/// <summary>Bir çalışanın aylık puantajı + ay toplamları.</summary>
public sealed record TimesheetDto(
    int EmployeeId,
    string EmployeeName,
    int Year,
    int Month,
    IReadOnlyList<TimesheetRowDto> Rows,
    int TotalWorkedMinutes,
    int TotalOvertimeMinutes);

/// <summary>Aylık özet satırı (SQL view) — fazla mesai bordroya beslenir.</summary>
public sealed record AttendanceSummaryDto(
    int EmployeeId,
    string EmployeeName,
    string Department,
    int TotalDays,
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int TotalWorkedMinutes,
    int TotalOvertimeMinutes);

public static class AttendanceMappings
{
    public static string ToDto(this AttendanceStatus status) => status switch
    {
        AttendanceStatus.OnTime => "ontime",
        AttendanceStatus.Late => "late",
        AttendanceStatus.Absent => "absent",
        AttendanceStatus.Early => "early",
        _ => status.ToString().ToLowerInvariant(),
    };

    /// <summary>Puantaj tipi frontend etiketleri: Tam | Mesai | Rapor.</summary>
    public static string ToDto(this TimesheetType type) => type switch
    {
        TimesheetType.Full => "Tam",
        TimesheetType.Overtime => "Mesai",
        TimesheetType.Leave => "Rapor",
        _ => type.ToString(),
    };

    public static TimesheetType ParseType(string? value) => value switch
    {
        null or "" => TimesheetType.Full,
        "Tam" => TimesheetType.Full,
        "Mesai" => TimesheetType.Overtime,
        "Rapor" => TimesheetType.Leave,
        _ => throw new ArgumentException($"Geçersiz puantaj tipi: {value} (Tam|Mesai|Rapor)."),
    };

    /// <summary>Satır durumu (ok|late|overtime|absent) — attendance.js timesheet kolonu.</summary>
    public static string ToTimesheetStatus(AttendanceStatus status, int overtimeMinutes) => status switch
    {
        AttendanceStatus.Absent => "absent",
        AttendanceStatus.Late => "late",
        _ when overtimeMinutes > 0 => "overtime",
        _ => "ok",
    };
}

/// <summary>
/// Puantaj hesap kuralları (tek kaynak): standart gün 09:00–18:00, 60 dk mola → 480 dk.
/// Giriş &lt; 08:45 erken, ≤ 09:00 zamanında, &gt; 09:00 geç; giriş yoksa devamsız.
/// </summary>
public static class AttendanceCalculator
{
    public const int StandardWorkMinutes = 480;
    private static readonly TimeOnly EarlyBefore = new(8, 45);
    private static readonly TimeOnly WorkStart = new(9, 0);

    public static (int WorkedMinutes, int OvertimeMinutes, AttendanceStatus Status) Compute(
        TimeOnly? checkIn, TimeOnly? checkOut, int breakMinutes)
    {
        if (checkIn is null)
        {
            return (0, 0, AttendanceStatus.Absent);
        }

        var status = checkIn < EarlyBefore ? AttendanceStatus.Early
            : checkIn <= WorkStart ? AttendanceStatus.OnTime
            : AttendanceStatus.Late;

        var worked = 0;
        if (checkOut is not null && checkOut > checkIn)
        {
            worked = Math.Max(0, (int)(checkOut.Value - checkIn.Value).TotalMinutes - breakMinutes);
        }

        var overtime = Math.Max(0, worked - StandardWorkMinutes);
        return (worked, overtime, status);
    }
}
