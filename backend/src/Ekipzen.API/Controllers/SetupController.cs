using Microsoft.AspNetCore.Mvc.ModelBinding;
using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Setup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.Controllers;

[ApiController]
[Route("api/setup")]
[Authorize(Policy = Policies.HrAdminOnly)]
public sealed class SetupController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<TenantSetupStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantSetupStatusDto>> GetStatus(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTenantSetupStatusQuery(), cancellationToken));

    [HttpGet("preview")]
    public async Task<ActionResult<TenantSetupPreviewDto>> Preview(CancellationToken ct)
        => Ok(await sender.Send(new GetTenantSetupPreviewQuery(), ct));

    [HttpPost]
    [ProducesResponseType<TenantSetupStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantSetupStatusDto>> Initialize([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] InitializeTenantSetupCommand? command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command ?? new InitializeTenantSetupCommand(), cancellationToken));
}
