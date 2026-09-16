using Ekipzen.Domain.Entities.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class GlobalActionConfiguration : IEntityTypeConfiguration<GlobalAction>
{
    public void Configure(EntityTypeBuilder<GlobalAction> b)
    {
        b.Property(a => a.Title).IsRequired().HasMaxLength(300);
        b.Property(a => a.Source).IsRequired().HasMaxLength(64);
        b.Property(a => a.SourceRoute).HasMaxLength(128);
        b.Property(a => a.Owner).IsRequired().HasMaxLength(128);
        b.Property(a => a.Due).HasMaxLength(64);
        b.Property(a => a.RecommendedAction).HasMaxLength(1000);

        b.HasIndex(a => new { a.Status, a.Priority });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(l => l.Actor).IsRequired().HasMaxLength(128);
        b.Property(l => l.Action).IsRequired().HasMaxLength(64);
        b.Property(l => l.Module).IsRequired().HasMaxLength(64);
        b.Property(l => l.Detail).HasMaxLength(2000);
        b.Property(l => l.EntityName).HasMaxLength(128);
        b.Property(l => l.EntityId).HasMaxLength(64);

        b.HasIndex(l => l.CreatedAtUtc);
        b.HasIndex(l => new { l.EntityName, l.EntityId });
    }
}
