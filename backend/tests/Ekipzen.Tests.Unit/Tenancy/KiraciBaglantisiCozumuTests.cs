using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Infrastructure;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Unit.Tenancy;

/// <summary>
/// Faz 1b'nin MERKEZİ garantisini doğrular: <c>AddDbContext</c> lambda'sı
/// (bkz. Infrastructure.DependencyInjection) aktif kiracıyı <see cref="ICurrentTenant"/>'tan
/// okuyup <see cref="ITenantConnectionResolver.ResolveFor"/>'a GEÇİRMELİ.
///
/// Bugün çözücü (TenantConnectionResolver) herkese aynı bağlantı dizesini döndürdüğü için
/// bu tel BURADA sessizce kopsa (ör. biri lambda'dan <c>kiraci.TenantId</c> okumayı çıkarıp
/// sabit/null geçse) 202 testin HİÇBİRİ bunu yakalamaz — hata ancak Faz 2'de "yanlış
/// müşterinin veritabanı" olarak ortaya çıkar. Bu test, çözücüyü bir kaydedici (spy) ile
/// sarmalayıp kiracıya sabitlenmiş bir kapsamdan <c>AppDbContext</c> çözüldüğünde
/// <c>ResolveFor</c>'un O KİRACININ kimliğiyle çağrıldığını kanıtlar.
///
/// Kırılabilirlik kanıtı (final-fix-report.md'de birebir çıktısı var): lambda'daki
/// `kiraci.TenantId` okuması geçici olarak `null` ile değiştirilip test koşturuldu,
/// test KIRMIZI oldu (spy'a null geldi, beklenen kiracı kimliği değil), sonra geri alındı.
/// </summary>
public class KiraciBaglantisiCozumuTests
{
    [Fact]
    public void AppDbContext_Cozulurken_AktifKiracininKimligiResolveForaGecer()
    {
        // Arrange — AddInfrastructure'ın istediği asgari yapılandırma (gerçek DB'ye
        // bağlanılmaz; UseSqlServer bağlantıyı AÇMADAN sadece dizeyi saklar).
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=EkipzenTestPlaceholder;Trusted_Connection=True;TrustServerCertificate=True;",
                ["ConnectionStrings:PlatformConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=EkipzenPlatformTestPlaceholder;Trusted_Connection=True;TrustServerCertificate=True;",
                ["Jwt:Secret"] = "birim-test-icin-en-az-32-karakterlik-gizli-anahtar",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        // ICurrentTenant, gerçek uygulamada Ekipzen.API/Services/CurrentTenant.cs'te (HTTP
        // bağımlı) kayıtlıdır — Infrastructure katmanı bunu kaydetmez. Testte HTTP'den
        // bağımsız, sade bir sahte (fake) yeterli: Impersonate/TenantId sözleşmesi aynı.
        services.AddScoped<ICurrentTenant, SahteKiraci>();

        // ICurrentUser de aynı şekilde API katmanında (HTTP bağımlı) kayıtlıdır;
        // AuditableEntityInterceptor bunu ister (AppDbContext'in interceptor zinciri) —
        // testin amacıyla ilgisiz ama DbContext'in inşası için gerekli, sade bir sahte.
        services.AddScoped<ICurrentUser, SahteKullanici>();

        // Gerçek TenantConnectionResolver'ın YERİNE geçen kaydedici (spy). AddInfrastructure
        // zaten bir ITenantConnectionResolver kaydeder; burada eklenen kayıt SONRAKİ olduğu
        // için GetRequiredService bunu döner (DI'da tekil çözümde son kayıt kazanır).
        var spy = new KaydediciBaglantiCozucusu();
        services.AddScoped<ITenantConnectionResolver>(_ => spy);

        using var provider = services.BuildServiceProvider();
        var tenantScopeFactory = provider.GetRequiredService<ITenantScopeFactory>();

        const int beklenenKiraciId = 42;

        // Act — kiracıya sabitlenmiş kapsamdan AppDbContext çöz (üretimde bunu Task 7'nin
        // dondurma/çözme ucu, purge, seed vb. HTTP-dışı yollar yapıyor; HTTP isteklerinde
        // ise ICurrentTenant scope başına JWT'den doldurulur — mekanizma aynı).
        using var kapsam = tenantScopeFactory.Create(beklenenKiraciId);
        _ = kapsam.Services.GetRequiredService<AppDbContext>();

        // Assert
        spy.CagrilanTenantIdler.Should().ContainSingle()
            .Which.Should().Be(beklenenKiraciId);
    }

    private sealed class SahteKiraci : ICurrentTenant
    {
        private int? _impersonated;

        public int? TenantId => _impersonated;

        public int TenantIdOrThrow() =>
            TenantId ?? throw new InvalidOperationException("Aktif kiracı bulunamadı.");

        public void Impersonate(int tenantId) => _impersonated = tenantId;
    }

    private sealed class SahteKullanici : ICurrentUser
    {
        public string? UserId => null;
        public string? UserName => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public int? EmployeeId => null;
    }

    private sealed class KaydediciBaglantiCozucusu : ITenantConnectionResolver
    {
        public List<int?> CagrilanTenantIdler { get; } = new();

        public string ResolveFor(int? tenantId)
        {
            CagrilanTenantIdler.Add(tenantId);
            return "Server=(localdb)\\MSSQLLocalDB;Database=EkipzenTestPlaceholder;Trusted_Connection=True;TrustServerCertificate=True;";
        }
    }
}
