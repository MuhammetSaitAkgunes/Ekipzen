using Ekipzen.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public sealed class EmployeeEmploymentPeriodConfiguration
    : IEntityTypeConfiguration<EmployeeEmploymentPeriod>
{
    public void Configure(EntityTypeBuilder<EmployeeEmploymentPeriod> b)
    {
        b.Property(item => item.AnnualLeaveEntitlementDays).HasDefaultValue(24);
        b.HasIndex(item => new { item.TenantId, item.EmployeeId, item.StartDate })
            .IsUnique();
        b.HasIndex(item => new { item.TenantId, item.EmployeeId })
            .IsUnique()
            .HasFilter("[EndDate] IS NULL");

        b.HasOne(item => item.Employee)
            .WithMany(employee => employee.EmploymentPeriods)
            .HasForeignKey(item => item.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class EmployeeCompensationPackageConfiguration
    : IEntityTypeConfiguration<EmployeeCompensationPackage>
{
    public void Configure(EntityTypeBuilder<EmployeeCompensationPackage> b)
    {
        b.Property(item => item.Currency).IsRequired().HasMaxLength(3);
        b.Property(item => item.SourceReference).IsRequired().HasMaxLength(256);
        b.Property(item => item.Version).HasDefaultValue(1).IsConcurrencyToken();
        b.HasIndex(item => new { item.TenantId, item.EmploymentPeriodId, item.EffectiveFrom })
            .IsUnique();

        b.HasOne(item => item.EmploymentPeriod)
            .WithMany(period => period.CompensationPackages)
            .HasForeignKey(item => item.EmploymentPeriodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class EmployeeCompensationPackageCorrectionConfiguration
    : IEntityTypeConfiguration<EmployeeCompensationPackageCorrection>
{
    public void Configure(EntityTypeBuilder<EmployeeCompensationPackageCorrection> b)
    {
        b.Property(item => item.BeforeJson).IsRequired();
        b.Property(item => item.AfterJson).IsRequired();
        b.Property(item => item.Reason).IsRequired().HasMaxLength(500);
        b.Property(item => item.ActorUserId).IsRequired().HasMaxLength(450);
        b.HasIndex(item => new { item.TenantId, item.CompensationPackageId, item.ToVersion })
            .IsUnique();
        b.HasOne(item => item.CompensationPackage)
            .WithMany()
            .HasForeignKey(item => item.CompensationPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeCompensationRemediationCaseConfiguration
    : IEntityTypeConfiguration<EmployeeCompensationRemediationCase>
{
    public void Configure(EntityTypeBuilder<EmployeeCompensationRemediationCase> b)
    {
        b.Property(item => item.Version).HasDefaultValue(1).IsConcurrencyToken();
        b.Property(item => item.Status).IsRequired().HasMaxLength(24);
        b.Property(item => item.IssueDescription).IsRequired().HasMaxLength(2000);
        b.Property(item => item.ProposedValuesJson).IsRequired();
        b.Property(item => item.ImpactSnapshotJson).IsRequired();
        b.Property(item => item.OpenedByUserId).IsRequired().HasMaxLength(450);
        b.Property(item => item.ReviewedByUserId).HasMaxLength(450);
        b.Property(item => item.ExpertReviewReference).HasMaxLength(256);
        b.Property(item => item.ReviewNotes).HasMaxLength(2000);
        b.HasIndex(item => new { item.TenantId, item.CompensationPackageId }).IsUnique();
        b.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(item => item.CompensationPackage).WithMany()
            .HasForeignKey(item => item.CompensationPackageId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeCompensationPayrollApplicationConfiguration
    : IEntityTypeConfiguration<EmployeeCompensationPayrollApplication>
{
    public void Configure(EntityTypeBuilder<EmployeeCompensationPayrollApplication> b)
    {
        b.Property(item => item.BeforeJson).IsRequired();
        b.Property(item => item.AfterJson).IsRequired();
        b.Property(item => item.ActorUserId).IsRequired().HasMaxLength(450);
        b.HasIndex(item => new { item.TenantId, item.RemediationCaseId, item.PayrollRowId }).IsUnique();
        b.HasOne(item => item.RemediationCase).WithMany()
            .HasForeignKey(item => item.RemediationCaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(item => item.PayrollRow).WithMany()
            .HasForeignKey(item => item.PayrollRowId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeCompensationDateCorrectionConfiguration
    : IEntityTypeConfiguration<EmployeeCompensationDateCorrection>
{
    public void Configure(EntityTypeBuilder<EmployeeCompensationDateCorrection> b)
    {
        b.Property(item => item.Reason).IsRequired().HasMaxLength(500);
        b.Property(item => item.ActorUserId).IsRequired().HasMaxLength(450);
        b.HasIndex(item => new { item.TenantId, item.CompensationPackageId, item.ToVersion })
            .IsUnique();
        b.HasOne(item => item.CompensationPackage).WithMany()
            .HasForeignKey(item => item.CompensationPackageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeCompensationPackage>().WithMany()
            .HasForeignKey(item => item.PreviousPackageId).OnDelete(DeleteBehavior.Restrict);
    }
}
