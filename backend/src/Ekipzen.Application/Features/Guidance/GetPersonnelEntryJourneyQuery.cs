using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

/// <summary>
/// Kullanıcının ilk rehber sürecine ait görünür kanıtı. Bir yeterlilik seviyesi
/// veya işe giriş sürecinin tamamlandığı hükmü değildir.
/// </summary>
public sealed record PersonnelEntryJourneyDto(
    string ProcessKey,
    int ContentVersion,
    bool HasSavedPosition,
    string BookmarkStepKey,
    int VerifiedEmployeeRecords,
    DateTime? LastVerifiedAtUtc);

public sealed record GetPersonnelEntryJourneyQuery : IRequest<PersonnelEntryJourneyDto>;

public sealed class GetPersonnelEntryJourneyQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetPersonnelEntryJourneyQuery, PersonnelEntryJourneyDto>
{
    public async Task<PersonnelEntryJourneyDto> Handle(
        GetPersonnelEntryJourneyQuery request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(currentUser);
        var bookmark = await context.GuidanceBookmarks.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId &&
                item.ProcessKey == PersonnelEntryGuide.ProcessKey, ct);
        var validBookmark = bookmark is not null &&
            bookmark.ContentVersion == PersonnelEntryGuide.ContentVersion &&
            PersonnelEntryGuide.IsStep(bookmark.StepKey);
        var evidence = context.EmployeeCreationEvidences.AsNoTracking()
            .Where(item => item.UserId == userId);
        var count = await evidence.CountAsync(ct);
        var latest = await evidence
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => (DateTime?)item.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return new(
            PersonnelEntryGuide.ProcessKey,
            PersonnelEntryGuide.ContentVersion,
            validBookmark,
            validBookmark ? bookmark!.StepKey : PersonnelEntryGuide.FirstStep,
            count,
            latest);
    }
}
