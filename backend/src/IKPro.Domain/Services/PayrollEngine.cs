using IKPro.Domain.Enums;

namespace IKPro.Domain.Services;

/// <summary>Gelir vergisi dilimi (Limit null = sınırsız üst dilim).</summary>
public sealed record PayrollBracket(decimal? Limit, decimal Base, decimal BaseTax, decimal Rate);

/// <summary>
/// Yürürlük tarihli kanuni bordro parametreleri. Oranlar yüzde olarak verilir
/// (SGK işçi 14 = %14); motor içeride /100 çevirir.
/// </summary>
public sealed record PayrollParameters(
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
    IReadOnlyList<PayrollBracket> TaxBrackets,
    decimal MinWageGross = 0m,
    decimal MealSgkDailyExemption = 0m,
    decimal MealIncomeTaxDailyExemption = 0m,
    decimal OtherSectorEmployerDiscountPoints = 2m,
    decimal ManufacturingEmployerDiscountPoints = 5m,
    decimal FirstDegreeDisabilityDeduction = 0m,
    decimal SecondDegreeDisabilityDeduction = 0m,
    decimal ThirdDegreeDisabilityDeduction = 0m);

/// <summary>
/// Standart yurt içi özel sektör 4/a bordro hesabı girdisi.
/// <paramref name="WorkedDays"/> SGK prim günüdür ve ayda en fazla 30 olabilir.
/// <paramref name="MealDays"/> ise yemek yardımına hak kazanılan fiili çalışma günüdür.
/// </summary>
public sealed record PayrollInput(
    decimal GrossSalary,
    int WorkedDays,
    decimal OvertimeHours,
    decimal PremiumPay,
    decimal RoadAllowance,
    decimal MealAllowance,
    decimal BenefitPay,
    decimal SpecialDeductions,
    decimal PreviousTaxBase,
    bool IbanComplete = true,
    bool TimesheetComplete = true,
    bool AwaitingControl = false,
    decimal? OvertimePayOverride = null,
    decimal? OvertimeMultiplierOverride = null,
    int MealDays = 0,
    decimal MinimumWageIncomeTaxBaseBefore = 0m,
    bool ApplyMinimumWageTaxExemption = true,
    PayrollMealTreatment MealTreatment = PayrollMealTreatment.Unspecified,
    EmployerSgkDiscountType EmployerSgkDiscount = EmployerSgkDiscountType.None,
    PayrollStatutoryProfile StatutoryProfile = PayrollStatutoryProfile.Unspecified,
    PayrollDisabilityReliefType DisabilityReliefType = PayrollDisabilityReliefType.Unspecified);

/// <summary>Bordro hesabı ve kanuni istisnaların denetlenebilir kırılımı.</summary>
public sealed record PayrollCalculation(
    decimal HourlyRate,
    decimal OvertimePay,
    decimal AdditionalPay,
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
    IReadOnlyList<string> Warnings,
    decimal SgkExemptMealAllowance = 0m,
    decimal IncomeTaxExemptMealAllowance = 0m,
    decimal MinimumWageIncomeTaxExemption = 0m,
    decimal MinimumWageStampTaxExemption = 0m,
    decimal AppliedEmployerSgkRate = 0m,
    decimal DisabilityIncomeTaxBaseDeduction = 0m);

public interface IPayrollEngine
{
    PayrollCalculation Calculate(PayrollInput input, PayrollParameters parameters);
}

public sealed class PayrollEngine : IPayrollEngine
{
    public PayrollCalculation Calculate(PayrollInput input, PayrollParameters parameters)
    {
        Validate(input, parameters);

        var sgkEmployeeRate = parameters.SgkEmployeeRate / 100m;
        var unemploymentEmployeeRate = parameters.UnemploymentEmployeeRate / 100m;
        var employerDiscountPoints = input.EmployerSgkDiscount switch
        {
            EmployerSgkDiscountType.None => 0m,
            EmployerSgkDiscountType.OtherSectorTwoPoints => parameters.OtherSectorEmployerDiscountPoints,
            EmployerSgkDiscountType.ManufacturingFivePoints => parameters.ManufacturingEmployerDiscountPoints,
            _ => throw new ArgumentOutOfRangeException(nameof(input.EmployerSgkDiscount)),
        };
        var appliedEmployerSgkRate = Math.Max(
            parameters.SgkEmployerRate - employerDiscountPoints, 0m);
        var sgkEmployerRate = appliedEmployerSgkRate / 100m;
        var unemploymentEmployerRate = parameters.UnemploymentEmployerRate / 100m;
        var stampTaxRate = parameters.StampTaxRate / 100m;
        var overtimeMultiplier = input.OvertimeMultiplierOverride ?? parameters.OvertimeMultiplier;

        var hourlyRate = input.GrossSalary / parameters.MonthlyWorkingHours;
        var overtimePay = Money(input.OvertimePayOverride
            ?? hourlyRate * input.OvertimeHours * overtimeMultiplier);
        var additionalPay = Money(
            input.PremiumPay + input.RoadAllowance + input.MealAllowance + input.BenefitPay);

        var dayRatio = input.WorkedDays / (decimal)parameters.DefaultWorkedDays;
        var baseGross = Money(input.GrossSalary * dayRatio);
        var grossEarnings = Money(baseGross + overtimePay + additionalPay);

        var applyMealExemptions = input.MealTreatment ==
            PayrollMealTreatment.CashExemptWhereNoMealProvided;
        var sgkExemptMealAllowance = applyMealExemptions
            ? Money(Math.Min(input.MealAllowance, parameters.MealSgkDailyExemption * input.MealDays))
            : 0m;
        var incomeTaxExemptMealAllowance = applyMealExemptions
            ? Money(Math.Min(input.MealAllowance, parameters.MealIncomeTaxDailyExemption * input.MealDays))
            : 0m;

        var sgkSubjectEarnings = Math.Max(grossEarnings - sgkExemptMealAllowance, 0m);
        var sgkFloor = Money(parameters.SgkBaseMin * dayRatio);
        var sgkCeiling = Money(parameters.SgkBaseMax * dayRatio);
        var sgkBase = input.WorkedDays == 0
            ? 0m
            : Money(Math.Min(Math.Max(sgkSubjectEarnings, sgkFloor), sgkCeiling));
        var sgkEmployee = Money(sgkBase * sgkEmployeeRate);
        var unemploymentEmployee = Money(sgkBase * unemploymentEmployeeRate);

        var incomeTaxSubjectEarnings = Math.Max(
            grossEarnings - incomeTaxExemptMealAllowance, 0m);
        var incomeTaxBaseBeforeDisability = Money(Math.Max(
            incomeTaxSubjectEarnings - sgkEmployee - unemploymentEmployee, 0m));
        var disabilityMonthlyDeduction = input.DisabilityReliefType switch
        {
            PayrollDisabilityReliefType.None or PayrollDisabilityReliefType.Unspecified => 0m,
            PayrollDisabilityReliefType.FirstDegree => parameters.FirstDegreeDisabilityDeduction,
            PayrollDisabilityReliefType.SecondDegree => parameters.SecondDegreeDisabilityDeduction,
            PayrollDisabilityReliefType.ThirdDegree => parameters.ThirdDegreeDisabilityDeduction,
            _ => throw new ArgumentOutOfRangeException(nameof(input.DisabilityReliefType)),
        };
        var disabilityIncomeTaxBaseDeduction = Money(Math.Min(
            incomeTaxBaseBeforeDisability, disabilityMonthlyDeduction));
        var incomeTaxBase = Money(Math.Max(
            incomeTaxBaseBeforeDisability - disabilityIncomeTaxBaseDeduction, 0m));
        var cumulativeBefore = input.PreviousTaxBase;
        var cumulativeAfter = Money(cumulativeBefore + incomeTaxBase);
        var rawIncomeTax = Money(
            CalculateIncomeTaxByBracket(cumulativeAfter, parameters.TaxBrackets) -
            CalculateIncomeTaxByBracket(cumulativeBefore, parameters.TaxBrackets));
        var minimumWageIncomeTaxExemption = input.ApplyMinimumWageTaxExemption
            ? CalculateMinimumWageIncomeTaxExemption(input, parameters, rawIncomeTax)
            : 0m;
        var incomeTax = Money(Math.Max(rawIncomeTax - minimumWageIncomeTaxExemption, 0m));

        var grossStampTax = Money(grossEarnings * stampTaxRate);
        var minimumWageStampTaxExemption = input.ApplyMinimumWageTaxExemption
            ? parameters.MinWageGross > 0m
                ? Money(Math.Min(grossEarnings, parameters.MinWageGross) * stampTaxRate)
                : Math.Min(parameters.MonthlyMinWageStampTaxExemption, grossStampTax)
            : 0m;
        var stampTax = Money(Math.Max(grossStampTax - minimumWageStampTaxExemption, 0m));

        var totalDeductions = Money(
            sgkEmployee + unemploymentEmployee + incomeTax + stampTax + input.SpecialDeductions);
        var netPay = Money(grossEarnings - totalDeductions);

        var employerSgk = Money(sgkBase * sgkEmployerRate);
        var employerUnemployment = Money(sgkBase * unemploymentEmployerRate);
        var employerCost = Money(grossEarnings + employerSgk + employerUnemployment);

        var warnings = new List<string>();
        if (input.StatutoryProfile != PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA)
            warnings.Add("Desteklenen standart yurt içi özel sektör 4/a profili doğrulanmadı");
        if (input.DisabilityReliefType == PayrollDisabilityReliefType.Unspecified)
            warnings.Add("Engellilik indirimi durumu seçilmedi");
        if (input.DisabilityReliefType is PayrollDisabilityReliefType.FirstDegree
                or PayrollDisabilityReliefType.SecondDegree
                or PayrollDisabilityReliefType.ThirdDegree
            && disabilityMonthlyDeduction <= 0m)
            warnings.Add("Engellilik indirimi mevzuat parametresi eksik");
        if (input.WorkedDays == parameters.DefaultWorkedDays && input.GrossSalary < parameters.MinWageGross)
            warnings.Add("Aylık brüt ücret yürürlükteki asgari ücretin altında");
        if (!input.TimesheetComplete) warnings.Add("Puantaj eksik");
        if (!input.IbanComplete) warnings.Add("IBAN eksik");
        if (input.MealAllowance > 0m && input.MealTreatment == PayrollMealTreatment.Unspecified)
            warnings.Add("Yemek yardımının vergi/SGK sınıfı seçilmedi");
        if (input.MealAllowance > 0m && applyMealExemptions && input.MealDays == 0)
            warnings.Add("Yemek istisna günü eksik");
        if (input.WorkedDays == 0 && additionalPay > 0m)
            warnings.Add("SGK prim günü olmayan ek ödeme dağıtım bekliyor");
        if (input.GrossSalary <= 0m) warnings.Add("Brüt maaş sıfır");
        if (netPay < 0m) warnings.Add("Net ödeme negatif");
        else if (netPay == 0m) warnings.Add("Net ödeme sıfır");
        if (input.WorkedDays > 0 && sgkBase >= sgkCeiling) warnings.Add("SGK tavan kontrolü");

        var firstBracketLimit = parameters.TaxBrackets.Count > 0
            ? parameters.TaxBrackets[0].Limit ?? decimal.MaxValue
            : decimal.MaxValue;
        if (cumulativeBefore < firstBracketLimit && cumulativeAfter > firstBracketLimit)
            warnings.Add("Vergi dilimi geçişi");

        if (input.AwaitingControl) warnings.Add("Kontrol bekliyor");

        return new PayrollCalculation(
            hourlyRate, overtimePay, additionalPay, baseGross, grossEarnings,
            sgkBase, sgkEmployee, unemploymentEmployee,
            incomeTaxBase, incomeTax, stampTax,
            totalDeductions, netPay,
            employerSgk, employerUnemployment, employerCost,
            warnings,
            sgkExemptMealAllowance, incomeTaxExemptMealAllowance,
            minimumWageIncomeTaxExemption, minimumWageStampTaxExemption,
            appliedEmployerSgkRate,
            disabilityIncomeTaxBaseDeduction);
    }

    private static decimal CalculateMinimumWageIncomeTaxExemption(
        PayrollInput input,
        PayrollParameters parameters,
        decimal rawIncomeTax)
    {
        if (parameters.MinWageGross <= 0m)
            return Math.Min(parameters.MonthlyMinWageIncomeTaxExemption, rawIncomeTax);

        var minimumWageMonthlyTaxBase = Money(parameters.MinWageGross
            * (1m - parameters.SgkEmployeeRate / 100m - parameters.UnemploymentEmployeeRate / 100m));
        var minimumWageTaxBefore = CalculateIncomeTaxByBracket(
            input.MinimumWageIncomeTaxBaseBefore,
            parameters.TaxBrackets);
        var minimumWageTaxAfter = CalculateIncomeTaxByBracket(
            input.MinimumWageIncomeTaxBaseBefore + minimumWageMonthlyTaxBase,
            parameters.TaxBrackets);
        return Money(Math.Min(minimumWageTaxAfter - minimumWageTaxBefore, rawIncomeTax));
    }

    private static decimal CalculateIncomeTaxByBracket(
        decimal taxBase,
        IReadOnlyList<PayrollBracket> brackets)
    {
        if (brackets.Count == 0) return 0m;

        var bracket = brackets.FirstOrDefault(b => taxBase <= (b.Limit ?? decimal.MaxValue))
            ?? brackets[^1];
        return bracket.BaseTax + Math.Max(taxBase - bracket.Base, 0m) * bracket.Rate;
    }

    private static void Validate(PayrollInput input, PayrollParameters parameters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameters.MonthlyWorkingHours, 0m);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(parameters.DefaultWorkedDays, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(input.WorkedDays);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(input.WorkedDays, parameters.DefaultWorkedDays);
        ArgumentOutOfRangeException.ThrowIfNegative(input.MealDays);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(input.MealDays, input.WorkedDays);
        ArgumentOutOfRangeException.ThrowIfNegative(input.GrossSalary);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            parameters.OtherSectorEmployerDiscountPoints, parameters.SgkEmployerRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            parameters.ManufacturingEmployerDiscountPoints, parameters.SgkEmployerRate);
    }

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
