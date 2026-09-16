using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Bir kiracının yönlendirme dizinini kiracı veritabanındaki kullanıcılardan
/// yeniden kurar.
///
/// Neden var: dizin TÜRETİLMİŞ bir tablodur. Bir geri yüklemeden sonra platform
/// veritabanı geri sarılmadığı için dizin, geri yüklenmiş kiracı veritabanıyla
/// sapabilir — dizinde olup kiracıda olmayan kullanıcılar kalabilir. Bu komut
/// sapmayı kalıcı olmaktan çıkarır ve geri yükleme prosedürünün zorunlu adımıdır.
/// </summary>
public sealed record RebuildDirectoryCommand(int TenantId) : IRequest<RebuildDirectoryResult>;

/// <param name="CakisanEpostalar">
/// Geri yüklenen kiracının bir kullanıcısıyla AYNI e-postayı hâlâ BAŞKA bir
/// kiracının adına tutan, bu yüzden atlanan satırlar. Boş değilse operatör elle
/// müdahale etmeli — "tek e-posta = tek kiracı" kuralı gevşetilmez, çakışan
/// e-posta bu kiracıya devredilmez.
/// </param>
public sealed record RebuildDirectoryResult(int TenantId, int YazilanKayit, IReadOnlyList<string> CakisanEpostalar);

public sealed class RebuildDirectoryCommandHandler(
    IPlatformDbContext platform,
    IUserDirectorySource kullanicilar,
    ITenantDirectory directory)
    : IRequestHandler<RebuildDirectoryCommand, RebuildDirectoryResult>
{
    public async Task<RebuildDirectoryResult> Handle(
        RebuildDirectoryCommand request, CancellationToken cancellationToken)
    {
        var tenantVarMi = await platform.Tenants.AnyAsync(t => t.Id == request.TenantId, cancellationToken);
        if (!tenantVarMi)
        {
            throw new NotFoundException("Kiracı", request.TenantId);
        }

        var epostalar = await kullanicilar.NormalizedEmailsAsync(request.TenantId, cancellationToken);

        var sonuc = await directory.RebuildForTenantAsync(request.TenantId, epostalar, cancellationToken);
        return new RebuildDirectoryResult(request.TenantId, sonuc.YazilanKayit, sonuc.CakisanEpostalar);
    }
}
