using FluentAssertions;
using IKPro.Application.Features.Employees.Offboarding;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Enums;

namespace IKPro.Tests.Unit.Employees;

public sealed class TerminationDraftCalculatorTests
{
    [Fact]
    public void Calculate_ApprovedInputs_UsesSnapshotsCeilingAndNoticeBand()
    {
        var item = Case(
            terminationDate: new DateOnly(2025, 1, 1),
            contractType: EmploymentContractType.Indefinite,
            noticeHandling: NoticeHandling.PayableToEmployee);
        var rule = Rule();

        var result = TerminationDraftCalculator.Calculate(
            item,
            new DateOnly(2024, 1, 1),
            rule,
            severanceEligible: true,
            noticeEligible: true);

        result.ServiceDays.Should().Be(366);
        result.DressedMonthlyWage.Should().Be(33000m);
        result.UnusedLeavePayment.Should().Be(3000m);
        result.SeverancePayment.Should().Be(32087.67m,
            "kıdem tabanı giydirilmiş ücret ile tavanın düşük olanıdır");
        result.NoticeDays.Should().Be(28);
        result.NoticePayment.Should().Be(30800m);
    }

    [Fact]
    public void Calculate_EligibilityNotConfirmed_DoesNotCreateLegalEntitlement()
    {
        var result = TerminationDraftCalculator.Calculate(
            Case(
                terminationDate: new DateOnly(2025, 1, 1),
                contractType: EmploymentContractType.Indefinite,
                noticeHandling: NoticeHandling.PayableToEmployee),
            new DateOnly(2024, 1, 1),
            Rule(),
            severanceEligible: false,
            noticeEligible: false);

        result.UnusedLeavePayment.Should().Be(3000m);
        result.SeverancePayment.Should().Be(0m);
        result.NoticePayment.Should().Be(0m);
    }

    [Theory]
    [InlineData(2024, 6, 30, 14)]
    [InlineData(2024, 7, 1, 28)]
    [InlineData(2025, 6, 30, 28)]
    [InlineData(2025, 7, 1, 42)]
    [InlineData(2026, 12, 31, 42)]
    [InlineData(2027, 1, 1, 56)]
    public void Calculate_NoticeThresholdBoundaries_UseCalendarMonths(
        int year, int month, int day, int expectedNoticeDays)
    {
        var start = new DateOnly(2024, 1, 1);
        var result = TerminationDraftCalculator.Calculate(
            Case(
                terminationDate: new DateOnly(year, month, day),
                contractType: EmploymentContractType.Indefinite,
                noticeHandling: NoticeHandling.PayableToEmployee),
            start,
            Rule(),
            severanceEligible: false,
            noticeEligible: true);

        result.NoticeDays.Should().Be(expectedNoticeDays);
    }

    [Fact]
    public void Calculate_FixedTermContract_DoesNotGenerateNoticePayment()
    {
        var result = TerminationDraftCalculator.Calculate(
            Case(
                terminationDate: new DateOnly(2025, 1, 1),
                contractType: EmploymentContractType.FixedTerm,
                noticeHandling: NoticeHandling.PayableToEmployee),
            new DateOnly(2024, 1, 1),
            Rule(),
            severanceEligible: false,
            noticeEligible: true);

        result.NoticeDays.Should().Be(28);
        result.NoticePayment.Should().Be(0m);
    }

    private static EmployeeOffboardingCase Case(
        DateOnly terminationDate,
        EmploymentContractType contractType,
        NoticeHandling noticeHandling) => new()
        {
            TerminationDate = terminationDate,
            ContractTypeSnapshot = contractType,
            NoticeHandling = noticeHandling,
            MonthlyGrossSalarySnapshot = 30000m,
            RegularPremiumSnapshot = 1000m,
            RoadAllowanceSnapshot = 500m,
            MealAllowanceSnapshot = 1000m,
            OtherRegularBenefitsSnapshot = 500m,
            UnusedLeaveDays = 3m,
        };

    private static TerminationCalculationRule Rule() => new()
    {
        DailyWageDivisor = 30m,
        SeveranceDaysPerYear = 30m,
        MinimumSeveranceServiceDays = 365,
        SeveranceCeiling = 32000m,
        NoticeDaysUnderSixMonths = 14,
        NoticeDaysSixToEighteenMonths = 28,
        NoticeDaysEighteenToThirtySixMonths = 42,
        NoticeDaysOverThirtySixMonths = 56,
        NoticeFirstThresholdMonths = 6,
        NoticeSecondThresholdMonths = 18,
        NoticeThirdThresholdMonths = 36,
        IncludeRegularPremiumInDressedWage = true,
        IncludeRoadAllowanceInDressedWage = true,
        IncludeMealAllowanceInDressedWage = true,
        IncludeOtherRegularBenefitsInDressedWage = true,
    };
}
