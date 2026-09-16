using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.Services;

namespace Ekipzen.Application.Features.Payroll;

public sealed record TaxBracketDto(int Order, decimal? Limit, decimal Base, decimal BaseTax, decimal Rate);

/// <summary>Bordro parametreleri — payroll.js payrollDefaultSettings alanlarıyla birebir.</summary>
public sealed record PayrollSettingsDto(
    int Id,
    int Version,
    string Status,
    DateOnly EffectiveFrom,
    decimal OvertimeMultiplier,
    decimal MonthlyWorkingHours,
    int DefaultWorkedDays,
    decimal SgkEmployeeRate,
    decimal UnemploymentEmployeeRate,
    decimal SgkEmployerRate,
    decimal UnemploymentEmployerRate,
    decimal StampTaxRate,
    decimal SgkBaseMin,
    decimal SgkBaseMax,
    decimal MonthlyMinWageIncomeTaxExemption,
    decimal MonthlyMinWageStampTaxExemption,
    decimal MinWageGross,
    decimal MealSgkDailyExemption,
    decimal MealIncomeTaxDailyExemption,
    decimal OtherSectorEmployerDiscountPoints,
    decimal ManufacturingEmployerDiscountPoints,
    decimal FirstDegreeDisabilityDeduction,
    decimal SecondDegreeDisabilityDeduction,
    decimal ThirdDegreeDisabilityDeduction,
    IReadOnlyList<TaxBracketDto> TaxBrackets,
    string SourceReference,
    string? ReviewNotes,
    DateTime? ApprovedAtUtc,
    string? ApprovedByUserId);

public sealed record PayrollPeriodListItemDto(
    int Id, string Name, int Year, int Month, string Status, int EmployeeCount);

/// <summary>Dönem satırı: girdi + motor çıktısı (onaylı satırlarda kalıcı snapshot).</summary>
public sealed record PayrollRowDto(
    int Id,
    int EmployeeId,
    string Name,
    string Title,
    string Department,
    // girdiler
    decimal GrossSalary,
    PayrollStatutoryProfile StatutoryProfile,
    int WorkedDays,
    bool SgkPremiumDaysConfirmed,
    decimal OvertimeHours,
    bool OvertimeTreatmentConfirmed,
    decimal PremiumPay,
    decimal RoadAllowance,
    decimal MealAllowance,
    int MealDays,
    PayrollMealTreatment MealTreatment,
    decimal BenefitPay,
    bool BenefitPayFullyTaxableConfirmed,
    bool ApplyMinimumWageTaxExemption,
    EmployerSgkDiscountType EmployerSgkDiscount,
    string? EmployerSgkDiscountReference,
    PayrollDisabilityReliefType DisabilityReliefType,
    string? DisabilityReliefReference,
    bool OtherIncomeTaxBaseDeductionsConfirmedNone,
    decimal SpecialDeductions,
    decimal PreviousTaxBase,
    decimal PreviousEmployerIncomeTaxBase,
    string? PreviousEmployerIncomeTaxBaseReference,
    bool CumulativeIncomeTaxBaseConfirmed,
    bool IbanComplete,
    bool TimesheetComplete,
    string ApprovalStatus,
    string? Notes,
    // motor çıktısı
    decimal HourlyRate,
    decimal OvertimePay,
    decimal BaseGross,
    decimal GrossEarnings,
    decimal SgkBase,
    decimal SgkEmployee,
    decimal UnemploymentEmployee,
    decimal IncomeTaxBase,
    decimal IncomeTax,
    decimal StampTax,
    decimal TotalDeductions,
    decimal NetPay,
    decimal EmployerSgk,
    decimal EmployerUnemployment,
    decimal EmployerCost,
    decimal SgkExemptMealAllowance,
    decimal IncomeTaxExemptMealAllowance,
    decimal MinimumWageIncomeTaxExemption,
    decimal MinimumWageStampTaxExemption,
    decimal AppliedEmployerSgkRate,
    decimal DisabilityIncomeTaxBaseDeduction,
    IReadOnlyList<string> Warnings);

public sealed record PayrollTotalsDto(decimal Gross, decimal Net, decimal EmployerCost, decimal Deductions);

/// <summary>Kontrol kartı — payroll.js controls[] şekli.</summary>
public sealed record PayrollControlDto(string Label, int Value, string Level);

public sealed record PayrollPeriodDetailDto(
    int Id,
    string Name,
    int Year,
    int Month,
    string Status,
    PayrollSettingsDto Settings,
    IReadOnlyList<PayrollRowDto> Rows,
    PayrollTotalsDto Totals,
    IReadOnlyList<PayrollControlDto> Controls);

/// <summary>Çalışanın kendi bordro pusulası listesi satırı.</summary>
public sealed record MyPayslipDto(
    int PeriodId,
    int RowId,
    string PeriodName,
    decimal GrossEarnings,
    decimal TotalDeductions,
    decimal NetPay,
    string ApprovalStatus);

public static class PayrollMappings
{
    /// <summary>Onay durumu — plan Ek A: Ön Hesap | Kontrol | Onaya Hazır | Eksik Veri | Onaylandı.</summary>
    public static string ToDto(this PayrollApprovalStatus status) => status switch
    {
        PayrollApprovalStatus.PreCalc => "Ön Hesap",
        PayrollApprovalStatus.Control => "Kontrol",
        PayrollApprovalStatus.ReadyForApproval => "Onaya Hazır",
        PayrollApprovalStatus.MissingData => "Eksik Veri",
        PayrollApprovalStatus.Approved => "Onaylandı",
        _ => status.ToString(),
    };

    public static string ToDto(this PayrollPeriodStatus status) => status switch
    {
        PayrollPeriodStatus.Draft => "draft",
        PayrollPeriodStatus.Control => "control",
        PayrollPeriodStatus.Approved => "approved",
        PayrollPeriodStatus.Closed => "closed",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static readonly string[] TurkishMonths =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
         "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    /// <summary>Dönem adı: "Nisan 2026" (frontend period formatı).</summary>
    public static string PeriodName(int year, int month) => $"{TurkishMonths[month - 1]} {year}";

    /// <summary>PayrollSettings entity → motor parametreleri (dilimler Order sırasıyla).</summary>
    public static PayrollParameters ToEngineParameters(this PayrollSettings s) => new(
        s.OvertimeMultiplier,
        s.MonthlyWorkingHours,
        s.DefaultWorkedDays,
        s.SgkEmployeeRate,
        s.UnemploymentEmployeeRate,
        s.SgkEmployerRate,
        s.UnemploymentEmployerRate,
        s.StampTaxRate,
        s.SgkBaseMin,
        s.SgkBaseMax,
        s.MonthlyMinWageIncomeTaxExemption,
        s.MonthlyMinWageStampTaxExemption,
        s.TaxBrackets
            .OrderBy(b => b.Order)
            .Select(b => new PayrollBracket(b.Limit, b.Base, b.BaseTax, b.Rate))
            .ToList(),
        s.MinWageGross,
        s.MealSgkDailyExemption,
        s.MealIncomeTaxDailyExemption,
        s.OtherSectorEmployerDiscountPoints,
        s.ManufacturingEmployerDiscountPoints,
        s.FirstDegreeDisabilityDeduction,
        s.SecondDegreeDisabilityDeduction,
        s.ThirdDegreeDisabilityDeduction);

    /// <summary>PayrollEmployee girdisi → motor girdisi.</summary>
    public static PayrollInput ToEngineInput(
        this PayrollEmployee pe,
        decimal minimumWageIncomeTaxBaseBefore) => new(
        pe.GrossSalary,
        pe.WorkedDays,
        pe.OvertimeHours,
        pe.PremiumPay,
        pe.RoadAllowance,
        pe.MealAllowance,
        pe.BenefitPay,
        pe.SpecialDeductions,
        pe.PreviousTaxBase,
        pe.IbanComplete,
        pe.TimesheetComplete,
        AwaitingControl: pe.ApprovalStatus == PayrollApprovalStatus.Control,
        MealDays: pe.MealDays,
        MinimumWageIncomeTaxBaseBefore: minimumWageIncomeTaxBaseBefore,
        ApplyMinimumWageTaxExemption: pe.ApplyMinimumWageTaxExemption,
        MealTreatment: pe.MealTreatment,
        EmployerSgkDiscount: pe.EmployerSgkDiscount,
        StatutoryProfile: pe.StatutoryProfile,
        DisabilityReliefType: pe.DisabilityReliefType);
}
