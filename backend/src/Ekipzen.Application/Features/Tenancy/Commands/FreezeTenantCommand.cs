using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Tenancy.Commands;

/// <summary>
/// Bir kiracıyı <see cref="TenantStatus.Active"/>'den <see cref="TenantStatus.Frozen"/>'a
/// geçirir ve kütüğü ANINDA düşürür (<see cref="ITenantRegistry.Invalidate"/>).
///
/// Neden var: operatörün elinde dondurmanın desteklenen bir yolu yoktu — yedekleme
/// runbook'u geri yükleme prosedürünün ilk adımı olarak "kiracıyı dondur" diyor, ama
/// tek seçenek doğrudan SQL ile <c>Tenants.Status</c> güncellemekti. O yol kütüğü
/// DÜŞÜRMEZ; değişiklik en geç 5 dakika (kütüğün TTL'i) sonra görünür olurdu — geri
/// yükleme gibi hemen etkili olması gereken bir işlem için kabul edilemez bir gecikme.
///
/// <c>Provisioning</c>/<c>Purging</c> durumundaki bir kiracı dondurulamaz: bu durumlar
/// yaşam döngüsünün kendi akışına aittir, elle müdahale edilmez.
/// </summary>
public sealed record FreezeTenantCommand(int TenantId) : IRequest<TenantStatusResult>;

/// <param name="AlreadyInTargetState">
/// <c>true</c> ise kiracı zaten dondurulmuştu — işlem idempotent no-op olarak
/// tamamlandı (operatör yeniden denerse hataya çarpmamalı).
/// </param>
public sealed record TenantStatusResult(int TenantId, string Slug, TenantStatus Status, bool AlreadyInTargetState = false);

public sealed class FreezeTenantCommandHandler(IPlatformDbContext platform, ITenantRegistry registry)
    : IRequestHandler<FreezeTenantCommand, TenantStatusResult>
{
    public async Task<TenantStatusResult> Handle(FreezeTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await platform.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Kiracı", request.TenantId);

        if (tenant.Status == TenantStatus.Frozen)
        {
            return new TenantStatusResult(tenant.Id, tenant.Slug, tenant.Status, AlreadyInTargetState: true);
        }

        if (tenant.Status != TenantStatus.Active)
        {
            throw new ConflictException(
                $"Kiracı '{tenant.Slug}' {tenant.Status} durumunda; yalnız Active bir kiracı dondurulabilir. " +
                "Provisioning/Purging yaşam döngüsünün kendi akışına aittir, elle dondurulmaz.");
        }

        tenant.Status = TenantStatus.Frozen;
        await platform.SaveChangesAsync(cancellationToken);

        // Asıl mesele burası: kütük ANINDA düşmeli, TTL'in dolmasını beklemeyiz.
        registry.Invalidate(tenant.Id);

        return new TenantStatusResult(tenant.Id, tenant.Slug, tenant.Status);
    }
}
