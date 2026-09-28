using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

internal sealed record LeaveApprovalCheckDefinition(
    string Question, IReadOnlyList<string> Choices, int CorrectChoice, string Explanation);

internal static class LeaveApprovalDecisionChecks
{
    public const string ProcessKey = "leave-approval";
    public const int ContentVersion = 1;

    private static readonly IReadOnlyDictionary<string, LeaveApprovalCheckDefinition> Checks =
        new Dictionary<string, LeaveApprovalCheckDefinition>(StringComparer.Ordinal)
        {
            ["review-request"] = new(
                "İzin türü, tarihler ve talep açıklaması birbiriyle çelişiyor. Karardan önce ne yaparsınız?",
                ["Talebi hemen onaylarım", "Gerekçeyi incelemeden reddederim",
                 "Türü, tarihleri, gün sayısını ve gerekli dayanağı netleştiririm"],
                2,
                "Çelişkili kaydı varsayımla tamamlamayın. Talep ve gereken dayanağı netleştirdikten sonra gerçek kayıt üzerinde karar verin."),
            ["decision-effect"] = new(
                "İzin kararını kaydederken hangi beklenti doğrudur?",
                ["Ekranda görülen eski bakiye her durumda yeterlidir",
                 "Sistem kararı güncel kayıtla değerlendirir; bakiyeden düşen onayda bakiyeyi yeniden kontrol eder",
                 "Ret kararı da yıllık izin bakiyesinden gün düşer"],
                1,
                "Onay anında bakiyeden düşen türler için rezervasyon yeniden doğrulanır ve kullanım hareketi yazılır. Ret talebi kapatır; bu ekranda karar geri alınmaz."),
        };

    public static bool IsStep(string stepKey) => Checks.ContainsKey(stepKey);

    public static LeaveApprovalCheckDefinition Get(string stepKey) =>
        Checks.TryGetValue(stepKey, out var check)
            ? check
            : throw new NotFoundException("Alıştırma", stepKey);
}

public sealed record LeaveApprovalDecisionCheckDto(
    string StepKey, int ContentVersion, string Question, IReadOnlyList<string> Choices,
    int? LastSelectedChoice, bool? LastCorrect, int AttemptCount, string? Explanation);

public sealed record GetLeaveApprovalDecisionCheckQuery(string StepKey)
    : IRequest<LeaveApprovalDecisionCheckDto>;

public sealed class GetLeaveApprovalDecisionCheckQueryValidator
    : AbstractValidator<GetLeaveApprovalDecisionCheckQuery>
{
    public GetLeaveApprovalDecisionCheckQueryValidator() =>
        RuleFor(request => request.StepKey).Must(LeaveApprovalDecisionChecks.IsStep)
            .WithMessage("Geçersiz rehber adımı.");
}

public sealed class GetLeaveApprovalDecisionCheckQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetLeaveApprovalDecisionCheckQuery, LeaveApprovalDecisionCheckDto>
{
    public async Task<LeaveApprovalDecisionCheckDto> Handle(
        GetLeaveApprovalDecisionCheckQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
        var check = LeaveApprovalDecisionChecks.Get(request.StepKey);
        var progress = await context.GuidanceDecisionProgresses.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.UserId == userId &&
                item.ProcessKey == LeaveApprovalDecisionChecks.ProcessKey &&
                item.StepKey == request.StepKey &&
                item.ContentVersion == LeaveApprovalDecisionChecks.ContentVersion, ct);
        return ToDto(request.StepKey, check, progress);
    }

    internal static LeaveApprovalDecisionCheckDto ToDto(
        string stepKey, LeaveApprovalCheckDefinition check, GuidanceDecisionProgress? progress) =>
        new(stepKey, LeaveApprovalDecisionChecks.ContentVersion, check.Question, check.Choices,
            progress?.LastSelectedChoice, progress?.LastCorrect, progress?.AttemptCount ?? 0,
            progress is null ? null : check.Explanation);
}

public sealed record SubmitLeaveApprovalDecisionCheckCommand(string StepKey, int SelectedChoice)
    : IRequest<LeaveApprovalDecisionCheckDto>;

public sealed class SubmitLeaveApprovalDecisionCheckCommandValidator
    : AbstractValidator<SubmitLeaveApprovalDecisionCheckCommand>
{
    public SubmitLeaveApprovalDecisionCheckCommandValidator()
    {
        RuleFor(request => request.StepKey).Must(LeaveApprovalDecisionChecks.IsStep)
            .WithMessage("Geçersiz rehber adımı.");
        RuleFor(request => request.SelectedChoice).InclusiveBetween(0, 2);
    }
}

public sealed class SubmitLeaveApprovalDecisionCheckCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<SubmitLeaveApprovalDecisionCheckCommand, LeaveApprovalDecisionCheckDto>
{
    public Task<LeaveApprovalDecisionCheckDto> Handle(
        SubmitLeaveApprovalDecisionCheckCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
        var check = LeaveApprovalDecisionChecks.Get(request.StepKey);
        return databaseLock.ExecuteExclusiveAsync(
            $"guidance-check:{userId}:{LeaveApprovalDecisionChecks.ProcessKey}:{request.StepKey}:{LeaveApprovalDecisionChecks.ContentVersion}",
            async lockedCt =>
            {
                var progress = await context.GuidanceDecisionProgresses.SingleOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.ProcessKey == LeaveApprovalDecisionChecks.ProcessKey &&
                    item.StepKey == request.StepKey &&
                    item.ContentVersion == LeaveApprovalDecisionChecks.ContentVersion, lockedCt);
                if (progress is null)
                {
                    progress = new GuidanceDecisionProgress
                    {
                        UserId = userId,
                        ProcessKey = LeaveApprovalDecisionChecks.ProcessKey,
                        StepKey = request.StepKey,
                        ContentVersion = LeaveApprovalDecisionChecks.ContentVersion,
                    };
                    context.GuidanceDecisionProgresses.Add(progress);
                }

                progress.AttemptCount++;
                progress.LastSelectedChoice = request.SelectedChoice;
                progress.LastCorrect = request.SelectedChoice == check.CorrectChoice;
                progress.LastAnsweredAtUtc = DateTime.UtcNow;
                await context.SaveChangesAsync(lockedCt);
                return GetLeaveApprovalDecisionCheckQueryHandler.ToDto(request.StepKey, check, progress);
            }, ct);
    }
}
