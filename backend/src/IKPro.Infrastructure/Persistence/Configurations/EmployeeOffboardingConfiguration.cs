using IKPro.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IKPro.Infrastructure.Persistence.Configurations;

public sealed class EmployeeOffboardingCaseConfiguration
    : IEntityTypeConfiguration<EmployeeOffboardingCase>
{
    public void Configure(EntityTypeBuilder<EmployeeOffboardingCase> builder)
    {
        builder.Property(item => item.ExitCode).IsRequired().HasMaxLength(64);
        builder.Property(item => item.ExitReason).IsRequired().HasMaxLength(1000);
        builder.Property(item => item.SettlementReference).IsRequired().HasMaxLength(256);
        builder.Property(item => item.SettlementNotes).HasMaxLength(2000);
        builder.Property(item => item.PreviousExitCode).HasMaxLength(64);
        builder.Property(item => item.CompletedByUserId).HasMaxLength(450);
        builder.Property(item => item.ReversedByUserId).HasMaxLength(450);
        builder.Property(item => item.CalculationGeneratedByUserId).HasMaxLength(450);
        builder.Property(item => item.ReversalReason).HasMaxLength(1000);
        builder.Property(item => item.Version).IsConcurrencyToken();

        builder.HasIndex(item => new { item.TenantId, item.EmployeeId })
            .IsUnique()
            .HasFilter("[Status] <> 'Reversed'");

        builder.HasOne(item => item.Employee)
            .WithMany()
            .HasForeignKey(item => item.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.CalculationRule)
            .WithMany()
            .HasForeignKey(item => item.CalculationRuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeOffboardingTeamTransferConfiguration
    : IEntityTypeConfiguration<EmployeeOffboardingTeamTransfer>
{
    public void Configure(EntityTypeBuilder<EmployeeOffboardingTeamTransfer> builder)
    {
        builder.HasIndex(item => new { item.TenantId, item.OffboardingCaseId, item.EmployeeId })
            .IsUnique();

        builder.HasOne(item => item.OffboardingCase)
            .WithMany(item => item.TeamTransfers)
            .HasForeignKey(item => item.OffboardingCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Employee)
            .WithMany()
            .HasForeignKey(item => item.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TerminationCalculationRuleConfiguration
    : IEntityTypeConfiguration<TerminationCalculationRule>
{
    public void Configure(EntityTypeBuilder<TerminationCalculationRule> builder)
    {
        builder.Property(item => item.SourceReference).IsRequired().HasMaxLength(1000);
        builder.Property(item => item.ReviewNotes).HasMaxLength(2000);
        builder.Property(item => item.ApprovedByUserId).HasMaxLength(450);
        builder.Property(item => item.Version).IsConcurrencyToken();

        builder.HasIndex(item => new { item.TenantId, item.EffectiveFrom })
            .IsUnique();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TerminationCalculationRules_PositiveValues",
            "[DailyWageDivisor] > 0 AND [SeveranceDaysPerYear] > 0 " +
            "AND [MinimumSeveranceServiceDays] > 0 AND [SeveranceCeiling] > 0 " +
            "AND [NoticeDaysUnderSixMonths] >= 0 AND [NoticeDaysSixToEighteenMonths] >= 0 " +
            "AND [NoticeDaysEighteenToThirtySixMonths] >= 0 AND [NoticeDaysOverThirtySixMonths] >= 0 " +
            "AND [NoticeFirstThresholdMonths] > 0 " +
            "AND [NoticeSecondThresholdMonths] > [NoticeFirstThresholdMonths] " +
            "AND [NoticeThirdThresholdMonths] > [NoticeSecondThresholdMonths]"));
    }
}
