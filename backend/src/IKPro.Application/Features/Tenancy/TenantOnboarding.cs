using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Tenancy;

namespace IKPro.Application.Features.Tenancy;

/// <summary>
/// Kiracı + ilk hr-admin oluşturmanın adımları — <c>ProvisionTenantCommand</c> (platform-key'li
/// operatör provizyonu) bunu doğrudan çağırır. Admin ŞİFRESİZ oluşturulur; davet e-postası
/// CreateTenantAdminAsync içinde gönderilir. Çağıran slug'ı (istemciden) ve hedef durumu belirler.
///
/// Tek çağıranı <c>ProvisionTenantCommand</c>'dir. Daha önce ikinci bir yol vardı
/// (<c>RegisterTenantCommand</c> — public self-servis kayıt, kendi slug-retry döngüsüyle);
/// self-servis kayıt tamamen kaldırıldığı için o kopya da silindi.
/// </summary>
public static class TenantOnboarding
{
    public static async Task<Tenant> CreateWithAdminAsync(
        IPlatformDbContext platform,
        IIdentityService identityService,
        ITenantDirectory directory,
        ITenantRegistry registry,
        string companyName,
        string slug,
        string adminName,
        string adminEmail,
        TenantStatus hedefDurum,
        CancellationToken cancellationToken)
    {
        // Admin e-postasını önce doğrula — kiracı yazılmadan çakışmayı yakala (orphan önlenir).
        if (await identityService.EmailExistsAsync(adminEmail, cancellationToken))
        {
            throw new ConflictException($"'{adminEmail}' e-postasıyla kayıtlı bir hesap zaten var.");
        }

        // Kiracı satırı ve uygulama verisi (admin kullanıcı) iki ayrı veritabanındadır,
        // dolayısıyla tek transaction değildir. Kiracı burada DAİMA Provisioning ile
        // yazılır — hedefDurum yalnız aşağıda, admin oluşturulduktan SONRA uygulanır.
        var tenant = new Tenant
        {
            Name = companyName,
            Slug = slug,
            Status = TenantStatus.Provisioning,
            CreatedAtUtc = DateTime.UtcNow,
        };
        platform.Tenants.Add(tenant);
        await platform.SaveChangesAsync(cancellationToken);

        // E-posta, admin kullanıcı oluşturulmadan ÖNCE rezerve edilir: eşzamanlı
        // iki kayıt aynı adresi alamaz. Rezervasyonu kullanıcı oluşturmaya
        // bıraksaydık iki müşteri yarışabilirdi.
        await directory.ReserveAsync(adminEmail, tenant.Id, cancellationToken);

        await identityService.CreateTenantAdminAsync(tenant.Id, adminName, adminEmail, tenant.Name, cancellationToken);

        // Son adım: kiracı ancak burada kullanılabilir hale gelir. Araya giren
        // bir hata kiracıyı Provisioning'de bırakır — erişilemez ama GÖRÜNÜR,
        // ve operatör yeniden deneyebilir ya da geri alabilir.
        tenant.Status = hedefDurum;
        await platform.SaveChangesAsync(cancellationToken);
        // Durum değişti — kütüğü düşür ki bir sonraki erişim kapısı sorgusu
        // bunu ANINDA görsün, süre dolmasını beklemesin.
        registry.Invalidate(tenant.Id);

        return tenant;
    }
}
