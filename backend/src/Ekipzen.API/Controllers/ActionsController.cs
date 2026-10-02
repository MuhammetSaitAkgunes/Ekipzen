using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Actions;
using Ekipzen.Application.Features.Actions.Commands;
using Ekipzen.Application.Features.Actions.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Aksiyon merkezi &amp; denetim izi &amp; birleşik arama uçları (Faz 10).
/// routes.js: /actions tüm rollere açık → liste/rozet/arama herkese; CRUD hr-admin;
/// durum geçişi hr-admin + manager; denetim izi yönetsel bilgi olduğundan Management.
/// apiClient.js sözleşmesi: GET /api/actions, GET /api/audit-logs, PATCH /api/actions/{id}/status.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public sealed class ActionsController(ISender sender) : ControllerBase
{
    [HttpGet("actions")]
    [ProducesResponseType<IReadOnlyList<GlobalActionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GlobalActionDto>>> GetActions(
        [FromQuery] string? priority, [FromQuery] string? source, [FromQuery] string? owner,
        [FromQuery] string? status, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetGlobalActionsQuery(priority, source, owner, status), cancellationToken));

    /// <summary>Sidebar açık-aksiyon rozet sayacı.</summary>
    [HttpGet("actions/badge")]
    [ProducesResponseType<ActionBadgeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionBadgeDto>> GetBadge(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetActionBadgeQuery(), cancellationToken));

    [HttpPost("actions")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<GlobalActionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<GlobalActionDto>> Create(
        CreateGlobalActionCommand command, CancellationToken cancellationToken)
    {
        var action = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetActions), new { }, action);
    }

    [HttpPut("actions/{id:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<GlobalActionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GlobalActionDto>> Update(
        int id, ActionUpdateBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new UpdateGlobalActionCommand(
                id, body.Title, body.Source, body.Owner, body.SourceRoute,
                body.Due, body.Priority, body.RecommendedAction, body.ExpectedRevision, body.GuidanceContentRevisionId),
            cancellationToken));

    /// <summary>Durum geçişi (open→week→done, ileri yönlü; geri dönüş 409).</summary>
    [HttpPatch("actions/{id:int}/status")]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<GlobalActionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GlobalActionDto>> SetStatus(
        int id, ActionStatusBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new SetGlobalActionStatusCommand(id, body.Status, body.ExpectedRevision, body.GuidanceContentRevisionId), cancellationToken));

    [HttpDelete("actions/{id:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, [FromQuery] string? expectedRevision, [FromQuery] int? guidanceContentRevisionId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteGlobalActionCommand(id, expectedRevision, guidanceContentRevisionId), cancellationToken);
        return NoContent();
    }

    /// <summary>Denetim izi (append-only; kritik tablo trigger'ları doldurur).</summary>
    [HttpGet("audit-logs")]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<IReadOnlyList<AuditLogDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> GetAuditLogs(
        [FromQuery] string? module, [FromQuery] string? search, [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => Ok(await sender.Send(new GetAuditLogsQuery(module, search, take), cancellationToken));

    /// <summary>Birleşik arama: personel (rol kapsamlı) + aksiyon + aday (hr-admin).</summary>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<SearchResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> Search(
        [FromQuery] string q, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GlobalSearchQuery(q ?? string.Empty), cancellationToken));

    public sealed record ActionUpdateBody(
        string Title, string Source, string Owner, string? SourceRoute,
        string? Due, string Priority, string? RecommendedAction, string? ExpectedRevision = null, int? GuidanceContentRevisionId = null);
    public sealed record ActionStatusBody(string Status, string? ExpectedRevision = null, int? GuidanceContentRevisionId = null);
}
