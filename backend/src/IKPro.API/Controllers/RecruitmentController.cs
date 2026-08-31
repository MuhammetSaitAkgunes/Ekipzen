using IKPro.Application.Common.Security;
using IKPro.Application.Features.Recruitment;
using IKPro.Application.Features.Recruitment.Commands;
using IKPro.Application.Features.Recruitment.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IKPro.API.Controllers;

/// <summary>
/// İşe alım (ATS) uçları — routes.js: /recruitment yalnız hr-admin.
/// Aday havuzu, pipeline geçişleri, not/değerlendirme, hire→Employee dönüşümü ve funnel.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Policy = Policies.HrAdminOnly)]
public sealed class RecruitmentController(ISender sender) : ControllerBase
{
    // --- adaylar ---

    [HttpGet("candidates")]
    [ProducesResponseType<IReadOnlyList<CandidateListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CandidateListItemDto>>> GetCandidates(
        [FromQuery] string? search, [FromQuery] string? status, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCandidatesQuery(search, status), cancellationToken));

    [HttpGet("candidates/{id:int}")]
    [ProducesResponseType<CandidateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateDetailDto>> GetCandidate(int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCandidateQuery(id), cancellationToken));

    [HttpPost("candidates")]
    [ProducesResponseType<CandidateDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CandidateDetailDto>> CreateCandidate(
        CreateCandidateCommand command, CancellationToken cancellationToken)
    {
        var candidate = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetCandidate), new { id = candidate.Id }, candidate);
    }

    [HttpPatch("candidates/{id:int}/status")]
    [ProducesResponseType<CandidateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CandidateDetailDto>> SetStatus(
        int id, CandidateStatusBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new SetCandidateStatusCommand(id, body.Status), cancellationToken));

    [HttpPost("candidates/{id:int}/notes")]
    [ProducesResponseType<InterviewNoteDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InterviewNoteDto>> AddNote(
        int id, CandidateNoteBody body, CancellationToken cancellationToken)
    {
        var note = await sender.Send(new AddInterviewNoteCommand(id, body.NoteType, body.Text), cancellationToken);
        return CreatedAtAction(nameof(GetCandidate), new { id }, note);
    }

    [HttpPost("candidates/{id:int}/evaluations")]
    [ProducesResponseType<CandidateEvaluationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CandidateEvaluationDto>> AddEvaluation(
        int id, CandidateEvaluationBody body, CancellationToken cancellationToken)
    {
        var evaluation = await sender.Send(
            new AddCandidateEvaluationCommand(id, body.Criterion, body.Score, body.MaxScore), cancellationToken);
        return CreatedAtAction(nameof(GetCandidate), new { id }, evaluation);
    }

    [HttpPost("candidates/{id:int}/hire")]
    [ProducesResponseType<HireResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HireResultDto>> Hire(
        int id, CandidateHireBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new HireCandidateCommand(id, body.DepartmentId, body.Email, body.Title, body.HireDate), cancellationToken));

    [HttpPost("candidates/{id:int}/provisioning/retry")]
    [ProducesResponseType<IdentityProvisioningDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IdentityProvisioningDto>> RetryProvisioning(
        int id, RetryProvisioningBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RetryCandidateProvisioningCommand(id, body.Email), cancellationToken));

    // --- pozisyonlar & funnel ---

    [HttpGet("positions")]
    [ProducesResponseType<IReadOnlyList<PositionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PositionDto>>> GetPositions(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPositionsQuery(), cancellationToken));

    [HttpPost("positions")]
    [ProducesResponseType<PositionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PositionDto>> CreatePosition(
        CreatePositionCommand command, CancellationToken cancellationToken)
    {
        var position = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetPositions), new { }, position);
    }

    [HttpGet("recruitment/funnel")]
    [ProducesResponseType<RecruitmentFunnelDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecruitmentFunnelDto>> GetFunnel(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetRecruitmentFunnelQuery(), cancellationToken));

    public sealed record CandidateStatusBody(string Status);
    public sealed record CandidateNoteBody(string NoteType, string Text);
    public sealed record CandidateEvaluationBody(string Criterion, int Score, int MaxScore = 5);
    public sealed record CandidateHireBody(int DepartmentId, string Email, string? Title = null, DateOnly? HireDate = null);
    public sealed record RetryProvisioningBody(string? Email = null);
}
