using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Payroll.Settings;
using IKPro.Domain.Entities.Payroll;
using IKPro.Domain.Enums;
using IKPro.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IKPro.Application.Features.Payroll.Periods;

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
public sealed record CreatePayrollPeriodCommand(int Year, int Month) : IRequest<PayrollPeriodDetailDto>;

public sealed class CreatePayrollPeriodCommandValidator : AbstractValidator<CreatePayrollPeriodCommand>
{
    public CreatePayrollPeriodCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class CreatePayrollPeriodCommandHandler(IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<CreatePayrollPeriodCommand, PayrollPeriodDetailDto>
{
    public async Task<PayrollPeriodDetailDto> Handle(
        CreatePayrollPeriodCommand request, CancellationToken cancellationToken)
    {
        if (await context.PayrollPeriods.AnyAsync(
                p => p.Year == request.Year && p.Month == request.Month, cancellationToken))
        {
            throw new ConflictException(
                $"{PayrollMappings.PeriodName(request.Year, request.Month)} dönemi zaten var.");
        }

        // Dönemin kendi ayının ilk günü: o tarihte yürürlükteki parametre seti kullanılır.
        var settings = await PayrollSettingsResolver.ResolveAsync(
            context, new DateOnly(request.Year, request.Month, 1), cancellationToken);

        var periodEnd = new DateOnly(request.Year, request.Month, 1).AddMonths(1).AddDays(-1);

        var employees = await context.Employees
            .AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active && e.HireDate <= periodEnd)
            .Select(e => new
            {
                e.Id,
                e.HireDate,
                HasIban = e.Profile != null && e.Profile.Iban != null,
            })
            .ToListAsync(cancellationToken);

        var timesheetCoverage = await PayrollTimesheetCoverage.CalculateAsync(
            context,
            employees.ToDictionary(e => e.Id, e => e.HireDate),
            request.Year,
            request.Month,
            cancellationToken);

        // Kümülatif GV matrahı devri: aynı yılın EN SON onaylı döneminin
        // (elle girilmiş taban dahil) dönem-sonu kümülatifi = PreviousTaxBase + IncomeTaxBase.
        var priorRows = await context.PayrollEmployees
            .Where(pe => pe.Period!.Year == request.Year
                         && pe.Period.Month < request.Month
                         && pe.Result != null)
            .Select(pe => new
            {
                pe.EmployeeId,
                pe.Period!.Month,
                CumulativeAfter = pe.PreviousTaxBase + pe.Result!.IncomeTaxBase,
            })
            .ToListAsync(cancellationToken);
        var previousBases = priorRows
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Month).First().CumulativeAfter);

        // Fazla mesai puantaj özetinden beslenir (plan Faz 5 → Faz 6 bağlantısı).
        var attendance = await context.MonthlyAttendanceSummaries
            .Where(s => s.Year == request.Year && s.Month == request.Month)
            .ToListAsync(cancellationToken);
        var attendanceByEmployee = attendance.ToDictionary(a => a.EmployeeId);

        var period = new PayrollPeriod
        {
            Name = PayrollMappings.PeriodName(request.Year, request.Month),
            Year = request.Year,
            Month = request.Month,
            Status = PayrollPeriodStatus.Draft,
            PayrollSettingsId = settings.Id,
        };

        foreach (var employee in employees)
        {
            var summary = attendanceByEmployee.GetValueOrDefault(employee.Id);
            period.Employees.Add(new PayrollEmployee
            {
                EmployeeId = employee.Id,
                GrossSalary = 0m, // maaş girdisi İK tarafından doldurulur
                WorkedDays = settings.DefaultWorkedDays,
                OvertimeHours = summary is null ? 0 : (int)Math.Round(summary.TotalOvertimeMinutes / 60.0),
                PreviousTaxBase = previousBases.GetValueOrDefault(employee.Id, 0m),
                IbanComplete = employee.HasIban,
                TimesheetComplete = timesheetCoverage.GetValueOrDefault(employee.Id),
                ApprovalStatus = PayrollApprovalStatus.PreCalc,
            });
        }

        context.PayrollPeriods.Add(period);
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
            .Include(p => p.Employees).ThenInclude(pe => pe.Employee)!.ThenInclude(e => e!.Department)
            .Include(p => p.Employees).ThenInclude(pe => pe.Result)
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken)
            ?? throw new NotFoundException("Bordro dönemi", periodId);

        var pendingRows = period.Employees
            .Where(row => row.ApprovalStatus != PayrollApprovalStatus.Approved)
            .ToList();
        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, period, pendingRows, cancellationToken);

        var parameters = period.Settings!.ToEngineParameters();

        var rows = period.Employees
            .OrderBy(pe => pe.Employee!.FirstName).ThenBy(pe => pe.Employee!.LastName)
            .Select(pe => ToRow(pe, engine, parameters))
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
                r.SgkBase >= parameters.SgkBaseMax || r.SgkBase <= parameters.SgkBaseMin * 0.95m), "medium"),
            new("Vergi Matrahı Uyarısı", rows.Count(r => r.Warnings.Contains("Vergi dilimi geçişi")), "medium"),
            new("Onay Bekleyen", rows.Count(r => r.ApprovalStatus != "Onaylandı"), "low"),
        };

        return new PayrollPeriodDetailDto(
            period.Id, period.Name, period.Year, period.Month, period.Status.ToDto(),
            rows, totals, controls);
    }

    public static PayrollRowDto ToRow(PayrollEmployee pe, IPayrollEngine engine, PayrollParameters parameters)
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
                    : JsonSerializer.Deserialize<List<string>>(r.WarningsJson) ?? []);
        }
        else
        {
            calc = engine.Calculate(pe.ToEngineInput(), parameters);
        }

        return new PayrollRowDto(
            pe.Id,
            pe.EmployeeId,
            pe.Employee?.FullName ?? string.Empty,
            pe.Employee?.Title ?? string.Empty,
            pe.Employee?.Department?.Name ?? string.Empty,
            pe.GrossSalary, pe.WorkedDays, pe.OvertimeHours,
            pe.PremiumPay, pe.RoadAllowance, pe.MealAllowance, pe.BenefitPay,
            pe.SpecialDeductions, pe.PreviousTaxBase,
            pe.IbanComplete, pe.TimesheetComplete,
            pe.ApprovalStatus.ToDto(), pe.Notes,
            calc.HourlyRate, calc.OvertimePay, calc.BaseGross, calc.GrossEarnings,
            calc.SgkBase, calc.SgkEmployee, calc.UnemploymentEmployee,
            calc.IncomeTaxBase, calc.IncomeTax, calc.StampTax,
            calc.TotalDeductions, calc.NetPay,
            calc.EmployerSgk, calc.EmployerUnemployment, calc.EmployerCost,
            calc.Warnings);
    }
}
