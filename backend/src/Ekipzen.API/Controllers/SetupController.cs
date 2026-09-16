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

    [HttpPost]
    [ProducesResponseType<TenantSetupStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantSetupStatusDto>> Initialize(CancellationToken cancellationToken)
        => Ok(await sender.Send(new InitializeTenantSetupCommand(), cancellationToken));
}
