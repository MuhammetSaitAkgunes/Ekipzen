using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

internal sealed record RecruitmentCheckDefinition(
    string Question, IReadOnlyList<string> Choices, int CorrectChoice, string Explanation);

internal static class RecruitmentDecisionChecks
{
    public const string ProcessKey = "recruitment";
    public const int ContentVersion = 1;

    private static readonly IReadOnlyDictionary<string, RecruitmentCheckDefinition> Checks =
        new Dictionary<string, RecruitmentCheckDefinition>(StringComparer.Ordinal)
        {
            ["candidate-create"] = new(
                "Başvuruyu kaydederken aday puanının dayanağı henüz yok. Ne yaparsınız?",
                ["Tahmini bir puan girerim", "Puanı başka adaydan kopyalarım", "Değerlendirme dayanağını netleştirmeden puan uydurmam"],
                2,
                "Mevcut aday kaydı puanı zorunlu tutuyor. Dayanak yoksa rastgele sayı girip başvuruyu değerlendirilmiş gibi göstermeyin; eksik API ihtiyacını ayrıca takip edin."),
            ["first-review"] = new(
                "Yeni adayın aşamasını değiştirmeden önce hangi kontrol gerekir?",
                ["Yalnız genel aday puanına bakarım", "Başvuru, not ve değerlendirme dayanaklarını karşılaştırır; aday iletişimini ayrı takip ederim", "Aşama seçimi adaya otomatik bildirim gönderir"],
                1,
                "Aşama etiketi tek başına karar gerekçesi veya adaya gönderilmiş mesaj değildir. Dayanak kayıtları inceleyin ve iletişimi ayrıca doğrulayın."),
        };

    public static bool IsStep(string stepKey) => Checks.ContainsKey(stepKey);

    public static RecruitmentCheckDefinition Get(string stepKey) =>
        Checks.TryGetValue(stepKey, out var check)
            ? check
            : throw new NotFoundException("Alıştırma", stepKey);
}

public sealed record RecruitmentDecisionCheckDto(
    string StepKey,
    int ContentVersion,
    string Question,
    IReadOnlyList<string> Choices,
    int? LastSelectedChoice,
    bool? LastCorrect,
    int AttemptCount,
    string? Explanation);

public sealed record GetRecruitmentDecisionCheckQuery(string StepKey)
    : IRequest<RecruitmentDecisionCheckDto>;

public sealed class GetRecruitmentDecisionCheckQueryValidator
    : AbstractValidator<GetRecruitmentDecisionCheckQuery>
{
    public GetRecruitmentDecisionCheckQueryValidator() =>
        RuleFor(request => request.StepKey).Must(RecruitmentDecisionChecks.IsStep)
            .WithMessage("Geçersiz rehber adımı.");
}

public sealed class GetRecruitmentDecisionCheckQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetRecruitmentDecisionCheckQuery, RecruitmentDecisionCheckDto>
{
    public async Task<RecruitmentDecisionCheckDto> Handle(
        GetRecruitmentDecisionCheckQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
        var check = RecruitmentDecisionChecks.Get(request.StepKey);
        var progress = await context.GuidanceDecisionProgresses.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.UserId == userId &&
                item.ProcessKey == RecruitmentDecisionChecks.ProcessKey &&
                item.StepKey == request.StepKey &&
                item.ContentVersion == RecruitmentDecisionChecks.ContentVersion, ct);
        return ToDto(request.StepKey, check, progress);
    }

    internal static RecruitmentDecisionCheckDto ToDto(
        string stepKey, RecruitmentCheckDefinition check, GuidanceDecisionProgress? progress) =>
        new(stepKey, RecruitmentDecisionChecks.ContentVersion, check.Question, check.Choices,
            progress?.LastSelectedChoice, progress?.LastCorrect, progress?.AttemptCount ?? 0,
            progress is null ? null : check.Explanation);
}

public sealed record SubmitRecruitmentDecisionCheckCommand(string StepKey, int SelectedChoice)
    : IRequest<RecruitmentDecisionCheckDto>;

public sealed class SubmitRecruitmentDecisionCheckCommandValidator
    : AbstractValidator<SubmitRecruitmentDecisionCheckCommand>
{
    public SubmitRecruitmentDecisionCheckCommandValidator()
    {
        RuleFor(request => request.StepKey).Must(RecruitmentDecisionChecks.IsStep)
            .WithMessage("Geçersiz rehber adımı.");
        RuleFor(request => request.SelectedChoice).InclusiveBetween(0, 2);
    }
}

public sealed class SubmitRecruitmentDecisionCheckCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<SubmitRecruitmentDecisionCheckCommand, RecruitmentDecisionCheckDto>
{
    public Task<RecruitmentDecisionCheckDto> Handle(
        SubmitRecruitmentDecisionCheckCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
        var check = RecruitmentDecisionChecks.Get(request.StepKey);
        return databaseLock.ExecuteExclusiveAsync(
            $"guidance-check:{userId}:{RecruitmentDecisionChecks.ProcessKey}:{request.StepKey}:{RecruitmentDecisionChecks.ContentVersion}",
            async lockedCt =>
            {
                var progress = await context.GuidanceDecisionProgresses.SingleOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.ProcessKey == RecruitmentDecisionChecks.ProcessKey &&
                    item.StepKey == request.StepKey &&
                    item.ContentVersion == RecruitmentDecisionChecks.ContentVersion, lockedCt);
                if (progress is null)
                {
                    progress = new GuidanceDecisionProgress
                    {
                        UserId = userId,
                        ProcessKey = RecruitmentDecisionChecks.ProcessKey,
                        StepKey = request.StepKey,
                        ContentVersion = RecruitmentDecisionChecks.ContentVersion,
                    };
                    context.GuidanceDecisionProgresses.Add(progress);
                }

                progress.AttemptCount++;
                progress.LastSelectedChoice = request.SelectedChoice;
                progress.LastCorrect = request.SelectedChoice == check.CorrectChoice;
                progress.LastAnsweredAtUtc = DateTime.UtcNow;
                await context.SaveChangesAsync(lockedCt);
                return GetRecruitmentDecisionCheckQueryHandler.ToDto(request.StepKey, check, progress);
            }, ct);
    }
}
