using IKPro.Domain.Common;
using IKPro.Domain.Enums;

namespace IKPro.Domain.Entities.Organization;

/// <summary>
/// İşten çıkış taslak hesabında kullanılabilecek, yürürlük tarihli ve uzman
/// onayı sonrasında değiştirilemeyen kural sürümü.
/// </summary>
public sealed class TerminationCalculationRule : AuditableEntity
{
    public int Version { get; set; } = 1;
    public DateOnly EffectiveFrom { get; set; }
    public TerminationCalculationRuleStatus Status { get; set; }
        = TerminationCalculationRuleStatus.Draft;

    public decimal DailyWageDivisor { get; set; } = 30m;
    public decimal SeveranceDaysPerYear { get; set; } = 30m;
    public int MinimumSeveranceServiceDays { get; set; } = 365;
    public decimal SeveranceCeiling { get; set; }
    public int NoticeDaysUnderSixMonths { get; set; } = 14;
    public int NoticeDaysSixToEighteenMonths { get; set; } = 28;
    public int NoticeDaysEighteenToThirtySixMonths { get; set; } = 42;
    public int NoticeDaysOverThirtySixMonths { get; set; } = 56;
    public int NoticeFirstThresholdMonths { get; set; } = 6;
    public int NoticeSecondThresholdMonths { get; set; } = 18;
    public int NoticeThirdThresholdMonths { get; set; } = 36;

    public bool IncludeRegularPremiumInDressedWage { get; set; } = true;
    public bool IncludeRoadAllowanceInDressedWage { get; set; } = true;
    public bool IncludeMealAllowanceInDressedWage { get; set; } = true;
    public bool IncludeOtherRegularBenefitsInDressedWage { get; set; } = true;

    public string SourceReference { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }
}
