using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

public static class GuidancePublishedSteps
{
    public static readonly IReadOnlyDictionary<string, string[]> Steps = new Dictionary<string, string[]>
    {
        ["personnel-entry"] = ["tab-kimlik", "tab-iletisim", "tab-is", "tab-mali", "tab-ozluk", "tab-evrak"],
        ["recruitment"] = ["candidate-create", "first-review"],
        ["leave"] = ["review-request", "decision-effect"],
        ["company"] = ["initial-setup", "company-profile", "departments", "access-preparation"],
        ["personnel"] = ["record-review", "import-review", "rehire"],
        ["attendance"] = ["entry-review", "entry-correction", "period-summary"],
        ["payroll"] = ["settings-review", "period-create", "row-review", "approval", "payslip-review", "correction-external"],
        ["compliance"] = ["record-review", "status-review", "record-correction"],
        ["offboarding"] = ["preparation", "settlement-review", "completion", "reversal-review", "correction-external"],
    };
    private static readonly IReadOnlyDictionary<string, string[]> ManagerSteps = new Dictionary<string, string[]>
    {
        ["leave"] = ["review-request", "decision-effect"],
        ["personnel"] = ["record-review"],
        ["attendance"] = ["entry-review", "entry-correction", "period-summary"],
        ["compliance"] = ["record-review"],
    };
    public static void RequireAccess(string process, string step, ICurrentUser user)
    {
        if (!Steps.TryGetValue(process, out var steps) || !steps.Contains(step))
            throw new NotFoundException("Rehber alt adımı", step);
        if (!user.Roles.Contains(Roles.HrAdmin) && !(user.Roles.Contains(Roles.Manager)
            && ManagerSteps.TryGetValue(process, out var managerSteps) && managerSteps.Contains(step)))
            throw new ForbiddenAccessException("Bu alıştırma ilgili süreci yöneten role açıktır.");
    }
    public static void ValidateBinding(string key, string process, GuidanceContentBody body)
    {
        if (body.StepKey is null) return;
        if (!Steps.TryGetValue(process, out var steps) || !steps.Contains(body.StepKey) || key != $"{process}-{body.StepKey}")
            throw new ConflictException("Alt adım süreci ve sabit içerik kimliği uyuşmuyor.");
    }
}
public sealed class GuidancePracticeDefinitionValidator : AbstractValidator<GuidancePracticeDefinition>
{
    public GuidancePracticeDefinitionValidator(bool complete = false)
    {
        RuleFor(x => x.Question).MaximumLength(2000);
        RuleFor(x => x.Explanation).MaximumLength(4000);
        RuleFor(x => x.Choices).NotNull().Must(x => x is not null && x.Length is >= 2 and <= 5);
        RuleForEach(x => x.Choices).MaximumLength(1000);
        RuleFor(x => x.CorrectChoice).Must((body, value) => body.Choices is not null && value >= 0 && value < body.Choices.Length);
        if (!complete) return;
        RuleFor(x => x.Question).NotEmpty(); RuleFor(x => x.Explanation).NotEmpty();
        RuleForEach(x => x.Choices).NotEmpty();
        RuleFor(x => x.Choices).Must(x => x is not null && x.Select(c => c?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == x.Length);
    }
}
public sealed record GuidancePublishedPracticeDto(int ContentRevisionId, string ContentKey, string ProcessKey,
    string StepKey, int ContentVersion, string Title, bool IsCurrent, string Question, string[] Choices,
    int AttemptCount, int? LastSelectedChoice, bool? LastCorrect, string? Explanation);
public sealed record GuidancePublishedPracticeProgressDto(int ContentRevisionId, string ContentKey, string ProcessKey,
    string StepKey, int ContentVersion, string Title, int AttemptCount, int LastSelectedChoice,
    bool LastCorrect, DateTime LastAnsweredAtUtc, bool IsCurrent);

public sealed record GetGuidancePublishedPracticeQuery(int? VersionId = null, string? ProcessKey = null, string? StepKey = null)
    : IRequest<GuidancePublishedPracticeDto?>;
public sealed class GetGuidancePublishedPracticeQueryHandler(IApplicationDbContext db, IPlatformDbContext platform,
    ICurrentUser user, ISender sender) : IRequestHandler<GetGuidancePublishedPracticeQuery, GuidancePublishedPracticeDto?>
{
    public async Task<GuidancePublishedPracticeDto?> Handle(GetGuidancePublishedPracticeQuery request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(user);
        GuidanceContentDto? content;
        if (request.VersionId is not null)
            content = (await sender.Send(new GetGuidanceCatalogQuery(VersionId: request.VersionId), ct)).Single();
        else
        {
            GuidancePublishedSteps.RequireAccess(request.ProcessKey!, request.StepKey!, user);
            content = (await sender.Send(new GetGuidanceCatalogQuery(request.ProcessKey), ct))
                .SingleOrDefault(x => x.ContentKey == $"{request.ProcessKey}-{request.StepKey}" && x.Body.StepKey == request.StepKey);
            if (content is null) return null;
        }
        GuidancePublishedSteps.RequireAccess(content.ProcessKey, content.Body.StepKey ?? "", user);
        var raw = await platform.GuidanceContentRevisions.AsNoTracking().SingleAsync(x => x.Id == content.Id, ct);
        var practice = GuidanceContentCatalog.Body(raw).Practice;
        if (practice is null)
        {
            if (request.VersionId is null) return null;
            throw new NotFoundException("Sürümlü alıştırma", content.Id);
        }
        var progress = await db.GuidancePublishedPracticeProgresses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.ContentRevisionId == content.Id, ct);
        return ToDto(content, practice, progress);
    }
    public static GuidancePublishedPracticeDto ToDto(GuidanceContentDto content, GuidancePracticeDefinition practice,
        GuidancePublishedPracticeProgress? progress) => new(content.Id, content.ContentKey, content.ProcessKey,
        content.Body.StepKey!, content.ContentVersion, content.Body.Title, content.IsCurrent, practice.Question,
        practice.Choices, progress?.AttemptCount ?? 0, progress?.LastSelectedChoice, progress?.LastCorrect,
        progress is null ? null : practice.Explanation);
}
public sealed record SubmitGuidancePublishedPracticeCommand(int ContentRevisionId, int SelectedChoice,
    int ExpectedAttemptCount, Guid AttemptId) : IRequest<GuidancePublishedPracticeDto>;
public sealed class SubmitGuidancePublishedPracticeCommandValidator : AbstractValidator<SubmitGuidancePublishedPracticeCommand>
{
    public SubmitGuidancePublishedPracticeCommandValidator()
    {
        RuleFor(x => x.ContentRevisionId).GreaterThan(0);
        RuleFor(x => x.SelectedChoice).InclusiveBetween(0, 4);
        RuleFor(x => x.ExpectedAttemptCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AttemptId).NotEqual(Guid.Empty);
    }
}
public sealed class SubmitGuidancePublishedPracticeCommandHandler(IApplicationDbContext db, IPlatformDbContext platform,
    ICurrentUser user, IDatabaseLock tenantGate, IGuidanceCatalogLock catalogGate, ISender sender)
    : IRequestHandler<SubmitGuidancePublishedPracticeCommand, GuidancePublishedPracticeDto>
{
    public async Task<GuidancePublishedPracticeDto> Handle(SubmitGuidancePublishedPracticeCommand request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(user);
        var key = await platform.GuidanceContentRevisions.AsNoTracking().Where(x => x.Id == request.ContentRevisionId)
            .Select(x => x.ContentKey).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Alıştırma sürümü", request.ContentRevisionId);
        // Yayın/kaldırma kilidi önce, kişisel kiracı kilidi sonra: iki veritabanında tutarlı kilit sırası.
        return await catalogGate.ExecuteAsync(key, async catalogCt =>
        {
            var content = (await sender.Send(new GetGuidanceCatalogQuery(VersionId: request.ContentRevisionId), catalogCt)).Single();
            GuidancePublishedSteps.RequireAccess(content.ProcessKey, content.Body.StepKey ?? "", user);
            if (!content.IsCurrent) throw new ConflictException("Alıştırma sürümü güncel değil. Yanıtınız korunur; güncel sürümü açıkça seçin.");
            var raw = await platform.GuidanceContentRevisions.AsNoTracking().SingleAsync(x => x.Id == content.Id, catalogCt);
            var practice = GuidanceContentCatalog.Body(raw).Practice ?? throw new NotFoundException("Alıştırma", content.Id);
            if (request.SelectedChoice >= practice.Choices.Length) throw new ConflictException("Seçenek bu sürümde bulunamadı.");
            return await tenantGate.ExecuteExclusiveAsync($"guidance-practice:{userId}:{content.Id}", async lockedCt =>
            {
                var item = await db.GuidancePublishedPracticeProgresses.SingleOrDefaultAsync(
                    x => x.UserId == userId && x.ContentRevisionId == content.Id, lockedCt);
                if (item?.LastAttemptId == request.AttemptId)
                {
                    if (item.LastSelectedChoice != request.SelectedChoice || item.AttemptCount != request.ExpectedAttemptCount + 1)
                        throw new ConflictException("Aynı deneme kimliği farklı bir yanıt için kullanılamaz.");
                    return GetGuidancePublishedPracticeQueryHandler.ToDto(content, practice, item);
                }
                if ((item?.AttemptCount ?? 0) != request.ExpectedAttemptCount)
                    throw new ConflictException("Alıştırma başka oturumda yanıtlanmış. Kaydı yeniden yükleyin.");
                if (item is null)
                {
                    item = new GuidancePublishedPracticeProgress { UserId = userId, ContentRevisionId = content.Id,
                        ContentKey = content.ContentKey, ProcessKey = content.ProcessKey, StepKey = content.Body.StepKey!,
                        ContentVersion = content.ContentVersion, Title = content.Body.Title };
                    db.GuidancePublishedPracticeProgresses.Add(item);
                }
                item.AttemptCount++; item.LastSelectedChoice = request.SelectedChoice;
                item.LastCorrect = request.SelectedChoice == practice.CorrectChoice;
                item.LastAttemptId = request.AttemptId; item.LastAnsweredAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(lockedCt);
                return GetGuidancePublishedPracticeQueryHandler.ToDto(content, practice, item);
            }, catalogCt);
        }, ct);
    }
}
public sealed record GetMyGuidancePublishedPracticesQuery : IRequest<IReadOnlyList<GuidancePublishedPracticeProgressDto>>;
public sealed class GetMyGuidancePublishedPracticesQueryHandler(IApplicationDbContext db, ICurrentUser user, ISender sender)
    : IRequestHandler<GetMyGuidancePublishedPracticesQuery, IReadOnlyList<GuidancePublishedPracticeProgressDto>>
{
    public async Task<IReadOnlyList<GuidancePublishedPracticeProgressDto>> Handle(GetMyGuidancePublishedPracticesQuery request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(user);
        var current = (await sender.Send(new GetGuidanceCatalogQuery(), ct)).Select(x => x.Id).ToHashSet();
        var items = await db.GuidancePublishedPracticeProgresses.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastAnsweredAtUtc).ToListAsync(ct);
        return items.Select(x => new GuidancePublishedPracticeProgressDto(x.ContentRevisionId, x.ContentKey,
            x.ProcessKey, x.StepKey, x.ContentVersion, x.Title, x.AttemptCount, x.LastSelectedChoice, x.LastCorrect,
            DateTime.SpecifyKind(x.LastAnsweredAtUtc, DateTimeKind.Utc), current.Contains(x.ContentRevisionId))).ToList();
    }
}
