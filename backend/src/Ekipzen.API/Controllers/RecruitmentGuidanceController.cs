using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Guidance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

[ApiController]
[Route("api/guidance/recruitment")]
[Authorize(Policy = Policies.HrAdminOnly)]
public sealed class RecruitmentGuidanceController(ISender sender) : ControllerBase
{
    [HttpGet("journey")]
    [ProducesResponseType<RecruitmentJourneyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecruitmentJourneyDto>> GetJourney(CancellationToken ct) =>
        Ok(await sender.Send(new GetRecruitmentJourneyQuery(), ct));
}
