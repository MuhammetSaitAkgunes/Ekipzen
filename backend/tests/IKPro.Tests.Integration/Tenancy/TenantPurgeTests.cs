using FluentAssertions;
using IKPro.Infrastructure.Storage;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Tenancy.Commands;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Entities.Tenancy;
using IKPro.Infrastructure.Identity;
using IKPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Kiracı purge: bir kiracının tüm verisini siler, başka kiracıya dokunmaz; confirm-slug
/// ve platform-key güvenliği; doğrulanmamış kiracı temizliği.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TenantPurgeTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    /// <summary>
    /// Dosya alanını silemeyen depo. Purge'ün "silme patlarsa yarıda kesme, ama
    /// çağırana bildir" sözleşmesini platformdan bağımsız sınamak için var:
    /// gerçek dosya kilitleme davranışı Windows ile Linux arasında ayrışır ve
    /// testi işletim sistemine bağlı hale getirirdi.
    /// </summary>
    private sealed class SilemeyenDepo : IFileStorage
    {
        public bool SilmeDenendi { get; private set; }

        public Task DeleteTenantSpaceAsync(int tenantId, CancellationToken cancellationToken)
        {
            SilmeDenendi = true;
            throw new IOException($"Kiracı {tenantId} dosya alanı silinemedi (test sahtesi).");
        }

        // Purge yalnız DeleteTenantSpaceAsync kullanır; kalanlar çağrılırsa
        // testin varsayımı bozulmuş demektir, sessizce geçmek yerine patlasın.
        public Task<StoredFile> SaveAsync(Stream content, string fileName, string category, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task DeleteAsync(string path, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task Purge_RemovesTargetTenantData_LeavesOthersIntact()
    {
        var a = await ProvisionAndActivateAsync("Silinecek A.Ş.", $"a-{Guid.NewGuid():N}@purge.local");
        var b = await ProvisionAndActivateAsync("Kalacak A.Ş.", $"b-{Guid.NewGuid():N}@purge.local");
        await SeedInTenantAsync(a.TenantId, db => { db.Departments.Add(new Department { Name = "A-Dept" }); return Task.CompletedTask; });
        await SeedInTenantAsync(b.TenantId, db => { db.Departments.Add(new Department { Name = "B-Dept" }); return Task.CompletedTask; });

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITenantPurger>()
                .PurgeAsync(a.TenantId, CancellationToken.None);
        }

        using (var kapsam = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(b.TenantId))
        {
            var sp = kapsam.Services;
            var db = sp.GetRequiredService<AppDbContext>();
            var platform = sp.GetRequiredService<IPlatformDbContext>();

            (await platform.Tenants.AnyAsync(t => t.Id == a.TenantId)).Should().BeFalse("kiracı satırı silinmeli");
            (await db.Set<ApplicationUser>().AnyAsync(u => u.TenantId == a.TenantId))
                .Should().BeFalse("kiracının kullanıcıları silinmeli");
            (await db.Departments.IgnoreQueryFilters().AnyAsync(d => d.TenantId == a.TenantId))
                .Should().BeFalse("kiracının verisi silinmeli");

            (await db.Departments.AnyAsync(d => d.Name == "B-Dept")).Should().BeTrue("başka kiracı korunmalı");
            (await platform.Tenants.AnyAsync(t => t.Id == b.TenantId)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Purge_RemovesDirectoryEntry_AllowsEmailReuse()
    {
        // İnceleme bulgusu #3: purge Directory satırlarını silmezse aynı e-posta
        // KALICI KİLİTLENİR — Identity'de kullanıcı kalmaz (EmailExistsAsync false)
        // ama dizinin birincil anahtarı hâlâ eski (silinmiş) kiracıyı gösterdiğinden
        // yeniden kayıt/provizyon denemesi rezervasyonda 409'a çarpar.
        var eposta = $"yeniden-{Guid.NewGuid():N}@purge.local";
        var t = await ProvisionAndActivateAsync("Dizin Purge A.Ş.", eposta);

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            (await platform.Directory.AnyAsync(d => d.TenantId == t.TenantId))
                .Should().BeTrue("purge öncesi dizin satırı var olmalı (kurulumun kontrolü)");
        }

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITenantPurger>()
                .PurgeAsync(t.TenantId, CancellationToken.None);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            (await platform.Directory.AnyAsync(d => d.NormalizedEmail == TenantDirectoryEntry.Normalize(eposta)))
                .Should().BeFalse("purge dizin satırını da silmeli — aksi halde e-posta kalıcı kilitlenir");
        }

        // Asıl doğrulama: aynı e-posta ile yeniden provizyon artık 409 ALMAMALI.
        var yeniden = await ProvisionRawAsync(new
        {
            companyName = "Yeniden A.Ş.",
            slug = TestSlug("yeniden"),
            adminName = "Yeniden Yonetici",
            adminEmail = eposta,
        });

        yeniden.StatusCode.Should().Be(HttpStatusCode.Created,
            "purge sonrası e-posta serbest kalmalı, kalıcı kilitlenmemeli");
    }

    private HttpClient PlatformClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", IKProApiFactory.PlatformKey);
        return client;
    }

    [Fact]
    public async Task Purge_DeletesPhysicalDocumentFiles()
    {
        var t = await ProvisionAndActivateAsync("Dosyalı A.Ş.", $"f-{Guid.NewGuid():N}@purge.local");

        // Depo artık kiracıya bağlı ve SCOPED; kök sağlayıcıdan çözülemez.
        // Kiracı bağlamı TenantPurger'ın yaptığı gibi ITenantScopeFactory ile kurulur.
        StoredFile stored;
        using (var yazmaKapsam = Factory.Services.GetRequiredService<ITenantScopeFactory>().Create(t.TenantId))
        {
            stored = await yazmaKapsam.Services.GetRequiredService<IFileStorage>().SaveAsync(
                new MemoryStream(new byte[] { 1, 2, 3 }), "gizli.pdf", "docs", CancellationToken.None);
        }

        await SeedInTenantAsync(t.TenantId, async db =>
        {
            var dept = new Department { Name = "Dosya-Dept" };
            db.Departments.Add(dept);
            await db.SaveChangesAsync();

            var emp = new Employee
            {
                FirstName = "Ada", LastName = "Kayıt", Title = "Uzman",
                DepartmentId = dept.Id, HireDate = new DateOnly(2024, 1, 1),
            };
            db.Employees.Add(emp);
            await db.SaveChangesAsync();

            db.EmployeeDocuments.Add(new EmployeeDocument
            {
                EmployeeId = emp.Id, DocumentType = "Sözleşme",
                FileName = "gizli.pdf", FilePath = stored.Path, SizeBytes = 3,
            });
        });

        var kiraciKlasoru = Path.Combine(Factory.StorageRoot, LocalFileStorage.TenantFolder(t.TenantId));
        var evrakYolu = Path.Combine(kiraciKlasoru, stored.Path.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(evrakYolu).Should().BeTrue("dosya purge öncesi diskte olmalı");

        // Evrak DIŞINDAKİ türler de kiracı alanındadır ve purge'de gitmeli.
        // Eski davranış yalnız EmployeeDocuments yollarını siliyordu; foto ve logo kalıyordu.
        var fotoYolu = Path.Combine(kiraciKlasoru, "photos", "vesikalik.png");
        var logoYolu = Path.Combine(kiraciKlasoru, "branding", "logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(fotoYolu)!);
        Directory.CreateDirectory(Path.GetDirectoryName(logoYolu)!);
        await File.WriteAllTextAsync(fotoYolu, "foto");
        await File.WriteAllTextAsync(logoYolu, "logo");

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITenantPurger>()
                .PurgeAsync(t.TenantId, CancellationToken.None);
        }

        File.Exists(evrakYolu).Should().BeFalse("purge fiziksel evrak dosyasını da silmeli (KVKK)");
        File.Exists(fotoYolu).Should().BeFalse("çalışan fotoğrafı da silinmeli");
        File.Exists(logoYolu).Should().BeFalse("şirket logosu da silinmeli");
        Directory.Exists(kiraciKlasoru).Should().BeFalse("kiracının tüm dosya alanı gitmeli");
    }

    [Fact]
    public async Task Purge_WithUndeletableFile_DoesNotThrow_AndStillPurges_ButReportsFailure()
    {
        var t = await ProvisionAndActivateAsync("Bozuk Dosya A.Ş.", $"b-{Guid.NewGuid():N}@purge.local");

        // Dosyalar silinemese bile veri temizliği yarıda kesilmemeli; hata loglanır
        // VE çağırana dönüş değeriyle (false) görünür kılınır — sessiz kalınmaz
        // (İnceleme bulgusu #3: aksi halde operatör 200 OK'i "her şey silindi"
        // sanır, oysa kiracının PII dosyaları diskte kalmış olabilir).
        //
        // Başarısızlık DEPO KATMANINDAN üretilir, işletim sisteminden değil. Bu test
        // eskiden dosyayı FileShare.None ile açık tutuyordu: Windows'ta bu
        // Directory.Delete'i engelliyor, ama Linux'ta (CI) POSIX semantiği açık
        // dosyanın silinmesine izin verdiği için silme BAŞARILI oluyor ve test
        // düşüyordu. Testin niyeti "silme başarısız olursa ne olur" — başarısızlığın
        // nasıl üretildiği değil. Sahte depo bunu her platformda aynı şekilde sınar.
        var patlayanDepo = new SilemeyenDepo();

        bool dosyalarSilindi = true;
        var purge = async () =>
        {
            using var scope = Factory.Services.CreateScope();
            var sp = scope.ServiceProvider;

            // Purger elle kurulur: yalnız IFileStorage değiştirilir, kalan her
            // bağımlılık gerçek DI'dan gelir — böylece sınanan şey gerçek purge
            // davranışıdır, sahte bir kurgu değil.
            var purger = new TenantPurger(
                sp.GetRequiredService<AppDbContext>(),
                sp.GetRequiredService<IPlatformDbContext>(),
                sp.GetRequiredService<ITenantScopeFactory>(),
                patlayanDepo,
                sp.GetRequiredService<ITenantDirectory>(),
                sp.GetRequiredService<ITenantRegistry>(),
                sp.GetRequiredService<ILogger<TenantPurger>>());

            dosyalarSilindi = await purger.PurgeAsync(t.TenantId, CancellationToken.None);
        };
        await purge.Should().NotThrowAsync("silinemeyen dosya alanı purge'ü yarıda kesmemeli");

        patlayanDepo.SilmeDenendi.Should().BeTrue("purge dosya alanını silmeyi gerçekten denemeli");

        dosyalarSilindi.Should().BeFalse(
            "dosya alanı silinemediğinde PurgeAsync bunu çağırana bildirmeli, sessiz kalmamalı");

        using var check = Factory.Services.CreateScope();
        var platform2 = check.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platform2.Tenants.AnyAsync(x => x.Id == t.TenantId)).Should().BeFalse("kiracı verisi yine de silinmeli");
    }

    /// <summary>
    /// Refresh token'lar PLATFORM veritabanındadır (Faz 2a) — purge'ün kiracı
    /// verisini silen ham SQL'i onlara ulaşmaz, ayrı bir adım gerekir. Bu adım
    /// atlanırsa silinmiş bir kiracının token'ları platformda kalır: KVKK açısından
    /// artık PII, ve teorik olarak oturum uzatmaya devam edebilir.
    /// </summary>
    [Fact]
    public async Task Purge_RefreshTokenlariPlatformdanDaSiler()
    {
        var eposta = $"rt-purge-{Guid.NewGuid():N}@purge.local";
        var t = await ProvisionAndActivateAsync("Token Silme A.Ş.", eposta);

        // Giriş, platform veritabanında bir refresh token satırı üretir.
        _ = await AuthedClientAsync(eposta);

        using (var once = Factory.Services.CreateScope())
        {
            var platform = once.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            (await platform.RefreshTokens.AnyAsync(x => x.TenantId == t.TenantId))
                .Should().BeTrue("testin anlamlı olması için önce token var olmalı");
        }

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITenantPurger>()
                .PurgeAsync(t.TenantId, CancellationToken.None);
        }

        using var sonra = Factory.Services.CreateScope();
        var platformSonra = sonra.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platformSonra.RefreshTokens.AnyAsync(x => x.TenantId == t.TenantId))
            .Should().BeFalse("purge refresh token'ları platform veritabanından da silmeli");
    }

    [Fact]
    public async Task Delete_WithWrongSlug_Returns409_AndKeepsData()
    {
        var t = await ProvisionAndActivateAsync("Yanlış Onay A.Ş.", $"w-{Guid.NewGuid():N}@purge.local");

        var resp = await PlatformClient().DeleteAsync($"/api/tenants/{t.TenantId}?confirmSlug=yanlis-slug");
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platform.Tenants.AnyAsync(x => x.Id == t.TenantId)).Should().BeTrue("yanlış slug'da silinmemeli");
    }

    [Fact]
    public async Task Delete_WithoutPlatformKey_Returns401()
    {
        var t = await ProvisionAndActivateAsync("Anahtarsız A.Ş.", $"k-{Guid.NewGuid():N}@purge.local");
        var resp = await Factory.CreateClient().DeleteAsync($"/api/tenants/{t.TenantId}?confirmSlug={t.Slug}");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithPlatformKeyAndSlug_PurgesTenant()
    {
        var t = await ProvisionAndActivateAsync("Doğru Onay A.Ş.", $"ok-{Guid.NewGuid():N}@purge.local");
        var resp = await PlatformClient().DeleteAsync($"/api/tenants/{t.TenantId}?confirmSlug={t.Slug}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Normal (dosya silme başarılı) yolda operatöre "DosyalarSilinemedi: false" dönmeli —
        // İnceleme bulgusu #3 alanının API sözleşmesinde de göründüğünü doğrular.
        var body = await resp.Content.ReadFromJsonAsync<PurgeTenantResult>();
        body.Should().NotBeNull();
        body!.DosyalarSilinemedi.Should().BeFalse("dosya alanı normal koşulda başarıyla silinmeli");

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        (await platform.Tenants.AnyAsync(x => x.Id == t.TenantId)).Should().BeFalse("silinmiş olmalı");
    }

    [Fact]
    public async Task CleanupUnverified_PurgesUnverified_KeepsVerifiedAndActive()
    {
        // Yarım kalmış provizyon: kiracı satırı var, davet hiç kabul edilmedi, tarihi eski.
        // (Self-servis kayıt kaldırıldı; Provisioning'de takılı kalma artık yalnız
        // provizyonun ortasında kesilmesiyle oluşur — kurulum onu taklit ediyor.)
        var unverified = await ProvisionTenantAsync("Doğrulanmamış A.Ş.", $"u-{Guid.NewGuid():N}@cleanup.local");
        var unverifiedId = unverified.TenantId;

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var t = await platform.Tenants.FirstAsync(x => x.Id == unverifiedId);
            t.Status = TenantStatus.Provisioning;
            t.CreatedAtUtc = DateTime.UtcNow.AddDays(-30);
            await platform.SaveChangesAsync(CancellationToken.None);
        }

        // Doğrulanmış aktif kiracı (korunmalı).
        var kept = await ProvisionAndActivateAsync("Aktif A.Ş.", $"a-{Guid.NewGuid():N}@cleanup.local");

        var resp = await PlatformClient().PostAsync("/api/tenants/cleanup-unverified?olderThanDays=7", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            (await platform.Tenants.AnyAsync(x => x.Id == unverifiedId)).Should().BeFalse("doğrulanmamış temizlenmeli");
            (await platform.Tenants.AnyAsync(x => x.Id == kept.TenantId)).Should().BeTrue("aktif kiracı korunmalı");
        }
    }
}
