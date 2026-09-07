using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PayrollSettingsEntity = IKPro.Domain.Entities.Payroll.PayrollSettings;

namespace IKPro.Application.Features.Payroll.Settings;

public static class PayrollSettingsResolver
{
    /// <summary>Verilen tarihte yürürlükteki, uzman tarafından onaylanmış mevzuat seti.</summary>
    public static async Task<PayrollSettingsEntity> ResolveAsync(
        IApplicationDbContext context, DateOnly? asOf, CancellationToken cancellationToken)
    {
        var query = context.PayrollSettings
            .Include(s => s.TaxBrackets)
            .Where(s => s.Status == PayrollSettingsStatus.Approved);
        var effectiveDate = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

        var settings = await query
            .Where(s => s.EffectiveFrom <= effectiveDate)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            throw new NotFoundException(
                "Onaylı bordro mevzuat seti", effectiveDate.ToString("yyyy-MM-dd"));
        }

        return settings;
    }

    public static PayrollSettingsDto ToDto(PayrollSettingsEntity s) => new(
        s.Id,
        s.Version,
        s.Status == PayrollSettingsStatus.Approved ? "approved" : "draft",
        s.EffectiveFrom,
        s.OvertimeMultiplier,
        s.MonthlyWorkingHours,
        s.DefaultWorkedDays,
        s.SgkEmployeeRate,
        s.UnemploymentEmployeeRate,
        s.SgkEmployerRate,
        s.UnemploymentEmployerRate,
        s.StampTaxRate,
        s.SgkBaseMin,
        s.SgkBaseMax,
        s.MonthlyMinWageIncomeTaxExemption,
        s.MonthlyMinWageStampTaxExemption,
        s.MinWageGross,
        s.MealSgkDailyExemption,
        s.MealIncomeTaxDailyExemption,
        s.OtherSectorEmployerDiscountPoints,
        s.ManufacturingEmployerDiscountPoints,
        s.FirstDegreeDisabilityDeduction,
        s.SecondDegreeDisabilityDeduction,
        s.ThirdDegreeDisabilityDeduction,
        s.TaxBrackets.OrderBy(b => b.Order)
            .Select(b => new TaxBracketDto(b.Order, b.Limit, b.Base, b.BaseTax, b.Rate))
            .ToList(),
        s.SourceReference,
        s.ReviewNotes,
        s.ApprovedAtUtc,
        s.ApprovedByUserId);
}

public sealed record GetPayrollSettingsQuery(DateOnly? AsOf = null) : IRequest<PayrollSettingsDto>;

public sealed class GetPayrollSettingsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollSettingsQuery, PayrollSettingsDto>
{
    public async Task<PayrollSettingsDto> Handle(
        GetPayrollSettingsQuery request, CancellationToken cancellationToken)
        => PayrollSettingsResolver.ToDto(
            await PayrollSettingsResolver.ResolveAsync(context, request.AsOf, cancellationToken));
}

/// <summary>Taslak mevzuat seti oluşturur veya beklenen sürümle günceller.</summary>
public sealed record UpdatePayrollSettingsCommand(
    DateOnly EffectiveFrom,
    decimal OvertimeMultiplier,
    decimal MonthlyWorkingHours,
    int DefaultWorkedDays,
    decimal SgkEmployeeRate,
    decimal UnemploymentEmployeeRate,
    decimal SgkEmployerRate,
    decimal UnemploymentEmployerRate,
    decimal StampTaxRate,
    decimal SgkBaseMin,
    decimal SgkBaseMax,
    decimal MonthlyMinWageIncomeTaxExemption,
    decimal MonthlyMinWageStampTaxExemption,
    decimal MinWageGross,
    decimal MealSgkDailyExemption,
    decimal MealIncomeTaxDailyExemption,
    decimal OtherSectorEmployerDiscountPoints,
    decimal ManufacturingEmployerDiscountPoints,
    decimal FirstDegreeDisabilityDeduction,
    decimal SecondDegreeDisabilityDeduction,
    decimal ThirdDegreeDisabilityDeduction,
    string SourceReference,
    string? ReviewNotes = null,
    int? ExpectedVersion = null,
    IReadOnlyList<TaxBracketDto>? TaxBrackets = null) : IRequest<PayrollSettingsDto>;

public sealed class UpdatePayrollSettingsCommandValidator : AbstractValidator<UpdatePayrollSettingsCommand>
{
    public UpdatePayrollSettingsCommandValidator()
    {
        RuleFor(x => x.EffectiveFrom)
            .Must(d => d.Year is >= 2000 and <= 2100)
            .WithMessage("Yürürlük tarihi 2000-2100 aralığında olmalı.")
            .Must(d => d.Day == 1)
            .WithMessage("Aylık bordro mevzuat seti ayın ilk gününden yürürlüğe girmelidir.");
        RuleFor(x => x.OvertimeMultiplier).InclusiveBetween(1m, 5m);
        RuleFor(x => x.MonthlyWorkingHours).GreaterThan(0m);
        RuleFor(x => x.DefaultWorkedDays).Equal(30)
            .WithMessage("Standart 4/a profilinde aylık SGK prim günü 30 olmalıdır.");
        RuleFor(x => x.SgkEmployeeRate).InclusiveBetween(0m, 100m);
        RuleFor(x => x.UnemploymentEmployeeRate).InclusiveBetween(0m, 100m);
        RuleFor(x => x.SgkEmployerRate).InclusiveBetween(0m, 100m);
        RuleFor(x => x.UnemploymentEmployerRate).InclusiveBetween(0m, 100m);
        RuleFor(x => x.StampTaxRate).InclusiveBetween(0m, 100m);
        RuleFor(x => x.SgkBaseMin).GreaterThan(0m);
        RuleFor(x => x.MinWageGross)
            .Equal(x => x.SgkBaseMin)
            .WithMessage("Standart 4/a profilinde aylık SGK alt sınırı brüt asgari ücrete eşit olmalı.");
        RuleFor(x => x.SgkBaseMax)
            .GreaterThan(x => x.SgkBaseMin)
            .WithMessage("SGK üst sınır alt sınırdan büyük olmalı.");
        RuleFor(x => x.MonthlyMinWageIncomeTaxExemption).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.MonthlyMinWageStampTaxExemption).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.MealSgkDailyExemption).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.MealIncomeTaxDailyExemption).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.OtherSectorEmployerDiscountPoints)
            .InclusiveBetween(0m, 100m)
            .LessThanOrEqualTo(x => x.SgkEmployerRate);
        RuleFor(x => x.ManufacturingEmployerDiscountPoints)
            .InclusiveBetween(0m, 100m)
            .LessThanOrEqualTo(x => x.SgkEmployerRate);
        RuleFor(x => x.FirstDegreeDisabilityDeduction).GreaterThan(0m);
        RuleFor(x => x.SecondDegreeDisabilityDeduction).GreaterThan(0m);
        RuleFor(x => x.ThirdDegreeDisabilityDeduction).GreaterThan(0m);
        RuleFor(x => x)
            .Must(x => x.FirstDegreeDisabilityDeduction >= x.SecondDegreeDisabilityDeduction
                && x.SecondDegreeDisabilityDeduction >= x.ThirdDegreeDisabilityDeduction)
            .WithMessage("Engellilik indirimleri birinci, ikinci ve üçüncü derece sırasıyla azalmamalıdır.");
        RuleFor(x => x)
            .Must(x => x.SgkEmployeeRate + x.UnemploymentEmployeeRate < 100m)
            .WithMessage("Çalışan SGK ve işsizlik oranları toplamı %100'den küçük olmalı.");
        RuleFor(x => x.SourceReference)
            .NotEmpty()
            .MaximumLength(2000)
            .Must(AllOfficialHttpsReferences)
            .WithMessage("Her mevzuat kaynağı resmî bir gov.tr HTTPS adresi olmalıdır.");
        RuleFor(x => x.ReviewNotes)
            .NotEmpty()
            .WithMessage("Uzman inceleme kaydı zorunludur.")
            .MaximumLength(2000);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).When(x => x.ExpectedVersion is not null);
        RuleFor(x => x.TaxBrackets)
            .NotNull()
            .Must(HaveValidTaxBrackets)
            .WithMessage("Vergi dilimleri sıralı, artan, kesintisiz ve matematiksel olarak tutarlı olmalıdır.");
    }

    private static bool AllOfficialHttpsReferences(string? sourceReference)
    {
        var references = sourceReference?.Split(
            ';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        return references.Length > 0 && references.All(value =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && (uri.Host.Equals("gov.tr", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".gov.tr", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HaveValidTaxBrackets(IReadOnlyList<TaxBracketDto>? brackets)
    {
        if (brackets is not { Count: > 0 } || brackets[^1].Limit is not null)
            return false;

        for (var index = 0; index < brackets.Count; index++)
        {
            var current = brackets[index];
            if (current.Order != index + 1 || current.Rate is <= 0m or > 1m)
                return false;
            if (index < brackets.Count - 1 &&
                (current.Limit is null || current.Limit <= current.Base))
                return false;

            if (index == 0)
            {
                if (current.Base != 0m || current.BaseTax != 0m)
                    return false;
                continue;
            }

            var previous = brackets[index - 1];
            if (previous.Limit is null || current.Base != previous.Limit.Value ||
                current.Rate < previous.Rate)
                return false;

            var expectedBaseTax = decimal.Round(
                previous.BaseTax + (previous.Limit.Value - previous.Base) * previous.Rate,
                2,
                MidpointRounding.AwayFromZero);
            if (current.BaseTax != expectedBaseTax)
                return false;
        }

        return true;
    }
}

public sealed class UpdatePayrollSettingsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdatePayrollSettingsCommand, PayrollSettingsDto>
{
    public async Task<PayrollSettingsDto> Handle(
        UpdatePayrollSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await context.PayrollSettings
            .Include(s => s.TaxBrackets)
            .FirstOrDefaultAsync(s => s.EffectiveFrom == request.EffectiveFrom, cancellationToken);

        if (settings is null)
        {
            if (request.ExpectedVersion is not null)
                throw new ConflictException("Yeni mevzuat seti için beklenen sürüm gönderilmemelidir.");

            settings = new PayrollSettingsEntity { EffectiveFrom = request.EffectiveFrom };
            context.PayrollSettings.Add(settings);
        }
        else
        {
            if (settings.Status == PayrollSettingsStatus.Approved)
                throw new ConflictException(
                    "Onaylanmış bordro mevzuat seti değiştirilemez; yeni yürürlük tarihli sürüm oluşturun.");
            if (settings.Version != request.ExpectedVersion)
                throw new ConflictException("Mevzuat seti başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
            settings.Version++;
        }

        settings.OvertimeMultiplier = request.OvertimeMultiplier;
        settings.MonthlyWorkingHours = request.MonthlyWorkingHours;
        settings.DefaultWorkedDays = request.DefaultWorkedDays;
        settings.SgkEmployeeRate = request.SgkEmployeeRate;
        settings.UnemploymentEmployeeRate = request.UnemploymentEmployeeRate;
        settings.SgkEmployerRate = request.SgkEmployerRate;
        settings.UnemploymentEmployerRate = request.UnemploymentEmployerRate;
        settings.StampTaxRate = request.StampTaxRate;
        settings.SgkBaseMin = request.SgkBaseMin;
        settings.SgkBaseMax = request.SgkBaseMax;
        settings.MonthlyMinWageIncomeTaxExemption = request.MonthlyMinWageIncomeTaxExemption;
        settings.MonthlyMinWageStampTaxExemption = request.MonthlyMinWageStampTaxExemption;
        settings.MinWageGross = request.MinWageGross;
        settings.MealSgkDailyExemption = request.MealSgkDailyExemption;
        settings.MealIncomeTaxDailyExemption = request.MealIncomeTaxDailyExemption;
        settings.OtherSectorEmployerDiscountPoints = request.OtherSectorEmployerDiscountPoints;
        settings.ManufacturingEmployerDiscountPoints = request.ManufacturingEmployerDiscountPoints;
        settings.FirstDegreeDisabilityDeduction = request.FirstDegreeDisabilityDeduction;
        settings.SecondDegreeDisabilityDeduction = request.SecondDegreeDisabilityDeduction;
        settings.ThirdDegreeDisabilityDeduction = request.ThirdDegreeDisabilityDeduction;
        settings.SourceReference = request.SourceReference.Trim();
        settings.ReviewNotes = string.IsNullOrWhiteSpace(request.ReviewNotes)
            ? null
            : request.ReviewNotes.Trim();

        settings.TaxBrackets.Clear();
        var order = 1;
        foreach (var bracket in request.TaxBrackets!)
        {
            settings.TaxBrackets.Add(new Domain.Entities.Payroll.IncomeTaxBracket
            {
                Order = order++,
                Limit = bracket.Limit,
                Base = bracket.Base,
                BaseTax = bracket.BaseTax,
                Rate = bracket.Rate,
            });
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Mevzuat seti başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
        }

        return PayrollSettingsResolver.ToDto(settings);
    }
}

public sealed record ApprovePayrollSettingsCommand(int Id, int ExpectedVersion)
    : IRequest<PayrollSettingsDto>;

public sealed class ApprovePayrollSettingsCommandValidator
    : AbstractValidator<ApprovePayrollSettingsCommand>
{
    public ApprovePayrollSettingsCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ApprovePayrollSettingsCommandHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser)
    : IRequestHandler<ApprovePayrollSettingsCommand, PayrollSettingsDto>
{
    public async Task<PayrollSettingsDto> Handle(
        ApprovePayrollSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var settings = await context.PayrollSettings
            .Include(s => s.TaxBrackets)
            .SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Bordro mevzuat seti", request.Id);

        if (settings.Status != PayrollSettingsStatus.Draft)
            throw new ConflictException("Bordro mevzuat seti zaten onaylanmış.");
        if (settings.Version != request.ExpectedVersion)
            throw new ConflictException("Mevzuat seti başka bir işlem tarafından değiştirildi; ekranı yenileyin.");
        if (settings.TaxBrackets.Count == 0 || string.IsNullOrWhiteSpace(settings.SourceReference)
            || string.IsNullOrWhiteSpace(settings.ReviewNotes))
            throw new ConflictException("Kaynak referansı, uzman inceleme kaydı ve vergi dilimleri eksik mevzuat seti onaylanamaz.");

        settings.Status = PayrollSettingsStatus.Approved;
        settings.ApprovedAtUtc = DateTime.UtcNow;
        settings.ApprovedByUserId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Bordro mevzuat seti onayı kullanıcı kimliği gerektirir.");
        settings.Version++;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Mevzuat seti başka bir işlem tarafından onaylandı; ekranı yenileyin.");
        }

        return PayrollSettingsResolver.ToDto(settings);
    }
}
