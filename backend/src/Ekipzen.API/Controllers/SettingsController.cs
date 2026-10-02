using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Sistem ayarları uçları (Faz 11) — routes.js: /settings yalnız hr-admin.
/// Şirket profili, bildirim toggle'ları, güvenlik (2FA) ve abonelik görünümü.
/// Şifre değişikliği mevcut /api/auth/change-password ucundan yapılır.
/// </summary>
[ApiController]
[Route("api/settings")]
[Authorize(Policy = Policies.HrAdminOnly)]
public sealed class SettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SettingsDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetSettingsQuery(), cancellationToken));

    [HttpPut("company")]
    [ProducesResponseType<CompanyProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyProfileDto>> UpdateCompany(
        UpdateCompanyProfileCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    [HttpPut("notifications")]
    [ProducesResponseType<NotificationSettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationSettingsDto>> UpdateNotifications(
        UpdateNotificationSettingsCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    [HttpPut("security")]
    [ProducesResponseType<SecuritySettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SecuritySettingsDto>> UpdateSecurity(
        UpdateSecuritySettingsCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    [HttpPost("company/logo")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadLogo(IFormFile file, [FromForm] string? expectedRecordToken, [FromForm] int? guidanceContentRevisionId, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var logoPath = await sender.Send(
            new UploadCompanyLogoCommand(stream, file.FileName, file.Length, expectedRecordToken, guidanceContentRevisionId), cancellationToken);
        return Ok(new { logoPath });
    }
}

/// <summary>
/// Şirket logosu tüm rollere görünür (layout header'ı); hr-admin'e kilitli
/// SettingsController'dan ayrı tutulur (attribute policy'ler VE ile birleşir).
/// </summary>
[ApiController]
[Route("api/settings")]
[Authorize]
public sealed class CompanyLogoController(ISender sender) : ControllerBase
{
    [HttpGet("company/logo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(CancellationToken cancellationToken)
    {
        var (content, fileName) = await sender.Send(new GetCompanyLogoQuery(), cancellationToken);

        if (!new FileExtensionContentTypeProvider().TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return File(content, contentType, fileName);
    }
}
