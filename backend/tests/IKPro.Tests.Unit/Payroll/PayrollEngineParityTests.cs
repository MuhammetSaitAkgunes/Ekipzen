using FluentAssertions;
using IKPro.Domain.Enums;
using IKPro.Domain.Services;

namespace IKPro.Tests.Unit.Payroll;

/// <summary>
/// Eski JavaScript prototipinin hatalı oranlarına pariteyi değil, kanuni motorun
/// güvenlik sınırlarını koruyan regresyon testleri.
/// </summary>
public sealed class PayrollEngineGuardTests
{
    private readonly PayrollEngine _engine = new();

    private static PayrollParameters Parameters => new(
        OvertimeMultiplier: 1.5m,
        MonthlyWorkingHours: 225m,
        DefaultWorkedDays: 30,
        SgkEmployeeRate: 14m,
        UnemploymentEmployeeRate: 1m,
        SgkEmployerRate: 21.75m,
        UnemploymentEmployerRate: 2m,
        StampTaxRate: 0.759m,
        SgkBaseMin: 33030m,
        SgkBaseMax: 297270m,
        MonthlyMinWageIncomeTaxExemption: 4211.33m,
        MonthlyMinWageStampTaxExemption: 250.70m,
        TaxBrackets:
        [
            new PayrollBracket(190000m, 0m, 0m, 0.15m),
            new PayrollBracket(400000m, 190000m, 28500m, 0.20m),
            new PayrollBracket(1500000m, 400000m, 70500m, 0.27m),
            new PayrollBracket(5300000m, 1500000m, 367500m, 0.35m),
            new PayrollBracket(null, 5300000m, 1697500m, 0.40m),
        ],
        MinWageGross: 33030m,
        MealSgkDailyExemption: 158m,
        MealIncomeTaxDailyExemption: 300m);

    [Fact]
    public void YemekTutariVarAmaGunYoksa_OnayUyarisiUretir()
    {
        var result = _engine.Calculate(Input() with { MealAllowance = 1000m }, Parameters);

        result.Warnings.Should().Contain("Yemek yardımının vergi/SGK sınıfı seçilmedi");
        result.SgkExemptMealAllowance.Should().Be(0m);
    }

    [Fact]
    public void AsgariUcretIstisnasiBuIsverendeUygulanmayacaksa_GelirVeDamgaVergisiniDusmez()
    {
        var result = _engine.Calculate(
            Input() with { ApplyMinimumWageTaxExemption = false },
            Parameters);

        result.MinimumWageIncomeTaxExemption.Should().Be(0m);
        result.IncomeTax.Should().BeGreaterThan(0m);
        result.MinimumWageStampTaxExemption.Should().Be(0m);
        result.StampTax.Should().Be(250.70m, "çok işveren seçiminde damga istisnası da bu işverende uygulanmaz");
    }

    [Fact]
    public void IsverenIndirimiBazOraniAsamaz()
    {
        var action = () => _engine.Calculate(
            Input() with { EmployerSgkDiscount = (EmployerSgkDiscountType)99 },
            Parameters);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParasalSonuclar_KurusHassasiyetindeYuvarlanir()
    {
        var result = _engine.Calculate(Input() with { GrossSalary = 50000m, OvertimeHours = 1m }, Parameters);

        decimal.Round(result.OvertimePay, 2).Should().Be(result.OvertimePay);
        decimal.Round(result.NetPay, 2).Should().Be(result.NetPay);
        decimal.Round(result.EmployerCost, 2).Should().Be(result.EmployerCost);
    }

    private static PayrollInput Input() => new(
        GrossSalary: 33030m,
        WorkedDays: 30,
        OvertimeHours: 0m,
        PremiumPay: 0m,
        RoadAllowance: 0m,
        MealAllowance: 0m,
        BenefitPay: 0m,
        SpecialDeductions: 0m,
        PreviousTaxBase: 0m,
        MinimumWageIncomeTaxBaseBefore: 0m,
        StatutoryProfile: PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA);
}
