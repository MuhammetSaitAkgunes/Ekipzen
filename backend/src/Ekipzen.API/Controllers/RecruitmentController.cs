using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Recruitment;
using Ekipzen.Application.Features.Recruitment.Commands;
using Ekipzen.Application.Features.Recruitment.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

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
        => Ok(await sender.Send(new SetCandidateStatusCommand(id, body.Status,
            body.ExpectedVersion, body.Reason, body.Evidence, body.GuidanceContentRevisionId), cancellationToken));

    [HttpPut("candidates/{id:int}")]
    [ProducesResponseType<CandidateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CandidateDetailDto>> CorrectCandidate(
        int id, CorrectCandidateBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new CorrectCandidateCommand(id, body.ExpectedVersion,
            body.Name, body.AppliedRole, body.PositionId, body.ExpectedPositionVersion,
            body.Score, body.Location, body.ExperienceYears, body.Summary,
            body.Reason, body.ConfirmExistingEvidenceReview, body.GuidanceContentRevisionId), cancellationToken));

    [HttpGet("candidates/{id:int}/corrections")]
    [ProducesResponseType<IReadOnlyList<CandidateCorrectionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CandidateCorrectionDto>>> GetCandidateCorrections(
        int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCandidateCorrectionsQuery(id), cancellationToken));

    [HttpPost("candidates/{id:int}/notes")]
    [ProducesResponseType<InterviewNoteDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InterviewNoteDto>> AddNote(
        int id, CandidateNoteBody body, CancellationToken cancellationToken)
    {
        var note = await sender.Send(new AddInterviewNoteCommand(id, body.NoteType, body.Text, body.GuidanceContentRevisionId), cancellationToken);
        return CreatedAtAction(nameof(GetCandidate), new { id }, note);
    }

    [HttpPost("candidates/{id:int}/evaluations")]
    [ProducesResponseType<CandidateEvaluationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CandidateEvaluationDto>> AddEvaluation(
        int id, CandidateEvaluationBody body, CancellationToken cancellationToken)
    {
        var evaluation = await sender.Send(
            new AddCandidateEvaluationCommand(id, body.Criterion, body.Score, body.MaxScore, body.GuidanceContentRevisionId), cancellationToken);
        return CreatedAtAction(nameof(GetCandidate), new { id }, evaluation);
    }

    [HttpPost("candidates/{id:int}/notes/{noteId:int}/revisions")]
    [ProducesResponseType<CandidateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CandidateDetailDto>> ReviseNote(
        int id, int noteId, ReviseNoteBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ReviseCandidateEvidenceCommand(id, noteId, true,
            body.ExpectedCandidateVersion, body.ExpectedRecordVersion, body.NoteType, body.Text,
            null, null, null, body.Reason, body.GuidanceContentRevisionId), cancellationToken));

    [HttpGet("candidates/{id:int}/notes/{noteId:int}/revisions")]
    [ProducesResponseType<CandidateEvidenceHistoryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidateEvidenceHistoryDto>> GetNoteRevisions(
        int id, int noteId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCandidateEvidenceHistoryQuery(id, noteId, true), cancellationToken));

    [HttpPost("candidates/{id:int}/evaluations/{evaluationId:int}/revisions")]
    [ProducesResponseType<CandidateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CandidateDetailDto>> ReviseEvaluation(
        int id, int evaluationId, ReviseEvaluationBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ReviseCandidateEvidenceCommand(id, evaluationId, false,
            body.ExpectedCandidateVersion, body.ExpectedRecordVersion, null, null,
            body.Criterion, body.Score, body.MaxScore, body.Reason, body.GuidanceContentRevisionId), cancellationToken));

    [HttpGet("candidates/{id:int}/evaluations/{evaluationId:int}/revisions")]
    [ProducesResponseType<CandidateEvidenceHistoryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidateEvidenceHistoryDto>> GetEvaluationRevisions(
        int id, int evaluationId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCandidateEvidenceHistoryQuery(id, evaluationId, false), cancellationToken));

    [HttpPost("candidates/{id:int}/hire")]
    [ProducesResponseType<HireResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HireResultDto>> Hire(
        int id, CandidateHireBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new HireCandidateCommand(id, body.DepartmentId, body.Email, body.ContractType, body.Title, body.HireDate, body.ExpectedCandidateVersion, body.GuidanceContentRevisionId), cancellationToken));

    [HttpPost("candidates/{id:int}/provisioning/retry")]
    [ProducesResponseType<IdentityProvisioningDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IdentityProvisioningDto>> RetryProvisioning(
        int id, RetryProvisioningBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RetryCandidateProvisioningCommand(id, body.Email, body.GuidanceContentRevisionId), cancellationToken));

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

    [HttpPut("positions/{id:int}")]
    [ProducesResponseType<PositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PositionDto>> CorrectPosition(
        int id, CorrectPositionBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new CorrectPositionCommand(id, body.ExpectedVersion,
            body.Title, body.DepartmentId, body.OpenCount, body.IsOpen, body.Reason, body.GuidanceContentRevisionId),
            cancellationToken));

    [HttpGet("positions/{id:int}/corrections")]
    [ProducesResponseType<IReadOnlyList<PositionCorrectionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PositionCorrectionDto>>> GetPositionCorrections(
        int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPositionCorrectionsQuery(id), cancellationToken));

    [HttpGet("recruitment/funnel")]
    [ProducesResponseType<RecruitmentFunnelDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecruitmentFunnelDto>> GetFunnel(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetRecruitmentFunnelQuery(), cancellationToken));

    public sealed record CandidateStatusBody(string Status, int ExpectedVersion, string Reason,
        IReadOnlyList<CandidateDecisionEvidenceReference>? Evidence = null, int? GuidanceContentRevisionId = null);
    public sealed record ReviseNoteBody(int ExpectedCandidateVersion, int ExpectedRecordVersion,
        string NoteType, string Text, string Reason, int? GuidanceContentRevisionId = null);
    public sealed record ReviseEvaluationBody(int ExpectedCandidateVersion, int ExpectedRecordVersion,
        string Criterion, int Score, int MaxScore, string Reason, int? GuidanceContentRevisionId = null);
    public sealed record CorrectCandidateBody(int ExpectedVersion, string Name, string AppliedRole,
        int? PositionId, int? ExpectedPositionVersion, int? Score, string? Location,
        int ExperienceYears, string? Summary, string Reason, bool ConfirmExistingEvidenceReview, int? GuidanceContentRevisionId = null);
    public sealed record CandidateNoteBody(string NoteType, string Text, int? GuidanceContentRevisionId = null);
    public sealed record CandidateEvaluationBody(string Criterion, int Score, int MaxScore = 5, int? GuidanceContentRevisionId = null);
    public sealed record CandidateHireBody(int DepartmentId, string Email, string ContractType, string? Title = null, DateOnly? HireDate = null, int? ExpectedCandidateVersion = null, int? GuidanceContentRevisionId = null);
    public sealed record RetryProvisioningBody(string? Email = null, int? GuidanceContentRevisionId = null);
    public sealed record CorrectPositionBody(int ExpectedVersion, string Title,
        int? DepartmentId, int OpenCount, bool IsOpen, string Reason, int? GuidanceContentRevisionId = null);
}
