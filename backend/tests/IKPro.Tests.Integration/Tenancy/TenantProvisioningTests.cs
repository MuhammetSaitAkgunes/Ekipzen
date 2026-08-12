using FluentAssertions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Auth;
using IKPro.Application.Features.Departments;
using IKPro.Domain.Entities.Tenancy;
using IKPro.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace IKPro.Tests.Integration.Tenancy;

/// <summary>
/// Faz 1: kiracı provizyonu (şirket + ilk hr-admin) ve kiracı-farkında login.
/// Provizyon platform anahtarıyla korunur; admin yalnız kendi kiracısını görür;
/// pasif kiracıya giriş reddedilir.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TenantProvisioningTests(IKProApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task Provision_CreatesTenantAndAdmin_WhoSeesOnlyOwnTenant()
    {
        var adminEmail = $"admin-{Guid.NewGuid():N}@acme.local";
        await ProvisionAndActivateAsync("Acme A.Ş.", adminEmail);

        var admin = await AuthedClientAsync(adminEmail);
        var depts = await GetAsync<List<DepartmentDto>>(admin, "/api/departments");
        depts.Should().BeEmpty("yeni kiracıda henüz veri yok ve başka kiracının verisi görünmemeli");
    }

    [Fact]
    public async Task LoggedInUser_CarriesTenantName()
    {
        // Frontend header'ının aktif şirketi göstermesi için /auth ve /me kiracı adını taşır.
        var adminEmail = $"admin-{Guid.NewGuid():N}@initech.local";
        await ProvisionAndActivateAsync("Initech A.Ş.", adminEmail);

        var anon = Factory.CreateClient();
        var provisioned = await anon.PostAsJsonAsync("/api/auth/login",
            new { email = adminEmail, password = "demo123" });
        (await provisioned.Content.ReadFromJsonAsync<AuthResponse>())!.User.TenantName
            .Should().Be("Initech A.Ş.");

        var demo = await anon.PostAsJsonAsync("/api/auth/login",
            new { email = "ik@hrmaster.local", password = "demo123" });
        (await demo.Content.ReadFromJsonAsync<AuthResponse>())!.User.TenantName
            .Should().Be("Demo Şirket");
    }

    [Fact]
    public async Task Provision_WithoutPlatformKey_Returns401()
    {
        var response = await ProvisionRawAsync(new
        {
            companyName = "Yetkisiz A.Ş.",
            slug = TestSlug("yetkisiz"),
            adminName = "X",
            adminEmail = $"x-{Guid.NewGuid():N}@x.local",
        }, withPlatformKey: false);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Provision_DuplicateSlug_Returns409()
    {
        var slug = TestSlug("dup");
        (await ProvisionRawAsync(new
        {
            companyName = "İlk A.Ş.", slug,
            adminName = "İlk", adminEmail = $"ilk-{Guid.NewGuid():N}@dup.local",
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ProvisionRawAsync(new
        {
            companyName = "İkinci A.Ş.", slug,
            adminName = "İkinci", adminEmail = $"ikinci-{Guid.NewGuid():N}@dup.local",
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Sözleşme (Görev 3): dondurulmuş/pasif kiracıda login artık 403 döner, 401
    /// değil — kimlik bilgileri doğru, engelleyen şey kiracının kapalı olması.
    /// Bu senaryo <see cref="ErisimKapisiTests.DondurulmusKiraci_LoginYapamaz"/>
    /// ile örtüşüyor (Faz 1a ledger'ında not edildi); burada tutuldu çünkü ayrıca
    /// provizyon akışının bir parçası olarak sınanıyor.
    /// </summary>
    [Fact]
    public async Task Login_InactiveTenant_Rejected()
    {
        var adminEmail = $"admin-{Guid.NewGuid():N}@pasif.local";
        var tenant = await ProvisionAndActivateAsync("Pasif A.Ş.", adminEmail);

        // Kiracıyı doğrudan dondur (pasife al).
        using (var scope = Factory.Services.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var entity = await platform.Tenants.FindAsync(tenant.TenantId);
            entity!.Status = TenantStatus.Frozen;
            await platform.SaveChangesAsync(CancellationToken.None);
            // Erişim kapısı ITenantRegistry'nin (singleton) önbelleğinden okur; bu
            // testte henüz bir GetStatusAsync çağrısı olmadığı için pratikte gerekli
            // değildir, ama davranışı DB durumuna bağımlı bırakmak yerine açıkça
            // düşürmek gelecekte bu testin başına ProvisionAndActivateAsync'ten SONRA
            // bir login eklenirse sessizce bayatlamasını önler.
            scope.ServiceProvider.GetRequiredService<ITenantRegistry>().Invalidate(tenant.TenantId);
        }

        var anonymous = Factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = adminEmail, password = "demo123" });
        login.StatusCode.Should().Be(HttpStatusCode.Forbidden, "pasif kiracıya giriş reddedilmeli");
    }
}
