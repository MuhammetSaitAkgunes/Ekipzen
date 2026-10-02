using Ekipzen.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public sealed class FinancialExternalActionConfiguration : IEntityTypeConfiguration<FinancialExternalActionRecord>
{
    public void Configure(EntityTypeBuilder<FinancialExternalActionRecord> b)
    {
        b.ToTable("FinancialExternalActionRecords", t => t.HasCheckConstraint("CK_FinancialExternal_Source",
            "([PayrollRowId] IS NOT NULL AND [OffboardingCaseId] IS NULL) OR ([PayrollRowId] IS NULL AND [OffboardingCaseId] IS NOT NULL)"));
        b.HasOne(x => x.PayrollRow).WithMany().HasForeignKey(x => x.PayrollRowId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OffboardingCase).WithMany().HasForeignKey(x => x.OffboardingCaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.PayrollRowId, x.Kind });
        b.HasIndex(x => new { x.TenantId, x.OffboardingCaseId, x.Kind });
        b.Property(x => x.Kind).IsRequired().HasMaxLength(32);
        b.Property(x => x.Outcome).IsRequired().HasMaxLength(32);
        b.Property(x => x.ResponsibleName).IsRequired().HasMaxLength(256);
        b.Property(x => x.EvidenceReference).IsRequired().HasMaxLength(256);
        b.Property(x => x.Notes).IsRequired().HasMaxLength(2000);
        b.Property(x => x.RecordedByUserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.RecordedAmount).HasPrecision(18, 2);
    }
}
