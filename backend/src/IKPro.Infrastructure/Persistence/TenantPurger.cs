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
/// Kiracının tüm verisini FK-güvenli sırada siler. Silinecek tablolar EF model
/// metadata'sından türetilir (ITenantScoped + PK'lı = view'ler hariç); yeni bir
/// kiracı-kapsamlı tablo eklendiğinde otomatik kapsama girer (unutma sızıntısı yok).
/// </summary>
public sealed class TenantPurger(
    AppDbContext context,
    IPlatformDbContext platform,
    ITenantScopeFactory tenantScopeFactory,
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

        // Silinecek kiracı için AYRI, TAZE bir kapsam açılır. Bu sınıfa constructor'da
        // enjekte edilen `context`, TenantPurger inşa edilirken ÇOKTAN kurulmuş olabilir —
        // ya çağıranın kendi kiracısıyla (operatör isteğiyse) ya da hiç kiracısız (arka
        // plan servisiyse). O bağlantıyı burada impersone etmeye çalışmak SIRAYA GÜVENMEK
        // olurdu: AppDbContext'in bağlantısı kapsam başına, ilk çözümde ICurrentTenant'tan
        // okunuyor (bkz. Infrastructure.DependencyInjection) — yani Impersonate BURADA
        // çağrılsa bile `context` zaten yanlış (ya da kiracısız) bağlantıyla kurulmuş olurdu,
        // ve bu SESSİZCE olurdu. Faz 1b'de tüm kiracılar aynı DB'yi paylaştığı için zararsız,
        // Faz 2'de kiracı başına DB'ye geçilince "yanlış kiracının verisini silme/görememe"
        // demek olur. Bu yüzden asıl silme, `tenantId`'ye özel taze bir kapsamdan çözülen
        // AppDbContext ile yapılır (bkz. ITenantScopeFactory).
        using var kapsam = tenantScopeFactory.Create(tenantId);
        var purgeContext = kapsam.Services.GetRequiredService<AppDbContext>();

        var tables = TenantScopedTablesInDeleteOrder(purgeContext);

        // Tablo adları metadata'dan çözülür (Identity tabloları yeniden adlandırılmış: Users/UserRoles…).
        var userTable = QualifiedTableName(purgeContext, typeof(ApplicationUser));

        await using var tx = await purgeContext.Database.BeginTransactionAsync(cancellationToken);

        // EF1002 bilinçli olarak bastırılıyor: tenantId SQL'e PARAMETRE olarak geçiyor
        // ({0} yer tutucusu + object[]), dolayısıyla enjeksiyona kapalı. Enterpole edilen
        // tek şey EF metadata'sından çözülen tablo adları — tablo adı SQL'de zaten
        // parametreleştirilemez ve kullanıcı girdisinden gelmez.
#pragma warning disable EF1002
        // 1) ITenantScoped tablolar (çocuk→ebeveyn). Açık TenantId filtresi (impersonation'a bağlı değil).
        foreach (var table in tables)
        {
            await purgeContext.Database.ExecuteSqlRawAsync(
                $"DELETE FROM {table} WHERE [TenantId] = {{0}}", new object[] { tenantId }, cancellationToken);
        }

        // 2) Kimlik: kullanıcılar (UserRoles/UserClaims cascade). Refresh token'lar
        // BURADA SİLİNMEZ — platform veritabanındadır (uygulama transaction'ının
        // dışında), aşağıda ayrı silinir (bkz. adım 3).
        await purgeContext.Database.ExecuteSqlRawAsync(
            $"DELETE FROM {userTable} WHERE [TenantId] = {{0}}", new object[] { tenantId }, cancellationToken);
#pragma warning restore EF1002

        await tx.CommitAsync(cancellationToken);

        // 3) Kiracı satırı, dizin kayıtları VE refresh token'lar platform veritabanındadır
        // — uygulama transaction'ı onları kapsamaz. Bu yüzden ayrı silinir. Dizin satırları
        // silinmezse e-posta KALICI KİLİTLENİR: purge sonrası Identity'de kullanıcı
        // kalmadığından EmailExistsAsync false döner ama dizinin birincil anahtarı hâlâ
        // eski kiracıyı gösterir → aynı e-postayla yeniden kayıt/provizyon denemesi
        // rezervasyonda 409'a çarpar ve asla açılamaz. Buradan önce patlarsa kiracı
        // satırı/dizin/token kalır ama uygulama verisi gitmiştir; durum Purging'de kaldığı
        // için kiracı erişilemez olarak durur (bkz. Task 6) ve operatör yeniden
        // çalıştırabilir (dizin silme de idempotenttir — kayıt yoksa no-op).
        //
        // Refresh token'lar kalıcı kalırsa gerçek bir zarar yoktur (kullanıcı zaten
        // silindi, token'lar RefreshAsync'te kullanıcı bulunamayınca reddedilir) ama
        // KVKK açısından PII artığı sayılır — bu yüzden purge onları da temizler.
        await platform.RefreshTokens
            .Where(t => t.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await directory.RemoveForTenantAsync(tenantId, cancellationToken);

        // tenantRow üstte, Purging'e geçirilirken zaten çekildi ve DbContext'te
        // izleniyor (bkz. metod başı) — burada yeniden sorgulamaya gerek yok.
        if (tenantRow is not null)
        {
            platform.Tenants.Remove(tenantRow);
            await platform.SaveChangesAsync(cancellationToken);
        }

        // 4) Fiziksel dosyalar (DB tutarlılığından SONRA — rollback olursa dosya kaybı olmasın).
        // Kiracının TÜM dosya alanı silinir. Eskiden yalnız EmployeeDocuments yolları tek tek
        // siliniyordu; bu, çalışan fotoğraflarını ve şirket logosunu KAÇIRIYORDU. Alan silme
        // ileride eklenecek her dosya türünü de otomatik kapsar.
        // Hata sessizce yutulmaz: DB temizlenip dosyalar kalırsa KVKK açısından PII riski
        // sürer, elle temizlik için LOUD loglanır. Hata purge'ü yarıda kesmez — ama artık
        // çağırana da sessiz kalmaz: dönüş değeri (false) operatöre "200 OK ama dosyalar
        // hâlâ diskte" durumunu görünür kılar (bkz. ITenantPurger.PurgeAsync sözleşmesi).
        try
        {
            await fileStorage.DeleteTenantSpaceAsync(tenantId, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Kiracı {TenantId} purge: dosya alanı silinemedi — elle temizlik gerekiyor.",
                tenantId);
            return false;
        }
    }

    public async Task<int> PurgeUnverifiedAsync(DateTime createdBeforeUtc, CancellationToken cancellationToken)
    {
        // Doğrulanmamış + eski kiracı adayları (Tenant filtresizdir).
        var candidateIds = await platform.Tenants
            .Where(t => t.Status == TenantStatus.Provisioning && t.CreatedAtUtc < createdBeforeUtc)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        if (candidateIds.Count == 0) return 0;

        // Şifre belirlemiş (davet kabul edilmiş / askıya alınmış) kullanıcısı olan kiracıları
        // hariç tut. Bu sorgu birden çok kiracıyı BİRDEN taradığı için (candidateIds.Contains)
        // ve ApplicationUser ITenantScoped OLMADIĞINDAN (kiracı-üstü, global filtre yok)
        // yapısı gereği tek bir kiracıya sabitlenemez — burada bilerek `context` alanı
        // (constructor'da enjekte edilen, kiracısız/ambient bağlantı) kullanılır. Faz 1b'de
        // tüm kiracılar aynı DB'yi paylaştığı için doğru sonucu verir; Faz 2'de kiracı başına
        // DB'ye geçilince bu sorgu kiracı-ötesi bir mekanizmaya (ör. platform DB'sinde
        // özetlenen bir alan) taşınmalı — TenantPurger'ın bugünkü kapsamı bunu çözmez.
        var verifiedTenantIds = await context.Set<ApplicationUser>()
            .Where(u => u.PasswordHash != null && candidateIds.Contains(u.TenantId))
            .Select(u => u.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var toPurge = candidateIds.Except(verifiedTenantIds).ToList();
        foreach (var id in toPurge)
        {
            // Bu yol (cron/otomatik temizlik) etkileşimli bir operatörü olmadığından dönüş
            // değerini bir sonuca taşımaz; yine de dosya silme başarısızlığı burada da
            // LOUD loglanır (PurgeAsync içinde) — sessizce kaybolmaz.
            await PurgeAsync(id, cancellationToken);
        }
        return toPurge.Count;
    }

    // ITenantScoped + tablo (PK'lı) tipleri FK bağımlılığına göre çocuk-önce sıralar.
    // `ctx` parametre olarak alınır (ambient `context` yerine): çağıran taze `purgeContext`
    // geçirir. Model METADATA'sı (tablo/şema adları, FK grafiği) hangi AppDbContext örneğinden
    // okunduğuna bağlı değildir — ikisi de aynı derlenmiş EF modelini paylaşır — ama ambient
    // `context` alanını burada kullanmak, purge akışının TAMAMEN `purgeContext` üzerinden
    // yürüdüğü izlenimini bozar ve ileride biri metadata-dışı bir kullanım eklerse (ör. bir
    // sorgu) yanlış kiracıya sessizce çalışabilir. Parametre almak bunu yapısal olarak imkânsız kılar.
    private static List<string> TenantScopedTablesInDeleteOrder(AppDbContext ctx)
    {
        var entityTypes = ctx.Model.GetEntityTypes()
            .Where(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType)
                        && e.FindPrimaryKey() is not null // keyless view'leri (read-model) dışla
                        && e.GetTableName() is not null)
            .Distinct()
            .ToList();

        var set = entityTypes.ToHashSet();
        var visited = new HashSet<IEntityType>();
        var ordered = new List<IEntityType>();

        void Visit(IEntityType node)
        {
            if (!visited.Add(node)) return;
            // node'un referans ettiği (principal) kiracı-kapsamlı tipleri önce ekle;
            // böylece 'ordered' principal-önce olur, tersine çevirince child-önce.
            foreach (var fk in node.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                if (principal != node && set.Contains(principal))
                {
                    Visit(principal);
                }
            }
            ordered.Add(node);
        }

        foreach (var e in entityTypes) Visit(e);

        ordered.Reverse(); // principal-önce → child-önce (silme için)

        return ordered
            .Select(e => e.GetSchema() is { } s ? $"[{s}].[{e.GetTableName()}]" : $"[{e.GetTableName()}]")
            .Distinct()
            .ToList();
    }

    // Bir CLR tipinin köşeli-parantezli, şema-nitelikli tablo adını metadata'dan üretir.
    // `ctx` parametre alır — bkz. TenantScopedTablesInDeleteOrder üstündeki gerekçe.
    private static string QualifiedTableName(AppDbContext ctx, Type clrType)
    {
        var entityType = ctx.Model.FindEntityType(clrType)
            ?? throw new InvalidOperationException($"{clrType.Name} için EF entity tipi bulunamadı.");
        var table = entityType.GetTableName()
            ?? throw new InvalidOperationException($"{clrType.Name} bir tabloya eşlenmemiş.");
        return entityType.GetSchema() is { } schema ? $"[{schema}].[{table}]" : $"[{table}]";
    }
}
