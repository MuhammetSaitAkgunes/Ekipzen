using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Common;
using IKPro.Domain.Entities.Tenancy;
using IKPro.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IKPro.Infrastructure.Persistence;

/// <summary>
/// Kiracının tüm verisini kalıcı olarak siler.
///
/// <b>Silme artık <c>DROP DATABASE</c>'dir.</b> Kiracının verisi kendi
/// kataloğunda olduğu için tablo tablo silmeye gerek yok — ve gerek olmaması
/// bir kolaylıktan fazlası: eski yol, silinecek tabloları EF metadata'sından
/// türetiyordu ve o listeye girmeyen her şey (view olmayan yeni bir tablo,
/// Identity'nin yan tabloları, ileride eklenecek bir şema nesnesi) sessizce
/// geride kalırdı. <c>DROP DATABASE</c> hiçbir şey kaçırmaz.
///
/// <b>Bu sınıf AMBIENT <c>AppDbContext</c> ALMAZ</b> — bilinçli. Kiracıya ait her
/// okuma, hedef kiracıya sabitlenmiş TAZE bir kapsamdan çözülür. Ambient bir
/// context çağıranın kiracısına (ya da hiçbirine) bağlı olurdu ve kiracı başına
/// veritabanında bu, yanlış katalogda çalışmak demek — üstelik sessizce.
/// Constructor'da o parametrenin bulunmaması, yanlış kullanımı derleme
/// seviyesinde imkânsız kılar.
/// </summary>
public sealed class TenantPurger(
    IPlatformDbContext platform,
    ITenantScopeFactory tenantScopeFactory,
    ITenantDatabaseProvisioner databaseProvisioner,
    IFileStorage fileStorage,
    ITenantDirectory directory,
    ITenantRegistry registry,
    ILogger<TenantPurger> logger) : ITenantPurger
{
    public async Task<bool> PurgeAsync(int tenantId, CancellationToken cancellationToken)
    {
        // Silme başlar başlamaz kiracı erişilemez olur ve öyle KALIR. Aşağıdaki
        // adımlardan biri patlarsa kiracı Purging'de takılı kalır — yarım silinmiş
        // bir kiracı asla erişilebilir bırakılmaz.
        var tenantRow = await platform.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenantRow is not null)
        {
            tenantRow.Status = TenantStatus.Purging;
            await platform.SaveChangesAsync(cancellationToken);
            // Durum değişti — kütüğü düşür ki erişim kapısı bunu ANINDA görsün.
            registry.Invalidate(tenantId);
        }

        // 1) Fiziksel dosyalar ÖNCE silinir.
        //
        // Sıra değişti ve gerekçesi de değişti: eskiden dosyalar EN SONA bırakılıyordu
        // ki silme transaction'ı geri alınırsa dosyalar boşuna gitmesin. DROP DATABASE
        // geri alınamaz, dolayısıyla o gerekçe ortadan kalktı. Şimdi belirleyici olan
        // hangi artığın daha kötü olduğu: veritabanı gidip dosyalar kalırsa diskte
        // PII (özlük evrakı, fotoğraf) sürer — KVKK riski. Dosyalar gidip veritabanı
        // kalırsa kiracı zaten Purging'de erişilemez durur ve operatör yeniden
        // çalıştırınca tamamlanır.
        //
        // Hata purge'ü YARIDA KESMEZ ama sessiz de kalmaz: LOUD loglanır ve dönüş
        // değeriyle (false) operatöre "silindi ama dosyalar diskte" durumu bildirilir.
        var dosyalarSilinemedi = false;
        try
        {
            await fileStorage.DeleteTenantSpaceAsync(tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            dosyalarSilinemedi = true;
            logger.LogError(ex,
                "Kiracı {TenantId} purge: dosya alanı silinemedi — elle temizlik gerekiyor.",
                tenantId);
        }

        // 2) Kiracının veritabanı düşürülür. Tek satırlık bu adım, eski yolun
        // onlarca DELETE'inin yerine geçer ve hiçbir tabloyu kaçıramaz.
        if (tenantRow is not null)
        {
            if (string.IsNullOrWhiteSpace(tenantRow.DatabaseName))
            {
                // Ayrılma tamamlandıktan sonra katalog adı boş bir kiracı OLMAMALI.
                // Sessizce eski satır-silme yoluna düşmek yanlış olurdu: o yol artık
                // yok ve olsaydı bile "hangi veritabanında?" sorusunun cevabı yok
                // demektir. Bu bir veri bütünlüğü arızasıdır, operatör görmeli.
                throw new InvalidOperationException(
                    $"Kiracı {tenantId} silinemiyor: DatabaseName boş. Ayrılmadan sonra " +
                    "bu duruma düşülmemeli — kiracı satırı elle incelenmelidir.");
            }

            await databaseProvisioner.DropAsync(tenantRow.DatabaseName, cancellationToken);
        }

        // 3) Platform veritabanındaki izler. Kiracının kataloğu gitti ama platform
        // hâlâ onu tanıyor; bu satırlar temizlenmezse kiracı "yok ama var" olur.
        //
        // Dizin satırları silinmezse e-posta KALICI KİLİTLENİR: dizinin birincil
        // anahtarı hâlâ eski kiracıyı gösterir → aynı e-postayla yeniden provizyon
        // denemesi rezervasyonda 409'a çarpar ve o adres bir daha asla açılamaz.
        //
        // Refresh token'lar kalıcı kalırsa işlevsel zarar yoktur (kullanıcı zaten
        // yok, token RefreshAsync'te reddedilir) ama KVKK açısından PII artığıdır.
        await platform.RefreshTokens
            .Where(t => t.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        // Abonelik de platformdadır ve kiracı silinince silinmelidir. DİKKAT: bu,
        // "geri yükleme aboneliği geri sarmaz" ilkesiyle çelişmez — orada kiracı
        // YAŞAMAYA DEVAM ediyor, burada tamamen yok ediliyor. Kalsaydı sahipsiz bir
        // faturalama satırı olur ve aynı slug yeniden açıldığında yanlış müşteriye
        // bağlanırdı.
        await platform.Subscriptions
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await directory.RemoveForTenantAsync(tenantId, cancellationToken);

        // tenantRow üstte, Purging'e geçirilirken zaten çekildi ve DbContext'te
        // izleniyor — burada yeniden sorgulamaya gerek yok.
        if (tenantRow is not null)
        {
            platform.Tenants.Remove(tenantRow);
            await platform.SaveChangesAsync(cancellationToken);
        }

        return !dosyalarSilinemedi;
    }

    public async Task<int> PurgeUnverifiedAsync(DateTime createdBeforeUtc, CancellationToken cancellationToken)
    {
        // Doğrulanmamış + eski kiracı adayları (Tenant filtresizdir).
        var adaylar = await platform.Tenants
            .Where(t => t.Status == TenantStatus.Provisioning && t.CreatedAtUtc < createdBeforeUtc)
            .Select(t => new { t.Id, t.DatabaseName })
            .ToListAsync(cancellationToken);
        if (adaylar.Count == 0) return 0;

        // Şifre belirlemiş (davet kabul edilmiş / askıya alınmış) kullanıcısı olan
        // kiracıları hariç tut — onlar "hiç kurulmamış" değil, "kurulmuş ama durumu
        // takılmış" kiracılardır ve silinmeleri gerçek veri kaybı olur.
        //
        // Kontrol her kiracının KENDİ katalogunda yapılır. Tek bir sorguyla hepsini
        // taramak (candidateIds.Contains(u.TenantId)) artık MÜMKÜN DEĞİL: kullanıcılar
        // kiracı veritabanlarına dağıldı. Eskiden ambient `context` bunu yapıyordu;
        // ayrılmadan sonra o bağlantı BOŞ paylaşılan veritabanına baktığı için
        // "hiç kimsenin şifresi yok" derdi ve koruma SESSİZCE kalkardı — yani tam
        // korunması gereken kiracılar silinirdi. Bkz.
        // TenantPurgeTests.CleanupUnverified_SifreBelirlemisKiraciyiKorur.
        //
        // Kapsam başına bir sorgu maliyeti kabul edilebilir: adaylar nadirdir
        // (yarıda kalmış kurulumlar) ve bu yol günde birkaç kez çalışır.
        List<int> toPurge = [];
        foreach (var aday in adaylar)
        {
            // Katalog hiç oluşmamışsa (provizyon 2. adımdan önce kesilmiş) kaybedilecek
            // veri YOKTUR — bu tam da temizlenmesi gereken enkazdır. Bu ayrımı yapmadan
            // "okuyamadım, atlıyorum" demek, o satırların sonsuza kadar birikmesine yol
            // açardı: temizlik servisinin var olma sebebi tam da onlar.
            if (string.IsNullOrWhiteSpace(aday.DatabaseName)
                || !await databaseProvisioner.ExistsAsync(aday.DatabaseName, cancellationToken))
            {
                logger.LogInformation(
                    "Kiracı {TenantId}: katalog yok, veri kaybı riski olmadan temizleniyor.", aday.Id);
                toPurge.Add(aday.Id);
                continue;
            }

            bool sifreliKullaniciVar;
            try
            {
                using var adayKapsami = tenantScopeFactory.Create(aday.Id);
                var adayContext = adayKapsami.Services.GetRequiredService<AppDbContext>();
                sifreliKullaniciVar = await adayContext.Set<ApplicationUser>()
                    .AnyAsync(u => u.PasswordHash != null, cancellationToken);
            }
            catch (Exception ex)
            {
                // Katalog VAR ama okunamıyor (şema eksik, izin sorunu, bağlantı arızası).
                // Burada emin olmadan silmiyoruz: yanlışlıkla veri silmektense artık bir
                // satır bırakmak yeğdir. Operatör onu stuck listesinde görür.
                logger.LogWarning(ex,
                    "Kiracı {TenantId} kullanıcıları okunamadı ama katalog VAR; otomatik " +
                    "temizlikten atlandı. /api/tenants/stuck ile elle inceleyin.",
                    aday.Id);
                continue;
            }

            if (!sifreliKullaniciVar)
            {
                toPurge.Add(aday.Id);
            }
        }
        foreach (var id in toPurge)
        {
            // Bu yol (cron/otomatik temizlik) etkileşimli bir operatörü olmadığından dönüş
            // değerini bir sonuca taşımaz; yine de dosya silme başarısızlığı burada da
            // LOUD loglanır (PurgeAsync içinde) — sessizce kaybolmaz.
            await PurgeAsync(id, cancellationToken);
        }
        return toPurge.Count;
    }
}
