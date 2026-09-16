using Ekipzen.Domain.Entities.Leaves;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Application.Features.Setup;

/// <summary>
/// Yalnız 2026 başlangıç şablonu; her çağrı yeni, izlenmeyen kayıtlar üretir.
/// Tam gün tatiller içerir; yarım gün ve işyeri izin politikası ayrıca doğrulanmalıdır.
/// Bordro değerleri uzman incelemesi ve açık onay olmadan hesaplarda kullanılamaz.
/// </summary>
public static class TenantBaseline2026
{
    public const int Year = 2026;

    public static LeaveType[] CreateLeaveTypes() =>
    [
        new LeaveType { Name = "Yıllık İzin", Code = "Yıllık", DeductsFromAnnualBalance = true, RequiresApproval = true },
        new LeaveType { Name = "Mazeret İzni", Code = "Mazeret", DeductsFromAnnualBalance = false, RequiresApproval = true },
        new LeaveType { Name = "Raporlu", Code = "Raporlu", DeductsFromAnnualBalance = false, RequiresApproval = false },
        new LeaveType { Name = "Uzaktan Çalışma", Code = "Uzaktan", DeductsFromAnnualBalance = false, RequiresApproval = true }
    ];

    public static Holiday[] CreateFullDayHolidays() =>
    [
        new Holiday { Date = new DateOnly(2026, 1, 1), Name = "Yılbaşı" },
        new Holiday { Date = new DateOnly(2026, 3, 20), Name = "Ramazan Bayramı 1. Gün" },
        new Holiday { Date = new DateOnly(2026, 3, 21), Name = "Ramazan Bayramı 2. Gün" },
        new Holiday { Date = new DateOnly(2026, 3, 22), Name = "Ramazan Bayramı 3. Gün" },
        new Holiday { Date = new DateOnly(2026, 4, 23), Name = "Ulusal Egemenlik ve Çocuk Bayramı" },
        new Holiday { Date = new DateOnly(2026, 5, 1), Name = "Emek ve Dayanışma Günü" },
        new Holiday { Date = new DateOnly(2026, 5, 19), Name = "Atatürk'ü Anma, Gençlik ve Spor Bayramı" },
        new Holiday { Date = new DateOnly(2026, 5, 27), Name = "Kurban Bayramı 1. Gün" },
        new Holiday { Date = new DateOnly(2026, 5, 28), Name = "Kurban Bayramı 2. Gün" },
        new Holiday { Date = new DateOnly(2026, 5, 29), Name = "Kurban Bayramı 3. Gün" },
        new Holiday { Date = new DateOnly(2026, 5, 30), Name = "Kurban Bayramı 4. Gün" },
        new Holiday { Date = new DateOnly(2026, 7, 15), Name = "Demokrasi ve Millî Birlik Günü" },
        new Holiday { Date = new DateOnly(2026, 8, 30), Name = "Zafer Bayramı" },
        new Holiday { Date = new DateOnly(2026, 10, 29), Name = "Cumhuriyet Bayramı" }
    ];

    public static PayrollSettings CreatePayrollDraft()
    {
        return new PayrollSettings
        {
            Version = 1,
            Status = PayrollSettingsStatus.Draft,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            IsDefault = true,
            SourceReference = "https://www.sgk.gov.tr/Content/Post/2e0c9e1a-2cfe-4456-af10-49d3de0c58ba/Prime-Esas-Kazanc-Miktarlari-2026-01-14-10-35-39; https://www.sgk.gov.tr/Content/Post/c7812ea8-5087-413f-aeb5-d3c1d153e11a/Isveren-Prim-Oranlari-2026-01-13-04-52-38; https://usak.gib.gov.tr/vergi-konulari/1_bireysel/11_ucret_geliri/11; https://gib.gov.tr/vergi-konulari/1_bireysel/8_engelliler_icin_vergi_avantajlari/8; https://www.csgb.gov.tr/tr/poco-pages/asgari-ucret/",
            OvertimeMultiplier = 1.5m,
            MonthlyWorkingHours = 225m,
            DefaultWorkedDays = 30,
            SgkEmployeeRate = 14m,
            UnemploymentEmployeeRate = 1m,
            SgkEmployerRate = 21.75m,
            UnemploymentEmployerRate = 2m,
            StampTaxRate = 0.759m,
            SgkBaseMin = 33030m,
            SgkBaseMax = 297270m,
            // İstisna = asgari ücret üzerinden hesaplanan gelir vergisi:
            // (33.030 − %15 SGK) × %15 = 28.075,50 × 0,15 = 4.211,325 → 4.211,33.
            // Yuvarlanmış 4.211 kullanıldığında asgari ücretliden 0,325 TL gelir
            // vergisi kesiliyor ve net, resmî 28.075,50 yerine 28.075,175 çıkıyordu.
            MonthlyMinWageIncomeTaxExemption = 4211.33m,
            MonthlyMinWageStampTaxExemption = 250.7m, // 33.030 × binde 7,59 = 250,6977
            MinWageGross = 33030m,
            MealSgkDailyExemption = 158m,
            MealIncomeTaxDailyExemption = 300m,
            OtherSectorEmployerDiscountPoints = 2m,
            ManufacturingEmployerDiscountPoints = 5m,
            FirstDegreeDisabilityDeduction = 12000m,
            SecondDegreeDisabilityDeduction = 7000m,
            ThirdDegreeDisabilityDeduction = 3000m,
            TaxBrackets =
            {
                new IncomeTaxBracket { Order = 1, Limit = 190000m, Base = 0m, BaseTax = 0m, Rate = 0.15m },
                new IncomeTaxBracket { Order = 2, Limit = 400000m, Base = 190000m, BaseTax = 28500m, Rate = 0.20m },
                new IncomeTaxBracket { Order = 3, Limit = 1500000m, Base = 400000m, BaseTax = 70500m, Rate = 0.27m },
                new IncomeTaxBracket { Order = 4, Limit = 5300000m, Base = 1500000m, BaseTax = 367500m, Rate = 0.35m },
                new IncomeTaxBracket { Order = 5, Limit = null, Base = 5300000m, BaseTax = 1697500m, Rate = 0.40m },
            },
        };
    }
}
