using IKPro.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IKPro.Infrastructure.Persistence.Configurations;

public class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> b)
    {
        b.Property(p => p.Name).IsRequired().HasMaxLength(64);
        // Bir dönem KİRACI İÇİNDE tektir. TenantId olmadan sistemdeki 2026/08
        // bordro dönemini yalnız TEK bir müşteri açabilirdi.
        b.HasIndex(p => new { p.TenantId, p.Year, p.Month }).IsUnique();

        b.HasOne(p => p.Settings)
            .WithMany()
            .HasForeignKey(p => p.PayrollSettingsId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PayrollSettingsConfiguration : IEntityTypeConfiguration<PayrollSettings>
{
    public void Configure(EntityTypeBuilder<PayrollSettings> b)
    {
        // Bir kiracıda aynı tarihten iki set yürürlüğe giremez (çözüm belirsizleşir).
        b.HasIndex(s => new { s.TenantId, s.EffectiveFrom }).IsUnique();
        b.Property(s => s.Version).IsConcurrencyToken();
        b.Property(s => s.SourceReference).IsRequired().HasMaxLength(2000);
        b.Property(s => s.ReviewNotes).HasMaxLength(2000);
        b.Property(s => s.ApprovedByUserId).HasMaxLength(450);
    }
}

public class IncomeTaxBracketConfiguration : IEntityTypeConfiguration<IncomeTaxBracket>
{
    public void Configure(EntityTypeBuilder<IncomeTaxBracket> b)
    {
        b.HasOne(x => x.Settings)
            .WithMany(s => s.TaxBrackets)
            .HasForeignKey(x => x.PayrollSettingsId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PayrollPeriodSummaryConfiguration
    : IEntityTypeConfiguration<IKPro.Domain.ReadModels.PayrollPeriodSummary>
{
    public void Configure(EntityTypeBuilder<IKPro.Domain.ReadModels.PayrollPeriodSummary> b)
    {
        // Salt-okunur view eşlemesi; migration'larda tablo üretilmez.
        b.HasNoKey().ToView("vw_PayrollPeriodSummary");
    }
}

public class PayrollEmployeeConfiguration : IEntityTypeConfiguration<PayrollEmployee>
{
    public void Configure(EntityTypeBuilder<PayrollEmployee> b)
    {
        // Audit trigger'lı tablo: EF'in OUTPUT-clause tabanlı kaydetme stratejisiyle çakışmasın.
        b.ToTable(tb => tb.HasTrigger("TR_PayrollEmployees_Audit"));

        b.Property(p => p.Notes).HasMaxLength(1000);
        b.Property(p => p.EmployerSgkDiscountReference).HasMaxLength(500);
        b.Property(p => p.DisabilityReliefReference).HasMaxLength(500);
        b.Property(p => p.PreviousEmployerIncomeTaxBaseReference).HasMaxLength(500);

        b.HasOne(p => p.Period)
            .WithMany(pp => pp.Employees)
            .HasForeignKey(p => p.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(p => p.Employee)
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(p => p.Result)
            .WithOne(r => r.PayrollEmployee)
            .HasForeignKey<PayrollResult>(r => r.PayrollEmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(p => new { p.PayrollPeriodId, p.EmployeeId }).IsUnique();
    }
}
