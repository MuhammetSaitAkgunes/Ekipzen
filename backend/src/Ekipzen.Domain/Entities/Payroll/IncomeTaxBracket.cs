using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Payroll;

/// <summary>
/// Artan oranlı gelir vergisi dilimi. Son dilimin <see cref="Limit"/> değeri
/// sınırsızdır (null = ∞). Kümülatif matrah üzerinden uygulanır.
/// </summary>
public class IncomeTaxBracket : AuditableEntity
{
    public int PayrollSettingsId { get; set; }
    public PayrollSettings? Settings { get; set; }

    public int Order { get; set; }

    /// <summary>Dilim üst sınırı; null ise sınırsız (en üst dilim).</summary>
    public decimal? Limit { get; set; }

    /// <summary>Dilimin başladığı kümülatif matrah.</summary>
    public decimal Base { get; set; }

    /// <summary>Bu dilime kadar birikmiş vergi.</summary>
    public decimal BaseTax { get; set; }

    /// <summary>Dilim oranı (ondalık, ör. 0.15).</summary>
    public decimal Rate { get; set; }
}
