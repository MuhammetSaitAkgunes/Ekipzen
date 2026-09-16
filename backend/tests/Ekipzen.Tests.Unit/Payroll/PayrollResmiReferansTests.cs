using FluentAssertions;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.Services;

namespace Ekipzen.Tests.Unit.Payroll;

/// <summary>
/// 2026 resmî SGK, GİB ve ÇSGB tutarlarını sabitleyen altın testler.
/// Desteklenen profil: yurt içi özel sektör 5510/4-a çalışanı.
/// </summary>
public sealed class PayrollResmiReferansTests
{
    private readonly PayrollEngine _engine = new();

    private const decimal MinWageGross2026 = 33030m;
    private const decimal MinWageTaxBase2026 = 28075.50m;

    private static PayrollParameters Parameters2026 => new(
        OvertimeMultiplier: 1.5m,
        MonthlyWorkingHours: 225m,
        DefaultWorkedDays: 30,
        SgkEmployeeRate: 14m,
        UnemploymentEmployeeRate: 1m,
        SgkEmployerRate: 21.75m,
        UnemploymentEmployerRate: 2m,
        StampTaxRate: 0.759m,
        SgkBaseMin: MinWageGross2026,
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
        MinWageGross: MinWageGross2026,
        MealSgkDailyExemption: 158m,
        MealIncomeTaxDailyExemption: 300m,
        FirstDegreeDisabilityDeduction: 12000m,
        SecondDegreeDisabilityDeduction: 7000m,
        ThirdDegreeDisabilityDeduction: 3000m);

    [Fact]
    public void AsgariUcretliTamAy_NetUcret_ResmiTutarlaAyni()
    {
        var result = CalculateMinimumWage(month: 1);

        result.SgkEmployee.Should().Be(4624.20m);
        result.UnemploymentEmployee.Should().Be(330.30m);
        result.IncomeTax.Should().Be(0m);
        result.StampTax.Should().Be(0m);
        result.NetPay.Should().Be(28075.50m);
    }

    [Theory]
    [InlineData(1, 4211.33)]
    [InlineData(6, 4211.33)]
    [InlineData(7, 4537.75)]
    [InlineData(8, 5615.10)]
    [InlineData(12, 5615.10)]
    public void AsgariUcretGelirVergisiIstisnasi_KumulatifTarifeyeGoreAylikDegisir(
        int month,
        decimal expectedExemption)
    {
        var result = CalculateMinimumWage(month);

        result.MinimumWageIncomeTaxExemption.Should().Be(expectedExemption);
        result.IncomeTax.Should().Be(0m);
    }

    [Fact]
    public void BirdenFazlaIsverendeSecilmeyenAsgariUcretIstisnasiGelirVeDamgaVergisineUygulanmaz()
    {
        var input = new PayrollInput(
            GrossSalary: 50000m,
            WorkedDays: 30,
            OvertimeHours: 0m,
            PremiumPay: 0m,
            RoadAllowance: 0m,
            MealAllowance: 0m,
            BenefitPay: 0m,
            SpecialDeductions: 0m,
            PreviousTaxBase: 0m,
            ApplyMinimumWageTaxExemption: false,
            DisabilityReliefType: PayrollDisabilityReliefType.None);

        var result = _engine.Calculate(input, Parameters2026);

        result.MinimumWageIncomeTaxExemption.Should().Be(0m);
        result.MinimumWageStampTaxExemption.Should().Be(0m);
        result.StampTax.Should().Be(379.50m);
    }

    [Fact]
    public void IsverenMaliyeti_IndirimsizResmiOranlaHesaplanir()
    {
        var result = CalculateMinimumWage(month: 1);

        result.AppliedEmployerSgkRate.Should().Be(21.75m);
        result.EmployerSgk.Should().Be(7184.03m);
        result.EmployerUnemployment.Should().Be(660.60m);
        result.EmployerCost.Should().Be(40874.63m);
    }

    [Theory]
    [InlineData(5, 16.75, 39223.13)]
    [InlineData(2, 19.75, 40214.03)]
    public void IsverenMaliyeti_ResmiSektorIndirimleriniUygular(
        decimal discountPoints,
        decimal expectedRate,
        decimal expectedCost)
    {
        var input = MinimumWageInput(month: 1) with { EmployerSgkDiscount = discountPoints == 5m
            ? EmployerSgkDiscountType.ManufacturingFivePoints
            : EmployerSgkDiscountType.OtherSectorTwoPoints };

        var result = _engine.Calculate(input, Parameters2026);

        result.AppliedEmployerSgkRate.Should().Be(expectedRate);
        result.EmployerCost.Should().Be(expectedCost);
    }

    [Fact]
    public void EksikAy_SgkTabanVeTavaniPrimGununeGoreOrantilar()
    {
        var highEarner = new PayrollInput(400000m, 15, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        var lowEarner = new PayrollInput(10000m, 15, 0m, 0m, 0m, 0m, 0m, 0m, 0m);

        _engine.Calculate(highEarner, Parameters2026).SgkBase.Should().Be(148635m);
        _engine.Calculate(lowEarner, Parameters2026).SgkBase.Should().Be(16515m);
    }

    [Fact]
    public void NakitYemekYardimi_FiiliGunVeResmiGunlukSinirlarlaIstisnaEdilir()
    {
        var input = new PayrollInput(
            GrossSalary: 50000m,
            WorkedDays: 30,
            OvertimeHours: 0m,
            PremiumPay: 0m,
            RoadAllowance: 0m,
            MealAllowance: 7000m,
            BenefitPay: 0m,
            SpecialDeductions: 0m,
            PreviousTaxBase: 0m,
            MealDays: 22,
            MealTreatment: PayrollMealTreatment.CashExemptWhereNoMealProvided);

        var result = _engine.Calculate(input, Parameters2026);

        result.SgkExemptMealAllowance.Should().Be(3476m, "22 × 158 TL SGK istisnası");
        result.IncomeTaxExemptMealAllowance.Should().Be(6600m, "22 × 300 TL GV istisnası");
        result.SgkBase.Should().Be(53524m);
        result.IncomeTaxBase.Should().Be(42371.40m);
    }

    [Theory]
    [InlineData(PayrollDisabilityReliefType.FirstDegree, 12000)]
    [InlineData(PayrollDisabilityReliefType.SecondDegree, 7000)]
    [InlineData(PayrollDisabilityReliefType.ThirdDegree, 3000)]
    public void EngellilikIndirimi_ResmiAylikTutariGelirVergisiMatrahindanDusurur(
        PayrollDisabilityReliefType reliefType,
        decimal expectedDeduction)
    {
        var input = new PayrollInput(
            GrossSalary: 50000m,
            WorkedDays: 30,
            OvertimeHours: 0m,
            PremiumPay: 0m,
            RoadAllowance: 0m,
            MealAllowance: 0m,
            BenefitPay: 0m,
            SpecialDeductions: 0m,
            PreviousTaxBase: 0m,
            DisabilityReliefType: reliefType);

        var result = _engine.Calculate(input, Parameters2026);

        result.DisabilityIncomeTaxBaseDeduction.Should().Be(expectedDeduction);
        result.IncomeTaxBase.Should().Be(42500m - expectedDeduction);
    }

    [Fact]
    public void AylikBrutAsgariUcretinAltindaysaUyariUretir()
    {
        var result = _engine.Calculate(
            new PayrollInput(30000m, 30, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
            Parameters2026);

        result.Warnings.Should().Contain("Aylık brüt ücret yürürlükteki asgari ücretin altında");
    }

    [Fact]
    public void YemekGunuPrimGununuAsamaz()
    {
        var action = () => _engine.Calculate(
            new PayrollInput(50000m, 20, 0m, 0m, 0m, 1000m, 0m, 0m, 0m, MealDays: 21),
            Parameters2026);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PrimGunuOlmayanEkOdeme_SgkHesaplamazVeOnayUyarisiUretir()
    {
        var input = new PayrollInput(50000m, 0, 0m, 1000m, 0m, 0m, 0m, 0m, 0m);

        var result = _engine.Calculate(input, Parameters2026);

        result.SgkBase.Should().Be(0m);
        result.Warnings.Should().Contain("SGK prim günü olmayan ek ödeme dağıtım bekliyor");
    }

    [Fact]
    public void OtuzBirPrimGunu_KanuniSiniriAstigiIcinReddedilir()
    {
        var input = new PayrollInput(50000m, 31, 0m, 0m, 0m, 0m, 0m, 0m, 0m);

        var action = () => _engine.Calculate(input, Parameters2026);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private PayrollCalculation CalculateMinimumWage(int month) =>
        _engine.Calculate(MinimumWageInput(month), Parameters2026);

    private static PayrollInput MinimumWageInput(int month) => new(
        GrossSalary: MinWageGross2026,
        WorkedDays: 30,
        OvertimeHours: 0m,
        PremiumPay: 0m,
        RoadAllowance: 0m,
        MealAllowance: 0m,
        BenefitPay: 0m,
        SpecialDeductions: 0m,
        PreviousTaxBase: MinWageTaxBase2026 * (month - 1),
        MinimumWageIncomeTaxBaseBefore: MinWageTaxBase2026 * (month - 1),
        StatutoryProfile: PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA);
}
