using FluentAssertions;
using IKPro.Domain.Services;

namespace IKPro.Tests.Unit.Payroll;

/// <summary>
/// KRİTİK KABUL KRİTERİ (plan §Doğrulama-5): C# motoru, components/payroll.js
/// motorunun birebir kopyası olmalı. Beklenen değerler gerçek JS motoru node ile
/// çalıştırılarak üretildi (payrollEmployees 5 örneği + çarpan override'lı senaryo).
/// Tolerans 0.01 TL (JS double ↔ C# decimal yuvarlama farkı).
/// </summary>
public sealed class PayrollEngineParityTests
{
    private const decimal Tolerance = 0.01m;
    private readonly PayrollEngine _engine = new();

    /// <summary>payrollDefaultSettings + taxBrackets (payroll.js) birebir.</summary>
    private static PayrollParameters DefaultParameters => new(
        OvertimeMultiplier: 1.5m,
        MonthlyWorkingHours: 225m,
        DefaultWorkedDays: 30,
        SgkEmployeeRate: 14m,
        UnemploymentEmployeeRate: 1m,
        SgkEmployerRate: 20.5m,
        UnemploymentEmployerRate: 2m,
        StampTaxRate: 0.759m,
        SgkBaseMin: 33030m,
        SgkBaseMax: 297270m,
        MonthlyMinWageIncomeTaxExemption: 4211m,
        MonthlyMinWageStampTaxExemption: 250.7m,
        TaxBrackets:
        [
            new PayrollBracket(190000m, 0m, 0m, 0.15m),
            new PayrollBracket(400000m, 190000m, 28500m, 0.20m),
            new PayrollBracket(1500000m, 400000m, 70500m, 0.27m),
            new PayrollBracket(5300000m, 1500000m, 367500m, 0.35m),
            new PayrollBracket(null, 5300000m, 1697500m, 0.40m),
        ]);

    public static TheoryData<string, PayrollInput, decimal[], string[]> Cases => new()
    {
        // beklenen dizi: [grossEarnings, sgkBase, sgkEmployee, unemploymentEmployee,
        //                 incomeTaxBase, incomeTax, stampTax, totalDeductions, netPay,
        //                 employerSgk, employerUnemployment, employerCost]
        {
            "pr-001 Ahmet Yılmaz (SGK tavana yakın, kontrol bekliyor)",
            new PayrollInput(118000m, 30, 12, 3500m, 1200m, 1800m, 3200m, 1800m, 312000m,
                IbanComplete: true, TimesheetComplete: true, AwaitingControl: true),
            [137140m, 137140m, 19199.6m, 1371.4m, 116569m, 21102.63m, 790.1926m,
             44263.8226m, 92876.1774m, 28113.7m, 2742.8m, 167996.5m],
            ["Kontrol bekliyor"]
        },
        {
            "pr-002 Selin Koç (vergi dilimi geçişi)",
            new PayrollInput(76000m, 30, 5, 2400m, 900m, 1300m, 2800m, 950m, 186000m),
            [85933.333333m, 85933.333333m, 12030.666667m, 859.333333m, 73043.333333m,
             10197.666667m, 401.534m, 24439.200667m, 61494.132667m,
             17616.333333m, 1718.666667m, 105268.333333m],
            ["Vergi dilimi geçişi"]
        },
        {
            "pr-003 Burak Demir (eksik IBAN, 28 gün, yüksek prim)",
            new PayrollInput(54000m, 28, 0, 18500m, 800m, 1300m, 2100m, 1200m, 148000m,
                IbanComplete: false, TimesheetComplete: true),
            [73100m, 73100m, 10234m, 731m, 62135m, 6116m, 304.129m,
             18585.129m, 54514.871m, 14985.5m, 1462m, 89547.5m],
            ["IBAN eksik", "Vergi dilimi geçişi"]
        },
        {
            "pr-004 Ayşe Vural (standart akış)",
            new PayrollInput(62000m, 30, 0, 0m, 900m, 1600m, 2400m, 700m, 167000m),
            [66900m, 66900m, 9366m, 669m, 56865m, 6012m, 257.071m,
             17004.071m, 49895.929m, 13714.5m, 1338m, 81952.5m],
            ["Vergi dilimi geçişi"]
        },
        {
            "pr-005 Mert Can (eksik puantaj, 27 gün, 19 saat mesai)",
            new PayrollInput(98000m, 27, 19, 2700m, 1200m, 1800m, 3500m, 1500m, 274000m,
                IbanComplete: true, TimesheetComplete: false),
            [109813.333333m, 109813.333333m, 15373.866667m, 1098.133333m, 93341.333333m,
             14457.266667m, 582.7832m, 33012.049867m, 76801.283467m,
             22511.733333m, 2196.266667m, 134521.333333m],
            ["Puantaj eksik"]
        },
        {
            "tekil hesap senaryosu (çarpan 2.0 override)",
            new PayrollInput(85000m, 30, 10, 1500m, 800m, 1200m, 0m, 500m, 185000m,
                OvertimeMultiplierOverride: 2m),
            [96055.555556m, 96055.555556m, 13447.777778m, 960.555556m, 81647.222222m,
             11868.444444m, 478.361667m, 27255.139444m, 68800.416111m,
             19691.388889m, 1921.111111m, 117668.055556m],
            ["Vergi dilimi geçişi"]
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Calculate_MatchesJsEngineOutput(
        string label, PayrollInput input, decimal[] expected, string[] expectedWarnings)
    {
        var result = _engine.Calculate(input, DefaultParameters);

        result.GrossEarnings.Should().BeApproximately(expected[0], Tolerance, label);
        result.SgkBase.Should().BeApproximately(expected[1], Tolerance, label);
        result.SgkEmployee.Should().BeApproximately(expected[2], Tolerance, label);
        result.UnemploymentEmployee.Should().BeApproximately(expected[3], Tolerance, label);
        result.IncomeTaxBase.Should().BeApproximately(expected[4], Tolerance, label);
        result.IncomeTax.Should().BeApproximately(expected[5], Tolerance, label);
        result.StampTax.Should().BeApproximately(expected[6], Tolerance, label);
        result.TotalDeductions.Should().BeApproximately(expected[7], Tolerance, label);
        result.NetPay.Should().BeApproximately(expected[8], Tolerance, label);
        result.EmployerSgk.Should().BeApproximately(expected[9], Tolerance, label);
        result.EmployerUnemployment.Should().BeApproximately(expected[10], Tolerance, label);
        result.EmployerCost.Should().BeApproximately(expected[11], Tolerance, label);

        result.Warnings.Should().Equal(expectedWarnings, label);
    }

    [Fact]
    public void Calculate_ClampsSgkBaseToFloor_WhenEarningsBelowMinimum()
    {
        // Brüt kazanç SGK tabanının altında: matrah tabana (gün oranlı) çekilir.
        var input = new PayrollInput(20000m, 30, 0, 0m, 0m, 0m, 0m, 0m, 0m);
        var result = _engine.Calculate(input, DefaultParameters);

        result.SgkBase.Should().Be(33030m, "taban clamp: max(20000, 33030×1)");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_ClampsSgkBaseToCeiling_AndFlagsWarning()
    {
        var input = new PayrollInput(400000m, 30, 0, 0m, 0m, 0m, 0m, 0m, 0m);
        var result = _engine.Calculate(input, DefaultParameters);

        result.SgkBase.Should().Be(297270m, "tavan clamp");
        result.Warnings.Should().Contain("SGK tavan kontrolü");
    }

    [Fact]
    public void Calculate_MinWageExemptions_CanZeroOutTaxes()
    {
        // Düşük matrah: GV istisnası (4211) ve damga istisnası (250.7) vergileri sıfırlayabilir.
        var input = new PayrollInput(30000m, 30, 0, 0m, 0m, 0m, 0m, 0m, 0m);
        var result = _engine.Calculate(input, DefaultParameters);

        // rawGV = %15 × (33030 taban matrah üzerinden değil, kazanç−kesintiler) → istisna altında kalırsa 0.
        result.IncomeTax.Should().BeGreaterThanOrEqualTo(0m);
        result.StampTax.Should().BeApproximately(
            Math.Max(30000m * 0.00759m - 250.7m, 0m), Tolerance);
    }

    [Fact]
    public void Calculate_FlagsZeroGrossAndNegativeNet()
    {
        var result = _engine.Calculate(
            new PayrollInput(
                GrossSalary: 0m, WorkedDays: 30, OvertimeHours: 0m,
                PremiumPay: 0m, RoadAllowance: 0m, MealAllowance: 0m, BenefitPay: 0m,
                SpecialDeductions: 100m, PreviousTaxBase: 0m),
            DefaultParameters);

        result.NetPay.Should().BeNegative();
        result.Warnings.Should().Contain(["Brüt maaş sıfır", "Net ödeme negatif"]);
    }
}
