using Ekipzen.Domain.Entities.Guidance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public sealed class GuidanceHelpPreferenceConfiguration : IEntityTypeConfiguration<GuidanceHelpPreference>
{
    public void Configure(EntityTypeBuilder<GuidanceHelpPreference> builder)
    {
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ProcessKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Mode).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.ProcessKey }).IsUnique();
    }
}

public sealed class GuidanceLeaveDecisionEvidenceConfiguration : IEntityTypeConfiguration<GuidanceLeaveDecisionEvidence>
{
    public void Configure(EntityTypeBuilder<GuidanceLeaveDecisionEvidence> builder)
    {
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.GuidanceSnapshotJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.LeaveRequestId }).IsUnique();
        builder.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GuidancePublishedPracticeProgressConfiguration : IEntityTypeConfiguration<GuidancePublishedPracticeProgress>
{
    public void Configure(EntityTypeBuilder<GuidancePublishedPracticeProgress> builder)
    {
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ContentKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ProcessKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.StepKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.ContentRevisionId }).IsUnique();
    }
}

public sealed class GuidanceContentReadingConfiguration : IEntityTypeConfiguration<GuidanceContentReading>
{
    public void Configure(EntityTypeBuilder<GuidanceContentReading> builder)
    {
        builder.Property(item => item.UserId).IsRequired().HasMaxLength(450);
        builder.Property(item => item.ContentKey).IsRequired().HasMaxLength(64);
        builder.Property(item => item.ProcessKey).IsRequired().HasMaxLength(64);
        builder.Property(item => item.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.ContentRevisionId }).IsUnique();
    }
}

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
        builder.Property(item => item.GuidanceSnapshotJson).HasMaxLength(4000);
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.EmployeeId }).IsUnique();
        builder.HasOne(item => item.Employee)
            .WithMany()
            .HasForeignKey(item => item.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CandidateReviewEvidenceConfiguration : IEntityTypeConfiguration<CandidateReviewEvidence>
{
    public void Configure(EntityTypeBuilder<CandidateReviewEvidence> builder)
    {
        builder.Property(item => item.UserId).IsRequired().HasMaxLength(450);
        builder.Property(item => item.GuidanceSnapshotJson).HasMaxLength(4000);
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.CandidateId }).IsUnique();
        builder.HasOne(item => item.Candidate)
            .WithMany()
            .HasForeignKey(item => item.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GuidanceDecisionProgressConfiguration : IEntityTypeConfiguration<GuidanceDecisionProgress>
{
    public void Configure(EntityTypeBuilder<GuidanceDecisionProgress> builder)
    {
        builder.Property(item => item.UserId).IsRequired().HasMaxLength(450);
        builder.Property(item => item.ProcessKey).IsRequired().HasMaxLength(64);
        builder.Property(item => item.StepKey).IsRequired().HasMaxLength(64);
        builder.HasIndex(item => new {
            item.TenantId, item.UserId, item.ProcessKey, item.StepKey, item.ContentVersion
        }).IsUnique();
    }
}
