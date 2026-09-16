using Ekipzen.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        // Audit trigger'lı tablo: EF'in OUTPUT-clause tabanlı kaydetme stratejisiyle çakışmasın.
        b.ToTable(tb => tb.HasTrigger("TR_Employees_Audit"));

        b.Property(e => e.FirstName).IsRequired().HasMaxLength(128);
        b.Property(e => e.LastName).IsRequired().HasMaxLength(128);
        b.Property(e => e.Title).IsRequired().HasMaxLength(128);
        b.Property(e => e.NationalId).HasMaxLength(32);
        b.Property(e => e.UserId).HasMaxLength(450);

        // Hesaplanan (get-only) özellikler DB'ye yansımaz.
        b.Ignore(e => e.FullName);
        b.Ignore(e => e.Initials);

        // TC Kimlik No tekilliği KİRACI İÇİNDE geçerlidir: aynı kişi iki farklı
        // müşteri firmada çalışıyor olabilir. Global tekillik hem bunu engellerdi
        // hem de hata mesajıyla "bu TC başka bir müşteride kayıtlı" bilgisini
        // sızdırırdı (KVKK).
        b.HasIndex(e => new { e.TenantId, e.NationalId }).IsUnique().HasFilter("[NationalId] IS NOT NULL");

        // UserId tekilliği GLOBAL kalır: Identity kullanıcısı tek kiracıya aittir,
        // dolayısıyla bir kullanıcı yalnız bir personel kaydına bağlanabilir.
        b.HasIndex(e => e.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");

        b.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(e => e.Profile)
            .WithOne(p => p.Employee)
            .HasForeignKey<EmployeeProfile>(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeProfile> b)
    {
        b.Property(p => p.Gender).HasMaxLength(16);
        b.Property(p => p.MaritalStatus).HasMaxLength(16);
        b.Property(p => p.BloodType).HasMaxLength(8);
        b.Property(p => p.PhotoPath).HasMaxLength(500);

        b.Property(p => p.MobilePhone).HasMaxLength(32);
        b.Property(p => p.PersonalEmail).HasMaxLength(200);
        b.Property(p => p.HomeAddress).HasMaxLength(500);
        b.Property(p => p.EmergencyContactName).HasMaxLength(128);
        b.Property(p => p.EmergencyContactRelation).HasMaxLength(64);
        b.Property(p => p.EmergencyContactPhone).HasMaxLength(32);

        b.Property(p => p.RehireEligibility).HasMaxLength(64);
        b.Property(p => p.ExitCode).HasMaxLength(64);

        b.Property(p => p.Iban).HasMaxLength(34);
        b.Property(p => p.BankName).HasMaxLength(128);
        b.Property(p => p.SalaryType).HasMaxLength(16);
        b.Property(p => p.PensionStatus).HasMaxLength(64);
        b.Property(p => p.MealCard).HasMaxLength(64);

        b.Property(p => p.TshirtSize).HasMaxLength(8);
        b.Property(p => p.PantsSize).HasMaxLength(8);
        b.Property(p => p.CoatSize).HasMaxLength(8);
        b.Property(p => p.ShoeSize).HasMaxLength(8);
        b.Property(p => p.HealthNotes).HasMaxLength(2000);
    }
}

public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> b)
    {
        b.Property(d => d.DocumentType).IsRequired().HasMaxLength(64);
        b.Property(d => d.FileName).IsRequired().HasMaxLength(260);
        b.Property(d => d.FilePath).IsRequired().HasMaxLength(500);
        b.Property(d => d.ContentType).HasMaxLength(128);

        b.HasOne(d => d.Employee)
            .WithMany(e => e.Documents)
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
