using FluentAssertions;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;
using Ekipzen.Infrastructure.Persistence;
using Ekipzen.Tests.Integration.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Integration.Performance;

/// <summary>
/// N+1 koruması: bir uç noktanın çalıştırdığı SQL komutu sayısı, döndürdüğü kayıt
/// sayısıyla BÜYÜMEMELİDİR.
///
/// Neden sayı ölçülüyor da süre değil: süre makineye, diske ve o anki yüke göre
/// oynar; CI'da eşik koymak testi güvenilmez yapar. Komut SAYISI ise N+1'in
/// doğrudan imzasıdır — kayıt başına ek sorgu açan bir değişiklik sayıyı anında
/// büyütür, makine ne kadar hızlı olursa olsun.
///
/// Ölçüm kiracıya izole edilmiş kendi verisiyle yapılır; paylaşılan test
/// veritabanındaki diğer testlerin sayımları etkilenmez.
/// </summary>
[Collection(ApiCollection.Name)]
public class NPlusBirKorumaTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    private const int BaslangicPersonel = 3;
    private const int EklenenPersonel = 40;

    /// <summary>
    /// Sayacın gerçekten sayabildiğini kanıtlar. Bu olmadan "sayı büyümedi"
    /// iddiası, sayacın hiç çalışmamasıyla ayırt edilemezdi.
    /// </summary>
    [Fact]
    public async Task Sayac_CalisanHerSorguyuSayar()
    {
        using var sayac = new SqlKomutSayaci();

        var olculen = await sayac.OlcAsync(async () =>
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 5; i++)
            {
                await db.Departments.AsNoTracking().Take(1).ToListAsync();
            }
        });

        olculen.Should().Be(5, "beş ayrı sorgu koşuldu; sayaç bunları görmüyorsa ölçüm anlamsızdır");
    }

    [Theory]
    [InlineData("/api/employees")]
    [InlineData("/api/attendance/live")]
    [InlineData("/api/dashboard/metrics")]
    [InlineData("/api/leaves/pending")]
    [InlineData("/api/departments")]
    public async Task UcNokta_SorguSayisi_PersonelSayisiylaBuyumez(string ucNokta)
    {
        // Şirket adı kısa tutulur: slug ondan türetilir ve 64 karakter sınırı vardır.
        var kiraci = await ProvisionAndActivateAsync(
            "NPlusBir", $"nplus-{Guid.NewGuid():N}@ornek.local");
        var client = await AuthedClientAsync(kiraci.AdminEmail);

        var departmanId = await DepartmanOlusturAsync(kiraci.TenantId);
        await PersonelEkleAsync(kiraci.TenantId, departmanId, BaslangicPersonel, "Az");

        using var sayac = new SqlKomutSayaci();

        // Isınma: ilk istek model derleme/plan önbelleği yüzünden ek sorgu açabilir.
        (await client.GetAsync(ucNokta)).EnsureSuccessStatusCode();

        var azVeriyle = await sayac.OlcAsync(async () =>
            (await client.GetAsync(ucNokta)).EnsureSuccessStatusCode());

        await PersonelEkleAsync(kiraci.TenantId, departmanId, EklenenPersonel, "Cok");

        var cokVeriyle = await sayac.OlcAsync(async () =>
            (await client.GetAsync(ucNokta)).EnsureSuccessStatusCode());

        cokVeriyle.Should().Be(azVeriyle,
            $"{ucNokta}: personel {BaslangicPersonel} → {BaslangicPersonel + EklenenPersonel} olurken " +
            $"SQL komutu {azVeriyle} → {cokVeriyle} oldu. Kayıt başına sorgu açılıyor (N+1).");
    }

    private async Task<int> DepartmanOlusturAsync(int tenantId)
    {
        var departman = new Department { Name = "Yük Testi" };
        await SeedInTenantAsync(tenantId, db =>
        {
            db.Departments.Add(departman);
            return Task.CompletedTask;
        });
        return departman.Id;
    }

    private Task PersonelEkleAsync(int tenantId, int departmanId, int adet, string etiket) =>
        SeedInTenantAsync(tenantId, db =>
        {
            for (var i = 0; i < adet; i++)
            {
                db.Employees.Add(new Employee
                {
                    FirstName = etiket,
                    LastName = $"Personel{i:D3}",
                    Title = "Uzman",
                    Status = EmployeeStatus.Active,
                    HireDate = new DateOnly(2024, 1, 1),
                    DepartmentId = departmanId,
                });
            }

            return Task.CompletedTask;
        });
}
