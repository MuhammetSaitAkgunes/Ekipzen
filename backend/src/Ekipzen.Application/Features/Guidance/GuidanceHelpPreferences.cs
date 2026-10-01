using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

public sealed record GuidanceHelpPreferenceDto(string ProcessKey, string Mode, int Revision);
public sealed record GetGuidanceHelpPreferencesQuery : IRequest<IReadOnlyList<GuidanceHelpPreferenceDto>>;
public sealed class GetGuidanceHelpPreferencesQueryHandler(IApplicationDbContext db, ICurrentUser user)
    : IRequestHandler<GetGuidanceHelpPreferencesQuery, IReadOnlyList<GuidanceHelpPreferenceDto>>
{
    public async Task<IReadOnlyList<GuidanceHelpPreferenceDto>> Handle(GetGuidanceHelpPreferencesQuery request, CancellationToken ct)
    {
        var id = PersonnelEntryGuide.RequireUserId(user);
        return await db.GuidanceHelpPreferences.AsNoTracking().Where(x => x.UserId == id)
            .OrderBy(x => x.ProcessKey).Select(x => new GuidanceHelpPreferenceDto(x.ProcessKey, x.Mode, x.Revision)).ToListAsync(ct);
    }
}
public sealed record SaveGuidanceHelpPreferenceCommand(string ProcessKey, string Mode, int? ExpectedRevision)
    : IRequest<GuidanceHelpPreferenceDto>;
public sealed class SaveGuidanceHelpPreferenceCommandValidator : AbstractValidator<SaveGuidanceHelpPreferenceCommand>
{
    public SaveGuidanceHelpPreferenceCommandValidator()
    {
        RuleFor(x => x.ProcessKey).Must(x => GuidanceContentCatalog.Processes.Contains(x));
        RuleFor(x => x.Mode).Must(x => x is "detailed" or "compact");
        RuleFor(x => x.ExpectedRevision).NotNull().GreaterThanOrEqualTo(0).LessThan(int.MaxValue);
    }
}
public sealed class SaveGuidanceHelpPreferenceCommandHandler(IApplicationDbContext db, ICurrentUser user, IDatabaseLock gate)
    : IRequestHandler<SaveGuidanceHelpPreferenceCommand, GuidanceHelpPreferenceDto>
{
    public Task<GuidanceHelpPreferenceDto> Handle(SaveGuidanceHelpPreferenceCommand request, CancellationToken ct)
    {
        var id = PersonnelEntryGuide.RequireUserId(user);
        var expectedRevision = request.ExpectedRevision!.Value;
        return gate.ExecuteExclusiveAsync($"guidance-help:{id}:{request.ProcessKey}", async lockedCt =>
        {
            var item = await db.GuidanceHelpPreferences.SingleOrDefaultAsync(x => x.UserId == id && x.ProcessKey == request.ProcessKey, lockedCt);
            var revision = item?.Revision ?? 0;
            if (revision != expectedRevision)
            {
                if (item is not null && revision == expectedRevision + 1 && item.Mode == request.Mode)
                    return new GuidanceHelpPreferenceDto(item.ProcessKey, item.Mode, item.Revision);
                throw new ConflictException("Yardım tercihi değişmiş. Güncel tercihi okuyup yeniden seçin.");
            }
            if ((item?.Mode ?? "detailed") == request.Mode)
                return new GuidanceHelpPreferenceDto(request.ProcessKey, request.Mode, revision);
            if (item is null)
            {
                item = new GuidanceHelpPreference { UserId = id, ProcessKey = request.ProcessKey };
                db.GuidanceHelpPreferences.Add(item);
            }
            item.Mode = request.Mode; item.Revision = revision + 1;
            await db.SaveChangesAsync(lockedCt);
            return new GuidanceHelpPreferenceDto(item.ProcessKey, item.Mode, item.Revision);
        }, ct);
    }
}
