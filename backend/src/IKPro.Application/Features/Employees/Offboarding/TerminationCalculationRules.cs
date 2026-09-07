using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Organization;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.Offboarding;

public sealed record TerminationCalculationRuleDto(
    int Id,
    int Version,
    DateOnly EffectiveFrom,
    string Status,
    decimal DailyWageDivisor,
    decimal SeveranceDaysPerYear,
    int MinimumSeveranceServiceDays,
    decimal SeveranceCeiling,
    int NoticeDaysUnderSixMonths,
    int NoticeDaysSixToEighteenMonths,
    int NoticeDaysEighteenToThirtySixMonths,
    int NoticeDaysOverThirtySixMonths,
    int NoticeFirstThresholdMonths,
    int NoticeSecondThresholdMonths,
    int NoticeThirdThresholdMonths,
    bool IncludeRegularPremiumInDressedWage,
    bool IncludeRoadAllowanceInDressedWage,
    bool IncludeMealAllowanceInDressedWage,
    bool IncludeOtherRegularBenefitsInDressedWage,
    string SourceReference,
    string? ReviewNotes,
    DateTime? ApprovedAtUtc,
    string? ApprovedByUserId);

public sealed record SaveTerminationCalculationRuleCommand(
    int? Id,
    int? ExpectedVersion,
    DateOnly EffectiveFrom,
    decimal DailyWageDivisor,
    decimal SeveranceDaysPerYear,
    int MinimumSeveranceServiceDays,
    decimal SeveranceCeiling,
    int NoticeDaysUnderSixMonths,
    int NoticeDaysSixToEighteenMonths,
    int NoticeDaysEighteenToThirtySixMonths,
    int NoticeDaysOverThirtySixMonths,
    int NoticeFirstThresholdMonths,
    int NoticeSecondThresholdMonths,
    int NoticeThirdThresholdMonths,
    bool IncludeRegularPremiumInDressedWage,
    bool IncludeRoadAllowanceInDressedWage,
    bool IncludeMealAllowanceInDressedWage,
    bool IncludeOtherRegularBenefitsInDressedWage,
    string SourceReference,
    string? ReviewNotes) : IRequest<TerminationCalculationRuleDto>;

public sealed class SaveTerminationCalculationRuleCommandValidator
    : AbstractValidator<SaveTerminationCalculationRuleCommand>
{
    public SaveTerminationCalculationRuleCommandValidator()
    {
        RuleFor(item => item.Id).GreaterThan(0).When(item => item.Id is not null);
        RuleFor(item => item.ExpectedVersion).GreaterThan(0).When(item => item.Id is not null);
        RuleFor(item => item.ExpectedVersion).Null().When(item => item.Id is null);
        RuleFor(item => item.EffectiveFrom).NotEmpty();
        RuleFor(item => item.DailyWageDivisor).GreaterThan(0);
        RuleFor(item => item.SeveranceDaysPerYear).GreaterThan(0);
        RuleFor(item => item.MinimumSeveranceServiceDays).GreaterThan(0);
        RuleFor(item => item.SeveranceCeiling).GreaterThan(0);
        RuleFor(item => item.NoticeDaysUnderSixMonths).GreaterThanOrEqualTo(0);
        RuleFor(item => item.NoticeDaysSixToEighteenMonths).GreaterThanOrEqualTo(0);
        RuleFor(item => item.NoticeDaysEighteenToThirtySixMonths).GreaterThanOrEqualTo(0);
        RuleFor(item => item.NoticeDaysOverThirtySixMonths).GreaterThanOrEqualTo(0);
        RuleFor(item => item.NoticeFirstThresholdMonths).GreaterThan(0);
        RuleFor(item => item.NoticeSecondThresholdMonths)
            .GreaterThan(item => item.NoticeFirstThresholdMonths);
        RuleFor(item => item.NoticeThirdThresholdMonths)
            .GreaterThan(item => item.NoticeSecondThresholdMonths);
        RuleFor(item => item.SourceReference).NotEmpty().MaximumLength(1000);
        RuleFor(item => item.ReviewNotes).MaximumLength(2000);
    }
}

public sealed record GetTerminationCalculationRulesQuery
    : IRequest<IReadOnlyList<TerminationCalculationRuleDto>>;

public sealed class GetTerminationCalculationRulesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTerminationCalculationRulesQuery, IReadOnlyList<TerminationCalculationRuleDto>>
{
    public async Task<IReadOnlyList<TerminationCalculationRuleDto>> Handle(
        GetTerminationCalculationRulesQuery request,
        CancellationToken cancellationToken) =>
        await context.TerminationCalculationRules
            .AsNoTracking()
            .OrderByDescending(item => item.EffectiveFrom)
            .Select(item => TerminationCalculationRuleMappings.ToDto(item))
            .ToListAsync(cancellationToken);
}

public sealed class SaveTerminationCalculationRuleCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SaveTerminationCalculationRuleCommand, TerminationCalculationRuleDto>
{
    public async Task<TerminationCalculationRuleDto> Handle(
        SaveTerminationCalculationRuleCommand request,
        CancellationToken cancellationToken)
    {
        TerminationCalculationRule item;
        if (request.Id is int id)
        {
            item = await context.TerminationCalculationRules
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
                ?? throw new NotFoundException("İşten çıkış hesap kuralı", id);
            if (item.Status != TerminationCalculationRuleStatus.Draft)
            {
                throw new ConflictException("Onaylanmış hesap kuralı değiştirilemez; yeni yürürlük tarihli sürüm oluşturun.");
            }

            if (item.Version != request.ExpectedVersion)
            {
                throw new ConflictException("Hesap kuralı başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
            }

            item.Version++;
        }
        else
        {
            item = new TerminationCalculationRule();
            context.TerminationCalculationRules.Add(item);
        }

        var dateTaken = await context.TerminationCalculationRules.AnyAsync(
            candidate => candidate.Id != item.Id && candidate.EffectiveFrom == request.EffectiveFrom,
            cancellationToken);
        if (dateTaken)
        {
            throw new ConflictException("Bu yürürlük tarihinde başka bir hesap kuralı bulunuyor.");
        }

        item.EffectiveFrom = request.EffectiveFrom;
        item.DailyWageDivisor = request.DailyWageDivisor;
        item.SeveranceDaysPerYear = request.SeveranceDaysPerYear;
        item.MinimumSeveranceServiceDays = request.MinimumSeveranceServiceDays;
        item.SeveranceCeiling = request.SeveranceCeiling;
        item.NoticeDaysUnderSixMonths = request.NoticeDaysUnderSixMonths;
        item.NoticeDaysSixToEighteenMonths = request.NoticeDaysSixToEighteenMonths;
        item.NoticeDaysEighteenToThirtySixMonths = request.NoticeDaysEighteenToThirtySixMonths;
        item.NoticeDaysOverThirtySixMonths = request.NoticeDaysOverThirtySixMonths;
        item.NoticeFirstThresholdMonths = request.NoticeFirstThresholdMonths;
        item.NoticeSecondThresholdMonths = request.NoticeSecondThresholdMonths;
        item.NoticeThirdThresholdMonths = request.NoticeThirdThresholdMonths;
        item.IncludeRegularPremiumInDressedWage = request.IncludeRegularPremiumInDressedWage;
        item.IncludeRoadAllowanceInDressedWage = request.IncludeRoadAllowanceInDressedWage;
        item.IncludeMealAllowanceInDressedWage = request.IncludeMealAllowanceInDressedWage;
        item.IncludeOtherRegularBenefitsInDressedWage = request.IncludeOtherRegularBenefitsInDressedWage;
        item.SourceReference = request.SourceReference.Trim();
        item.ReviewNotes = string.IsNullOrWhiteSpace(request.ReviewNotes) ? null : request.ReviewNotes.Trim();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Hesap kuralı başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
        }

        return item.ToDto();
    }
}

public sealed record ApproveTerminationCalculationRuleCommand(int Id, int ExpectedVersion)
    : IRequest<TerminationCalculationRuleDto>;

public sealed class ApproveTerminationCalculationRuleCommandValidator
    : AbstractValidator<ApproveTerminationCalculationRuleCommand>
{
    public ApproveTerminationCalculationRuleCommandValidator()
    {
        RuleFor(item => item.Id).GreaterThan(0);
        RuleFor(item => item.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ApproveTerminationCalculationRuleCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser)
    : IRequestHandler<ApproveTerminationCalculationRuleCommand, TerminationCalculationRuleDto>
{
    public async Task<TerminationCalculationRuleDto> Handle(
        ApproveTerminationCalculationRuleCommand request,
        CancellationToken cancellationToken)
    {
        var item = await context.TerminationCalculationRules
            .SingleOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("İşten çıkış hesap kuralı", request.Id);
        if (item.Status != TerminationCalculationRuleStatus.Draft)
        {
            throw new ConflictException("Hesap kuralı zaten onaylanmış.");
        }

        if (item.Version != request.ExpectedVersion)
        {
            throw new ConflictException("Hesap kuralı başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
        }

        item.Status = TerminationCalculationRuleStatus.Approved;
        item.ApprovedAtUtc = DateTime.UtcNow;
        item.ApprovedByUserId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Hesap kuralı onayı kullanıcı kimliği gerektirir.");
        item.Version++;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Hesap kuralı başka bir işlem tarafından onaylandı; ekranı yenileyin.");
        }
        return item.ToDto();
    }
}

public sealed record GenerateTerminationCalculationDraftCommand(
    int EmployeeId,
    int CaseId,
    int ExpectedVersion,
    bool ConfirmSeveranceEligibility,
    bool ConfirmNoticeEligibility,
    string ExpertReviewReference) : IRequest<EmployeeOffboardingCaseDto>;

public sealed class GenerateTerminationCalculationDraftCommandValidator
    : AbstractValidator<GenerateTerminationCalculationDraftCommand>
{
    public GenerateTerminationCalculationDraftCommandValidator()
    {
        RuleFor(item => item.CaseId).GreaterThan(0);
        RuleFor(item => item.ExpectedVersion).GreaterThan(0);
        RuleFor(item => item.ExpertReviewReference).NotEmpty().MaximumLength(256);
    }
}

public sealed class GenerateTerminationCalculationDraftCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IDatabaseLock databaseLock)
    : IRequestHandler<GenerateTerminationCalculationDraftCommand, EmployeeOffboardingCaseDto>
{
    public Task<EmployeeOffboardingCaseDto> Handle(
        GenerateTerminationCalculationDraftCommand request,
        CancellationToken cancellationToken) =>
        databaseLock.ExecuteExclusiveAsync(
            $"employee-offboarding:{request.EmployeeId}",
            token => GenerateAsync(request, token),
            cancellationToken);

    private async Task<EmployeeOffboardingCaseDto> GenerateAsync(
        GenerateTerminationCalculationDraftCommand request,
        CancellationToken cancellationToken)
    {
        var item = await context.EmployeeOffboardingCases
            .SingleOrDefaultAsync(candidate => candidate.Id == request.CaseId
                && candidate.EmployeeId == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("İşten çıkış dosyası", request.CaseId);
        if (item.Status != EmployeeOffboardingStatus.Draft)
        {
            throw new ConflictException("Hesap taslağı yalnız taslak işten çıkış dosyasında üretilebilir.");
        }

        if (item.Version != request.ExpectedVersion)
        {
            throw new ConflictException("İşten çıkış taslağı değişti; ekranı yenileyin.");
        }

        if (item.EmploymentPeriodId is not int employmentPeriodId)
        {
            throw new ConflictException("Hesap için istihdam dönemi snapshot'ı bulunamadı; taslağı yeniden kaydedin.");
        }

        var employmentPeriod = await context.EmployeeEmploymentPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(period => period.Id == employmentPeriodId, cancellationToken)
            ?? throw new ConflictException(
                "Hesap için kullanılan istihdam dönemi artık bulunamıyor; taslağı yeniden kaydedin.");
        var rule = await context.TerminationCalculationRules
            .Where(candidate => candidate.Status == TerminationCalculationRuleStatus.Approved
                && candidate.EffectiveFrom <= item.TerminationDate)
            .OrderByDescending(candidate => candidate.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException(
                "Ayrılış tarihinde yürürlükte olan uzman onaylı hesap kuralı bulunamadı; harici uzman girişi kullanın.");

        var result = TerminationDraftCalculator.Calculate(
            item,
            employmentPeriod.StartDate,
            rule,
            request.ConfirmSeveranceEligibility,
            request.ConfirmNoticeEligibility);

        item.UnusedLeavePayment = result.UnusedLeavePayment;
        item.SeverancePayment = result.SeverancePayment;
        item.NoticePayment = result.NoticePayment;
        item.SettlementSource = OffboardingSettlementSource.ApprovedRuleDraft;
        item.SettlementReference = request.ExpertReviewReference.Trim();
        item.CalculationRuleId = rule.Id;
        item.CalculationRuleVersion = rule.Version;
        item.CalculationGeneratedAtUtc = DateTime.UtcNow;
        item.CalculationGeneratedByUserId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Hesap taslağı kullanıcı kimliği gerektirir.");
        item.Version++;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("İşten çıkış taslağı başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
        }
        return item.ToDto();
    }
}

public sealed record TerminationDraftCalculation(
    int ServiceDays,
    decimal DressedMonthlyWage,
    int NoticeDays,
    decimal UnusedLeavePayment,
    decimal SeverancePayment,
    decimal NoticePayment);

public static class TerminationDraftCalculator
{
    public static TerminationDraftCalculation Calculate(
        EmployeeOffboardingCase item,
        DateOnly employmentStart,
        TerminationCalculationRule rule,
        bool severanceEligible,
        bool noticeEligible)
    {
        var serviceDays = Math.Max(0, item.TerminationDate.DayNumber - employmentStart.DayNumber);
        var dressedMonthlyWage = item.MonthlyGrossSalarySnapshot
            + (rule.IncludeRegularPremiumInDressedWage ? item.RegularPremiumSnapshot : 0m)
            + (rule.IncludeRoadAllowanceInDressedWage ? item.RoadAllowanceSnapshot : 0m)
            + (rule.IncludeMealAllowanceInDressedWage ? item.MealAllowanceSnapshot : 0m)
            + (rule.IncludeOtherRegularBenefitsInDressedWage ? item.OtherRegularBenefitsSnapshot : 0m);
        var dailyGross = item.MonthlyGrossSalarySnapshot / rule.DailyWageDivisor;
        var dressedDaily = dressedMonthlyWage / rule.DailyWageDivisor;

        var unusedLeavePayment = Round(dailyGross * item.UnusedLeaveDays);
        var severancePayment = severanceEligible && serviceDays >= rule.MinimumSeveranceServiceDays
            ? Round(Math.Min(dressedMonthlyWage, rule.SeveranceCeiling)
                * serviceDays / 365m
                * rule.SeveranceDaysPerYear / rule.DailyWageDivisor)
            : 0m;

        var noticeDays = ResolveNoticeDays(employmentStart, item.TerminationDate, rule);
        var noticePayment = noticeEligible
            && item.ContractTypeSnapshot == EmploymentContractType.Indefinite
            && item.NoticeHandling == NoticeHandling.PayableToEmployee
            ? Round(dressedDaily * noticeDays)
            : 0m;

        return new TerminationDraftCalculation(
            serviceDays,
            Round(dressedMonthlyWage),
            noticeDays,
            unusedLeavePayment,
            severancePayment,
            noticePayment);
    }

    private static int ResolveNoticeDays(
        DateOnly employmentStart,
        DateOnly terminationDate,
        TerminationCalculationRule rule) =>
        terminationDate < employmentStart.AddMonths(rule.NoticeFirstThresholdMonths)
            ? rule.NoticeDaysUnderSixMonths
            : terminationDate < employmentStart.AddMonths(rule.NoticeSecondThresholdMonths)
                ? rule.NoticeDaysSixToEighteenMonths
                : terminationDate < employmentStart.AddMonths(rule.NoticeThirdThresholdMonths)
                    ? rule.NoticeDaysEighteenToThirtySixMonths
                    : rule.NoticeDaysOverThirtySixMonths;

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

internal static class TerminationCalculationRuleMappings
{
    public static TerminationCalculationRuleDto ToDto(this TerminationCalculationRule item) => new(
        item.Id,
        item.Version,
        item.EffectiveFrom,
        item.Status == TerminationCalculationRuleStatus.Approved ? "approved" : "draft",
        item.DailyWageDivisor,
        item.SeveranceDaysPerYear,
        item.MinimumSeveranceServiceDays,
        item.SeveranceCeiling,
        item.NoticeDaysUnderSixMonths,
        item.NoticeDaysSixToEighteenMonths,
        item.NoticeDaysEighteenToThirtySixMonths,
        item.NoticeDaysOverThirtySixMonths,
        item.NoticeFirstThresholdMonths,
        item.NoticeSecondThresholdMonths,
        item.NoticeThirdThresholdMonths,
        item.IncludeRegularPremiumInDressedWage,
        item.IncludeRoadAllowanceInDressedWage,
        item.IncludeMealAllowanceInDressedWage,
        item.IncludeOtherRegularBenefitsInDressedWage,
        item.SourceReference,
        item.ReviewNotes,
        item.ApprovedAtUtc,
        item.ApprovedByUserId);
}
