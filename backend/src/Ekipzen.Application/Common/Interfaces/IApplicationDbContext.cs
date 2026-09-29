using Ekipzen.Domain.Entities.Actions;
using Ekipzen.Domain.Entities.Analytics;
using Ekipzen.Domain.Entities.Attendance;
using Ekipzen.Domain.Entities.Compliance;
using Ekipzen.Domain.Entities.Guidance;
using Ekipzen.Domain.Entities.Identity;
using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Entities.Recruitment;
using Ekipzen.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Common.Interfaces;

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
    DbSet<EmployeeOffboardingCase> EmployeeOffboardingCases { get; }
    DbSet<EmployeeOffboardingTeamTransfer> EmployeeOffboardingTeamTransfers { get; }
    DbSet<EmployeeEmploymentPeriod> EmployeeEmploymentPeriods { get; }
    DbSet<EmployeeCompensationPackage> EmployeeCompensationPackages { get; }
    DbSet<EmployeeCompensationPackageCorrection> EmployeeCompensationPackageCorrections { get; }
    DbSet<EmployeeCompensationRemediationCase> EmployeeCompensationRemediationCases { get; }
    DbSet<EmployeeCompensationPayrollApplication> EmployeeCompensationPayrollApplications { get; }
    DbSet<EmployeeCompensationDateCorrection> EmployeeCompensationDateCorrections { get; }
    DbSet<TerminationCalculationRule> TerminationCalculationRules { get; }

    // Leaves
    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<LeaveLedgerEntry> LeaveLedgerEntries { get; }

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

    // Guidance
    DbSet<GuidanceBookmark> GuidanceBookmarks { get; }
    DbSet<EmployeeCreationEvidence> EmployeeCreationEvidences { get; }
    DbSet<CandidateReviewEvidence> CandidateReviewEvidences { get; }
    DbSet<GuidanceDecisionProgress> GuidanceDecisionProgresses { get; }

    // Durable Identity provisioning
    DbSet<IdentityProvisioningOperation> IdentityProvisioningOperations { get; }

    // Settings
    DbSet<CompanyProfile> CompanyProfiles { get; }
    DbSet<NotificationSettings> NotificationSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
