using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Compliance;
using Ekipzen.Application.Features.Compliance.Commands;
using Ekipzen.Application.Features.Compliance.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Uyum &amp; belge uçları (Faz 9). Okuma: hr-admin + manager (manager yalnız ekibini görür);
/// mutasyonlar (oluştur/güncelle/durum/owner) yalnız hr-admin.
/// </summary>
[ApiController]
[Route("api/compliance")]
[Authorize(Policy = Policies.Management)]
public sealed class ComplianceController(ISender sender) : ControllerBase
{
    [HttpGet("documents")]
    [ProducesResponseType<IReadOnlyList<ComplianceDocumentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComplianceDocumentDto>>> GetDocuments(
        [FromQuery] string? status, [FromQuery] string? level, [FromQuery] string? search,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetComplianceDocumentsQuery(status, level, search), cancellationToken));

    [HttpGet("readiness")]
    [ProducesResponseType<ComplianceReadinessDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ComplianceReadinessDto>> GetReadiness(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetComplianceReadinessQuery(), cancellationToken));

    [HttpPost("documents")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<ComplianceDocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ComplianceDocumentDto>> Create(
        CreateComplianceDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetDocuments), new { }, document);
    }

    [HttpPut("documents/{id:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<ComplianceDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplianceDocumentDto>> Update(
        int id, ComplianceUpdateBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new UpdateComplianceDocumentCommand(id, body.DocumentName, body.DueDate, body.Level, body.GuidanceContentRevisionId, body.ExpectedRevision, body.OwnerName, body.UpdateOwner),
            cancellationToken));

    [HttpPatch("documents/{id:int}/status")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<ComplianceDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ComplianceDocumentDto>> SetStatus(
        int id, ComplianceStatusBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new SetComplianceStatusCommand(id, body.Status, body.GuidanceContentRevisionId, body.ExpectedRevision), cancellationToken));

    [HttpPatch("documents/{id:int}/owner")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<ComplianceDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplianceDocumentDto>> AssignOwner(
        int id, ComplianceOwnerBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new AssignComplianceOwnerCommand(id, body.OwnerName, body.GuidanceContentRevisionId, body.ExpectedRevision), cancellationToken));

    public sealed record ComplianceUpdateBody(string DocumentName, DateOnly? DueDate, string Level, int? GuidanceContentRevisionId = null, string? ExpectedRevision = null, string? OwnerName = null, bool UpdateOwner = false);
    public sealed record ComplianceStatusBody(string Status, int? GuidanceContentRevisionId = null, string? ExpectedRevision = null);
    public sealed record ComplianceOwnerBody(string OwnerName, int? GuidanceContentRevisionId = null, string? ExpectedRevision = null);
}
