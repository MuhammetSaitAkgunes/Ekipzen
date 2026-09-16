namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// İstek bağlamındaki aktif kiracı (müşteri şirket). JWT <c>tenant</c> claim'inden
/// çözülür. <c>AppDbContext</c> global query filter ve audit interceptor bunu kullanır.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Aktif kiracı kimliği; kimlik doğrulanmamış/kiracısız bağlamda <c>null</c>.</summary>
    int? TenantId { get; }

    /// <summary>Kiracı zorunlu olan bağlamda; yoksa anlamlı hata.</summary>
    int TenantIdOrThrow();

    /// <summary>
    /// Platform/seed işlemleri için aktif kiracıyı geçici olarak sabitler (impersonation).
    /// HTTP isteği dışında (ör. veri tohumlama) global filtre ve damgalamanın doğru
    /// kiracıya çalışmasını sağlar.
    ///
    /// DOĞRUDAN ÇAĞIRMA. <c>AppDbContext</c>'in bağlantısı kapsam başına, ilk çözümde
    /// bu arayüzden okunur (bkz. <c>Infrastructure.DependencyInjection</c>) — servis/context
    /// zaten çözüldükten SONRA bu metodu çağırmak sıraya güvenmek olur ve o sıra bozulursa
    /// hata FIRLATMADAN, SESSİZCE yanlış kiracının bağlantısıyla çalışmaya devam eder.
    /// Bunun yerine <see cref="ITenantScopeFactory"/> kullan: kiracıyı kapsam DÖNMEDEN
    /// ÖNCE sabitler, bu yüzden yanlış sıra imkânsız olur.
    /// </summary>
    void Impersonate(int tenantId);
}
