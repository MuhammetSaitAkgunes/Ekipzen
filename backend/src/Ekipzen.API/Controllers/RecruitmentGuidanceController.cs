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

    [HttpGet("checks/{stepKey}")]
    [ProducesResponseType<RecruitmentDecisionCheckDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecruitmentDecisionCheckDto>> GetCheck(
        string stepKey, CancellationToken ct) =>
        Ok(await sender.Send(new GetRecruitmentDecisionCheckQuery(stepKey), ct));

    [HttpPost("checks/{stepKey}/attempts")]
    [ProducesResponseType<RecruitmentDecisionCheckDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecruitmentDecisionCheckDto>> SubmitCheck(
        string stepKey, RecruitmentCheckAnswerBody body, CancellationToken ct) =>
        Ok(await sender.Send(new SubmitRecruitmentDecisionCheckCommand(stepKey, body.SelectedChoice), ct));

    public sealed record RecruitmentCheckAnswerBody(int SelectedChoice);
}
