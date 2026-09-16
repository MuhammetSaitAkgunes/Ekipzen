using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.Extensions.Logging;

namespace Ekipzen.Application.Features.Tenancy;

/// <summary>
/// Kiracı kurmanın TEK yolu. Adımların sırası kritiktir ve her biri aşağıda
/// gerekçesiyle işaretlidir.
///
/// Statik bir yardımcıydı; kiracı başına veritabanına geçilince bağımlılık sayısı
/// (platform, kimlik, dizin, kütük, katalog sağlayıcı, şema hazırlayıcı, log)
/// parametre listesiyle taşınamaz hale geldi ve servise dönüştü.
/// </summary>
public sealed class TenantOnboarding(
    IPlatformDbContext platform,
    IIdentityService identityService,
    ITenantDirectory directory,
    ITenantRegistry registry,
    ITenantDatabaseProvisioner databaseProvisioner,
    ITenantDatabaseNamer databaseNamer,
    ITenantSchemaInitializer schemaInitializer,
    ILogger<TenantOnboarding> logger)
{
    public async Task<Tenant> CreateWithAdminAsync(
        string companyName,
        string slug,
        string adminName,
        string adminEmail,
        CancellationToken cancellationToken)
    {
        // Ön kontrol: e-posta başka bir kiracıda kayıtlıysa hiçbir şeye başlama.
        // Kiracı satırı yazıldıktan sonra fark etmek, temizlenmesi gereken bir
        // artık bırakırdı.
        if (await identityService.EmailExistsAsync(adminEmail, cancellationToken))
        {
            throw new ConflictException($"'{adminEmail}' e-postasıyla kayıtlı bir hesap zaten var.");
        }

        var katalogAdi = databaseNamer.For(slug);

        // Kiracının katalogu ZATEN VARSA reddet. Başarısız bir denemeden kalmış
        // veritabanını sessizce yeniden kullanmak, iki müşterinin verisinin aynı
        // yerde toplanması demektir — bu ürünün en pahalı hatası olurdu. Operatör
        // önce artığı bilinçli olarak temizlemeli.
        if (await databaseProvisioner.ExistsAsync(katalogAdi, cancellationToken))
        {
            throw new ConflictException(
                $"'{katalogAdi}' veritabanı sunucuda zaten var. Yarıda kalmış bir kurulumdan " +
                "kalmış olabilir; devam etmeden önce elle incelenip temizlenmelidir.");
        }

        // ADIM 1 — Platform, TEK transaction: kiracı satırı + e-posta rezervasyonu.
        // İkisi birlikte olmalı: rezervasyon çakışırsa (eşzamanlı bir istek aynı
        // adresi kaptıysa) geriye sahipsiz bir kiracı satırı kalmamalı; o satır hem
        // slug'ı işgal ederdi hem de temizlenmesi gereken bir artık olurdu.
        //
        // E-posta kullanıcı oluşturulmadan ÖNCE rezerve edilir: rezervasyonu
        // kullanıcı oluşturmaya bıraksaydık iki müşteri aynı adres için yarışabilirdi.
        var tenant = new Tenant
        {
            Name = companyName,
            Slug = slug,
            DatabaseName = katalogAdi,
            // Kiracı DAİMA Provisioning ile doğar; Active'e ancak her şey bittiğinde geçer.
            Status = TenantStatus.Provisioning,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await using (var islem = await platform.BeginTransactionAsync(cancellationToken))
        {
            platform.Tenants.Add(tenant);
            await platform.SaveChangesAsync(cancellationToken);
            await directory.ReserveAsync(adminEmail, tenant.Id, cancellationToken);
            await islem.CommitAsync(cancellationToken);
        }

        // Kütük bu kiracıyı henüz tanımıyor; bağlantı çözücünün katalog adını
        // görebilmesi için önbelleği temiz bırak. (Yeni kimlik olduğu için ıska
        // zaten kaçınılmaz, ama açıkça düşürmek niyeti görünür kılar.)
        registry.Invalidate(tenant.Id);

        // ADIM 2 — fiziksel veritabanı.
        await databaseProvisioner.CreateAsync(katalogAdi, cancellationToken);

        // ADIM 3+4 — şema (migration) ve roller. Roller admin'den ÖNCE gelmeli:
        // rol yoksa AddToRoleAsync sessizce düşer ve admin yetkisiz kalır.
        await schemaInitializer.PrepareAsync(tenant.Id, cancellationToken);

        // ADIM 5 — admin kullanıcı (şifresiz) + davet e-postası.
        await identityService.CreateTenantAdminAsync(
            tenant.Id, adminName, adminEmail, tenant.Name, cancellationToken);

        // ADIM 6 — kiracı ancak burada kullanılabilir hale gelir. Araya giren bir
        // hata onu Provisioning'de bırakır: erişilemez ama GÖRÜNÜR
        // (GET /api/tenants/stuck), yani operatör fark eder ve temizleyebilir.
        tenant.Status = TenantStatus.Active;
        await platform.SaveChangesAsync(cancellationToken);
        // Durum değişti — kütüğü düşür ki erişim kapısı bunu ANINDA görsün.
        registry.Invalidate(tenant.Id);

        logger.LogInformation(
            "Kiracı provizyonu tamamlandı: {TenantId} ({Slug}) → {Katalog}",
            tenant.Id, tenant.Slug, katalogAdi);

        return tenant;
    }
}
