using IKPro.Application.Features.Auth;
using IKPro.Application.Features.Auth.AcceptInvite;
using IKPro.Application.Features.Auth.ChangePassword;
using IKPro.Application.Features.Auth.Login;
using IKPro.Application.Features.Auth.Logout;
using IKPro.Application.Features.Auth.Refresh;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IKPro.API.Controllers;

/// <summary>
/// Kimlik doğrulama uçları — apiClient.js sözleşmesi: POST /api/auth/login;
/// ek olarak refresh/logout/change-password.
///
/// <c>POST /api/auth/register</c> KASITLI olarak yok: anonim self-servis kayıt
/// her yeni kullanıcıyı <c>DefaultTenantIdAsync</c> (platformdaki EN DÜŞÜK Id'li
/// kiracı — üretimde gerçek bir müşteri) altına bağlıyor ve token'ı erişim
/// kapısını BİLİNÇLİ ATLAYARAK (<c>skipAccessCheck: true</c>) veriyordu; internetten
/// herhangi biri kaydolup o müşterinin İK panosunu okuyabiliyordu (kiracı
/// sızıntısı). İK ürününde kimse başkasının şirketine kendi kendine kaydolmaz —
/// hesap açmanın tek meşru yolu operatörün kiracıyı provizyonlaması
/// (<c>POST /api/tenants</c>, platform anahtarlı) ve ardından davetin kabulüdür
/// (<c>POST /api/auth/accept-invite</c>). Public şirket kaydı
/// (<c>POST /api/tenants/signup</c>) da kaldırıldı — bkz. TenancyController xmldoc.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    /// <remarks>Refresh token'ın kendisi kimlik kanıtıdır; erişim token'ı süresi dolmuş olsa da çıkış yapılabilir.</remarks>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <remarks>Davet token'ıyla ilk şifreyi belirler (anonim; kullanıcının henüz oturumu yok).</remarks>
    [HttpPost("accept-invite")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvite(AcceptInviteCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }
}
