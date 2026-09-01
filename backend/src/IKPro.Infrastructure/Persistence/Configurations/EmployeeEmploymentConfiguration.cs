using IKPro.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IKPro.Infrastructure.Persistence.Configurations;

public sealed class EmployeeEmploymentPeriodConfiguration
    : IEntityTypeConfiguration<EmployeeEmploymentPeriod>
{
    public void Configure(EntityTypeBuilder<EmployeeEmploymentPeriod> b)
    {
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
        b.HasIndex(item => new { item.TenantId, item.EmploymentPeriodId, item.EffectiveFrom })
            .IsUnique();

        b.HasOne(item => item.EmploymentPeriod)
            .WithMany(period => period.CompensationPackages)
            .HasForeignKey(item => item.EmploymentPeriodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}