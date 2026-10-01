using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

public sealed record GuidanceContentReadingDto(
    int ContentRevisionId, string ContentKey, string ProcessKey, int ContentVersion,
    string Title, DateTime FirstReadAtUtc, bool IsCurrent);
public sealed record GetMyGuidanceContentReadingsQuery : IRequest<IReadOnlyList<GuidanceContentReadingDto>>;
public sealed class GetMyGuidanceContentReadingsQueryHandler(IApplicationDbContext db, ICurrentUser user, ISender sender)
    : IRequestHandler<GetMyGuidanceContentReadingsQuery, IReadOnlyList<GuidanceContentReadingDto>>
{
    public async Task<IReadOnlyList<GuidanceContentReadingDto>> Handle(GetMyGuidanceContentReadingsQuery request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(user);
        var current = (await sender.Send(new GetGuidanceCatalogQuery(), ct)).Select(x => x.Id).ToHashSet();
        var records = await db.GuidanceContentReadings.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.FirstReadAtUtc).ToListAsync(ct);
        return records.Select(x => ToDto(x, current.Contains(x.ContentRevisionId))).ToList();
    }
    public static GuidanceContentReadingDto ToDto(GuidanceContentReading item, bool current) => new(
        item.ContentRevisionId, item.ContentKey, item.ProcessKey, item.ContentVersion, item.Title,
        DateTime.SpecifyKind(item.FirstReadAtUtc, DateTimeKind.Utc), current);
}
public sealed record RecordGuidanceContentReadingCommand(int ContentRevisionId, bool Confirmed) : IRequest<GuidanceContentReadingDto>;
public sealed class RecordGuidanceContentReadingCommandValidator : AbstractValidator<RecordGuidanceContentReadingCommand>
{
    public RecordGuidanceContentReadingCommandValidator()
    {
        RuleFor(x => x.ContentRevisionId).GreaterThan(0);
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("İçeriği incelediğinizi açıkça doğrulayın.");
    }
}
public sealed class RecordGuidanceContentReadingCommandHandler(
    IApplicationDbContext db, IPlatformDbContext platform, ICurrentUser user,
    IDatabaseLock tenantGate, IGuidanceCatalogLock catalogGate, ISender sender)
    : IRequestHandler<RecordGuidanceContentReadingCommand, GuidanceContentReadingDto>
{
    public async Task<GuidanceContentReadingDto> Handle(RecordGuidanceContentReadingCommand request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(user);
        // Yayın/kaldırma ile yarışmayı aynı merkezî anahtar kilidi önler. Okur rolü sunucuda doğrulanır.
        var key = await platform.GuidanceContentRevisions.AsNoTracking().Where(x => x.Id == request.ContentRevisionId)
            .Select(x => x.ContentKey).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Rehber sürümü", request.ContentRevisionId);
        return await catalogGate.ExecuteAsync(key, async catalogCt =>
        {
            var content = (await sender.Send(new GetGuidanceCatalogQuery(VersionId: request.ContentRevisionId), catalogCt)).Single();
            if (!content.IsCurrent) throw new ConflictException("Bu sürüm güncel değil; yeni okuma kaydı için güncel içeriği açın.");
            return await tenantGate.ExecuteExclusiveAsync($"guidance-reading:{userId}:{content.Id}", async lockedCt =>
            {
                var item = await db.GuidanceContentReadings.SingleOrDefaultAsync(
                    x => x.UserId == userId && x.ContentRevisionId == content.Id, lockedCt);
                if (item is null)
                {
                    item = new GuidanceContentReading
                    {
                        UserId = userId, ContentRevisionId = content.Id, ContentKey = content.ContentKey,
                        ProcessKey = content.ProcessKey, ContentVersion = content.ContentVersion,
                        Title = content.Body.Title, FirstReadAtUtc = DateTime.UtcNow,
                    };
                    db.GuidanceContentReadings.Add(item);
                    await db.SaveChangesAsync(lockedCt);
                }
                return GetMyGuidanceContentReadingsQueryHandler.ToDto(item, true);
            }, catalogCt);
        }, ct);
    }
}
