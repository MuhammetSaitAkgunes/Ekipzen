using Ekipzen.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class ComplianceDocumentConfiguration : IEntityTypeConfiguration<ComplianceDocument>
{
    public void Configure(EntityTypeBuilder<ComplianceDocument> b)
    {
        // Audit trigger'lı tablo: EF'in OUTPUT-clause tabanlı kaydetme stratejisiyle çakışmasın.
        b.ToTable(tb => tb.HasTrigger("TR_ComplianceDocuments_Audit"));

        b.Property(d => d.DocumentName).IsRequired().HasMaxLength(200);
        b.Property(d => d.OwnerUserId).HasMaxLength(450);
        b.Property(d => d.OwnerName).HasMaxLength(128);

        // Denetim izi kritik: personel silinse bile uyum kaydı yanlışlıkla kaybolmasın.
        b.HasOne(d => d.Employee)
            .WithMany()
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(d => d.Status);
        b.HasIndex(d => d.DueDate);
    }
}

public class ComplianceReadinessConfiguration
    : IEntityTypeConfiguration<Domain.ReadModels.ComplianceReadiness>
{
    public void Configure(EntityTypeBuilder<Domain.ReadModels.ComplianceReadiness> b)
    {
        b.HasNoKey().ToView("vw_ComplianceReadiness");
    }
}
