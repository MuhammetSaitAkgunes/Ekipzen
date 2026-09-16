using Ekipzen.Application.Common.Interfaces;
using System.Security.Claims;

namespace Ekipzen.API.Services;

/// <summary>
/// Aktif kiracıyı JWT <c>tenant</c> claim'inden sağlar (MapInboundClaims kapalı).
/// Scoped: bir istek/scope boyunca aynı kiracı. <see cref="Impersonate"/> ile
/// HTTP dışı (seed/platform) senaryolarda kiracı sabitlenebilir.
/// </summary>
public sealed class CurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    private int? _impersonated;

    public int? TenantId =>
        _impersonated
        ?? (int.TryParse(accessor.HttpContext?.User.FindFirstValue("tenant"), out var id) ? id : null);

    public int TenantIdOrThrow() =>
        TenantId ?? throw new InvalidOperationException("Aktif kiracı bulunamadı (tenant claim yok).");

    public void Impersonate(int tenantId) => _impersonated = tenantId;
}
