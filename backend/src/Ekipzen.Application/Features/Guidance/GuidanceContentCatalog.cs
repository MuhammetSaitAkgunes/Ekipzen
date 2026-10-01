using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

public sealed record GuidanceContentBody(
    string Title, string[] Audience, string LearningGoal, string Prerequisites,
    string Action, string Why, string Example, string CounterExample, string Terms,
    string Outcome, string CompletionEvidence, string NextStep, string Correction,
    string ExpertEscalation, string SourceKind, string SourceReference, string Provision,
    string Applicability, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record GuidanceExpertReview(
    string Name, string Role, string Reference, DateOnly ReviewedOn, DateOnly ReviewDueOn, bool Confirmed);
public sealed record GuidanceContributor(string UserId, string Name);
public sealed record GuidanceContentDto(
    int Id, string ContentKey, string ProcessKey, int ContentVersion, string State,
    GuidanceContentBody Body, string? ExpertName, string? ExpertRole, DateOnly? ReviewedOn,
    DateOnly? ReviewDueOn, DateTime? PublishedAtUtc, bool IsCurrent);
public sealed record GuidanceContentEventDto(
    int Revision, string Action, string ActorName, string? Reason, DateTime AtUtc);
public sealed record GuidanceEditorialDto(
    GuidanceContentDto Content, int Revision, int? BasedOnId, IReadOnlyList<GuidanceContributor> Contributors,
    string? ExpertReference, bool CanPublish, IReadOnlyList<GuidanceContentEventDto> Events);
public sealed record GuidanceEditorialAccessDto(bool CanManage);

public static class GuidanceContentCatalog
{
    public static readonly string[] Processes = ["company", "recruitment", "personnel-entry", "personnel",
        "leave", "attendance", "payroll", "compliance", "offboarding", "daily"];
    public static DateOnly Today => LocalDate(DateTime.UtcNow);
    public static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")));
    public static GuidanceContentBody Body(GuidanceContentRevision item) =>
        JsonSerializer.Deserialize<GuidanceContentBody>(item.BodyJson)!;
    public static List<GuidanceContributor> Contributors(GuidanceContentRevision item) =>
        JsonSerializer.Deserialize<List<GuidanceContributor>>(item.ContributorsJson)!;
    public static bool IsUsable(GuidanceContentRevision item, DateOnly today) =>
        item.State == "published" && item.EffectiveFrom <= today
        && (item.EffectiveTo is null || item.EffectiveTo >= today) && item.ReviewDueOn >= today;
    public static GuidanceContentDto ToDto(GuidanceContentRevision item, bool current) => new(
        item.Id, item.ContentKey, item.ProcessKey, item.ContentVersion, item.State, Body(item),
        item.ExpertName, item.ExpertRole, item.ReviewedOn, item.ReviewDueOn,
        item.PublishedAtUtc is null ? null : DateTime.SpecifyKind(item.PublishedAtUtc.Value, DateTimeKind.Utc), current);
    public static GuidanceEditorialDto ToEditorial(GuidanceContentRevision item, string userId, bool? current = null) => new(
        ToDto(item, current ?? IsUsable(item, Today)), item.Revision, item.BasedOnId, Contributors(item),
        item.ExpertReference, item.State == "in-review" && !Contributors(item).Any(x => x.UserId == userId),
        item.Events.OrderByDescending(x => x.Revision).Select(x => new GuidanceContentEventDto(
            x.Revision, x.Action, x.ActorName, x.Reason, DateTime.SpecifyKind(x.AtUtc, DateTimeKind.Utc))).ToList());
    public static string RequireEditor(IGuidanceEditorialAccess access, ICurrentUser user)
    {
        if (!access.CanManage) throw new ForbiddenAccessException("İçerik yönetimi yalnız Ekipzen yayın ekibine açıktır.");
        return PersonnelEntryGuide.RequireUserId(user);
    }
    public static void ApplyBody(GuidanceContentRevision item, GuidanceContentBody body, ICurrentUser user)
    {
        item.BodyJson = JsonSerializer.Serialize(body);
        item.EffectiveFrom = body.EffectiveFrom;
        item.EffectiveTo = body.EffectiveTo;
        item.LastEditedAtUtc = DateTime.UtcNow;
        var contributors = Contributors(item);
        if (!contributors.Any(x => x.UserId == user.UserId))
            contributors.Add(new(user.UserId!, user.UserName ?? "Ekipzen yayın ekibi"));
        item.ContributorsJson = JsonSerializer.Serialize(contributors);
    }
    public static void Event(GuidanceContentRevision item, string action, ICurrentUser user, string? reason = null) =>
        item.Events.Add(new GuidanceContentEvent
        {
            Revision = item.Revision, Action = action, ActorUserId = user.UserId!,
            ActorName = user.UserName ?? "Ekipzen yayın ekibi", Reason = reason?.Trim(), AtUtc = DateTime.UtcNow,
        });
}

public sealed class GuidanceContentBodyValidator : AbstractValidator<GuidanceContentBody>
{
    public GuidanceContentBodyValidator(bool complete = false)
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Audience).NotEmpty().Must(x => x is not null && x.Length <= 3 && x.Distinct().Count() == x.Length);
        RuleForEach(x => x.Audience).Must(x => Roles.All.Contains(x));
        RuleFor(x => x.SourceKind).Must(x => x is "legislation" or "company-policy" or "good-practice");
        RuleFor(x => x.EffectiveFrom).GreaterThanOrEqualTo(new DateOnly(2000, 1, 1));
        RuleFor(x => x.EffectiveTo).Must((body, value) => value is null || value >= body.EffectiveFrom);
        RuleFor(x => x.LearningGoal).MaximumLength(2000);
        RuleFor(x => x.Prerequisites).MaximumLength(4000);
        RuleFor(x => x.Action).MaximumLength(4000);
        RuleFor(x => x.Why).MaximumLength(4000);
        RuleFor(x => x.Example).MaximumLength(4000);
        RuleFor(x => x.CounterExample).MaximumLength(4000);
        RuleFor(x => x.Terms).MaximumLength(4000);
        RuleFor(x => x.Outcome).MaximumLength(4000);
        RuleFor(x => x.CompletionEvidence).MaximumLength(4000);
        RuleFor(x => x.NextStep).MaximumLength(4000);
        RuleFor(x => x.Correction).MaximumLength(4000);
        RuleFor(x => x.ExpertEscalation).MaximumLength(4000);
        RuleFor(x => x.SourceReference).MaximumLength(2000);
        RuleFor(x => x.Provision).MaximumLength(2000);
        RuleFor(x => x.Applicability).MaximumLength(4000);
        if (!complete) return;
        RuleFor(x => x.LearningGoal).NotEmpty(); RuleFor(x => x.Prerequisites).NotEmpty();
        RuleFor(x => x.Action).NotEmpty(); RuleFor(x => x.Why).NotEmpty();
        RuleFor(x => x.Example).NotEmpty(); RuleFor(x => x.CounterExample).NotEmpty();
        RuleFor(x => x.Outcome).NotEmpty(); RuleFor(x => x.CompletionEvidence).NotEmpty();
        RuleFor(x => x.NextStep).NotEmpty(); RuleFor(x => x.Correction).NotEmpty();
        RuleFor(x => x.ExpertEscalation).NotEmpty(); RuleFor(x => x.SourceReference).NotEmpty();
        RuleFor(x => x.Provision).NotEmpty(); RuleFor(x => x.Applicability).NotEmpty();
        When(x => x.SourceKind == "legislation", () => RuleFor(x => x.SourceReference).Must(value =>
            Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == "https"
            && (url.Host.EndsWith(".gov.tr", StringComparison.OrdinalIgnoreCase) || url.Host == "gov.tr"))
            .WithMessage("Mevzuat dayanağı için HTTPS üzerinden resmî .gov.tr kaynak bağlantısı gerekli."));
    }
}

public sealed record GetGuidanceEditorialAccessQuery : IRequest<GuidanceEditorialAccessDto>;
public sealed class GetGuidanceEditorialAccessQueryHandler(IGuidanceEditorialAccess access)
    : IRequestHandler<GetGuidanceEditorialAccessQuery, GuidanceEditorialAccessDto>
{
    public Task<GuidanceEditorialAccessDto> Handle(GetGuidanceEditorialAccessQuery request, CancellationToken ct) =>
        Task.FromResult(new GuidanceEditorialAccessDto(access.CanManage));
}
public sealed record GetGuidanceCatalogQuery(string? ProcessKey = null, int? VersionId = null)
    : IRequest<IReadOnlyList<GuidanceContentDto>>;
public sealed class GetGuidanceCatalogQueryHandler(IPlatformDbContext db, ICurrentUser user)
    : IRequestHandler<GetGuidanceCatalogQuery, IReadOnlyList<GuidanceContentDto>>
{
    public async Task<IReadOnlyList<GuidanceContentDto>> Handle(GetGuidanceCatalogQuery request, CancellationToken ct)
    {
        var today = GuidanceContentCatalog.Today;
        var query = db.GuidanceContentRevisions.AsNoTracking().Where(x => x.State == "published" || x.State == "retired");
        if (request.ProcessKey is not null) query = query.Where(x => x.ProcessKey == request.ProcessKey);
        if (request.VersionId is not null) query = query.Where(x => x.Id == request.VersionId);
        var items = await query.ToListAsync(ct);
        var keys = items.Select(x => x.ContentKey).Distinct().ToArray();
        var currentItems = await db.GuidanceContentRevisions.AsNoTracking()
            .Where(x => keys.Contains(x.ContentKey) && (x.State == "published" || x.State == "retired")
                && x.EffectiveFrom <= today).ToListAsync(ct);
        var latest = currentItems.GroupBy(x => x.ContentKey).ToDictionary(x => x.Key,
            x => x.OrderByDescending(y => y.EffectiveFrom).ThenByDescending(y => y.ContentVersion).First());
        var currentIds = latest.Values.Where(x => GuidanceContentCatalog.IsUsable(x, today)).Select(x => x.Id).ToHashSet();
        var result = items.Where(x => (request.VersionId is not null || currentIds.Contains(x.Id))
            && GuidanceContentCatalog.Body(x).Audience.Intersect(user.Roles).Any())
            .OrderBy(x => x.ProcessKey).ThenBy(x => x.ContentKey)
            .Select(x => GuidanceContentCatalog.ToDto(x, currentIds.Contains(x.Id))).ToList();
        if (request.VersionId is not null && result.Count == 0) throw new NotFoundException("Yayımlanmış rehber", request.VersionId);
        return result;
    }
}
public sealed record GetGuidanceEditorialQuery : IRequest<IReadOnlyList<GuidanceEditorialDto>>;
public sealed class GetGuidanceEditorialQueryHandler(
    IPlatformDbContext db, ICurrentUser user, IGuidanceEditorialAccess access)
    : IRequestHandler<GetGuidanceEditorialQuery, IReadOnlyList<GuidanceEditorialDto>>
{
    public async Task<IReadOnlyList<GuidanceEditorialDto>> Handle(GetGuidanceEditorialQuery request, CancellationToken ct)
    {
        var id = GuidanceContentCatalog.RequireEditor(access, user);
        var items = await db.GuidanceContentRevisions.AsNoTracking().Include(x => x.Events)
            .OrderBy(x => x.ContentKey).ThenByDescending(x => x.ContentVersion).ToListAsync(ct);
        var currentIds = items.Where(x => (x.State is "published" or "retired") && x.EffectiveFrom <= GuidanceContentCatalog.Today)
            .GroupBy(x => x.ContentKey).Select(x => x.OrderByDescending(y => y.EffectiveFrom)
                .ThenByDescending(y => y.ContentVersion).First())
            .Where(x => GuidanceContentCatalog.IsUsable(x, GuidanceContentCatalog.Today)).Select(x => x.Id).ToHashSet();
        return items.Select(x => GuidanceContentCatalog.ToEditorial(x, id, currentIds.Contains(x.Id))).ToList();
    }
}

public sealed record CreateGuidanceContentCommand(
    string ContentKey, string ProcessKey, int? BasedOnId, GuidanceContentBody Body) : IRequest<GuidanceEditorialDto>;
public sealed class CreateGuidanceContentCommandValidator : AbstractValidator<CreateGuidanceContentCommand>
{
    public CreateGuidanceContentCommandValidator()
    {
        RuleFor(x => x.ContentKey).NotEmpty().MaximumLength(64).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.ProcessKey).Must(x => GuidanceContentCatalog.Processes.Contains(x));
        RuleFor(x => x.Body).NotNull().SetValidator(new GuidanceContentBodyValidator());
    }
}
public sealed class CreateGuidanceContentCommandHandler(
    IPlatformDbContext db, ICurrentUser user, IGuidanceEditorialAccess access, IGuidanceCatalogLock gate)
    : IRequestHandler<CreateGuidanceContentCommand, GuidanceEditorialDto>
{
    public Task<GuidanceEditorialDto> Handle(CreateGuidanceContentCommand request, CancellationToken ct)
    {
        var userId = GuidanceContentCatalog.RequireEditor(access, user);
        return gate.ExecuteAsync(request.ContentKey, async lockedCt =>
        {
            var versions = await db.GuidanceContentRevisions.Where(x => x.ContentKey == request.ContentKey).ToListAsync(lockedCt);
            if (versions.Any(x => x.State is "draft" or "in-review"))
                throw new ConflictException("Bu içerik için açık taslak veya inceleme zaten var.");
            if (versions.Count > 0 && !versions.Any(x => x.Id == request.BasedOnId && x.ProcessKey == request.ProcessKey))
                throw new ConflictException("Yeni sürümün temel aldığı yayımlanmış kayıt doğrulanamadı.");
            if (versions.Count == 0 && request.BasedOnId is not null)
                throw new ConflictException("İlk sürüm başka içeriğin kaydını temel alamaz.");
            var item = new GuidanceContentRevision
            {
                ContentKey = request.ContentKey, ProcessKey = request.ProcessKey,
                ContentVersion = versions.Count == 0 ? 1 : versions.Max(x => x.ContentVersion) + 1,
                BasedOnId = request.BasedOnId, CreatedAtUtc = DateTime.UtcNow,
            };
            GuidanceContentCatalog.ApplyBody(item, request.Body, user);
            GuidanceContentCatalog.Event(item, "created", user);
            db.GuidanceContentRevisions.Add(item);
            await db.SaveChangesAsync(lockedCt);
            return GuidanceContentCatalog.ToEditorial(item, userId);
        }, ct);
    }
}
public sealed record UpdateGuidanceContentCommand(int Id, int ExpectedRevision, GuidanceContentBody Body)
    : IRequest<GuidanceEditorialDto>;
public sealed class UpdateGuidanceContentCommandValidator : AbstractValidator<UpdateGuidanceContentCommand>
{
    public UpdateGuidanceContentCommandValidator()
    {
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
        RuleFor(x => x.Body).NotNull().SetValidator(new GuidanceContentBodyValidator());
    }
}
public sealed record ChangeGuidanceContentStateCommand(
    int Id, int ExpectedRevision, string Action, string? Reason, GuidanceExpertReview? Review)
    : IRequest<GuidanceEditorialDto>;
public sealed class ChangeGuidanceContentStateCommandValidator : AbstractValidator<ChangeGuidanceContentStateCommand>
{
    public ChangeGuidanceContentStateCommandValidator()
    {
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
        RuleFor(x => x.Action).Must(x => x is "submit" or "return" or "publish" or "retire");
        RuleFor(x => x.Reason).MaximumLength(2000);
        When(x => x.Action is "return" or "retire", () => RuleFor(x => x.Reason).NotEmpty());
        When(x => x.Action == "publish", () => RuleFor(x => x.Review).NotNull().SetValidator(new GuidanceExpertReviewValidator()!));
    }
}
public sealed class GuidanceExpertReviewValidator : AbstractValidator<GuidanceExpertReview>
{
    public GuidanceExpertReviewValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("Uzman incelemesinin bu içerik sürümünü kapsadığı doğrulanmalı.");
        RuleFor(x => x.ReviewedOn).Must(x => x <= GuidanceContentCatalog.Today && x >= new DateOnly(2000, 1, 1));
        RuleFor(x => x.ReviewDueOn).Must((review, date) => date > review.ReviewedOn && date >= GuidanceContentCatalog.Today);
    }
}
public sealed class EditGuidanceContentCommandHandler(
    IPlatformDbContext db, ICurrentUser user, IGuidanceEditorialAccess access, IGuidanceCatalogLock gate)
    : IRequestHandler<UpdateGuidanceContentCommand, GuidanceEditorialDto>,
      IRequestHandler<ChangeGuidanceContentStateCommand, GuidanceEditorialDto>
{
    public Task<GuidanceEditorialDto> Handle(UpdateGuidanceContentCommand request, CancellationToken ct) =>
        EditAsync(request.Id, request.ExpectedRevision, "updated", null, null, request.Body, ct);
    public Task<GuidanceEditorialDto> Handle(ChangeGuidanceContentStateCommand request, CancellationToken ct) =>
        EditAsync(request.Id, request.ExpectedRevision, request.Action, request.Reason, request.Review, null, ct);

    private async Task<GuidanceEditorialDto> EditAsync(int id, int expected, string action, string? reason,
        GuidanceExpertReview? review, GuidanceContentBody? body, CancellationToken ct)
    {
        var userId = GuidanceContentCatalog.RequireEditor(access, user);
        var key = await db.GuidanceContentRevisions.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.ContentKey).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("İçerik", id);
        return await gate.ExecuteAsync(key, async lockedCt =>
        {
            var item = await db.GuidanceContentRevisions.Include(x => x.Events).SingleAsync(x => x.Id == id, lockedCt);
            if (item.Revision != expected) throw new ConflictException("İçerik değişmiş; güncel sürümü yeniden inceleyin.");
            if (action == "updated")
            {
                RequireState(item, "draft");
                GuidanceContentCatalog.ApplyBody(item, body!, user);
            }
            else if (action == "submit")
            {
                RequireState(item, "draft");
                EnsureComplete(item);
                item.State = "in-review";
            }
            else if (action == "return")
            {
                RequireState(item, "in-review"); item.State = "draft";
            }
            else if (action == "publish")
            {
                RequireState(item, "in-review"); EnsureComplete(item);
                if (GuidanceContentCatalog.Contributors(item).Any(x => x.UserId == userId))
                    throw new ConflictException("İçeriği hazırlayan veya düzenleyen kişi bu sürümü yayımlayamaz.");
                if (review!.ReviewedOn < GuidanceContentCatalog.LocalDate(item.LastEditedAtUtc))
                    throw new ConflictException("Uzman incelemesi son içerik düzenlemesinden önce olamaz.");
                var latestDate = await db.GuidanceContentRevisions.Where(x => x.ContentKey == key && x.PublishedAtUtc != null)
                    .MaxAsync(x => (DateOnly?)x.EffectiveFrom, lockedCt);
                if (latestDate > item.EffectiveFrom) throw new ConflictException("Yeni sürüm önceki yayımdan daha eski bir yürürlükle açılamaz.");
                item.ExpertName = review.Name.Trim(); item.ExpertRole = review.Role.Trim();
                item.ExpertReference = review.Reference.Trim(); item.ReviewedOn = review.ReviewedOn;
                item.ReviewDueOn = review.ReviewDueOn; item.PublishedAtUtc = DateTime.UtcNow;
                item.PublishedByName = user.UserName; item.State = "published";
            }
            else if (action == "retire")
            {
                RequireState(item, "published"); item.State = "retired";
            }
            item.Revision++;
            GuidanceContentCatalog.Event(item, action, user, reason);
            await db.SaveChangesAsync(lockedCt);
            return GuidanceContentCatalog.ToEditorial(item, userId);
        }, ct);
    }
    private static void RequireState(GuidanceContentRevision item, string state)
    {
        if (item.State != state) throw new ConflictException("İçerik bu durumda belirtilen işleme açık değil.");
    }
    private static void EnsureComplete(GuidanceContentRevision item)
    {
        var result = new GuidanceContentBodyValidator(complete: true).Validate(GuidanceContentCatalog.Body(item));
        if (!result.IsValid) throw new ConflictException("İnceleme için içerik eksik: "
            + string.Join("; ", result.Errors.Select(x => x.ErrorMessage)));
    }
}
