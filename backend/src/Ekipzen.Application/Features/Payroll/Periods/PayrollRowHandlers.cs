using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ekipzen.Application.Features.Payroll.Periods;

// --- girdi güncelleme ---

/// <summary>Bordro girdi satırı güncelleme gövdesi (maaş + ek ödemeler + bayraklar).</summary>
public sealed record PayrollRowInputModel(
    decimal GrossSalary,
    PayrollStatutoryProfile StatutoryProfile,
    int WorkedDays,
    bool SgkPremiumDaysConfirmed,
    int OvertimeHours,
    bool OvertimeTreatmentConfirmed,
    decimal PremiumPay = 0m,
    decimal RoadAllowance = 0m,
    decimal MealAllowance = 0m,
    int MealDays = 0,
    PayrollMealTreatment MealTreatment = PayrollMealTreatment.Unspecified,
    decimal BenefitPay = 0m,
    bool BenefitPayFullyTaxableConfirmed = false,
    bool ApplyMinimumWageTaxExemption = true,
    EmployerSgkDiscountType EmployerSgkDiscount = EmployerSgkDiscountType.None,
    string? EmployerSgkDiscountReference = null,
    PayrollDisabilityReliefType DisabilityReliefType = PayrollDisabilityReliefType.Unspecified,
    string? DisabilityReliefReference = null,
    bool OtherIncomeTaxBaseDeductionsConfirmedNone = false,
    decimal PreviousEmployerIncomeTaxBase = 0m,
    string? PreviousEmployerIncomeTaxBaseReference = null,
    bool CumulativeIncomeTaxBaseConfirmed = false,
    decimal SpecialDeductions = 0m,
    bool IbanComplete = true,
    string? Notes = null);

public sealed class PayrollRowInputModelValidator : AbstractValidator<PayrollRowInputModel>
{
    public PayrollRowInputModelValidator()
    {
        RuleFor(x => x.GrossSalary).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.StatutoryProfile).IsInEnum();
        RuleFor(x => x.WorkedDays).InclusiveBetween(0, 30);
        RuleFor(x => x.MealDays).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.WorkedDays)
            .WithMessage("Yemek fiili gün sayısı SGK prim gününü aşamaz.");
        RuleFor(x => x.MealTreatment).IsInEnum();
        RuleFor(x => x.EmployerSgkDiscount).IsInEnum();
        RuleFor(x => x.EmployerSgkDiscountReference).MaximumLength(500);
        RuleFor(x => x.DisabilityReliefType).IsInEnum();
        RuleFor(x => x.DisabilityReliefReference).MaximumLength(500);
        RuleFor(x => x.PreviousEmployerIncomeTaxBase).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.PreviousEmployerIncomeTaxBaseReference).MaximumLength(500);
        RuleFor(x => x.OvertimeHours).InclusiveBetween(0, 300);
        RuleFor(x => x.PremiumPay).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.SpecialDeductions).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed record UpdatePayrollRowCommand(int PeriodId, int RowId, PayrollRowInputModel Model)
    : IRequest<PayrollRowDto>;

public sealed class UpdatePayrollRowCommandValidator : AbstractValidator<UpdatePayrollRowCommand>
{
    public UpdatePayrollRowCommandValidator()
    {
        RuleFor(x => x.Model).NotNull().SetValidator(new PayrollRowInputModelValidator());
    }
}

public sealed class UpdatePayrollRowCommandHandler(
    IApplicationDbContext context,
    IPayrollEngine engine,
    IDatabaseLock databaseLock)
    : IRequestHandler<UpdatePayrollRowCommand, PayrollRowDto>
{
    public Task<PayrollRowDto> Handle(
        UpdatePayrollRowCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync(
            PayrollRowGuards.LockResource(request.PeriodId),
            lockedCancellationToken => UpdateAsync(request, lockedCancellationToken),
            cancellationToken);

    private async Task<PayrollRowDto> UpdateAsync(
        UpdatePayrollRowCommand request, CancellationToken cancellationToken)
    {
        var row = await PayrollRowGuards.GetMutableRowAsync(
            context, request.PeriodId, request.RowId, cancellationToken);

        if (row.ApprovalStatus == PayrollApprovalStatus.Approved)
        {
            throw new ConflictException("Onaylanmış bordro satırı düzenlenemez.");
        }

        var m = request.Model;
        row.GrossSalary = m.GrossSalary;
        row.StatutoryProfile = m.StatutoryProfile;
        row.WorkedDays = m.WorkedDays;
        row.SgkPremiumDaysConfirmed = m.SgkPremiumDaysConfirmed;
        row.OvertimeHours = m.OvertimeHours;
        row.OvertimeTreatmentConfirmed = m.OvertimeTreatmentConfirmed;
        row.PremiumPay = m.PremiumPay;
        row.RoadAllowance = m.RoadAllowance;
        row.MealAllowance = m.MealAllowance;
        row.MealDays = m.MealDays;
        row.MealTreatment = m.MealTreatment;
        row.BenefitPay = m.BenefitPay;
        row.BenefitPayFullyTaxableConfirmed = m.BenefitPayFullyTaxableConfirmed;
        row.ApplyMinimumWageTaxExemption = m.ApplyMinimumWageTaxExemption;
        row.EmployerSgkDiscount = m.EmployerSgkDiscount;
        row.EmployerSgkDiscountReference = string.IsNullOrWhiteSpace(m.EmployerSgkDiscountReference)
            ? null
            : m.EmployerSgkDiscountReference.Trim();
        row.DisabilityReliefType = m.DisabilityReliefType;
        row.DisabilityReliefReference = string.IsNullOrWhiteSpace(m.DisabilityReliefReference)
            ? null
            : m.DisabilityReliefReference.Trim();
        row.OtherIncomeTaxBaseDeductionsConfirmedNone = m.OtherIncomeTaxBaseDeductionsConfirmedNone;
        row.PreviousEmployerIncomeTaxBase = m.PreviousEmployerIncomeTaxBase;
        row.PreviousEmployerIncomeTaxBaseReference = string.IsNullOrWhiteSpace(m.PreviousEmployerIncomeTaxBaseReference)
            ? null
            : m.PreviousEmployerIncomeTaxBaseReference.Trim();
        row.CumulativeIncomeTaxBaseConfirmed = m.CumulativeIncomeTaxBaseConfirmed;
        row.SpecialDeductions = m.SpecialDeductions;
        row.IbanComplete = m.IbanComplete;
        row.Notes = m.Notes;

        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, row.Period!, [row], cancellationToken);
        await PayrollCumulativeTaxBase.RefreshAsync(
            context, [row], row.Period!.Year, row.Period.Month, cancellationToken);
        var parameters = row.Period!.Settings!.ToEngineParameters();
        var calculation = engine.Calculate(row.ToEngineInput(row.Period!.MinimumWageIncomeTaxBaseBefore), parameters);

        // Girdi değişti: tüm sunucu kapıları açıksa Kontrol, değilse Eksik Veri.
        row.ApprovalStatus = PayrollRowReadiness.IsReady(row, calculation)
            ? PayrollApprovalStatus.Control
            : PayrollApprovalStatus.MissingData;

        await context.SaveChangesAsync(cancellationToken);

        return PayrollPeriodProjector.ToRow(
            row, engine, parameters, row.Period!.MinimumWageIncomeTaxBaseBefore);
    }
}

// --- dönem kontrolü (check) ---

/// <summary>Onaysız satırların durumunu toplu günceller: eksik veri → Eksik Veri, değilse Onaya Hazır.</summary>
public sealed record RunPayrollCheckCommand(int PeriodId) : IRequest<PayrollPeriodDetailDto>;

public sealed class RunPayrollCheckCommandHandler(
    IApplicationDbContext context,
    IPayrollEngine engine,
    IDatabaseLock databaseLock)
    : IRequestHandler<RunPayrollCheckCommand, PayrollPeriodDetailDto>
{
    public Task<PayrollPeriodDetailDto> Handle(
        RunPayrollCheckCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync(
            PayrollRowGuards.LockResource(request.PeriodId),
            lockedCancellationToken => CheckAsync(request, lockedCancellationToken),
            cancellationToken);

    private async Task<PayrollPeriodDetailDto> CheckAsync(
        RunPayrollCheckCommand request, CancellationToken cancellationToken)
    {
        var period = await context.PayrollPeriods
            .Include(p => p.Settings)!.ThenInclude(s => s!.TaxBrackets)
            .Include(p => p.Employees).ThenInclude(row => row.Employee)
            .FirstOrDefaultAsync(p => p.Id == request.PeriodId, cancellationToken)
            ?? throw new NotFoundException("Bordro dönemi", request.PeriodId);

        PayrollRowGuards.EnsurePeriodOpen(period);

        var pendingRows = period.Employees
            .Where(r => r.ApprovalStatus != PayrollApprovalStatus.Approved)
            .ToList();
        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, period, pendingRows, cancellationToken);
        await PayrollCumulativeTaxBase.RefreshAsync(
            context, pendingRows, period.Year, period.Month, cancellationToken);
        var parameters = period.Settings!.ToEngineParameters();

        foreach (var row in pendingRows)
        {
            var calculation = engine.Calculate(row.ToEngineInput(row.Period!.MinimumWageIncomeTaxBaseBefore), parameters);
            row.ApprovalStatus = PayrollRowReadiness.IsReady(row, calculation)
                ? PayrollApprovalStatus.ReadyForApproval
                : PayrollApprovalStatus.MissingData;
        }

        period.Status = PayrollPeriodStatus.Control;
        await context.SaveChangesAsync(cancellationToken);

        return await PayrollPeriodProjector.BuildDetailAsync(context, engine, period.Id, cancellationToken);
    }
}

// --- satır onayı (sonuç snapshot'ı) ---

public sealed record ApprovePayrollRowCommand(int PeriodId, int RowId) : IRequest<PayrollRowDto>;

public sealed class ApprovePayrollRowCommandHandler(
    IApplicationDbContext context,
    IPayrollEngine engine,
    IDatabaseLock databaseLock)
    : IRequestHandler<ApprovePayrollRowCommand, PayrollRowDto>
{
    public Task<PayrollRowDto> Handle(
        ApprovePayrollRowCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync(
            PayrollRowGuards.LockResource(request.PeriodId),
            lockedCancellationToken => ApproveAsync(request, lockedCancellationToken),
            cancellationToken);

    private async Task<PayrollRowDto> ApproveAsync(
        ApprovePayrollRowCommand request, CancellationToken cancellationToken)
    {
        var row = await PayrollRowGuards.GetMutableRowAsync(
            context, request.PeriodId, request.RowId, cancellationToken);

        if (row.ApprovalStatus == PayrollApprovalStatus.Approved)
        {
            throw new ConflictException("Satır zaten onaylı.");
        }

        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, row.Period!, [row], cancellationToken);
        await PayrollCumulativeTaxBase.RefreshAsync(
            context, [row], row.Period!.Year, row.Period.Month, cancellationToken);
        var parameters = row.Period!.Settings!.ToEngineParameters();
        var calc = engine.Calculate(row.ToEngineInput(row.Period!.MinimumWageIncomeTaxBaseBefore), parameters);
        PayrollRowReadiness.EnsureApprovable(row, calc);

        var result = row.Result ?? new PayrollResult { PayrollEmployeeId = row.Id };
        result.HourlyRate = calc.HourlyRate;
        result.OvertimePay = calc.OvertimePay;
        result.AdditionalPay = calc.AdditionalPay;
        result.BaseGross = calc.BaseGross;
        result.GrossEarnings = calc.GrossEarnings;
        result.SgkBase = calc.SgkBase;
        result.SgkEmployee = calc.SgkEmployee;
        result.UnemploymentEmployee = calc.UnemploymentEmployee;
        result.IncomeTaxBase = calc.IncomeTaxBase;
        result.IncomeTax = calc.IncomeTax;
        result.StampTax = calc.StampTax;
        result.TotalDeductions = calc.TotalDeductions;
        result.NetPay = calc.NetPay;
        result.EmployerSgk = calc.EmployerSgk;
        result.EmployerUnemployment = calc.EmployerUnemployment;
        result.EmployerCost = calc.EmployerCost;
        result.SgkExemptMealAllowance = calc.SgkExemptMealAllowance;
        result.IncomeTaxExemptMealAllowance = calc.IncomeTaxExemptMealAllowance;
        result.MinimumWageIncomeTaxExemption = calc.MinimumWageIncomeTaxExemption;
        result.MinimumWageStampTaxExemption = calc.MinimumWageStampTaxExemption;
        result.AppliedEmployerSgkRate = calc.AppliedEmployerSgkRate;
        result.DisabilityIncomeTaxBaseDeduction = calc.DisabilityIncomeTaxBaseDeduction;
        result.WarningsJson = JsonSerializer.Serialize(calc.Warnings);
        result.CalculatedAtUtc = DateTime.UtcNow;

        row.Result = result;
        row.ApprovalStatus = PayrollApprovalStatus.Approved;

        await context.SaveChangesAsync(cancellationToken);
        return PayrollPeriodProjector.ToRow(
            row, engine, parameters, row.Period!.MinimumWageIncomeTaxBaseBefore);
    }
}

// --- dönem gönderimi ---

/// <summary>Tüm satırlar onaylıysa dönemi kapatır (Approved).</summary>
public sealed record SubmitPayrollPeriodCommand(int PeriodId) : IRequest<PayrollPeriodDetailDto>;

public sealed class SubmitPayrollPeriodCommandHandler(
    IApplicationDbContext context,
    IPayrollEngine engine,
    IDatabaseLock databaseLock)
    : IRequestHandler<SubmitPayrollPeriodCommand, PayrollPeriodDetailDto>
{
    public Task<PayrollPeriodDetailDto> Handle(
        SubmitPayrollPeriodCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync(
            PayrollRowGuards.LockResource(request.PeriodId),
            lockedCancellationToken => SubmitAsync(request, lockedCancellationToken),
            cancellationToken);

    private async Task<PayrollPeriodDetailDto> SubmitAsync(
        SubmitPayrollPeriodCommand request, CancellationToken cancellationToken)
    {
        var period = await context.PayrollPeriods
            .Include(p => p.Employees)
            .FirstOrDefaultAsync(p => p.Id == request.PeriodId, cancellationToken)
            ?? throw new NotFoundException("Bordro dönemi", request.PeriodId);

        PayrollRowGuards.EnsurePeriodOpen(period);

        var unapproved = period.Employees.Count(r => r.ApprovalStatus != PayrollApprovalStatus.Approved);
        if (unapproved > 0)
        {
            throw new ConflictException($"Dönem gönderilemez: {unapproved} satır henüz onaylı değil.");
        }

        period.Status = PayrollPeriodStatus.Approved;
        await context.SaveChangesAsync(cancellationToken);

        return await PayrollPeriodProjector.BuildDetailAsync(context, engine, period.Id, cancellationToken);
    }
}

/// <summary>Satır/dönem ortak korumaları.</summary>
public static class PayrollRowGuards
{
    public static string LockResource(int periodId) => $"payroll-period:{periodId}";

    public static void EnsurePeriodOpen(PayrollPeriod period)
    {
        if (period.Status is PayrollPeriodStatus.Approved or PayrollPeriodStatus.Closed)
        {
            throw new ConflictException("Onaylanmış/kapatılmış dönem üzerinde değişiklik yapılamaz.");
        }
    }

    public static async Task<PayrollEmployee> GetMutableRowAsync(
        IApplicationDbContext context, int periodId, int rowId, CancellationToken cancellationToken)
    {
        var row = await context.PayrollEmployees
            .Include(pe => pe.Period)!.ThenInclude(p => p!.Settings)!.ThenInclude(s => s!.TaxBrackets)
            .Include(pe => pe.Employee)!.ThenInclude(e => e!.Department)
            .Include(pe => pe.Result)
            .FirstOrDefaultAsync(pe => pe.Id == rowId && pe.PayrollPeriodId == periodId, cancellationToken)
            ?? throw new NotFoundException("Bordro satırı", rowId);

        EnsurePeriodOpen(row.Period!);
        return row;
    }
}
