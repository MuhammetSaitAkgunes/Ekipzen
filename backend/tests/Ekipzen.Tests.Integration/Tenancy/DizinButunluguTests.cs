using System.Net.Http.Json;
using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>
/// Dizin bütünlüğü: kullanıcı yaratan HER yol dizine yazmalı. Dizinde olmayan
/// kullanıcı, kiracı veritabanları ayrıldığında (Faz 2) hangi veritabanına
/// bakılacağı çözülemediği için giriş yapamaz — ve bu, yazım anında hiçbir
/// hata vermediği için sessizce kaybolur.
/// </summary>
[Collection(ApiCollection.Name)]
public class DizinButunluguTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    /// <summary>
    /// İşe alım (aday → personel), ürünün en yüksek hacimli kullanıcı yaratma
    /// yoludur ve Faz 1a'da dizin iddiası olan tek testi yoktu.
    ///
    /// DİKKAT: `POST /api/employees` Identity kullanıcısı YARATMAZ — yalnız
    /// personel kaydı açar. Kullanıcı yaratan tek yol işe alımdır
    /// (`RecruitmentCommands.cs:378` → `CreateEmployeeLoginAsync`).
    /// </summary>
    [Fact]
    public async Task IseAlimlaOlusanKullanici_DizineYazilir()
    {
        var kiraci = await ProvisionAndActivateAsync("DizinIsealim", $"iad-{Guid.NewGuid():N}@ornek.local");
        var client = await AuthedClientAsync(kiraci.AdminEmail);

        var departmanId = await DepartmanIdAsync(kiraci.TenantId);
        var adayId = await AdayIdAsync(kiraci.TenantId, departmanId);
        var personelEpostasi = $"personel-{Guid.NewGuid():N}@ornek.local";

        var iseAl = await client.PostAsJsonAsync($"/api/candidates/{adayId}/hire", new
        {
            departmentId = departmanId,
            email = personelEpostasi,
            title = "Uzman",
            hireDate = "2024-01-01",
        });
        iseAl.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var kayit = await platform.Directory
            .FirstOrDefaultAsync(d => d.NormalizedEmail == TenantDirectoryEntry.Normalize(personelEpostasi));

        kayit.Should().NotBeNull("işe alımla oluşan kullanıcı da dizine yazılmalı");
        kayit!.TenantId.Should().Be(kiraci.TenantId);
    }

    /// <summary>
    /// Dizine yazma, kullanıcı yaratmadan ÖNCE yapılır. Kullanıcı yaratma
    /// başarısız olursa dizinde kullanıcısız bir satır kalır; aynı kiracı için
    /// yeniden denendiğinde bu satır kilit oluşturmamalı, idempotentlik
    /// sayesinde akış devam etmeli.
    /// </summary>
    [Fact]
    public async Task DizindeOksuzSatirVarsa_AyniKiraciIcinIseAlimCalisir()
    {
        var kiraci = await ProvisionAndActivateAsync("DizinOksuz", $"okz-{Guid.NewGuid():N}@ornek.local");
        var client = await AuthedClientAsync(kiraci.AdminEmail);

        var departmanId = await DepartmanIdAsync(kiraci.TenantId);
        var adayId = await AdayIdAsync(kiraci.TenantId, departmanId);
        var personelEpostasi = $"oksuz-{Guid.NewGuid():N}@ornek.local";

        // Öksüz satırı simüle et: dizinde var, Identity'de karşılığı yok.
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            platform.Directory.Add(new TenantDirectoryEntry
            {
                NormalizedEmail = TenantDirectoryEntry.Normalize(personelEpostasi),
                TenantId = kiraci.TenantId,
            });
            await platform.SaveChangesAsync(default);
        }

        var iseAl = await client.PostAsJsonAsync($"/api/candidates/{adayId}/hire", new
        {
            departmentId = departmanId,
            email = personelEpostasi,
            title = "Uzman",
            hireDate = "2024-01-01",
        });

        iseAl.IsSuccessStatusCode.Should().BeTrue(
            "aynı kiracıya ait öksüz dizin satırı, o kiracının yeniden denemesini engellememeli");
    }

    private async Task<int> DepartmanIdAsync(int tenantId)
    {
        var departman = new Domain.Entities.Organization.Department { Name = $"Dizin {Guid.NewGuid():N}"[..20] };
        await SeedInTenantAsync(tenantId, db =>
        {
            db.Departments.Add(departman);
            return Task.CompletedTask;
        });
        return departman.Id;
    }

    private async Task<int> AdayIdAsync(int tenantId, int departmanId)
    {
        // Candidate varlığında Email alanı YOK (brief'in taslağı bunu varsayıyordu —
        // Domain/Entities/Recruitment/Candidate.cs'te yalnız Name + AppliedRole vb. var).
        // Personelin e-postası hire isteğinin gövdesinden gelir (CandidateHireBody.Email).
        var aday = new Domain.Entities.Recruitment.Candidate
        {
            Name = "Dizin Adayı",
            AppliedRole = "Uzman",
        };
        await SeedInTenantAsync(tenantId, db =>
        {
            db.Candidates.Add(aday);
            return Task.CompletedTask;
        });
        return aday.Id;
    }
}
