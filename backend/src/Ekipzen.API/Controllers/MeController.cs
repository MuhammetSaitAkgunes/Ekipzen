using Ekipzen.Application.Features.Auth;
using Ekipzen.Application.Features.Auth.DataExport;
using Ekipzen.Application.Features.Auth.GetMe;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

/// <summary>Oturum açmış kullanıcının profili — apiClient.js sözleşmesi: GET /api/me.</summary>
[ApiController]
[Route("api/me")]
public sealed class MeController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMeQuery(), cancellationToken));

    /// <remarks>KVKK taşınabilirlik: kullanıcının kendi verisini JSON paketi olarak indirir.</remarks>
    [HttpGet("data-export")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DataExport(CancellationToken cancellationToken)
    {
        var (content, fileName) = await sender.Send(new GetMyDataExportQuery(), cancellationToken);
        return File(content, "application/json", fileName);
    }
}
