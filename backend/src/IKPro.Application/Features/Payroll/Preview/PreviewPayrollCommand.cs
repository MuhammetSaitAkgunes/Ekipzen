using FluentValidation;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Payroll.Settings;
using IKPro.Domain.Services;
using IKPro.Domain.Enums;
using MediatR;

namespace IKPro.Application.Features.Payroll.Preview;

/// <summary>
/// Tekil bordro hesabı (payroll.js calculateSinglePayrollScenario) — kalıcılaştırılmaz.
/// Çarpan verilirse dönem varsayılanının yerine geçer.
/// </summary>
public sealed record PreviewPayrollCommand(
    decimal GrossSalary,
    PayrollStatutoryProfile StatutoryProfile,
    int WorkedDays,
    decimal OvertimeHours = 0m,
    decimal? OvertimeMultiplier = null,
    decimal PremiumPay = 0m,
    decimal RoadAllowance = 0m,
    decimal MealAllowance = 0m,
    int MealDays = 0,
    PayrollMealTreatment MealTreatment = PayrollMealTreatment.Unspecified,
    decimal BenefitPay = 0m,
    bool BenefitPayFullyTaxableConfirmed = false,
    bool ApplyMinimumWageTaxExemption = true,
    PayrollDisabilityReliefType DisabilityReliefType = PayrollDisabilityReliefType.Unspecified,
    EmployerSgkDiscountType EmployerSgkDiscount = EmployerSgkDiscountType.None,
    decimal SpecialDeductions = 0m,
    decimal PreviousTaxBase = 0m,
    /// <summary>Hangi tarihte yürürlükteki parametrelerle hesaplansın (boşsa bugün).</summary>
    DateOnly? AsOf = null) : IRequest<PayrollCalculation>;

public sealed class PreviewPayrollCommandValidator : AbstractValidator<PreviewPayrollCommand>
{
    public PreviewPayrollCommandValidator()
    {
        RuleFor(x => x.GrossSalary).GreaterThan(0m);
        RuleFor(x => x.StatutoryProfile)
            .Equal(PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA)
            .WithMessage("Önizleme yalnız doğrulanmış standart yurt içi özel sektör 4/a profili içindir.");
        RuleFor(x => x.WorkedDays).InclusiveBetween(1, 30);
        RuleFor(x => x.OvertimeHours).InclusiveBetween(0m, 300m);
        RuleFor(x => x.MealDays).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.WorkedDays)
            .WithMessage("Yemek fiili gün sayısı SGK prim gününü aşamaz.");
        RuleFor(x => x.MealTreatment).IsInEnum();
        RuleFor(x => x.MealTreatment)
            .NotEqual(PayrollMealTreatment.Unspecified)
            .When(x => x.MealAllowance > 0m);
        RuleFor(x => x.MealDays).GreaterThan(0)
            .When(x => x.MealAllowance > 0m
                && x.MealTreatment == PayrollMealTreatment.CashExemptWhereNoMealProvided);
        RuleFor(x => x.BenefitPayFullyTaxableConfirmed).Equal(true)
            .When(x => x.BenefitPay > 0m);
        RuleFor(x => x.EmployerSgkDiscount).IsInEnum();
        RuleFor(x => x.DisabilityReliefType).IsInEnum()
            .NotEqual(PayrollDisabilityReliefType.Unspecified)
            .WithMessage("Engellilik indirimi durumu açıkça seçilmelidir.");
        RuleFor(x => x.OvertimeMultiplier).InclusiveBetween(1m, 5m).When(x => x.OvertimeMultiplier is not null);
        RuleFor(x => x.PreviousTaxBase).GreaterThanOrEqualTo(0m);
    }
}

public sealed class PreviewPayrollCommandHandler(IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<PreviewPayrollCommand, PayrollCalculation>
{
    public async Task<PayrollCalculation> Handle(
        PreviewPayrollCommand request, CancellationToken cancellationToken)
    {
        var settings = await PayrollSettingsResolver.ResolveAsync(context, request.AsOf, cancellationToken);
        var effectiveDate = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var minimumWageIncomeTaxBaseBefore =
            await Periods.PayrollMinimumWageTaxBase.CalculateBeforeAsync(
                context, effectiveDate.Year, effectiveDate.Month, cancellationToken);

        var input = new PayrollInput(
            request.GrossSalary,
            request.WorkedDays,
            request.OvertimeHours,
            request.PremiumPay,
            request.RoadAllowance,
            request.MealAllowance,
            request.BenefitPay,
            request.SpecialDeductions,
            request.PreviousTaxBase,
            OvertimeMultiplierOverride: request.OvertimeMultiplier,
            MealDays: request.MealDays,
            MinimumWageIncomeTaxBaseBefore: minimumWageIncomeTaxBaseBefore,
            ApplyMinimumWageTaxExemption: request.ApplyMinimumWageTaxExemption,
            MealTreatment: request.MealTreatment,
            EmployerSgkDiscount: request.EmployerSgkDiscount,
            StatutoryProfile: request.StatutoryProfile,
            DisabilityReliefType: request.DisabilityReliefType);

        return engine.Calculate(input, settings.ToEngineParameters());
    }
}
