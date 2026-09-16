using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using Ekipzen.Infrastructure.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ekipzen.Tests.Unit.Identity;

/// <summary>
/// <c>EmailExistsAsync</c>, yönlendirme dizini SAPTIĞINDA da bir cevap döndürmek
/// zorundadır — patlamak değil.
///
/// Neden önemli: dizin türetilmiş bir tablodur ve karşılığı olmayan bir kiracıya
/// işaret edebilir (geri yükleme, elle müdahale, yarıda kalmış purge). Kiracı
/// başına veritabanına geçildikten sonra böyle bir kimlikle kapsam açmak bağlantı
/// çözümlemesinde FIRLAR; bu metot ise <c>bool</c> sözleşmesindedir ve üç yolun
/// ön kontrolüdür (provizyon, işe alım, davet). Fırlarsa o yollar 500'e döner —
/// üstelik hata, aslında "böyle bir kullanıcı yok" demek olan bir durumdan çıkar.
///
/// Test, ayrılmadan SONRAKİ dünyayı taklit eder: kapsam fabrikası çözülemeyen
/// kiracıda fırlatır. Metot fabrikaya HİÇ gitmemelidir; giderse test kırılır.
/// Bu yüzden testin değeri fazdan bağımsızdır.
/// </summary>
public class EmailExistsAsyncTests
{
    private sealed class SapmisDizin(int kiraciId) : ITenantDirectory
    {
        public Task<int?> FindTenantIdAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult<int?>(kiraciId);

        public Task ReserveAsync(string email, int tenantId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public Task<int> RemoveForTenantAsync(int tenantId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public Task<RebuildOutcome> RebuildForTenantAsync(
            int tenantId, IReadOnlyList<string> normalizedEmails, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    /// <summary>Kiracıyı tanımayan kütük: durum sorulunca null döner.</summary>
    private sealed class BosKutuk : ITenantRegistry
    {
        public Task<TenantStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken)
            => Task.FromResult<TenantStatus?>(null);
        public string? GetDatabaseName(int tenantId) => null;
        public void Invalidate(int tenantId) { }
    }

    /// <summary>Ayrılmadan sonraki davranış: çözülemeyen kiracıda kapsam açılamaz.</summary>
    private sealed class FirlatanKapsamFabrikasi : ITenantScopeFactory
    {
        public ITenantScope Create(int tenantId) =>
            throw new InvalidOperationException($"Kiracı {tenantId} için bağlantı çözülemedi.");
    }

    private static IdentityService ServisiKur(int dizindekiKiraciId) =>
        new(new FirlatanKapsamFabrikasi(),
            tokenService: null!,
            currentTenant: null!,
            emailSender: null!,
            configuration: null!,
            platform: null!,
            new SapmisDizin(dizindekiKiraciId),
            new BosKutuk(),
            accessGuard: null!,
            NullLogger<IdentityService>.Instance);

    [Fact]
    public async Task Dizin_VarOlmayanKiraciyaIsaretEdiyorsa_FalseDoner()
    {
        var servis = ServisiKur(dizindekiKiraciId: -1);

        var sonuc = await servis.EmailExistsAsync("sapmis@ornek.local", CancellationToken.None);

        sonuc.Should().BeFalse(
            "kiracısı olmayan bir dizin kaydı 'kullanıcı yok' demektir; kapsam açılmamalı ve istisna fırlamamalı");
    }
}
