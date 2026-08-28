using FluentAssertions;
using IKPro.Application.Features.Payroll.Periods;

namespace IKPro.Tests.Unit.Payroll;

public sealed class PayrollTimesheetCoverageTests
{
    [Fact]
    public void IsComplete_CombinesAttendanceApprovedLeaveWeekendsAndHolidays()
    {
        var complete = PayrollTimesheetCoverage.IsComplete(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 7),
            new DateOnly(2026, 9, 1),
            null,
            new HashSet<DateOnly> { new(2026, 9, 7) },
            new HashSet<DateOnly> { new(2026, 9, 1), new(2026, 9, 2) },
            [(new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 4))]);

        complete.Should().BeTrue(
            "1-2 Eylül puantaj, 3-4 Eylül izin, 5-6 hafta sonu ve 7 Eylül tatildir");
    }

    [Fact]
    public void IsComplete_ReturnsFalse_WhenOneExpectedWorkdayIsUncovered()
    {
        var complete = PayrollTimesheetCoverage.IsComplete(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 4),
            new DateOnly(2026, 9, 1),
            null,
            new HashSet<DateOnly>(),
            new HashSet<DateOnly>
            {
                new(2026, 9, 1), new(2026, 9, 2), new(2026, 9, 3),
            },
            []);

        complete.Should().BeFalse("4 Eylül beklenen fakat karşılanmayan iş günüdür");
    }

    [Fact]
    public void IsComplete_DoesNotRequireDatesBeforeHireDate()
    {
        var complete = PayrollTimesheetCoverage.IsComplete(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 4),
            new DateOnly(2026, 9, 3),
            null,
            new HashSet<DateOnly>(),
            new HashSet<DateOnly> { new(2026, 9, 3), new(2026, 9, 4) },
            []);

        complete.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_DoesNotRequireDatesAfterTerminationDate()
    {
        var complete = PayrollTimesheetCoverage.IsComplete(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 4),
            new HashSet<DateOnly>(),
            new HashSet<DateOnly>
            {
                new(2026, 9, 1), new(2026, 9, 2),
                new(2026, 9, 3), new(2026, 9, 4),
            },
            []);

        complete.Should().BeTrue(
            "işten ayrılma tarihinden sonraki günler puantaj kapsamına girmez");
    }
}
