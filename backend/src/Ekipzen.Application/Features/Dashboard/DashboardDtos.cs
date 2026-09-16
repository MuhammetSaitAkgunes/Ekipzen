using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.ReadModels;

namespace Ekipzen.Application.Features.Dashboard;

/// <summary>Risk merkezi personel satırı — dashboard.js enrichedEmployees şekli.</summary>
public sealed record RiskEmployeeDto(
    int EmployeeId,
    string Name,
    string Title,
    string Dept,
    string? Manager,
    int Absence,
    int Lateness,
    int Overtime,
    int UnusedLeave,
    int Pulse,
    int Performance,
    int RoleCriticality,
    int RiskScore,
    string AttritionRisk,
    string BurnoutRisk,
    string? Trend,
    string? Action);

/// <summary>Departman risk kartı — dashboard.js departmentRisk şekli.</summary>
public sealed record DepartmentRiskDto(
    int DepartmentId, string Dept, int Risk, int EmployeeCount,
    int HighAttritionCount, int HighBurnoutCount);

/// <summary>Yetenek/kapasite kartı — dashboard.js talentCapacity şekli.</summary>
public sealed record TalentCapacityItemDto(string Label, int Value, string Meta, string Tone);

/// <summary>Risk merkezi ana yükü — dashboard.js getDashboardMetrics() dönüşü.</summary>
public sealed record DashboardMetricsDto(
    int RiskScore,
    int ManagerLoadIndex,
    int AttritionHigh,
    int BurnoutRisk,
    int CriticalActions,
    int PulseScore,
    int HiringHealth,
    int SkillGap,
    int CriticalRoleRisk,
    IReadOnlyList<int> RiskTrend,
    IReadOnlyList<DepartmentRiskDto> DepartmentRisk,
    IReadOnlyList<TalentCapacityItemDto> TalentCapacity,
    IReadOnlyList<RiskEmployeeDto> Employees);

/// <summary>Ayrılma/tükenmişlik detay sayfası: KPI + sıralı personel satırları.</summary>
public sealed record RiskDetailDto(
    int HighCount,
    int MediumCount,
    int CriticalRoleCount,
    int AveragePulse,
    int AverageOvertime,
    int AverageUnusedLeave,
    IReadOnlyList<RiskEmployeeDto> Employees);

/// <summary>Yönetici yükü satırı — dashboard.js managers şekli.</summary>
public sealed record ManagerLoadItemDto(
    int EmployeeId,
    string Name,
    int Team,
    int Approvals,
    int Actions,
    int Overtime,
    int Pulse,
    int Load);

public sealed record ManagerLoadDto(
    int ManagerLoadIndex,
    int CriticalManagerCount,
    int PendingApprovals,
    int OpenActions,
    IReadOnlyList<ManagerLoadItemDto> Managers);

/// <summary>Departman nabız satırı — dashboard.js employeeVoiceMetrics.departments şekli.</summary>
public sealed record VoiceDepartmentDto(
    int DepartmentId,
    string Dept,
    int Pulse,
    int ENps,
    int Participation,
    string? Mood,
    string? Driver,
    string Level);

public sealed record EmployeeVoiceDto(
    int PulseScore,
    int ENps,
    int ParticipationRate,
    int DecliningTeams,
    string SentimentTrend,
    IReadOnlyList<VoiceDepartmentDto> Departments,
    IReadOnlyList<string> Signals,
    IReadOnlyList<string> RecommendedActions);

/// <summary>Uyum evrak satırı — dashboard.js complianceMetrics.records şekli.</summary>
public sealed record ComplianceRecordDto(
    int Id,
    string Employee,
    string Dept,
    string Document,
    string? Owner,
    string DueDate,
    string Status,
    string Level);

/// <summary>Yaklaşan son tarih grubu — dashboard.js complianceMetrics.deadlines şekli.</summary>
public sealed record ComplianceDeadlineDto(
    string Title, int Count, string DueDate, string? Owner, string Level);

public sealed record ComplianceRiskDto(
    int DocumentComplianceScore,
    int MissingDocuments,
    int UpcomingDocuments,
    string AuditReadinessRisk,
    int AuditReadinessScore,
    IReadOnlyList<ComplianceRecordDto> Records,
    IReadOnlyList<ComplianceDeadlineDto> Deadlines);

/// <summary>Genel durum KPI'ları — dashboard.js OverviewDashboard şekli.</summary>
public sealed record OverviewDto(
    int ActiveEmployees,
    int PendingApprovals,
    int OpenPositions,
    int NewApplications,
    int InOfficeToday,
    int OnLeaveToday,
    int PulseScore,
    IReadOnlyList<DepartmentCountDto> DepartmentDistribution,
    RecruitmentFunnelSliceDto RecruitmentFunnel);

public sealed record DepartmentCountDto(string Dept, int Count);

public sealed record RecruitmentFunnelSliceDto(
    int Total, int New, int Interview, int Offer, int Rejected, int Hired);

public static class DashboardMappings
{
    public static RiskEmployeeDto ToDto(this EmployeeRiskMetric m) => new(
        m.EmployeeId, m.FullName, m.Title, m.DepartmentName, m.ManagerName,
        m.AbsencePct, m.LatenessPct, m.OvertimePct, m.UnusedLeavePct,
        m.Pulse, m.Performance, m.RoleCriticality,
        m.RiskScore, m.AttritionRisk, m.BurnoutRisk, m.TrendNote, m.RecommendedAction);

    /// <summary>Skor→seviye eşiği (dashboard.js getRiskLevel ile aynı).</summary>
    public static string LevelOf(int score) => score >= 70 ? "high" : score >= 55 ? "medium" : "low";

    /// <summary>Nabız→seviye eşiği (dashboard.js voice.departments verisiyle uyumlu).</summary>
    public static string PulseLevelOf(int pulse) => pulse < 60 ? "high" : pulse < 65 ? "medium" : "low";

    /// <summary>
    /// Rol bazlı risk kapsamı: hr-admin → tüm şirket; manager → kendi ekibi + kendisi.
    /// (routes.js kapsam ilkesinin risk view karşılığı; employee bu uçlara giremez.)
    /// </summary>
    public static IQueryable<EmployeeRiskMetric> ScopeFor(
        this IQueryable<EmployeeRiskMetric> query, ICurrentUser user)
    {
        if (user.Roles.Contains(Roles.HrAdmin))
        {
            return query;
        }

        var selfId = user.EmployeeId ?? -1;
        return query.Where(m => m.ManagerId == selfId || m.EmployeeId == selfId);
    }
}
