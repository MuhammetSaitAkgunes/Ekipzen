using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Recruitment.Commands;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ekipzen.Application.Features.Recruitment.Queries;

// --- aday havuzu ---

public sealed record GetCandidatesQuery(string? Search = null, string? Status = null)
    : IRequest<IReadOnlyList<CandidateListItemDto>>;

public sealed class GetCandidatesQueryValidator : AbstractValidator<GetCandidatesQuery>
{
    public GetCandidatesQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || RecruitmentMappings.IsStatus(s))
            .WithMessage("Aday durumu filtresi: Yeni | Mülakat | Teklif | Red | İşe Alındı.");
    }
}

public sealed class GetCandidatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCandidatesQuery, IReadOnlyList<CandidateListItemDto>>
{
    public async Task<IReadOnlyList<CandidateListItemDto>> Handle(
        GetCandidatesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Candidates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.AppliedRole.Contains(term));
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            var status = RecruitmentMappings.ParseStatus(request.Status);
            query = query.Where(c => c.Status == status);
        }

        var rows = await query
            .OrderByDescending(c => c.AppliedAtUtc)
            .Select(c => new { c.Id, c.Name, c.AppliedRole, c.Status, c.Score, c.AppliedAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .Select(c => new CandidateListItemDto(
                c.Id, c.Name, c.AppliedRole, c.Status.ToDto(), c.Score,
                RecruitmentMappings.InitialsOf(c.Name), c.AppliedAtUtc))
            .ToList();
    }
}

// --- aday detayı ---

public sealed record GetCandidateQuery(int Id) : IRequest<CandidateDetailDto>;

public sealed class GetCandidateQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCandidateQuery, CandidateDetailDto>
{
    public async Task<CandidateDetailDto> Handle(GetCandidateQuery request, CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates
            .AsNoTracking()
            .Include(c => c.Position)
            .Include(c => c.Skills)
            .Include(c => c.Experiences)
            .Include(c => c.Notes)
            .Include(c => c.Evaluations)
            .Include(c => c.History)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Aday", request.Id);

        var provisioning = await context.IdentityProvisioningOperations
            .AsNoTracking()
            .Where(operation => operation.CandidateId == candidate.Id
                && operation.Type == IdentityProvisioningOperationType.EmployeeLogin)
            .OrderByDescending(operation => operation.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var revisions = await context.CandidateEvidenceRevisions.AsNoTracking()
            .Where(item => item.CandidateId == candidate.Id).ToListAsync(cancellationToken);
        var noteVersions = revisions.Where(item => item.InterviewNoteId != null)
            .GroupBy(item => item.InterviewNoteId!.Value)
            .ToDictionary(group => group.Key, group => group.MaxBy(item => item.Version)!);
        var evaluationVersions = revisions.Where(item => item.EvaluationId != null)
            .GroupBy(item => item.EvaluationId!.Value)
            .ToDictionary(group => group.Key, group => group.MaxBy(item => item.Version)!);
        var decisions = await context.CandidateStageDecisions.AsNoTracking()
            .Where(item => item.CandidateId == candidate.Id)
            .OrderByDescending(item => item.ToVersion).ToListAsync(cancellationToken);

        return new CandidateDetailDto(
            candidate.Id,
            candidate.Name,
            candidate.AppliedRole,
            candidate.PositionId,
            candidate.Position?.Title,
            candidate.Status.ToDto(),
            candidate.Score,
            RecruitmentMappings.InitialsOf(candidate.Name),
            candidate.AppliedAtUtc,
            candidate.Location,
            candidate.ExperienceYears,
            candidate.Summary,
            candidate.Skills.Select(s => new CandidateSkillDto(s.Id, s.Name)).ToList(),
            candidate.Experiences
                .Select(x => new CandidateExperienceDto(x.Id, x.Title, x.Company, x.Period, x.Description))
                .ToList(),
            candidate.Notes
                .OrderByDescending(n => n.CreatedAtUtc)
                .Select(n =>
                {
                    var revision = noteVersions.GetValueOrDefault(n.Id);
                    var state = revision is null ? null : CandidateEvidenceVersions.Read(revision.AfterJson);
                    return new InterviewNoteDto(n.Id, n.AuthorName, state?.NoteType ?? n.NoteType,
                        state?.Text ?? n.Text, n.CreatedAtUtc, revision?.Version ?? 1);
                })
                .ToList(),
            candidate.Evaluations
                .Select(e =>
                {
                    var revision = evaluationVersions.GetValueOrDefault(e.Id);
                    var state = revision is null ? null : CandidateEvidenceVersions.Read(revision.AfterJson);
                    return new CandidateEvaluationDto(e.Id, state?.Criterion ?? e.Criterion,
                        state?.Score ?? e.Score, state?.MaxScore ?? e.MaxScore, revision?.Version ?? 1);
                })
                .ToList(),
            candidate.History
                .OrderByDescending(h => h.OccurredAtUtc)
                .Select(h => new CandidateHistoryDto(h.Id, h.Event, h.OccurredAtUtc))
                .ToList(),
            provisioning?.ToDto(), candidate.Version,
            decisions.Select(item => new CandidateStageDecisionDto(item.Id,
                item.FromStatus.ToDto(), item.ToStatus.ToDto(), item.FromVersion, item.ToVersion,
                item.Reason, JsonSerializer.Deserialize<List<CandidateDecisionEvidenceDto>>(item.EvidenceJson)!,
                item.DecidedByUserId, item.DecidedByName, item.DecidedAtUtc)).ToList());
    }
}

// --- pozisyonlar ---

public sealed record GetPositionsQuery : IRequest<IReadOnlyList<PositionDto>>;

public sealed class GetPositionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPositionsQuery, IReadOnlyList<PositionDto>>
{
    public async Task<IReadOnlyList<PositionDto>> Handle(
        GetPositionsQuery request, CancellationToken cancellationToken)
        => await context.Positions
            .AsNoTracking()
            .OrderBy(p => p.Title)
            .Select(p => new PositionDto(
                p.Id, p.Title, p.DepartmentId, p.Department!.Name, p.IsOpen, p.OpenCount, p.Candidates.Count,
                p.Version))
            .ToListAsync(cancellationToken);
}

// --- funnel ---

public sealed record GetRecruitmentFunnelQuery : IRequest<RecruitmentFunnelDto>;

public sealed class GetRecruitmentFunnelQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetRecruitmentFunnelQuery, RecruitmentFunnelDto>
{
    public async Task<RecruitmentFunnelDto> Handle(
        GetRecruitmentFunnelQuery request, CancellationToken cancellationToken)
    {
        var counts = await context.Candidates
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Of(CandidateStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        return new RecruitmentFunnelDto(
            counts.Sum(c => c.Count),
            Of(CandidateStatus.New),
            Of(CandidateStatus.Interview),
            Of(CandidateStatus.Offer),
            Of(CandidateStatus.Rejected),
            Of(CandidateStatus.Hired));
    }
}
