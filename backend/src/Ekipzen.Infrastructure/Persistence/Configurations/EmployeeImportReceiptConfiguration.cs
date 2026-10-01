using Ekipzen.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public sealed class EmployeeImportReceiptConfiguration : IEntityTypeConfiguration<EmployeeImportReceipt>
{
    public void Configure(EntityTypeBuilder<EmployeeImportReceipt> builder)
    {
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.FileFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PreviewToken).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResultJson).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.ImportRequestId }).IsUnique();
    }
}
