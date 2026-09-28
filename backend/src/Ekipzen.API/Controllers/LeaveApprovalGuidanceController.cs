using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Guidance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

[ApiController]
[Route("api/guidance/leave-approval")]
[Authorize(Policy = Policies.Management)]
public sealed class LeaveApprovalGuidanceController(ISender sender) : ControllerBase
{
    [HttpGet("journey")]
    [ProducesResponseType<LeaveApprovalJourneyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaveApprovalJourneyDto>> GetJourney(CancellationToken ct) =>
        Ok(await sender.Send(new GetLeaveApprovalJourneyQuery(), ct));

    [HttpGet("checks/{stepKey}")]
    [ProducesResponseType<LeaveApprovalDecisionCheckDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaveApprovalDecisionCheckDto>> GetCheck(
        string stepKey, CancellationToken ct) =>
        Ok(await sender.Send(new GetLeaveApprovalDecisionCheckQuery(stepKey), ct));

    [HttpPost("checks/{stepKey}/attempts")]
    [ProducesResponseType<LeaveApprovalDecisionCheckDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaveApprovalDecisionCheckDto>> SubmitCheck(
        string stepKey, LeaveApprovalCheckAnswerBody body, CancellationToken ct) =>
        Ok(await sender.Send(new SubmitLeaveApprovalDecisionCheckCommand(stepKey, body.SelectedChoice), ct));

    public sealed record LeaveApprovalCheckAnswerBody(int SelectedChoice);
}
