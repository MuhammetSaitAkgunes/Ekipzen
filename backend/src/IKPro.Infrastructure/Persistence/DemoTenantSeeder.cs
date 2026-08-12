using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IKPro.Infrastructure.Persistence;

/// <summary>
/// Geliştirme kurulumundaki iki demo kiracısını (Demo Şirket + Globex) kurar.
///
/// <b>Kiracıları GERÇEK PROVİZYON YOLUNDAN oluşturur</b> — doğrudan satır yazmaz.
/// Bu bilinçli: kayıt akışı böylece her geliştirme kurulumunda baştan sona
/// çalıştırılmış olur. Seed kendi kestirmesini kullansaydı, provizyonu bozan bir
/// değişiklik yalnız testlerde görünürdü; oysa bu yol, uygulamayı her açan
/// geliştiricinin karşısına çıkar.
/// </summary>
public sealed class DemoTenantSeeder(
    IPlatformDbContext platform,
    TenantOnboarding onboarding,
    ITenantScopeFactory tenantScopeFactory,
    ILogger<DemoTenantSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await KiraciKurVeDoldurAsync(
            slug: "demo",
            sirketAdi: "Demo Şirket",
            adminAdi: "İK Yöneticisi",
            adminEposta: "ik@hrmaster.local",
            doldur: init => init.SeedAsync(),
            cancellationToken);

        await KiraciKurVeDoldurAsync(
            slug: "globex",
            sirketAdi: "Globex Bilişim A.Ş.",
            adminAdi: "Globex Yöneticisi",
            adminEposta: "globex-admin@globex.local",
            doldur: init => init.SeedGlobexTenantAsync(),
            cancellationToken);
    }

    private async Task KiraciKurVeDoldurAsync(
        string slug,
        string sirketAdi,
        string adminAdi,
        string adminEposta,
        Func<AppDbContextInitializer, Task> doldur,
        CancellationToken cancellationToken)
    {
        var tenant = await platform.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
        if (tenant is null)
        {
            try
            {
                tenant = await onboarding.CreateWithAdminAsync(
                    sirketAdi, slug, adminAdi, adminEposta, cancellationToken);
            }
            catch (ConflictException ex)
            {
                // En sık sebep: platform veritabanı düşürülmüş ama kiracı katalogları
                // (IKPro_demo / IKPro_globex) yerinde kalmış. Provizyon var olan
                // katalogu bilinçli olarak reddediyor — sessizce yeniden kullanmak
                // iki müşterinin verisini karıştırmak olurdu. Mesaj, geliştiriciye
                // ne yapacağını söylesin diye zenginleştiriliyor.
                throw new InvalidOperationException(
                    $"Demo kiracısı '{slug}' kurulamadı: {ex.Message} " +
                    "Temiz bir başlangıç için IKProPlatform, IKProDb ve TÜM IKPro_* " +
                    "kiracı veritabanlarını birlikte düşürün.", ex);
            }
        }

        using var kapsam = tenantScopeFactory.Create(tenant.Id);
        var initializer = kapsam.Services.GetRequiredService<AppDbContextInitializer>();
        await doldur(initializer);

        logger.LogInformation(
            "Demo kiracısı hazır: {Slug} → {Katalog}", tenant.Slug, tenant.DatabaseName);
    }
}
