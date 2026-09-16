using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Settings;

/// <summary>
/// Abonelik / faturalama bilgisi — kiracı başına tek kayıt.
///
/// <b>PLATFORM veritabanında durur, kiracının kendi kataloğunda DEĞİL.</b> İki
/// gerekçe:
///
/// 1. "Kim hangi pakette, kimin yenilemesi yaklaşıyor, kim ödemedi" soruları
///    kiracı-ÜSTÜ sorulardır. Kayıt kiracı veritabanlarında dursaydı her fatura
///    raporu bütün veritabanlarını tek tek gezmek zorunda kalırdı.
/// 2. Bir müşteriyi yedekten geri yüklemek aboneliği GERİ SARMAMALI. Ödeme ve
///    plan geçmişi, müşterinin İK verisinden bağımsız bir ticari gerçektir;
///    dün iptal edilen bir abonelik, veri geri yüklendi diye canlanmamalıdır.
///
/// <b>Dikkat:</b> platform context'inde <c>AuditableEntityInterceptor</c> ve global
/// kiracı filtresi ÇALIŞMAZ. <see cref="Common.BaseEntity.TenantId"/> ve audit
/// alanları elle doldurulmalı, sorgular kiracıyı AÇIKÇA filtrelemelidir.
/// </summary>
public class Subscription : AuditableEntity
{
    /// <summary>Plan rozeti (settings.js: "PRO").</summary>
    public string Plan { get; set; } = string.Empty;

    /// <summary>Plan görünen adı (settings.js: "HR Master Kurumsal").</summary>
    public string? PlanName { get; set; }
    public string? BillingCycle { get; set; }
    public decimal Price { get; set; }
    public DateOnly? RenewalDate { get; set; }
    public string? PaymentMethodMasked { get; set; }
}
