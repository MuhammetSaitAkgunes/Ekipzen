using Ekipzen.Application.Features.DemoRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Tanıtım sitesinin anonim uçları. Kiracı bağlamı yoktur; TenantAccessMiddleware
/// [AllowAnonymous] uçlara dokunmaz.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicController(ISender sender) : ControllerBase
{
    /// <remarks>Demo talebini platform sahibine e-postayla iletir. Hesap açmaz.</remarks>
    [HttpPost("demo-requests")]
    [EnableRateLimiting("public-form")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SubmitDemoRequest(SubmitDemoRequestCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }
}
