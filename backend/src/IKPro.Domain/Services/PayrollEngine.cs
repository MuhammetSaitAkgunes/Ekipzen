namespace IKPro.Domain.Services;

/// <summary>Gelir vergisi dilimi (Limit null = sınırsız üst dilim).</summary>
public sealed record PayrollBracket(decimal? Limit, decimal Base, decimal BaseTax, decimal Rate);

/// <summary>
/// Motor parametreleri — frontend payrollDefaultSettings ile birebir alan adları.
/// Oranlar yüzde olarak verilir (SGK işçi 14 = %14); motor içeride /100 çevirir.
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
    IReadOnlyList<PayrollBracket> TaxBrackets);

/// <summary>
/// Motor girdisi — payroll.js payrollEmployees satırı ile birebir.
/// <paramref name="OvertimeMultiplierOverride"/> tekil hesap senaryosu için,
/// <paramref name="OvertimePayOverride"/> hazır hesaplanmış fazla mesai tutarı içindir.
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
    decimal? OvertimeMultiplierOverride = null);

/// <summary>Motor çıktısı — calculatePayrollPreview dönüş alanlarıyla birebir.</summary>
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
    IReadOnlyList<string> Warnings);

/// <summary>
/// Türk 4/a brüt→net bordro motoru — components/payroll.js
/// <c>calculatePayrollPreview</c> mantığının birebir C# kopyası.
/// </summary>
public interface IPayrollEngine
{
    PayrollCalculation Calculate(PayrollInput input, PayrollParameters parameters);
}

public sealed class PayrollEngine : IPayrollEngine
{
    public PayrollCalculation Calculate(PayrollInput input, PayrollParameters parameters)
    {
        // Oranlar: yüzde → ondalık (JS: sgkEmployeeRateDecimal = sgkEmployeeRate / 100).
        var sgkEmployeeRate = parameters.SgkEmployeeRate / 100m;
        var unemploymentEmployeeRate = parameters.UnemploymentEmployeeRate / 100m;
        var sgkEmployerRate = parameters.SgkEmployerRate / 100m;
        var unemploymentEmployerRate = parameters.UnemploymentEmployerRate / 100m;
        var stampTaxRate = parameters.StampTaxRate / 100m;
        var overtimeMultiplier = input.OvertimeMultiplierOverride ?? parameters.OvertimeMultiplier;

        // buildPayrollInput
        var hourlyRate = input.GrossSalary / parameters.MonthlyWorkingHours;
        var overtimePay = input.OvertimePayOverride
            ?? hourlyRate * input.OvertimeHours * overtimeMultiplier;
        var additionalPay = input.PremiumPay + input.RoadAllowance + input.MealAllowance + input.BenefitPay;

        // calculatePayrollPreview
        var dayRatio = input.WorkedDays / (decimal)parameters.DefaultWorkedDays;
        var baseGross = input.GrossSalary * dayRatio;
        var grossEarnings = baseGross + overtimePay + additionalPay;

        var sgkBase = Math.Min(
            Math.Max(grossEarnings, parameters.SgkBaseMin * dayRatio),
            parameters.SgkBaseMax);
        var sgkEmployee = sgkBase * sgkEmployeeRate;
        var unemploymentEmployee = sgkBase * unemploymentEmployeeRate;

        var incomeTaxBase = Math.Max(grossEarnings - sgkEmployee - unemploymentEmployee, 0m);
        var cumulativeBefore = input.PreviousTaxBase;
        var cumulativeAfter = cumulativeBefore + incomeTaxBase;
        var rawIncomeTax =
            CalculateIncomeTaxByBracket(cumulativeAfter, parameters.TaxBrackets) -
            CalculateIncomeTaxByBracket(cumulativeBefore, parameters.TaxBrackets);
        var incomeTax = Math.Max(rawIncomeTax - parameters.MonthlyMinWageIncomeTaxExemption, 0m);

        var stampTax = Math.Max(
            grossEarnings * stampTaxRate - parameters.MonthlyMinWageStampTaxExemption, 0m);

        var totalDeductions =
            sgkEmployee + unemploymentEmployee + incomeTax + stampTax + input.SpecialDeductions;
        var netPay = grossEarnings - totalDeductions;

        var employerSgk = sgkBase * sgkEmployerRate;
        var employerUnemployment = sgkBase * unemploymentEmployerRate;
        var employerCost = grossEarnings + employerSgk + employerUnemployment;

        var warnings = new List<string>();
        if (!input.TimesheetComplete) warnings.Add("Puantaj eksik");
        if (!input.IbanComplete) warnings.Add("IBAN eksik");
        if (input.GrossSalary <= 0m) warnings.Add("Brüt maaş sıfır");
        if (netPay < 0m) warnings.Add("Net ödeme negatif");
        else if (netPay == 0m) warnings.Add("Net ödeme sıfır");
        if (sgkBase >= parameters.SgkBaseMax) warnings.Add("SGK tavan kontrolü");

        // JS 190000 sabitini kullanır; parite için ilk dilim limiti esas alınır (aynı değer).
        var firstBracketLimit = parameters.TaxBrackets.Count > 0
            ? parameters.TaxBrackets[0].Limit ?? decimal.MaxValue
            : decimal.MaxValue;
        if (cumulativeBefore < firstBracketLimit && cumulativeAfter > firstBracketLimit)
        {
            warnings.Add("Vergi dilimi geçişi");
        }

        if (input.AwaitingControl) warnings.Add("Kontrol bekliyor");

        return new PayrollCalculation(
            hourlyRate, overtimePay, additionalPay, baseGross, grossEarnings,
            sgkBase, sgkEmployee, unemploymentEmployee,
            incomeTaxBase, incomeTax, stampTax,
            totalDeductions, netPay,
            employerSgk, employerUnemployment, employerCost,
            warnings);
    }

    /// <summary>calculateIncomeTaxByBracket: matrahın düştüğü dilim + o dilime kadar birikmiş vergi.</summary>
    private static decimal CalculateIncomeTaxByBracket(decimal taxBase, IReadOnlyList<PayrollBracket> brackets)
    {
        if (brackets.Count == 0) return 0m;

        var bracket = brackets.FirstOrDefault(b => taxBase <= (b.Limit ?? decimal.MaxValue))
            ?? brackets[^1];
        return bracket.BaseTax + Math.Max(taxBase - bracket.Base, 0m) * bracket.Rate;
    }
}
