using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Infrastructure.Tenancy;

/// <summary>
/// <see cref="ITenantDirectory"/> implementasyonu: dizini platform veritabanında
/// (<see cref="IPlatformDbContext.Directory"/>) tutar.
///
/// Identity'den taşındı (Faz 1b, Görev 1): dizine artık yalnız kullanıcı oluşturma
/// yolları değil, login ve bağlantı katmanı da bakacağı için Identity'ye bağımlı
/// kalmamalıydı. Davranış (idempotentlik, çakışma kuralları) AYNEN korunmuştur.
/// </summary>
public sealed class TenantDirectory(IPlatformDbContext platform) : ITenantDirectory
{
    /// <summary>
    /// Kullanıcıyı yönlendirme dizinine İDEMPOTENT yazar ve ÇAKIŞMAYI 409'a çevirir.
    ///
    /// Dizinin birincil anahtarı e-postadır; "tek e-posta = tek kiracı" kuralı
    /// burada, veritabanı seviyesinde uygulanır:
    ///   - kayıt yoksa → eklenir,
    ///   - kayıt var ve AYNI kiracıya aitse → sessizce geçilir (no-op),
    ///   - kayıt var ve BAŞKA kiracıya aitse → <c>ConflictException</c>.
    ///
    /// İdempotent olması, çağrının GÜVENLE tekrarlanabilmesini sağlar: hem önceden
    /// rezervasyon yapmış bir çağıranın ardından kullanıcı oluşturma yolunun tekrar
    /// çağırması sorun çıkarmaz, hem de rezervasyonu atlayan bir çağıran için dizine
    /// yazma güvenlik ağı olarak kalır.
    ///
    /// Yukarıdaki okuma-sonra-karar mantığı eşzamanlı iki isteği ayıramaz (TOCTOU);
    /// asıl güvence alttaki <c>catch</c>'tir — INSERT birincil anahtara çarparsa da
    /// 409'a çevrilir.
    ///
    /// Dizine yazan TEK YER burası DEĞİLDİR: <see cref="RebuildForTenantAsync"/> de
    /// dizine yazar. İkisinin semantiği kasıtlı olarak FARKLIDIR — burası kullanıcı
    /// OLUŞTURURKEN çakışmayı 409'a çevirip reddeder (yetkisiz bir yazının başka
    /// kiracıyı ele geçirmesini önler), yeniden kurma ise zaten yetkili kaynaktan
    /// (kiracının kendi Users tablosu) yazdığı için çakışan satırları reddetmek
    /// yerine atlar ve raporlar.
    /// </summary>
    public async Task ReserveAsync(string email, int tenantId, CancellationToken cancellationToken)
    {
        var normalizedEmail = TenantDirectoryEntry.Normalize(email);

        var mevcut = await platform.Directory
            .FirstOrDefaultAsync(d => d.NormalizedEmail == normalizedEmail, cancellationToken);

        if (mevcut is not null)
        {
            if (mevcut.TenantId != tenantId)
            {
                throw new ConflictException($"'{email}' e-postasıyla kayıtlı bir hesap zaten var.");
            }

            return; // Aynı kiracı için zaten rezerve/yazılmış — idempotent no-op.
        }

        platform.Directory.Add(new TenantDirectoryEntry
        {
            NormalizedEmail = normalizedEmail,
            TenantId = tenantId,
        });

        try
        {
            await platform.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Eşzamanlı bir istek yukarıdaki kontrolden SONRA, biz SaveChanges'ten ÖNCE
            // aynı e-postayı kaptı: TOCTOU. INSERT birincil anahtara çarptı.
            throw new ConflictException($"'{email}' e-postasıyla kayıtlı bir hesap zaten var.");
        }
    }

    public async Task<int?> FindTenantIdAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = TenantDirectoryEntry.Normalize(email);
        return await platform.Directory
            .Where(d => d.NormalizedEmail == normalizedEmail)
            .Select(d => (int?)d.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> RemoveForTenantAsync(int tenantId, CancellationToken cancellationToken)
    {
        var satirlar = await platform.Directory
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        if (satirlar.Count == 0)
        {
            return 0;
        }

        platform.Directory.RemoveRange(satirlar);
        await platform.SaveChangesAsync(cancellationToken);
        return satirlar.Count;
    }

    /// <summary>
    /// Bir geri yüklemeden sonra platform veritabanı geri sarılmadığı için dizin,
    /// geri yüklenmiş kiracı veritabanıyla sapabilir — dizinde olup kiracıda olmayan
    /// kullanıcılar kalabilir. Bu metod sapmayı kalıcı olmaktan çıkarır.
    ///
    /// Yazmadan ÖNCE çakışmayı denetler: geri yüklenen kullanıcılardan biri, dizinde
    /// HÂLÂ başka bir kiracıya kayıtlı bir e-postayı taşıyabilir. NormalizedEmail
    /// birincil anahtar olduğu için ham bir Add + SaveChanges tek satırda çakışsa
    /// bile TÜM SaveChangesAsync çağrısını (bu kiracının diğer geçerli satırları
    /// dahil) geri alırdı — kurtarma aracının en çok gerektiği anda hiçbir şey
    /// yapmadan çökmesi demektir. Bunun yerine çakışan satır atlanır, kalanlar
    /// yazılır ve atlananlar sonuçta açıkça raporlanır.
    /// </summary>
    public async Task<RebuildOutcome> RebuildForTenantAsync(
        int tenantId, IReadOnlyList<string> normalizedEmails, CancellationToken cancellationToken)
    {
        var mevcut = await platform.Directory
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        platform.Directory.RemoveRange(mevcut);

        var digerKiracilardaki = await platform.Directory
            .Where(d => d.TenantId != tenantId && normalizedEmails.Contains(d.NormalizedEmail))
            .Select(d => d.NormalizedEmail)
            .ToListAsync(cancellationToken);
        var cakisanlar = new HashSet<string>(digerKiracilardaki);

        var atlanan = new List<string>();
        foreach (var eposta in normalizedEmails)
        {
            if (cakisanlar.Contains(eposta))
            {
                atlanan.Add(eposta);
                continue;
            }

            platform.Directory.Add(new TenantDirectoryEntry
            {
                NormalizedEmail = eposta,
                TenantId = tenantId,
            });
        }

        await platform.SaveChangesAsync(cancellationToken);
        return new RebuildOutcome(normalizedEmails.Count - atlanan.Count, atlanan);
    }
}
