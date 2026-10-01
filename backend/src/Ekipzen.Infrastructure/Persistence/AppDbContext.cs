using System.Reflection;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Common;
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
using Ekipzen.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ekipzen.Infrastructure.Persistence;

/// <summary>
/// Uygulamanın EF Core DbContext'i. Identity tablolarını da barındırır (IdentityDbContext).
/// Entity konfigürasyonları assembly'den otomatik yüklenir; tüm enum'lar string olarak saklanır.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant? currentTenant = null)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    /// <summary>
    /// Global query filter'ın her sorguda okuduğu aktif kiracı. HTTP dışı (tasarım
    /// zamanı/seed başlangıcı) bağlamda <c>currentTenant</c> null olabilir → 0 (hiçbir
    /// kiracı eşleşmez, güvenli varsayılan). Seed, ICurrentTenant.Impersonate ile
    /// doğru kiracıyı sabitler.
    /// </summary>
    private int CurrentTenantId => currentTenant?.TenantId ?? 0;

    // Organization
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeOffboardingCase> EmployeeOffboardingCases => Set<EmployeeOffboardingCase>();
    public DbSet<EmployeeOffboardingSettlementRevision> EmployeeOffboardingSettlementRevisions
        => Set<EmployeeOffboardingSettlementRevision>();
    public DbSet<EmployeeOffboardingExternalActionRecord> EmployeeOffboardingExternalActionRecords
        => Set<EmployeeOffboardingExternalActionRecord>();
    public DbSet<EmployeeOffboardingTeamTransfer> EmployeeOffboardingTeamTransfers => Set<EmployeeOffboardingTeamTransfer>();
    public DbSet<EmployeeEmploymentPeriod> EmployeeEmploymentPeriods => Set<EmployeeEmploymentPeriod>();
    public DbSet<EmployeeCompensationPackage> EmployeeCompensationPackages => Set<EmployeeCompensationPackage>();
    public DbSet<EmployeeCompensationPackageCorrection> EmployeeCompensationPackageCorrections
        => Set<EmployeeCompensationPackageCorrection>();
    public DbSet<EmployeeCompensationRemediationCase> EmployeeCompensationRemediationCases
        => Set<EmployeeCompensationRemediationCase>();
    public DbSet<EmployeeCompensationPayrollApplication> EmployeeCompensationPayrollApplications
        => Set<EmployeeCompensationPayrollApplication>();
    public DbSet<EmployeeCompensationDateCorrection> EmployeeCompensationDateCorrections
        => Set<EmployeeCompensationDateCorrection>();
    public DbSet<TerminationCalculationRule> TerminationCalculationRules => Set<TerminationCalculationRule>();

    // Leaves
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveLedgerEntry> LeaveLedgerEntries => Set<LeaveLedgerEntry>();

    // Attendance
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    // Payroll
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollSettings> PayrollSettings => Set<PayrollSettings>();
    public DbSet<IncomeTaxBracket> IncomeTaxBrackets => Set<IncomeTaxBracket>();
    public DbSet<PayrollEmployee> PayrollEmployees => Set<PayrollEmployee>();
    public DbSet<PayrollResult> PayrollResults => Set<PayrollResult>();
    public DbSet<PayrollApprovedRowRevision> PayrollApprovedRowRevisions => Set<PayrollApprovedRowRevision>();
    public DbSet<PayrollApprovedRevisionBatch> PayrollApprovedRevisionBatches
        => Set<PayrollApprovedRevisionBatch>();
    public DbSet<PayrollApprovedRowExternalActionRecord> PayrollApprovedRowExternalActionRecords
        => Set<PayrollApprovedRowExternalActionRecord>();

    // Recruitment
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<PositionCorrection> PositionCorrections => Set<PositionCorrection>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateCorrection> CandidateCorrections => Set<CandidateCorrection>();
    public DbSet<CandidateEvidenceRevision> CandidateEvidenceRevisions => Set<CandidateEvidenceRevision>();
    public DbSet<CandidateStageDecision> CandidateStageDecisions => Set<CandidateStageDecision>();
    public DbSet<CandidateSkill> CandidateSkills => Set<CandidateSkill>();
    public DbSet<CandidateExperience> CandidateExperiences => Set<CandidateExperience>();
    public DbSet<InterviewNote> InterviewNotes => Set<InterviewNote>();
    public DbSet<CandidateEvaluation> CandidateEvaluations => Set<CandidateEvaluation>();
    public DbSet<CandidateHistory> CandidateHistory => Set<CandidateHistory>();

    // Analytics
    public DbSet<EmployeeMetricSnapshot> EmployeeMetricSnapshots => Set<EmployeeMetricSnapshot>();
    public DbSet<EngagementMetric> EngagementMetrics => Set<EngagementMetric>();

    // Compliance
    public DbSet<ComplianceDocument> ComplianceDocuments => Set<ComplianceDocument>();

    // Actions & Audit
    public DbSet<GlobalAction> GlobalActions => Set<GlobalAction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Guidance
    public DbSet<GuidanceBookmark> GuidanceBookmarks => Set<GuidanceBookmark>();
    public DbSet<GuidanceContentReading> GuidanceContentReadings => Set<GuidanceContentReading>();
    public DbSet<GuidanceHelpPreference> GuidanceHelpPreferences => Set<GuidanceHelpPreference>();
    public DbSet<GuidancePayrollPeriodEvidence> GuidancePayrollPeriodEvidences => Set<GuidancePayrollPeriodEvidence>();
    public DbSet<GuidancePayrollRowEvidence> GuidancePayrollRowEvidences => Set<GuidancePayrollRowEvidence>();
    public DbSet<GuidanceOffboardingEvidence> GuidanceOffboardingEvidences => Set<GuidanceOffboardingEvidence>();
    public DbSet<GuidanceSetupEvidence> GuidanceSetupEvidences => Set<GuidanceSetupEvidence>();
    public DbSet<GuidanceAttendanceEvidence> GuidanceAttendanceEvidences => Set<GuidanceAttendanceEvidence>();
    public DbSet<GuidanceComplianceEvidence> GuidanceComplianceEvidences => Set<GuidanceComplianceEvidence>();
    public DbSet<GuidanceLeaveDecisionEvidence> GuidanceLeaveDecisionEvidences => Set<GuidanceLeaveDecisionEvidence>();
    public DbSet<GuidancePublishedPracticeProgress> GuidancePublishedPracticeProgresses => Set<GuidancePublishedPracticeProgress>();
    public DbSet<EmployeeCreationEvidence> EmployeeCreationEvidences => Set<EmployeeCreationEvidence>();
    public DbSet<CandidateReviewEvidence> CandidateReviewEvidences => Set<CandidateReviewEvidence>();
    public DbSet<GuidanceDecisionProgress> GuidanceDecisionProgresses => Set<GuidanceDecisionProgress>();

    // Durable Identity provisioning
    public DbSet<IdentityProvisioningOperation> IdentityProvisioningOperations => Set<IdentityProvisioningOperation>();

    // Settings
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();

    // RefreshTokens BURADA DEĞİL: platform veritabanına taşındı (bkz.
    // PlatformDbContext.RefreshTokens / RefreshToken.cs xmldoc) — hangi kiracı
    // DB'sine bakılacağı token dizesinden başka türlü bilinemez.

    // Okuma modelleri (SQL view)
    public DbSet<Domain.ReadModels.MonthlyAttendanceSummary> MonthlyAttendanceSummaries
        => Set<Domain.ReadModels.MonthlyAttendanceSummary>();
    public DbSet<Domain.ReadModels.PayrollPeriodSummary> PayrollPeriodSummaries
        => Set<Domain.ReadModels.PayrollPeriodSummary>();
    public DbSet<Domain.ReadModels.EmployeeRiskMetric> EmployeeRiskMetrics
        => Set<Domain.ReadModels.EmployeeRiskMetric>();
    public DbSet<Domain.ReadModels.DepartmentRiskSummary> DepartmentRiskSummaries
        => Set<Domain.ReadModels.DepartmentRiskSummary>();
    public DbSet<Domain.ReadModels.ComplianceReadiness> ComplianceReadiness
        => Set<Domain.ReadModels.ComplianceReadiness>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Para ve oran alanları: 0.759 gibi binde oranları da kaybolmasın diye 4 ondalık.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Tüm enum property'lerini okunabilirlik için string olarak sakla.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                {
                    var converterType = typeof(EnumToStringConverter<>).MakeGenericType(type);
                    var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
                    property.SetValueConverter(converter);
                    property.SetMaxLength(64);
                }
            }
        }

        // Multi-tenant global query filter: kiracı-kapsamlı (ITenantScoped) HER tip —
        // kalıcı varlıklar VE SQL view read-model'leri — aktif kiracıya otomatik izole
        // edilir. Reflection ile uygulanır ki yeni bir tip eklendiğinde filtreyi eklemeyi
        // UNUTMAK imkânsız olsun (sızıntı önlemi). Not: ApplicationUser ITenantScoped
        // DEĞİL — login e-posta araması kiracı-üstüdür, elle kapsamlanır. RefreshToken
        // artık bu context'te bile değil (platform veritabanında).
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            typeof(AppDbContext)
                .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder]);

            // Filtre yüzünden HER sorgu "TenantId = @p" içerir; indeks olmadan her sorgu
            // tam tablo taraması olur. Filtreyle aynı yerde eklenir ki yeni bir kiracı
            // varlığı geldiğinde indeksi eklemeyi unutmak mümkün olmasın.
            // Atlananlar: SQL view'ları (indekslenemez) ve TenantId ile BAŞLAYAN bileşik
            // indeksi zaten olanlar (ör. PayrollSettings) — ikinci indeks gereksiz yazma
            // maliyeti olurdu.
            var viewMi = entityType.GetViewName() is not null;
            var tenantIndeksiVar = entityType.GetIndexes().Any(i =>
                i.Properties.Count > 0 && i.Properties[0].Name == nameof(ITenantScoped.TenantId));

            if (!viewMi && !tenantIndeksiVar)
            {
                builder.Entity(entityType.ClrType).HasIndex(nameof(ITenantScoped.TenantId));
            }
        }

        // ApplicationUser ITenantScoped DEĞİL (login e-posta araması kiracı-üstüdür),
        // bu yüzden yukarıdaki reflection döngüsüne girmez. Ama TenantId taşır ve
        // kiracı bazında sorgulanır (kullanıcı listesi, purge) — indeksi elle eklenir.
        // RefreshToken burada YOK — platform veritabanında, indeksi orada tanımlanır.
        builder.Entity<ApplicationUser>().HasIndex(u => u.TenantId);

        // Kısa Identity tablo adları.
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");
    }

    /// <summary>Tek bir kiracı-kapsamlı tipe (varlık veya view read-model) TenantId filtresi uygular.</summary>
    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantScoped =>
        builder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
}
