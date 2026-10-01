using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Guidance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

[ApiController]
[Route("api/guidance/operations")]
[Authorize(Policy = Policies.Management)]
public sealed class GuidanceOperationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GuidanceOperationHistoryDto>>> Get(CancellationToken ct) =>
        Ok(await sender.Send(new GetMyGuidanceOperationsQuery(), ct));
}
