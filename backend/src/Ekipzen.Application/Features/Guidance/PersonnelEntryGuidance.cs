using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

public static class PersonnelEntryGuide
{
    public const string ProcessKey = "personnel-entry";
    public const int ContentVersion = 1;
    public const string FirstStep = "tab-kimlik";

    public static bool IsStep(string? value) => value is
        "tab-kimlik" or "tab-iletisim" or "tab-is" or
        "tab-mali" or "tab-ozluk" or "tab-evrak";

    public static string RequireUserId(ICurrentUser currentUser) =>
        !string.IsNullOrWhiteSpace(currentUser.UserId)
            ? currentUser.UserId
            : throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
}

public sealed record GuidanceBookmarkDto(string ProcessKey, string StepKey, int ContentVersion);
public sealed record EmployeeCreationEvidenceDto(int EmployeeId, bool CreatedByCurrentUser, DateTime? CreatedAtUtc);

public sealed record GetPersonnelEntryBookmarkQuery : IRequest<GuidanceBookmarkDto>;

public sealed class GetPersonnelEntryBookmarkQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetPersonnelEntryBookmarkQuery, GuidanceBookmarkDto>
{
    public async Task<GuidanceBookmarkDto> Handle(GetPersonnelEntryBookmarkQuery request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(currentUser);
        var item = await context.GuidanceBookmarks.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.ProcessKey == PersonnelEntryGuide.ProcessKey, ct);

        return item is null || item.ContentVersion != PersonnelEntryGuide.ContentVersion || !PersonnelEntryGuide.IsStep(item.StepKey)
            ? new(PersonnelEntryGuide.ProcessKey, PersonnelEntryGuide.FirstStep, PersonnelEntryGuide.ContentVersion)
            : new(item.ProcessKey, item.StepKey, item.ContentVersion);
    }
}

public sealed record SavePersonnelEntryBookmarkCommand(string StepKey) : IRequest<GuidanceBookmarkDto>;

public sealed class SavePersonnelEntryBookmarkCommandValidator : AbstractValidator<SavePersonnelEntryBookmarkCommand>
{
    public SavePersonnelEntryBookmarkCommandValidator() =>
        RuleFor(x => x.StepKey).Must(PersonnelEntryGuide.IsStep)
            .WithMessage("Geçersiz rehber adımı.");
}

public sealed class SavePersonnelEntryBookmarkCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<SavePersonnelEntryBookmarkCommand, GuidanceBookmarkDto>
{
    public Task<GuidanceBookmarkDto> Handle(SavePersonnelEntryBookmarkCommand request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(currentUser);
        return databaseLock.ExecuteExclusiveAsync($"guidance:{userId}:{PersonnelEntryGuide.ProcessKey}", async lockedCt =>
        {
            var item = await context.GuidanceBookmarks.SingleOrDefaultAsync(
                x => x.UserId == userId && x.ProcessKey == PersonnelEntryGuide.ProcessKey, lockedCt);
            if (item is null)
            {
                item = new GuidanceBookmark
                {
                    UserId = userId,
                    ProcessKey = PersonnelEntryGuide.ProcessKey,
                };
                context.GuidanceBookmarks.Add(item);
            }

            item.StepKey = request.StepKey;
            item.ContentVersion = PersonnelEntryGuide.ContentVersion;
            await context.SaveChangesAsync(lockedCt);
            return new GuidanceBookmarkDto(item.ProcessKey, item.StepKey, item.ContentVersion);
        }, ct);
    }
}

public sealed record GetEmployeeCreationEvidenceQuery(int EmployeeId) : IRequest<EmployeeCreationEvidenceDto>;

public sealed class GetEmployeeCreationEvidenceQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetEmployeeCreationEvidenceQuery, EmployeeCreationEvidenceDto>
{
    public async Task<EmployeeCreationEvidenceDto> Handle(GetEmployeeCreationEvidenceQuery request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(currentUser);
        if (!await context.Employees.AnyAsync(x => x.Id == request.EmployeeId, ct))
            throw new NotFoundException("Çalışan", request.EmployeeId);

        var evidence = await context.EmployeeCreationEvidences.AsNoTracking()
            .Where(x => x.EmployeeId == request.EmployeeId && x.UserId == userId)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .SingleOrDefaultAsync(ct);

        return new(request.EmployeeId, evidence.HasValue, evidence);
    }
}
