using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Constants;
using IKPro.Domain.Entities.Compliance;
using IKPro.Domain.Enums;

namespace IKPro.Application.Features.Compliance;

/// <summary>Uyum belgesi satırı — dashboard.js complianceMetrics.records şekli + kimlikler.</summary>
public sealed record ComplianceDocumentDto(
    int Id,
    int EmployeeId,
    string Employee,
    string Dept,
    string Document,
    string? Owner,
    DateOnly? DueDate,
    string DueLabel,
    string Status,
    string Level);

/// <summary>Denetim hazırlığı çubuğu — dashboard.js auditChecklist şekli.</summary>
public sealed record AuditChecklistItemDto(string Label, int Value, string Level);

/// <summary>Hazırlık skoru özeti (vw_ComplianceReadiness) + türetilmiş kontrol listesi.</summary>
public sealed record ComplianceReadinessDto(
    int TotalCount,
    int CompletedCount,
    int MissingCount,
    int DueSoonCount,
    int InReviewCount,
    int OwnedCount,
    int DocumentComplianceScore,
    int ReadinessScore,
    string ReadinessRisk,
    IReadOnlyList<AuditChecklistItemDto> AuditChecklist,
    IReadOnlyList<string> RecommendedActions);

public static class ComplianceMappings
{
    public static bool IsStatus(string? value)
        => value is "Eksik" or "İncelemede" or "Süresi Yaklaşıyor" or "Tamamlandı";

    public static bool IsLevel(string? value)
        => value is "high" or "medium" or "low";

    /// <summary>Enum kataloğu (plan Ek A): Eksik | İncelemede | Süresi Yaklaşıyor | Tamamlandı.</summary>
    public static string ToLabel(this ComplianceStatus status) => status switch
    {
        ComplianceStatus.Missing => "Eksik",
        ComplianceStatus.InReview => "İncelemede",
        ComplianceStatus.DueSoon => "Süresi Yaklaşıyor",
        ComplianceStatus.Completed => "Tamamlandı",
        _ => status.ToString(),
    };

    public static ComplianceStatus ParseStatus(string value) => value switch
    {
        "Eksik" => ComplianceStatus.Missing,
        "İncelemede" => ComplianceStatus.InReview,
        "Süresi Yaklaşıyor" => ComplianceStatus.DueSoon,
        "Tamamlandı" => ComplianceStatus.Completed,
        _ => throw new ArgumentException(
            $"Geçersiz belge durumu: {value} (Eksik|İncelemede|Süresi Yaklaşıyor|Tamamlandı)."),
    };

    public static string ToLabel(this RiskLevel level) => level switch
    {
        RiskLevel.High => "high",
        RiskLevel.Medium => "medium",
        _ => "low",
    };

    public static RiskLevel ParseLevel(string value) => value switch
    {
        "high" => RiskLevel.High,
        "medium" => RiskLevel.Medium,
        "low" => RiskLevel.Low,
        _ => throw new ArgumentException($"Geçersiz risk seviyesi: {value} (high|medium|low)."),
    };

    /// <summary>Frontend son tarih etiketi: Tamamlandı | Bugün | Gecikti | "N gün" | "-".</summary>
    public static string DueLabel(ComplianceStatus status, DateOnly? dueDate, DateOnly today)
    {
        if (status == ComplianceStatus.Completed) return "Tamamlandı";
        if (dueDate is null) return "-";
        if (dueDate.Value == today) return "Bugün";
        if (dueDate.Value < today) return "Gecikti";
        return $"{dueDate.Value.DayNumber - today.DayNumber} gün";
    }

    public static ComplianceDocumentDto ToDto(this ComplianceDocument document, DateOnly today) => new(
        document.Id,
        document.EmployeeId,
        document.Employee?.FullName ?? string.Empty,
        document.Employee?.Department?.Name ?? string.Empty,
        document.DocumentName,
        document.OwnerName,
        document.DueDate,
        DueLabel(document.Status, document.DueDate, today),
        document.Status.ToLabel(),
        document.Level.ToLabel());

    /// <summary>
    /// Rol bazlı belge kapsamı: hr-admin → hepsi; manager → ekibi + kendisi
    /// (routes.js kapsam ilkesinin uyum karşılığı; employee bu uçlara giremez).
    /// </summary>
    public static IQueryable<ComplianceDocument> ScopeFor(
        this IQueryable<ComplianceDocument> query, ICurrentUser user)
    {
        if (user.Roles.Contains(Roles.HrAdmin))
        {
            return query;
        }

        var selfId = user.EmployeeId ?? -1;
        return query.Where(d => d.Employee!.ManagerId == selfId || d.EmployeeId == selfId);
    }
}
