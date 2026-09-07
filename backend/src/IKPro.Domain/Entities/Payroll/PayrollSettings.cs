using IKPro.Domain.Common;
using IKPro.Domain.Enums;

namespace IKPro.Domain.Entities.Payroll;

/// <summary>
/// Bordro parametreleri, yürürlük tarihine göre versiyonlu. Oranlar yüzde/binde
/// saklanır.
///
/// Neden yıl değil tarih: Türkiye'de asgari ücret ve SGK sınırları yıl ORTASINDA
/// da değişebiliyor (temmuz güncellemeleri). Yıl granülaritesi bunu ifade edemez
/// ve geçmiş dönemin yanlış parametreyle hesaplanmasına yol açar.
/// </summary>
public class PayrollSettings : AuditableEntity
{
    public int Version { get; set; } = 1;
    public PayrollSettingsStatus Status { get; set; } = PayrollSettingsStatus.Draft;
    public string SourceReference { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedByUserId { get; set; }

    /// <summary>Bu setin yürürlüğe girdiği tarih; bir sonraki set başlayana kadar geçerlidir.</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Hiçbir set verilen tarihten önce başlamıyorsa kullanılacak yedek set.</summary>
    public bool IsDefault { get; set; }

    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    public decimal MonthlyWorkingHours { get; set; } = 225m;
    public int DefaultWorkedDays { get; set; } = 30;

    // Çalışan kesintileri (%)
    public decimal SgkEmployeeRate { get; set; } = 14m;
    public decimal UnemploymentEmployeeRate { get; set; } = 1m;

    // İşveren maliyetleri (%)
    public decimal SgkEmployerRate { get; set; } = 21.75m;
    public decimal UnemploymentEmployerRate { get; set; } = 2m;

    /// <summary>Damga vergisi oranı (binde).</summary>
    public decimal StampTaxRate { get; set; } = 0.759m;

    // SGK prime esas kazanç taban/tavan
    public decimal SgkBaseMin { get; set; } = 33030m;
    public decimal SgkBaseMax { get; set; } = 297270m;

    // Asgari ücret istisnaları (aylık)
    public decimal MonthlyMinWageIncomeTaxExemption { get; set; } = 4211m;
    public decimal MonthlyMinWageStampTaxExemption { get; set; } = 250.7m;

    /// <summary>Asgari ücret brütü (istisna/kontrol hesapları için).</summary>
    public decimal MinWageGross { get; set; } = 33030m;

    /// <summary>Nakit yemek yardımının günlük SGK PEK istisnası.</summary>
    public decimal MealSgkDailyExemption { get; set; } = 158m;

    /// <summary>Nakit yemek yardımının günlük gelir vergisi istisnası.</summary>
    public decimal MealIncomeTaxDailyExemption { get; set; } = 300m;

    public decimal OtherSectorEmployerDiscountPoints { get; set; } = 2m;
    public decimal ManufacturingEmployerDiscountPoints { get; set; } = 5m;

    // Gelir Vergisi Kanunu m.31 aylık engellilik indirimi tutarları.
    public decimal FirstDegreeDisabilityDeduction { get; set; } = 12000m;
    public decimal SecondDegreeDisabilityDeduction { get; set; } = 7000m;
    public decimal ThirdDegreeDisabilityDeduction { get; set; } = 3000m;

    public ICollection<IncomeTaxBracket> TaxBrackets { get; set; } = new List<IncomeTaxBracket>();
}
