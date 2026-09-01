using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;

namespace IKPro.Application.Features.Leaves;

public sealed record LeaveTypeDto(
    int Id, string Name, string? Code, bool DeductsFromAnnualBalance, bool RequiresApproval);

/// <summary>İzin talebi — leaves.js geçmiş tablosu + onay kuyruğu şekli (status küçük harf).</summary>
public sealed record LeaveRequestDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    int LeaveTypeId,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days,
    string Status,
    string? Description,
    int? SubstituteEmployeeId,
    string? DecisionNote,
    DateTime? DecisionAtUtc,
    DateTime CreatedAtUtc);

/// <summary>Hareket defterinden üretilen hizmet dönemi bakiyesi.</summary>
public sealed record LeaveBalanceDto(
    int Year,
    int EntitledDays,
    int CarriedOverDays,
    int UsedDays,
    int PendingDays,
    int RemainingDays,
    DateOnly ThroughDate,
    DateOnly? NextAccrualDate,
    int EmploymentPeriodId);

public sealed record LeaveLedgerEntryDto(
    int Id,
    int EmploymentPeriodId,
    int? LeaveRequestId,
    DateOnly OccurredOn,
    string Type,
    int Days,
    int? ServiceYear,
    string SourceReference);

/// <summary>Takım yokluk widget'ı satırı.</summary>
public sealed record TeamLeaveDto(
    int EmployeeId,
    string EmployeeName,
    string Initials,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate);

public static class LeaveMappings
{
    /// <summary>Frontend durum değerleri: approved | pending | rejected | cancelled.</summary>
    public static string ToDto(this LeaveStatus status) => status switch
    {
        LeaveStatus.Pending => "pending",
        LeaveStatus.Approved => "approved",
        LeaveStatus.Rejected => "rejected",
        LeaveStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant(),
    };
}

public static class LeaveGuards
{
    /// <summary>İzin uçları personel bağı gerektirir (hr-admin demo hesabı gibi bağsız kullanıcılar hariç tutulur).</summary>
    public static int RequireEmployeeId(ICurrentUser currentUser)
        => currentUser.EmployeeId
            ?? throw new ForbiddenAccessException("Hesabınıza bağlı personel kaydı yok; izin işlemleri yapılamaz.");
}
