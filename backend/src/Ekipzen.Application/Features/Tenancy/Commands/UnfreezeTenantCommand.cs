using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Bir kiracıyı <see cref="TenantStatus.Frozen"/>'dan <see cref="TenantStatus.Active"/>'e
/// geri döndürür ve kütüğü ANINDA düşürür — bkz. <see cref="FreezeTenantCommand"/>.
///
/// <c>Provisioning</c>/<c>Purging</c> durumundaki bir kiracı bu uçla çözülemez: bu
/// durumlar yaşam döngüsünün kendi akışına aittir, elle müdahale edilmez.
/// </summary>
public sealed record UnfreezeTenantCommand(int TenantId) : IRequest<TenantStatusResult>;

public sealed class UnfreezeTenantCommandHandler(IPlatformDbContext platform, ITenantRegistry registry)
    : IRequestHandler<UnfreezeTenantCommand, TenantStatusResult>
{
    public async Task<TenantStatusResult> Handle(UnfreezeTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await platform.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Kiracı", request.TenantId);

        if (tenant.Status == TenantStatus.Active)
        {
            return new TenantStatusResult(tenant.Id, tenant.Slug, tenant.Status, AlreadyInTargetState: true);
        }

        if (tenant.Status != TenantStatus.Frozen)
        {
            throw new ConflictException(
                $"Kiracı '{tenant.Slug}' {tenant.Status} durumunda; yalnız Frozen bir kiracı çözülebilir. " +
                "Provisioning/Purging yaşam döngüsünün kendi akışına aittir, elle çözülmez.");
        }

        tenant.Status = TenantStatus.Active;
        await platform.SaveChangesAsync(cancellationToken);

        registry.Invalidate(tenant.Id);

        return new TenantStatusResult(tenant.Id, tenant.Slug, tenant.Status);
    }
}
