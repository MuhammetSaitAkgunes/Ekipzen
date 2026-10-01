using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Payroll.Settings;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Ekipzen.Application.Features.Payroll.Periods;

// --- dönem listesi ---

public sealed record GetPayrollPeriodsQuery : IRequest<IReadOnlyList<PayrollPeriodListItemDto>>;

public sealed class GetPayrollPeriodsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollPeriodsQuery, IReadOnlyList<PayrollPeriodListItemDto>>
{
    public async Task<IReadOnlyList<PayrollPeriodListItemDto>> Handle(
        GetPayrollPeriodsQuery request, CancellationToken cancellationToken)
        => await context.PayrollPeriods
            .AsNoTracking()
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .Select(p => new PayrollPeriodListItemDto(
                p.Id, p.Name, p.Year, p.Month,
                p.Status == PayrollPeriodStatus.Draft ? "draft"
                    : p.Status == PayrollPeriodStatus.Control ? "control"
                    : p.Status == PayrollPeriodStatus.Approved ? "approved" : "closed",
                p.Employees.Count))
            .ToListAsync(cancellationToken);
}

// --- dönem oluştur ---

/// <summary>
/// Yeni bordro dönemi: aktif personel için girdi satırları üretilir.
/// Kümülatif matrah aynı yılın önceki onaylı sonuçlarından, fazla mesai saati
/// aylık puantaj özetinden (vw_MonthlyAttendanceSummary), IBAN durumu profilden gelir.
/// </summary>
public sealed record CreatePayrollPeriodCommand(int Year, int Month, int? GuidanceContentRevisionId = null, string? ExpectedPreviewToken = null) : IRequest<PayrollPeriodDetailDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "payroll-period-create"; }

public sealed class CreatePayrollPeriodCommandValidator : AbstractValidator<CreatePayrollPeriodCommand>
{
    public CreatePayrollPeriodCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.ExpectedPreviewToken).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreatePayrollPeriodCommandHandler(
    IApplicationDbContext context,
    IPayrollEngine engine,
    IDatabaseLock databaseLock, ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<CreatePayrollPeriodCommand, PayrollPeriodDetailDto>
{
    public Task<PayrollPeriodDetailDto> Handle(
        CreatePayrollPeriodCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync(
            "payroll-period-create",
            lockedCancellationToken => CreateAsync(request, lockedCancellationToken),
            cancellationToken);

    private async Task<PayrollPeriodDetailDto> CreateAsync(
        CreatePayrollPeriodCommand request, CancellationToken cancellationToken)
    {
        var (period, preview) = await PayrollPeriodDraftBuilder.BuildAsync(context, request.Year, request.Month, cancellationToken);
        if (!string.Equals(request.ExpectedPreviewToken, preview.Token, StringComparison.Ordinal))
            throw new ConflictException("Bordro başlangıç verileri değişti; önizlemeyi yenileyip yeniden kontrol edin.");

        context.PayrollPeriods.Add(period);
        FinancialOperationEvidence.RecordPeriod(context, user, guidance, period, request.GuidanceOperationKey);
        await context.SaveChangesAsync(cancellationToken);

        return await PayrollPeriodProjector.BuildDetailAsync(context, engine, period.Id, cancellationToken);
    }
}

// --- dönem detayı (canlı hesap + toplamlar + kontrol kartları) ---

public sealed record GetPayrollPeriodQuery(int Id) : IRequest<PayrollPeriodDetailDto>;

public sealed class GetPayrollPeriodQueryHandler(IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<GetPayrollPeriodQuery, PayrollPeriodDetailDto>
{
    public Task<PayrollPeriodDetailDto> Handle(GetPayrollPeriodQuery request, CancellationToken cancellationToken)
        => PayrollPeriodProjector.BuildDetailAsync(context, engine, request.Id, cancellationToken);
}

/// <summary>
/// Dönem detay projeksiyonu: onaylı satırlar kalıcı snapshot'tan (PayrollResult),
/// diğerleri motorla canlı hesaplanır. Toplamlar ve kontrol kartları
/// payroll.js getPayrollData ile birebir.
/// </summary>
public static class PayrollPeriodProjector
{
    public static async Task<PayrollPeriodDetailDto> BuildDetailAsync(
        IApplicationDbContext context, IPayrollEngine engine, int periodId, CancellationToken cancellationToken)
    {
        var period = await context.PayrollPeriods
            .AsNoTracking()
            .Include(p => p.Settings)!.ThenInclude(s => s!.TaxBrackets)
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken)
            ?? throw new NotFoundException("Bordro dönemi", periodId);

        // Ayarlar/vergi dilimleri ile çalışan satırlarını ayrı sorgulamak,
        // iki koleksiyon Include kaynaklı çalışan × vergi dilimi çarpımını önler.
        var employeeRows = await context.PayrollEmployees
            .AsNoTracking()
            .Where(row => row.PayrollPeriodId == periodId)
            .Include(row => row.Employee)!.ThenInclude(employee => employee!.Department)
            .Include(row => row.Result)
            .ToListAsync(cancellationToken);

        foreach (var row in employeeRows) row.Period = period;
        var pendingRows = employeeRows
            .Where(row => row.ApprovalStatus != PayrollApprovalStatus.Approved)
            .ToList();
        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, period, pendingRows, cancellationToken);
        await PayrollCumulativeTaxBase.RefreshAsync(
            context, pendingRows, period.Year, period.Month, cancellationToken);

        var parameters = period.Settings!.ToEngineParameters();

        var revisions = await ApprovedPayrollRevisionRules.LatestByRowAsync(
            context, employeeRows.Select(row => row.Id).ToArray(), cancellationToken);

        var rows = employeeRows
            .OrderBy(pe => pe.Employee!.FirstName).ThenBy(pe => pe.Employee!.LastName)
            .Select(pe => revisions.TryGetValue(pe.Id, out var revision)
                ? ApprovedPayrollRevisionRules.EffectiveRow(revision)
                    with { RevisionNumber = revision.RevisionNumber }
                : ToRow(pe, engine, parameters, period.MinimumWageIncomeTaxBaseBefore))
            .ToList();

        var totals = new PayrollTotalsDto(
            rows.Sum(r => r.GrossEarnings),
            rows.Sum(r => r.NetPay),
            rows.Sum(r => r.EmployerCost),
            rows.Sum(r => r.TotalDeductions));

        var controls = new List<PayrollControlDto>
        {
            new("Eksik Puantaj", rows.Count(r => !r.TimesheetComplete), "high"),
            new("Eksik IBAN", rows.Count(r => !r.IbanComplete), "high"),
            new("SGK Matrah Uyarısı", rows.Count(r =>
                r.Warnings.Contains("SGK tavan kontrolü")), "medium"),
            new("Kanuni Sınıflandırma Eksik", rows.Count(r =>
                r.Warnings.Any(w => w.Contains("sınıfı seçilmedi")
                    || w.Contains("dağıtım bekliyor")
                    || w.Contains("istisna günü eksik"))), "high"),
            new("Vergi Matrahı Uyarısı", rows.Count(r => r.Warnings.Contains("Vergi dilimi geçişi")), "medium"),
            new("Onay Bekleyen", rows.Count(r => r.ApprovalStatus != "Onaylandı"), "low"),
        };

        return new PayrollPeriodDetailDto(
            period.Id, period.Name, period.Year, period.Month, period.Status.ToDto(),
            PayrollSettingsResolver.ToDto(period.Settings!), rows, totals, controls);
    }

    public static PayrollRowDto ToRow(
        PayrollEmployee pe,
        IPayrollEngine engine,
        PayrollParameters parameters,
        decimal minimumWageIncomeTaxBaseBefore)
    {
        PayrollCalculation calc;
        if (pe.ApprovalStatus == PayrollApprovalStatus.Approved && pe.Result is not null)
        {
            var r = pe.Result;
            calc = new PayrollCalculation(
                r.HourlyRate, r.OvertimePay, r.AdditionalPay, r.BaseGross, r.GrossEarnings,
                r.SgkBase, r.SgkEmployee, r.UnemploymentEmployee,
                r.IncomeTaxBase, r.IncomeTax, r.StampTax,
                r.TotalDeductions, r.NetPay,
                r.EmployerSgk, r.EmployerUnemployment, r.EmployerCost,
                r.WarningsJson is null
                    ? []
                    : JsonSerializer.Deserialize<List<string>>(r.WarningsJson) ?? [],
                r.SgkExemptMealAllowance, r.IncomeTaxExemptMealAllowance,
                r.MinimumWageIncomeTaxExemption, r.MinimumWageStampTaxExemption,
                r.AppliedEmployerSgkRate,
                r.DisabilityIncomeTaxBaseDeduction);
        }
        else
        {
            calc = engine.Calculate(
                pe.ToEngineInput(minimumWageIncomeTaxBaseBefore), parameters);
        }

        var dto = new PayrollRowDto(
            pe.Id,
            pe.EmployeeId,
            pe.Employee?.FullName ?? string.Empty,
            pe.Employee?.Title ?? string.Empty,
            pe.Employee?.Department?.Name ?? string.Empty,
            pe.GrossSalary, pe.StatutoryProfile,
            pe.WorkedDays, pe.SgkPremiumDaysConfirmed,
            pe.OvertimeHours, pe.OvertimeTreatmentConfirmed, pe.PremiumPay, pe.RoadAllowance, pe.MealAllowance,
            pe.MealDays, pe.MealTreatment,
            pe.BenefitPay, pe.BenefitPayFullyTaxableConfirmed,
            pe.ApplyMinimumWageTaxExemption, pe.EmployerSgkDiscount,
            pe.EmployerSgkDiscountReference,
            pe.DisabilityReliefType, pe.DisabilityReliefReference,
            pe.OtherIncomeTaxBaseDeductionsConfirmedNone,
            pe.SpecialDeductions, pe.PreviousTaxBase,
            pe.PreviousEmployerIncomeTaxBase, pe.PreviousEmployerIncomeTaxBaseReference,
            pe.CumulativeIncomeTaxBaseConfirmed,
            pe.IbanComplete, pe.TimesheetComplete,
            pe.ApprovalStatus.ToDto(), pe.Notes,
            calc.HourlyRate, calc.OvertimePay, calc.BaseGross, calc.GrossEarnings,
            calc.SgkBase, calc.SgkEmployee, calc.UnemploymentEmployee,
            calc.IncomeTaxBase, calc.IncomeTax, calc.StampTax,
            calc.TotalDeductions, calc.NetPay,
            calc.EmployerSgk, calc.EmployerUnemployment, calc.EmployerCost,
            calc.SgkExemptMealAllowance, calc.IncomeTaxExemptMealAllowance,
            calc.MinimumWageIncomeTaxExemption, calc.MinimumWageStampTaxExemption,
            calc.AppliedEmployerSgkRate, calc.DisabilityIncomeTaxBaseDeduction,
            calc.Warnings);
        return dto with { Revision = PayrollRowReview.Revision(dto, pe) };
    }
}
