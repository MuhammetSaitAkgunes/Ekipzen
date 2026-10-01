using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Payroll.Settings;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Periods;

public sealed record PayrollPeriodCreationRowDto(int EmployeeId, string Name, int? EmploymentPeriodId,
    int? CompensationPackageId, int? CompensationVersion, string? SourceReference)
{
    public DateOnly HireDate { get; init; }
    public DateOnly? TerminationDate { get; init; }
    public DateOnly? CompensationEffectiveFrom { get; init; }
    public DateOnly? CompensationEffectiveTo { get; init; }
    public string EmployeeStatus { get; init; } = "";
    public decimal GrossSalary { get; init; }
    public decimal PremiumPay { get; init; }
    public decimal RoadAllowance { get; init; }
    public decimal MealAllowance { get; init; }
    public decimal BenefitPay { get; init; }
    public int WorkedDays { get; init; }
    public int OvertimeHours { get; init; }
    public decimal PreviousTaxBase { get; init; }
    public bool IbanComplete { get; init; }
    public bool TimesheetComplete { get; init; }
}
public sealed record PayrollPeriodCreationPreviewDto(int Year, int Month, string Name,
    PayrollSettingsDto Settings, decimal MinimumWageIncomeTaxBaseBefore,
    IReadOnlyList<PayrollPeriodCreationRowDto> Rows, string Token);
public sealed record GetPayrollPeriodCreationPreviewQuery(int Year, int Month) : IRequest<PayrollPeriodCreationPreviewDto>;
public sealed class GetPayrollPeriodCreationPreviewQueryValidator : AbstractValidator<GetPayrollPeriodCreationPreviewQuery>
{
    public GetPayrollPeriodCreationPreviewQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}
public sealed class GetPayrollPeriodCreationPreviewQueryHandler(IApplicationDbContext context, IDatabaseLock databaseLock)
    : IRequestHandler<GetPayrollPeriodCreationPreviewQuery, PayrollPeriodCreationPreviewDto>
{
    public Task<PayrollPeriodCreationPreviewDto> Handle(GetPayrollPeriodCreationPreviewQuery request, CancellationToken ct)
        => databaseLock.ExecuteExclusiveAsync("payroll-period-create", async token =>
            (await PayrollPeriodDraftBuilder.BuildAsync(context, request.Year, request.Month, token)).Preview, ct);
}

// Preview and final creation use exactly the same transient draft and source queries.
internal static class PayrollPeriodDraftBuilder
{
    public static async Task<(PayrollPeriod Period, PayrollPeriodCreationPreviewDto Preview)> BuildAsync(
        IApplicationDbContext context, int year, int month, CancellationToken cancellationToken)
    {
        if (await context.PayrollPeriods.AnyAsync(
                p => p.Year == year && p.Month == month, cancellationToken))
        {
            throw new ConflictException(
                $"{PayrollMappings.PeriodName(year, month)} dönemi zaten var.");
        }

        await PayrollPeriodSequence.EnsureCanCreateAsync(
            context, year, month, cancellationToken);

        // Dönemin kendi ayının ilk günü: o tarihte yürürlükteki parametre seti kullanılır.
        var settings = await PayrollSettingsResolver.ResolveAsync(
            context, new DateOnly(year, month, 1), cancellationToken);

        var periodStart = new DateOnly(year, month, 1);
        var minimumWageIncomeTaxBaseBefore = await PayrollMinimumWageTaxBase.CalculateBeforeAsync(
            context, year, month, cancellationToken);
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
                e.FirstName, e.LastName, e.Status,
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
            year,
            month,
            cancellationToken);

        // Fazla mesai puantaj özetinden beslenir (plan Faz 5 → Faz 6 bağlantısı).
        var attendance = await context.MonthlyAttendanceSummaries
            .Where(s => s.Year == year && s.Month == month)
            .ToListAsync(cancellationToken);
        var attendanceByEmployee = attendance.ToDictionary(a => a.EmployeeId);

        var period = new PayrollPeriod
        {
            Name = PayrollMappings.PeriodName(year, month),
            Year = year,
            Month = month,
            Status = PayrollPeriodStatus.Draft,
            PayrollSettingsId = settings.Id,
            MinimumWageIncomeTaxBaseBefore = minimumWageIncomeTaxBaseBefore,
        };

        var sources = new List<PayrollPeriodCreationRowDto>();
        foreach (var employee in employees.OrderBy(e => e.Id))
        {
            var summary = attendanceByEmployee.GetValueOrDefault(employee.Id);
            var compensation = employee.EmploymentPeriodId is int employmentPeriodId
                ? compensationByEmploymentPeriod.GetValueOrDefault(employmentPeriodId)
                : null;
            sources.Add(new PayrollPeriodCreationRowDto(employee.Id, employee.FirstName + " " + employee.LastName,
                employee.EmploymentPeriodId, compensation?.Id, compensation?.Version, compensation?.SourceReference) {
                HireDate = employee.HireDate, TerminationDate = employee.TerminationDate,
                EmployeeStatus = employee.Status == EmployeeStatus.Active ? "active" : "passive",
                CompensationEffectiveFrom = compensation?.EffectiveFrom, CompensationEffectiveTo = compensation?.EffectiveTo });
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
            context, period.Employees.ToList(), year, month, cancellationToken);

        var rows = period.Employees.Select(row => sources.Single(source => source.EmployeeId == row.EmployeeId) with {
            GrossSalary = row.GrossSalary, PremiumPay = row.PremiumPay, RoadAllowance = row.RoadAllowance,
            MealAllowance = row.MealAllowance, BenefitPay = row.BenefitPay, WorkedDays = row.WorkedDays,
            OvertimeHours = row.OvertimeHours, PreviousTaxBase = row.PreviousTaxBase,
            IbanComplete = row.IbanComplete, TimesheetComplete = row.TimesheetComplete }).ToArray();
        var preview = new PayrollPeriodCreationPreviewDto(year, month, period.Name,
            PayrollSettingsResolver.ToDto(settings), minimumWageIncomeTaxBaseBefore, rows, "");
        preview = preview with { Token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(preview)))) };
        return (period, preview);
    }
}
