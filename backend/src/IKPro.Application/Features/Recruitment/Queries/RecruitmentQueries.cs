using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Recruitment.Queries;

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
                .Select(n => new InterviewNoteDto(n.Id, n.AuthorName, n.NoteType, n.Text, n.CreatedAtUtc))
                .ToList(),
            candidate.Evaluations
                .Select(e => new CandidateEvaluationDto(e.Id, e.Criterion, e.Score, e.MaxScore))
                .ToList(),
            candidate.History
                .OrderByDescending(h => h.OccurredAtUtc)
                .Select(h => new CandidateHistoryDto(h.Id, h.Event, h.OccurredAtUtc))
                .ToList());
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
                p.Id, p.Title, p.DepartmentId, p.Department!.Name, p.IsOpen, p.OpenCount, p.Candidates.Count))
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
