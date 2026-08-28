using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Entities.Payroll;
using IKPro.Domain.Enums;
using IKPro.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IKPro.Application.Features.Payroll.Periods;

// --- girdi güncelleme ---

/// <summary>Bordro girdi satırı güncelleme gövdesi (maaş + ek ödemeler + bayraklar).</summary>
public sealed record PayrollRowInputModel(
    decimal GrossSalary,
    int WorkedDays,
    int OvertimeHours,
    decimal PremiumPay = 0m,
    decimal RoadAllowance = 0m,
    decimal MealAllowance = 0m,
    decimal BenefitPay = 0m,
    decimal SpecialDeductions = 0m,
    decimal PreviousTaxBase = 0m,
    bool IbanComplete = true,
    string? Notes = null);

public sealed class PayrollRowInputModelValidator : AbstractValidator<PayrollRowInputModel>
{
    public PayrollRowInputModelValidator()
    {
        RuleFor(x => x.GrossSalary).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.WorkedDays).InclusiveBetween(0, 31);
        RuleFor(x => x.OvertimeHours).InclusiveBetween(0, 300);
        RuleFor(x => x.PremiumPay).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.SpecialDeductions).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.PreviousTaxBase).GreaterThanOrEqualTo(0m);
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

public sealed class UpdatePayrollRowCommandHandler(IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<UpdatePayrollRowCommand, PayrollRowDto>
{
    public async Task<PayrollRowDto> Handle(UpdatePayrollRowCommand request, CancellationToken cancellationToken)
    {
        var row = await PayrollRowGuards.GetMutableRowAsync(
            context, request.PeriodId, request.RowId, cancellationToken);

        if (row.ApprovalStatus == PayrollApprovalStatus.Approved)
        {
            throw new ConflictException("Onaylanmış bordro satırı düzenlenemez.");
        }

        var m = request.Model;
        row.GrossSalary = m.GrossSalary;
        row.WorkedDays = m.WorkedDays;
        row.OvertimeHours = m.OvertimeHours;
        row.PremiumPay = m.PremiumPay;
        row.RoadAllowance = m.RoadAllowance;
        row.MealAllowance = m.MealAllowance;
        row.BenefitPay = m.BenefitPay;
        row.SpecialDeductions = m.SpecialDeductions;
        row.PreviousTaxBase = m.PreviousTaxBase;
        row.IbanComplete = m.IbanComplete;
        row.Notes = m.Notes;

        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, row.Period!, [row], cancellationToken);
        var parameters = row.Period!.Settings!.ToEngineParameters();
        var calculation = engine.Calculate(row.ToEngineInput(), parameters);

        // Girdi değişti: tüm sunucu kapıları açıksa Kontrol, değilse Eksik Veri.
        row.ApprovalStatus = PayrollRowReadiness.IsReady(row, calculation)
            ? PayrollApprovalStatus.Control
            : PayrollApprovalStatus.MissingData;

        await context.SaveChangesAsync(cancellationToken);

        return PayrollPeriodProjector.ToRow(row, engine, parameters);
    }
}

// --- dönem kontrolü (check) ---

/// <summary>Onaysız satırların durumunu toplu günceller: eksik veri → Eksik Veri, değilse Onaya Hazır.</summary>
public sealed record RunPayrollCheckCommand(int PeriodId) : IRequest<PayrollPeriodDetailDto>;

public sealed class RunPayrollCheckCommandHandler(IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<RunPayrollCheckCommand, PayrollPeriodDetailDto>
{
    public async Task<PayrollPeriodDetailDto> Handle(
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
        var parameters = period.Settings!.ToEngineParameters();

        foreach (var row in pendingRows)
        {
            var calculation = engine.Calculate(row.ToEngineInput(), parameters);
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

public sealed class ApprovePayrollRowCommandHandler(IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<ApprovePayrollRowCommand, PayrollRowDto>
{
    public async Task<PayrollRowDto> Handle(
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
        var parameters = row.Period!.Settings!.ToEngineParameters();
        var calc = engine.Calculate(row.ToEngineInput(), parameters);
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
        result.WarningsJson = JsonSerializer.Serialize(calc.Warnings);
        result.CalculatedAtUtc = DateTime.UtcNow;

        row.Result = result;
        row.ApprovalStatus = PayrollApprovalStatus.Approved;

        await context.SaveChangesAsync(cancellationToken);
        return PayrollPeriodProjector.ToRow(row, engine, parameters);
    }
}

// --- dönem gönderimi ---

/// <summary>Tüm satırlar onaylıysa dönemi kapatır (Approved).</summary>
public sealed record SubmitPayrollPeriodCommand(int PeriodId) : IRequest<PayrollPeriodDetailDto>;

public sealed class SubmitPayrollPeriodCommandHandler(IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<SubmitPayrollPeriodCommand, PayrollPeriodDetailDto>
{
    public async Task<PayrollPeriodDetailDto> Handle(
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
