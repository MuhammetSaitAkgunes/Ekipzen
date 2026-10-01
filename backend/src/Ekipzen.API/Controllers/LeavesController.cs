using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Leaves;
using Ekipzen.Application.Features.Leaves.Commands;
using Ekipzen.Application.Features.Leaves.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

/// <summary>
/// İzin &amp; onay uçları (routes.js: /leaves tüm roller). Bakiye/talepler kişiye,
/// onay kuyruğu yönetim rollerine ve ekip kapsamına bağlıdır.
/// </summary>
[ApiController]
[Route("api/leaves")]
[Authorize]
public sealed class LeavesController(ISender sender) : ControllerBase
{
    [HttpGet("types")]
    [ProducesResponseType<IReadOnlyList<LeaveTypeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeDto>>> GetTypes(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetLeaveTypesQuery(), cancellationToken));

    [HttpGet("balance")]
    [ProducesResponseType<LeaveBalanceDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LeaveBalanceDto>> GetMyBalance(
        [FromQuery] int? year, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMyLeaveBalanceQuery(year), cancellationToken));

    [HttpGet("ledger")]
    [ProducesResponseType<IReadOnlyList<LeaveLedgerEntryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaveLedgerEntryDto>>> GetMyLedger(
        [FromQuery] int? year, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMyLeaveLedgerQuery(year), cancellationToken));

    [HttpGet("my")]
    [ProducesResponseType<IReadOnlyList<LeaveRequestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> GetMyRequests(
        [FromQuery] int? year, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMyLeaveRequestsQuery(year), cancellationToken));

    [HttpPost("preview")]
    [ProducesResponseType<LeaveRequestPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaveRequestPreviewDto>> Preview(
        PreviewLeaveRequestQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    [HttpPost]
    [ProducesResponseType<LeaveRequestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveRequestDto>> Create(
        CreateLeaveRequestCommand command, CancellationToken cancellationToken)
    {
        var created = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetMyRequests), new { }, created);
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelLeaveRequestCommand(id), cancellationToken);
        return NoContent();
    }

    // --- onay kuyruğu ---

    [HttpGet("pending")]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<IReadOnlyList<LeaveRequestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> GetPending(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPendingLeaveRequestsQuery(), cancellationToken));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<LeaveRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveRequestDto>> Approve(
        int id, DecisionBody? body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new DecideLeaveRequestCommand(id, true, body?.Note, body?.GuidanceContentRevisionId), cancellationToken));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<LeaveRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveRequestDto>> Reject(
        int id, DecisionBody? body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new DecideLeaveRequestCommand(id, false, body?.Note, body?.GuidanceContentRevisionId), cancellationToken));

    // --- takım yokluk widget'ı ---

    [HttpGet("team")]
    [ProducesResponseType<IReadOnlyList<TeamLeaveDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamLeaveDto>>> GetTeamLeaves(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTeamLeavesQuery(), cancellationToken));

    public sealed record DecisionBody(string? Note, int? GuidanceContentRevisionId = null);
}
