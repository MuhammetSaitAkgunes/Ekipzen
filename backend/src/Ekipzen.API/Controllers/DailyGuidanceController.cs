using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Guidance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

[ApiController]
[Route("api/guidance/daily-tasks")]
[Authorize(Policy = Policies.Management)]
public sealed class DailyGuidanceController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DailyGuidanceTaskDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DailyGuidanceTaskDto>>> Get(CancellationToken ct) =>
        Ok(await sender.Send(new GetDailyGuidanceTasksQuery(), ct));
}
