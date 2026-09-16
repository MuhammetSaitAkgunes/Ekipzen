using Ekipzen.Domain.Entities.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class EmployeeMetricSnapshotConfiguration : IEntityTypeConfiguration<EmployeeMetricSnapshot>
{
    public void Configure(EntityTypeBuilder<EmployeeMetricSnapshot> b)
    {
        b.Property(m => m.TrendNote).HasMaxLength(200);
        b.Property(m => m.RecommendedAction).HasMaxLength(200);

        b.HasOne(m => m.Employee)
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(m => new { m.EmployeeId, m.PeriodDate }).IsUnique();
    }
}

public class EmployeeRiskMetricConfiguration
    : IEntityTypeConfiguration<Domain.ReadModels.EmployeeRiskMetric>
{
    public void Configure(EntityTypeBuilder<Domain.ReadModels.EmployeeRiskMetric> b)
    {
        b.HasNoKey().ToView("vw_EmployeeRiskMetric");
    }
}

public class DepartmentRiskSummaryConfiguration
    : IEntityTypeConfiguration<Domain.ReadModels.DepartmentRiskSummary>
{
    public void Configure(EntityTypeBuilder<Domain.ReadModels.DepartmentRiskSummary> b)
    {
        b.HasNoKey().ToView("vw_DepartmentRisk");
    }
}

public class EngagementMetricConfiguration : IEntityTypeConfiguration<EngagementMetric>
{
    public void Configure(EntityTypeBuilder<EngagementMetric> b)
    {
        b.Property(m => m.Mood).HasMaxLength(64);
        b.Property(m => m.Driver).HasMaxLength(200);

        b.HasOne(m => m.Department)
            .WithMany()
            .HasForeignKey(m => m.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(m => new { m.DepartmentId, m.PeriodDate }).IsUnique();
    }
}
