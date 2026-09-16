namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracının erişilebilir olduğunu doğrular. Yalnız <c>Active</c> geçer;
/// <c>Provisioning</c> (kurulum sürüyor/yarıda kaldı), <c>Frozen</c>
/// (bakım/geri yükleme) ve <c>Purging</c> (siliniyor) reddedilir.
///
/// Kapı iki darboğaza yerleşir: <c>IdentityService.IssueTokensAsync</c>
/// (login VE refresh'in ortak yolu — token üretimini kapatır) ve
/// <c>TenantAccessMiddleware</c> (elinde hâlâ geçerli access token'ı olan
/// isteği karşılar). Biri diğerinin yerini tutmaz.
/// </summary>
public interface ITenantAccessGuard
{
    /// <summary>Erişilebilir değilse <c>TenantInaccessibleException</c> fırlatır.</summary>
    Task EnsureAccessibleAsync(int tenantId, CancellationToken cancellationToken);
}
