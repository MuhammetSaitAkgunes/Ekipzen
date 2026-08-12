using IKPro.Application.Features.Tenancy.Commands;
using IKPro.Application.Features.Tenancy.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IKPro.API.Controllers;

/// <summary>
/// Platform (kiracı-üstü) uçları. Kiracı provizyonu normal rol sisteminin DIŞINDADIR —
/// bir kiracı başka bir kiracı açamaz. Bu yüzden JWT değil, sunucu-tarafı bir
/// <c>X-Platform-Key</c> ile korunur (yapılandırmadan/ortamdan gelir).
///
/// <para>
/// <c>POST /api/tenants/signup</c> (anonim self-servis kayıt) KASITLI olarak yoktur.
/// Kiracı başına ayrı veritabanına geçildiğinde bir kiracı açmak artık bir satır
/// değil bir <b>veritabanı</b> oluşturmak demek; anonim bir uç bunu tetikleyebilseydi
/// internetten herhangi biri bir script'le SQL Server'a istediği kadar veritabanı
/// kurdurabilirdi. Veritabanı oluşturmak otomatik bir yan etki değil, operatörün
/// <b>bilinçli bir eylemi</b> olmalıdır — bu yüzden kiracı yalnız platform
/// anahtarıyla (<c>POST /api/tenants</c>) açılır.
/// </para>
/// </summary>
[ApiController]
[Route("api/tenants")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class TenancyController(ISender sender, IConfiguration configuration) : ControllerBase
{
    private const string PlatformKeyHeader = "X-Platform-Key";

    [HttpPost]
    [ProducesResponseType<ProvisionTenantResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProvisionTenantResult>> Provision(
        ProvisionTenantCommand command, CancellationToken cancellationToken)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        var result = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <remarks>Bir kiracının TÜM verisini kalıcı siler (KVKK). Platform-key + confirmSlug zorunlu.</remarks>
    [HttpDelete("{id:int}")]
    [ProducesResponseType<PurgeTenantResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurgeTenantResult>> Purge(
        int id, [FromQuery] string? confirmSlug, CancellationToken cancellationToken)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        return Ok(await sender.Send(new PurgeTenantCommand(id, confirmSlug ?? ""), cancellationToken));
    }

    /// <remarks>Doğrulanmamış (pasif, davet kabul edilmemiş) eski kiracıları toplu siler. Cron.</remarks>
    [HttpPost("cleanup-unverified")]
    [ProducesResponseType<CleanupUnverifiedResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CleanupUnverifiedResult>> CleanupUnverified(
        [FromQuery] int olderThanDays, CancellationToken cancellationToken)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        return Ok(await sender.Send(new CleanupUnverifiedTenantsCommand(olderThanDays), cancellationToken));
    }

    /// <remarks>Kiracının yönlendirme dizinini yeniden kurar (geri yükleme sonrası zorunlu adım).</remarks>
    [HttpPost("{id:int}/rebuild-directory")]
    [ProducesResponseType<RebuildDirectoryResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RebuildDirectoryResult>> RebuildDirectory(
        int id, CancellationToken cancellationToken)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        return Ok(await sender.Send(new RebuildDirectoryCommand(id), cancellationToken));
    }

    /// <remarks>Kiracıyı Active'den Frozen'a alır ve kütüğü ANINDA düşürür (bakım/geri
    /// yükleme öncesi zorunlu adım — bkz. yedekleme-ve-kurtarma.md). Provisioning/Purging
    /// durumundaki bir kiracı dondurulamaz.</remarks>
    [HttpPost("{id:int}/freeze")]
    [ProducesResponseType<TenantStatusResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantStatusResult>> Freeze(
        int id, CancellationToken cancellationToken)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        return Ok(await sender.Send(new FreezeTenantCommand(id), cancellationToken));
    }

    /// <remarks>Kiracıyı Frozen'dan Active'e döndürür ve kütüğü ANINDA düşürür.
    /// Provisioning/Purging durumundaki bir kiracı bu uçla çözülemez.</remarks>
    [HttpPost("{id:int}/unfreeze")]
    [ProducesResponseType<TenantStatusResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantStatusResult>> Unfreeze(
        int id, CancellationToken cancellationToken)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        return Ok(await sender.Send(new UnfreezeTenantCommand(id), cancellationToken));
    }

    /// <remarks>
    /// Kiracıdaki, henüz şifre belirlememiş bir kullanıcıya davet e-postasını YENİDEN
    /// gönderir. Provizyonun e-posta adımı başarısız olduğunda (SMTP arızası, spam,
    /// yanlış adres) operatörün kurtarma aracı budur — o olmadan tek çare kiracıyı
    /// silip baştan kurmaktı. Şifresi olan hesaba gönderilmez (409).
    /// </remarks>
    [HttpPost("{id:int}/resend-invite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendInvite(
        int id, ResendInviteRequest request, CancellationToken cancellationToken)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        await sender.Send(new ResendTenantInviteCommand(id, request.Email), cancellationToken);
        return NoContent();
    }

    /// <summary>Davet yeniden gönderme isteği gövdesi.</summary>
    public sealed record ResendInviteRequest(string Email);

    /// <remarks>Provizyonu/silmesi yarıda kalmış kiracılar (operatör görünürlüğü).</remarks>
    [HttpGet("stuck")]
    [ProducesResponseType<IReadOnlyList<StuckTenantDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<StuckTenantDto>>> Stuck(
        [FromQuery] int olderThanMinutes = 60, CancellationToken cancellationToken = default)
    {
        if (!PlatformKeyValid())
        {
            return Unauthorized(new { title = "Platform anahtarı geçersiz veya eksik." });
        }

        return Ok(await sender.Send(new GetStuckTenantsQuery(olderThanMinutes), cancellationToken));
    }

    private bool PlatformKeyValid()
    {
        var expected = configuration["Platform:ProvisioningKey"];
        var provided = Request.Headers[PlatformKeyHeader].ToString();
        return !string.IsNullOrEmpty(expected) && provided == expected;
    }

}
