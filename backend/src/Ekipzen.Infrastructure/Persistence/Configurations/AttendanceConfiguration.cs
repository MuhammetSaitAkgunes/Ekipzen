using Ekipzen.Domain.Entities.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.Property(a => a.Note).HasMaxLength(500);

        b.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir çalışanın bir güne tek puantaj kaydı olur.
        b.HasIndex(a => new { a.EmployeeId, a.WorkDate }).IsUnique();
        b.HasIndex(a => a.WorkDate);
    }
}

public class MonthlyAttendanceSummaryConfiguration
    : IEntityTypeConfiguration<Domain.ReadModels.MonthlyAttendanceSummary>
{
    public void Configure(EntityTypeBuilder<Domain.ReadModels.MonthlyAttendanceSummary> b)
    {
        // Salt-okunur view eşlemesi; migration'larda tablo üretilmez.
        b.HasNoKey().ToView("vw_MonthlyAttendanceSummary");
    }
}
