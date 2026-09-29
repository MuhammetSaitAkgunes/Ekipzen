using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll.Payslips;

// --- çalışanın kendi bordroları ---

public sealed record GetMyPayslipsQuery : IRequest<IReadOnlyList<MyPayslipDto>>;

public sealed class GetMyPayslipsQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyPayslipsQuery, IReadOnlyList<MyPayslipDto>>
{
    public async Task<IReadOnlyList<MyPayslipDto>> Handle(
        GetMyPayslipsQuery request, CancellationToken cancellationToken)
    {
        var employeeId = currentUser.EmployeeId
            ?? throw new ForbiddenAccessException("Hesabınıza bağlı personel kaydı yok.");

        var rows = await context.PayrollEmployees
            .AsNoTracking()
            .Where(pe => pe.EmployeeId == employeeId && pe.Result != null)
            .OrderByDescending(pe => pe.Period!.Year).ThenByDescending(pe => pe.Period!.Month)
            .Select(pe => new { pe.PayrollPeriodId, pe.Id, PeriodName = pe.Period!.Name,
                pe.ApprovalStatus, pe.Result!.GrossEarnings, pe.Result.TotalDeductions,
                pe.Result.NetPay })
            .ToListAsync(cancellationToken);
        var revisions = await ApprovedPayrollRevisionRules.LatestByRowAsync(
            context, rows.Select(row => row.Id).ToArray(), cancellationToken);
        return rows.Select(row =>
        {
            var revised = revisions.TryGetValue(row.Id, out var revision);
            var effective = revised ? ApprovedPayrollRevisionRules.EffectiveRow(revision!) : null;
            return new MyPayslipDto(row.PayrollPeriodId, row.Id, row.PeriodName,
                effective?.GrossEarnings ?? row.GrossEarnings,
                effective?.TotalDeductions ?? row.TotalDeductions,
                effective?.NetPay ?? row.NetPay,
                row.ApprovalStatus == PayrollApprovalStatus.Approved ? "Onaylandı" : "Kontrol",
                revised);
        }).ToList();
    }
}

// --- bordro pusulası PDF ---

/// <summary>QuestPDF pusula üretici girdisi — payroll.js slip-paper alanları.</summary>
public sealed record PayslipModel(
    string PeriodName,
    string EmployeeName,
    string Title,
    string Department,
    int WorkedDays,
    decimal OvertimeHours,
    decimal OvertimePay,
    decimal BaseGross,
    decimal PremiumPay,
    decimal RoadAllowance,
    decimal MealAllowance,
    decimal BenefitPay,
    decimal GrossEarnings,
    decimal SgkEmployee,
    decimal UnemploymentEmployee,
    decimal IncomeTax,
    decimal StampTax,
    decimal SpecialDeductions,
    decimal TotalDeductions,
    decimal NetPay,
    decimal SgkBase,
    decimal IncomeTaxBase,
    decimal DisabilityIncomeTaxBaseDeduction,
    decimal PreviousTaxBase,
    int MealDays,
    string MealTreatment,
    string DisabilityReliefType,
    decimal SgkExemptMealAllowance,
    decimal IncomeTaxExemptMealAllowance,
    decimal MinimumWageIncomeTaxExemption,
    decimal MinimumWageStampTaxExemption,
    decimal AppliedEmployerSgkRate,
    int SettingsVersion,
    DateOnly SettingsEffectiveFrom,
    string SettingsSourceReference);

/// <summary>Bordro pusulası PDF üretimi (Infrastructure: QuestPDF).</summary>
public interface IPayslipGenerator
{
    byte[] Generate(PayslipModel model);
}

/// <summary>
/// Pusula PDF'i. hr-admin herkesinkini, çalışan yalnız kendisininkini indirebilir;
/// sonuç (onay snapshot'ı) olmayan satır için 409.
/// </summary>
public sealed record GetPayslipPdfQuery(int PeriodId, int RowId, bool Original = false)
    : IRequest<(byte[] Content, string FileName)>;

public sealed class GetPayslipPdfQueryHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IPayslipGenerator generator)
    : IRequestHandler<GetPayslipPdfQuery, (byte[] Content, string FileName)>
{
    public async Task<(byte[] Content, string FileName)> Handle(
        GetPayslipPdfQuery request, CancellationToken cancellationToken)
    {
        var row = await context.PayrollEmployees
            .AsNoTracking()
            .Include(pe => pe.Period)!.ThenInclude(period => period!.Settings)
            .Include(pe => pe.Employee)!.ThenInclude(e => e!.Department)
            .Include(pe => pe.Result)
            .FirstOrDefaultAsync(
                pe => pe.Id == request.RowId && pe.PayrollPeriodId == request.PeriodId, cancellationToken)
            ?? throw new NotFoundException("Bordro satırı", request.RowId);

        if (!currentUser.Roles.Contains(Roles.HrAdmin) && row.EmployeeId != currentUser.EmployeeId)
        {
            throw new ForbiddenAccessException("Yalnız kendi bordro pusulanızı indirebilirsiniz.");
        }

        var result = row.Result
            ?? throw new ConflictException("Bu satır için onaylanmış bordro sonucu yok.");

        var revisions = await ApprovedPayrollRevisionRules.LatestByRowAsync(
            context, [row.Id], cancellationToken);
        var revised = !request.Original && revisions.TryGetValue(row.Id, out var revision)
            ? ApprovedPayrollRevisionRules.EffectiveRow(revision)
            : null;

        var model = new PayslipModel(
            row.Period!.Name,
            row.Employee!.FullName,
            row.Employee.Title,
            row.Employee.Department?.Name ?? string.Empty,
            row.WorkedDays,
            row.OvertimeHours,
            result.OvertimePay,
            result.BaseGross,
            row.PremiumPay,
            row.RoadAllowance,
            row.MealAllowance,
            row.BenefitPay,
            result.GrossEarnings,
            result.SgkEmployee,
            result.UnemploymentEmployee,
            result.IncomeTax,
            result.StampTax,
            row.SpecialDeductions,
            result.TotalDeductions,
            result.NetPay,
            result.SgkBase,
            result.IncomeTaxBase,
            result.DisabilityIncomeTaxBaseDeduction,
            row.PreviousTaxBase,
            row.MealDays,
            row.MealTreatment == PayrollMealTreatment.CashExemptWhereNoMealProvided
                ? "İşyerinde yemek yok, nakit istisna"
                : row.MealTreatment == PayrollMealTreatment.FullyTaxable
                    ? "Tam prime ve vergiye tabi"
                    : "Yemek yok",
            row.DisabilityReliefType switch
            {
                PayrollDisabilityReliefType.FirstDegree => "1. derece",
                PayrollDisabilityReliefType.SecondDegree => "2. derece",
                PayrollDisabilityReliefType.ThirdDegree => "3. derece",
                PayrollDisabilityReliefType.None => "Yok",
                _ => "Belirtilmedi",
            },
            result.SgkExemptMealAllowance,
            result.IncomeTaxExemptMealAllowance,
            result.MinimumWageIncomeTaxExemption,
            result.MinimumWageStampTaxExemption,
            result.AppliedEmployerSgkRate,
            row.Period.Settings!.Version,
            row.Period.Settings.EffectiveFrom,
            row.Period.Settings.SourceReference);

        if (revised is not null)
            model = model with
            {
                BaseGross = revised.BaseGross, PremiumPay = revised.PremiumPay,
                RoadAllowance = revised.RoadAllowance, MealAllowance = revised.MealAllowance,
                BenefitPay = revised.BenefitPay, GrossEarnings = revised.GrossEarnings,
                OvertimePay = revised.OvertimePay, SgkEmployee = revised.SgkEmployee,
                UnemploymentEmployee = revised.UnemploymentEmployee,
                IncomeTax = revised.IncomeTax, StampTax = revised.StampTax,
                SpecialDeductions = revised.SpecialDeductions,
                TotalDeductions = revised.TotalDeductions, NetPay = revised.NetPay,
                SgkBase = revised.SgkBase, IncomeTaxBase = revised.IncomeTaxBase,
                DisabilityIncomeTaxBaseDeduction = revised.DisabilityIncomeTaxBaseDeduction,
                PreviousTaxBase = revised.PreviousTaxBase,
                SgkExemptMealAllowance = revised.SgkExemptMealAllowance,
                IncomeTaxExemptMealAllowance = revised.IncomeTaxExemptMealAllowance,
                MinimumWageIncomeTaxExemption = revised.MinimumWageIncomeTaxExemption,
                MinimumWageStampTaxExemption = revised.MinimumWageStampTaxExemption,
                AppliedEmployerSgkRate = revised.AppliedEmployerSgkRate,
            };

        var fileName = $"bordro-{row.Period.Year}-{row.Period.Month:00}-{row.Employee.FullName
            .Replace(' ', '-').ToLowerInvariant()}.pdf";

        if (request.Original && revisions.Count > 0)
            fileName = fileName.Replace(".pdf", "-ilk-onay.pdf", StringComparison.Ordinal);

        return (generator.Generate(model), fileName);
    }
}
