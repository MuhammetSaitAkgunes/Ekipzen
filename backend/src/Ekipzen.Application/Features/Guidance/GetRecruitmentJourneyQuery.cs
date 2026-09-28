using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

/// <summary>İlk aday aşama kararının kullanıcıya ait işlem kanıtı; yeterlilik düzeyi değildir.</summary>
public sealed record RecruitmentJourneyDto(
    string ProcessKey,
    int VerifiedFirstReviews,
    DateTime? LastVerifiedAtUtc);

public sealed record GetRecruitmentJourneyQuery : IRequest<RecruitmentJourneyDto>;

public sealed class GetRecruitmentJourneyQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetRecruitmentJourneyQuery, RecruitmentJourneyDto>
{
    public async Task<RecruitmentJourneyDto> Handle(GetRecruitmentJourneyQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
        var evidence = context.CandidateReviewEvidences.AsNoTracking()
            .Where(item => item.UserId == userId);
        var count = await evidence.CountAsync(ct);
        var latest = await evidence.OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => (DateTime?)item.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        return new("recruitment", count, latest);
    }
}
