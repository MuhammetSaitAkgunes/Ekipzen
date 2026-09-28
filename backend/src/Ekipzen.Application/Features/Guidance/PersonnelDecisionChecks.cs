using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

internal sealed record DecisionCheckDefinition(
    string Question, IReadOnlyList<string> Choices, int CorrectChoice, string Explanation);

internal static class PersonnelDecisionChecks
{
    private static readonly IReadOnlyDictionary<string, DecisionCheckDefinition> Checks =
        new Dictionary<string, DecisionCheckDefinition>(StringComparer.Ordinal)
        {
            ["tab-kimlik"] = new(
                "Aynı ad ve soyadla iki çalışan kaydı görüyorsunuz. Hangisini seçersiniz?",
                ["İlk sıradaki kaydı", "Kimlik bilgisi ve iş bilgileri doğrulanan kaydı", "Yeni bir kayıt açarım"],
                1,
                "Aynı ad yeterli değildir. Kimliği ve ilgili iş bilgilerini dayanak kayıtla karşılaştırın; yanlış kişiye işlem bağlamayın."),
            ["tab-iletisim"] = new(
                "Acil durumda aranacak kişinin telefonu henüz doğrulanmadı. Ne yaparsınız?",
                ["Tahmini bir numara girerim", "Başka çalışanın numarasını örnek alırım", "Numarayı doğrulamadan girmem ve eksikliği takip ederim"],
                2,
                "Doğrulanmamış numara acil durumda yanlış kişiye ulaşılmasına neden olabilir. Bilgiyi ilgili kişiyle teyit edin."),
            ["tab-is"] = new(
                "Çalışanın sözleşme türü henüz teyit edilmedi. Hangi yol doğrudur?",
                ["Varsayılan türü seçip devam ederim", "İmzalı iş belgesini yetkili kişiyle doğrularım", "Başka çalışanın türünü kopyalarım"],
                1,
                "Sözleşme türü sonraki süreçleri etkiler. Seçimi gerçek iş belgesine dayandırın; bilinmeyen bilgiyi varsaymayın."),
            ["tab-mali"] = new(
                "Çalışan kartı kaydedildi, fakat ücret paketi bulunmuyor. Ne anlama gelir?",
                ["Bordro hazırlığı tamamdır", "Ücret paketi ve yürürlük bilgisi ayrıca doğrulanmalıdır", "Başka çalışanın ücretini kopyalayabilirim"],
                1,
                "Kartın oluşması ücret paketini kaydetmez. Tutarı, yürürlük tarihini ve kaynak referansını ayrı adımda kontrol edin."),
            ["tab-ozluk"] = new(
                "Çalışma koşuluna uygunluk değerlendirmesi henüz yapılmadı. Nasıl ilerlersiniz?",
                ["Kutunun değerinden uygunluk sonucu çıkarırım", "Değerlendirme sonucunu yetkili uzmanla netleştiririm", "Önceki çalışanın seçimini kullanırım"],
                1,
                "Form alanı tek başına uygunluk kararı değildir. Değerlendirilmemiş durumu doğrulanmış sonuç gibi sunmayın."),
            ["tab-evrak"] = new(
                "Çalışan kartı açıldı, fakat gerekli belge henüz gelmedi. Ne yaparsınız?",
                ["İşe girişi tümüyle tamamlandı sayarım", "Boş bir dosya yüklerim", "Belge eksikliğini takip edip gerçek dosya geldiğinde doğru türe yüklerim"],
                2,
                "Kartın varlığı belgeyi veya uygulama dışındaki adımları kanıtlamaz. Eksikliği görünür tutun ve gerçek dosyayı kontrol edin."),
        };

    public static DecisionCheckDefinition Get(string stepKey) =>
        Checks.TryGetValue(stepKey, out var check)
            ? check
            : throw new NotFoundException("Alıştırma", stepKey);
}

public sealed record PersonnelDecisionCheckDto(
    string StepKey,
    int ContentVersion,
    string Question,
    IReadOnlyList<string> Choices,
    int? LastSelectedChoice,
    bool? LastCorrect,
    int AttemptCount,
    string? Explanation);

public sealed record GetPersonnelDecisionCheckQuery(string StepKey) : IRequest<PersonnelDecisionCheckDto>;

public sealed class GetPersonnelDecisionCheckQueryValidator : AbstractValidator<GetPersonnelDecisionCheckQuery>
{
    public GetPersonnelDecisionCheckQueryValidator() =>
        RuleFor(request => request.StepKey).Must(PersonnelEntryGuide.IsStep)
            .WithMessage("Geçersiz rehber adımı.");
}

public sealed class GetPersonnelDecisionCheckQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetPersonnelDecisionCheckQuery, PersonnelDecisionCheckDto>
{
    public async Task<PersonnelDecisionCheckDto> Handle(GetPersonnelDecisionCheckQuery request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(currentUser);
        var check = PersonnelDecisionChecks.Get(request.StepKey);
        var progress = await context.GuidanceDecisionProgresses.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.UserId == userId &&
                item.ProcessKey == PersonnelEntryGuide.ProcessKey &&
                item.StepKey == request.StepKey &&
                item.ContentVersion == PersonnelEntryGuide.ContentVersion, ct);
        return ToDto(request.StepKey, check, progress);
    }

    internal static PersonnelDecisionCheckDto ToDto(
        string stepKey, DecisionCheckDefinition check, GuidanceDecisionProgress? progress) =>
        new(stepKey, PersonnelEntryGuide.ContentVersion, check.Question, check.Choices,
            progress?.LastSelectedChoice, progress?.LastCorrect, progress?.AttemptCount ?? 0,
            progress is null ? null : check.Explanation);
}

public sealed record SubmitPersonnelDecisionCheckCommand(string StepKey, int SelectedChoice)
    : IRequest<PersonnelDecisionCheckDto>;

public sealed class SubmitPersonnelDecisionCheckCommandValidator
    : AbstractValidator<SubmitPersonnelDecisionCheckCommand>
{
    public SubmitPersonnelDecisionCheckCommandValidator()
    {
        RuleFor(request => request.StepKey).Must(PersonnelEntryGuide.IsStep)
            .WithMessage("Geçersiz rehber adımı.");
        RuleFor(request => request.SelectedChoice).InclusiveBetween(0, 2);
    }
}

public sealed class SubmitPersonnelDecisionCheckCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<SubmitPersonnelDecisionCheckCommand, PersonnelDecisionCheckDto>
{
    public Task<PersonnelDecisionCheckDto> Handle(SubmitPersonnelDecisionCheckCommand request, CancellationToken ct)
    {
        var userId = PersonnelEntryGuide.RequireUserId(currentUser);
        var check = PersonnelDecisionChecks.Get(request.StepKey);
        return databaseLock.ExecuteExclusiveAsync(
            $"guidance-check:{userId}:{PersonnelEntryGuide.ProcessKey}:{request.StepKey}:{PersonnelEntryGuide.ContentVersion}",
            async lockedCt =>
            {
                var progress = await context.GuidanceDecisionProgresses.SingleOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.ProcessKey == PersonnelEntryGuide.ProcessKey &&
                    item.StepKey == request.StepKey &&
                    item.ContentVersion == PersonnelEntryGuide.ContentVersion, lockedCt);
                if (progress is null)
                {
                    progress = new GuidanceDecisionProgress
                    {
                        UserId = userId,
                        ProcessKey = PersonnelEntryGuide.ProcessKey,
                        StepKey = request.StepKey,
                        ContentVersion = PersonnelEntryGuide.ContentVersion,
                    };
                    context.GuidanceDecisionProgresses.Add(progress);
                }

                progress.AttemptCount++;
                progress.LastSelectedChoice = request.SelectedChoice;
                progress.LastCorrect = request.SelectedChoice == check.CorrectChoice;
                progress.LastAnsweredAtUtc = DateTime.UtcNow;
                await context.SaveChangesAsync(lockedCt);
                return GetPersonnelDecisionCheckQueryHandler.ToDto(request.StepKey, check, progress);
            }, ct);
    }
}
