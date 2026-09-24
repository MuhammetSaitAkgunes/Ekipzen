using Ekipzen.Domain.Entities.Guidance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public sealed class GuidanceBookmarkConfiguration : IEntityTypeConfiguration<GuidanceBookmark>
{
    public void Configure(EntityTypeBuilder<GuidanceBookmark> builder)
    {
        builder.Property(item => item.UserId).IsRequired().HasMaxLength(450);
        builder.Property(item => item.ProcessKey).IsRequired().HasMaxLength(64);
        builder.Property(item => item.StepKey).IsRequired().HasMaxLength(64);
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.ProcessKey }).IsUnique();
    }
}

public sealed class EmployeeCreationEvidenceConfiguration : IEntityTypeConfiguration<EmployeeCreationEvidence>
{
    public void Configure(EntityTypeBuilder<EmployeeCreationEvidence> builder)
    {
        builder.Property(item => item.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.EmployeeId }).IsUnique();
        builder.HasOne(item => item.Employee)
            .WithMany()
            .HasForeignKey(item => item.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
