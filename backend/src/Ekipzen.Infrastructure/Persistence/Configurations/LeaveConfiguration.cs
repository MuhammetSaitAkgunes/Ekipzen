using Ekipzen.Domain.Entities.Leaves;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        // Audit trigger'lı tablo: EF'in OUTPUT-clause tabanlı kaydetme stratejisiyle çakışmasın.
        b.ToTable(tb => tb.HasTrigger("TR_LeaveRequests_Audit"));

        b.Property(l => l.Description).HasMaxLength(1000);
        b.Property(l => l.DecisionNote).HasMaxLength(1000);
        b.Property(l => l.DecisionByUserId).HasMaxLength(450);

        b.HasOne(l => l.Employee)
            .WithMany()
            .HasForeignKey(l => l.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(l => l.EmploymentPeriod)
            .WithMany()
            .HasForeignKey(l => l.EmploymentPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(l => l.SubstituteEmployee)
            .WithMany()
            .HasForeignKey(l => l.SubstituteEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(l => l.LeaveType)
            .WithMany(t => t.Requests)
            .HasForeignKey(l => l.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(l => new { l.EmployeeId, l.Status });
    }
}

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.Property(h => h.Name).IsRequired().HasMaxLength(200);
        // Multi-tenant: tatil benzersizliği kiracı başına (iki şirket aynı tarihi tutabilir).
        b.HasIndex(h => new { h.TenantId, h.Date }).IsUnique();
    }
}

public sealed class LeaveLedgerEntryConfiguration : IEntityTypeConfiguration<LeaveLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LeaveLedgerEntry> b)
    {
        b.Property(item => item.SourceKey).IsRequired().HasMaxLength(160);
        b.Property(item => item.SourceReference).IsRequired().HasMaxLength(256);
        b.HasIndex(item => new { item.TenantId, item.SourceKey }).IsUnique();
        b.HasIndex(item => new { item.TenantId, item.EmploymentPeriodId, item.OccurredOn });

        b.HasOne(item => item.EmploymentPeriod)
            .WithMany(period => period.LeaveLedgerEntries)
            .HasForeignKey(item => item.EmploymentPeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(item => item.LeaveRequest)
            .WithMany(request => request.LedgerEntries)
            .HasForeignKey(item => item.LeaveRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
