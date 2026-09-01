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

public sealed class CreatePayrollPeriodCommandHandler(
    IApplicationDbContext context,
    IPayrollEngine engine,
    IDatabaseLock databaseLock)
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
        if (await context.PayrollPeriods.AnyAsync(
                p => p.Year == request.Year && p.Month == request.Month, cancellationToken))
        {
            throw new ConflictException(
                $"{PayrollMappings.PeriodName(request.Year, request.Month)} dönemi zaten var.");
        }

        await PayrollPeriodSequence.EnsureCanCreateAsync(
            context, request.Year, request.Month, cancellationToken);

        // Dönemin kendi ayının ilk günü: o tarihte yürürlükteki parametre seti kullanılır.
        var settings = await PayrollSettingsResolver.ResolveAsync(
            context, new DateOnly(request.Year, request.Month, 1), cancellationToken);

        var periodStart = new DateOnly(request.Year, request.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var employees = await context.Employees
            .AsNoTracking()
            .Where(e => e.HireDate <= periodEnd
                        && (e.Status == EmployeeStatus.Active
                            || (e.Status == EmployeeStatus.Passive
                                && e.TerminationDate != null
                                && e.TerminationDate >= periodStart)))
            .Select(e => new
            {
                e.Id,
                e.HireDate,
                e.TerminationDate,
                EmploymentPeriodId = e.EmploymentPeriods
                    .Where(employment => employment.StartDate <= periodStart
                        && (employment.EndDate == null || employment.EndDate >= periodStart))
                    .Select(employment => (int?)employment.Id)
                    .SingleOrDefault(),
                HasIban = e.Profile != null && e.Profile.Iban != null,
            })
            .ToListAsync(cancellationToken);

        var employmentPeriodIds = employees
            .Where(employee => employee.EmploymentPeriodId is not null)
            .Select(employee => employee.EmploymentPeriodId!.Value)
            .ToArray();
        var compensationPackages = await context.EmployeeCompensationPackages
            .AsNoTracking()
            .Where(package => employmentPeriodIds.Contains(package.EmploymentPeriodId)
                && package.EffectiveFrom <= periodStart
                && (package.EffectiveTo == null || package.EffectiveTo >= periodStart))
            .ToListAsync(cancellationToken);
        var compensationByEmploymentPeriod = compensationPackages
            .ToDictionary(package => package.EmploymentPeriodId);

        var timesheetCoverage = await PayrollTimesheetCoverage.CalculateAsync(
            context,
            employees.ToDictionary(
                employee => employee.Id,
                employee => new PayrollEmploymentPeriod(
                    employee.HireDate, employee.TerminationDate)),
            request.Year,
            request.Month,
            cancellationToken);

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
            var compensation = employee.EmploymentPeriodId is int employmentPeriodId
                ? compensationByEmploymentPeriod.GetValueOrDefault(employmentPeriodId)
                : null;
            period.Employees.Add(new PayrollEmployee
            {
                EmployeeId = employee.Id,
                // Paket yalnız dönem başı girdisini sağlar; satır yine İK kontrolü ve onayına tabidir.
                GrossSalary = compensation?.MonthlyGrossSalary ?? 0m,
                PremiumPay = compensation?.RegularPremium ?? 0m,
                RoadAllowance = compensation?.RoadAllowance ?? 0m,
                MealAllowance = compensation?.MealAllowance ?? 0m,
                BenefitPay = compensation?.OtherRegularBenefits ?? 0m,
                WorkedDays = settings.DefaultWorkedDays,
                OvertimeHours = summary is null ? 0 : (int)Math.Round(summary.TotalOvertimeMinutes / 60.0),
                IbanComplete = employee.HasIban,
                TimesheetComplete = timesheetCoverage.GetValueOrDefault(employee.Id),
                ApprovalStatus = PayrollApprovalStatus.PreCalc,
            });
        }

        await PayrollCumulativeTaxBase.RefreshAsync(
            context, period.Employees.ToList(), request.Year, request.Month, cancellationToken);

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

        var pendingRows = employeeRows
            .Where(row => row.ApprovalStatus != PayrollApprovalStatus.Approved)
            .ToList();
        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, period, pendingRows, cancellationToken);
        await PayrollCumulativeTaxBase.RefreshAsync(
            context, pendingRows, period.Year, period.Month, cancellationToken);

        var parameters = period.Settings!.ToEngineParameters();

        var rows = employeeRows
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
