using IKPro.Domain.Entities.Actions;
using IKPro.Domain.Entities.Analytics;
using IKPro.Domain.Entities.Attendance;
using IKPro.Domain.Entities.Compliance;
using IKPro.Domain.Entities.Identity;
using IKPro.Domain.Entities.Leaves;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Entities.Payroll;
using IKPro.Domain.Entities.Recruitment;
using IKPro.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Common.Interfaces;

/// <summary>
/// Uygulama katmanının veri erişim sözleşmesi (UoW). Infrastructure'daki
/// AppDbContext tarafından implemente edilir; Identity tabloları burada yer almaz.
/// </summary>
public interface IApplicationDbContext
{
    // Organization
    DbSet<Department> Departments { get; }
    DbSet<Employee> Employees { get; }
    DbSet<EmployeeProfile> EmployeeProfiles { get; }
    DbSet<EmployeeDocument> EmployeeDocuments { get; }

    // Leaves
    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<LeaveBalance> LeaveBalances { get; }
    DbSet<Domain.ReadModels.LeaveBalanceSummary> LeaveBalanceSummaries { get; }

    // Attendance
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<Domain.ReadModels.MonthlyAttendanceSummary> MonthlyAttendanceSummaries { get; }

    // Payroll
    DbSet<PayrollPeriod> PayrollPeriods { get; }
    DbSet<PayrollSettings> PayrollSettings { get; }
    DbSet<IncomeTaxBracket> IncomeTaxBrackets { get; }
    DbSet<PayrollEmployee> PayrollEmployees { get; }
    DbSet<PayrollResult> PayrollResults { get; }
    DbSet<Domain.ReadModels.PayrollPeriodSummary> PayrollPeriodSummaries { get; }

    // Recruitment
    DbSet<Position> Positions { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<CandidateSkill> CandidateSkills { get; }
    DbSet<CandidateExperience> CandidateExperiences { get; }
    DbSet<InterviewNote> InterviewNotes { get; }
    DbSet<CandidateEvaluation> CandidateEvaluations { get; }
    DbSet<CandidateHistory> CandidateHistory { get; }

    // Analytics
    DbSet<EmployeeMetricSnapshot> EmployeeMetricSnapshots { get; }
    DbSet<EngagementMetric> EngagementMetrics { get; }
    DbSet<Domain.ReadModels.EmployeeRiskMetric> EmployeeRiskMetrics { get; }
    DbSet<Domain.ReadModels.DepartmentRiskSummary> DepartmentRiskSummaries { get; }

    // Compliance
    DbSet<ComplianceDocument> ComplianceDocuments { get; }
    DbSet<Domain.ReadModels.ComplianceReadiness> ComplianceReadiness { get; }

    // Actions & Audit
    DbSet<GlobalAction> GlobalActions { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // Durable Identity provisioning
    DbSet<IdentityProvisioningOperation> IdentityProvisioningOperations { get; }

    // Settings
    DbSet<CompanyProfile> CompanyProfiles { get; }
    DbSet<NotificationSettings> NotificationSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
