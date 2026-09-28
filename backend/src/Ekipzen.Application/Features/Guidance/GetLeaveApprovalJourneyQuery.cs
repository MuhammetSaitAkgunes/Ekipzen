using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

/// <summary>Gerçek izin kararları ve kişisel alıştırma ilerlemesi ayrı sayılır; karar doğruluğu ölçülmez.</summary>
public sealed record LeaveApprovalJourneyDto(
    string ProcessKey, int VerifiedDecisions, DateTime? LastVerifiedAtUtc,
    int ContentVersion, int AnsweredChecks, int CorrectChecks);

public sealed record GetLeaveApprovalJourneyQuery : IRequest<LeaveApprovalJourneyDto>;

public sealed class GetLeaveApprovalJourneyQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetLeaveApprovalJourneyQuery, LeaveApprovalJourneyDto>
{
    public async Task<LeaveApprovalJourneyDto> Handle(
        GetLeaveApprovalJourneyQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
        var decisions = context.LeaveRequests.AsNoTracking()
            .Where(item => item.DecisionByUserId == userId && item.DecisionAtUtc != null);
        var count = await decisions.CountAsync(ct);
        var latest = await decisions.OrderByDescending(item => item.DecisionAtUtc)
            .Select(item => item.DecisionAtUtc)
            .FirstOrDefaultAsync(ct);
        var checks = await context.GuidanceDecisionProgresses.AsNoTracking()
            .Where(item => item.UserId == userId &&
                item.ProcessKey == LeaveApprovalDecisionChecks.ProcessKey &&
                item.ContentVersion == LeaveApprovalDecisionChecks.ContentVersion)
            .Select(item => item.LastCorrect)
            .ToListAsync(ct);
        return new(LeaveApprovalDecisionChecks.ProcessKey, count, latest,
            LeaveApprovalDecisionChecks.ContentVersion, checks.Count, checks.Count(correct => correct));
    }
}
