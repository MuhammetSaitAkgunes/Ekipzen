using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Application.Features.Payroll.Periods;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record ApprovedPayrollImpactPeriodDto(int PeriodId, int RowId, int Year, int Month, string Status);
public sealed record ApprovedPayrollAmountDto(decimal GrossEarnings, decimal IncomeTaxBase,
    decimal IncomeTax, decimal NetPay, decimal EmployerCost);
public sealed record ApprovedCompensationPayrollImpactDto(
    int PeriodId, int RowId, string PeriodStatus, int RemediationCaseId,
    string ExpertReviewReference, DateTime ReviewedAtUtc,
    bool CanEstimate, IReadOnlyList<string> ReviewWarnings,
    ApprovedPayrollAmountDto Recorded, ApprovedPayrollAmountDto? Hypothetical,
    IReadOnlyList<ApprovedPayrollImpactPeriodDto> LaterSameYearRows,
    IReadOnlyList<ApprovedPayrollImpactPeriodDto> LaterApprovedRows,
    bool CanApply, IReadOnlyList<string> BlockingReasons, string PreviewToken,
    ApprovedPayrollRevisionDto? Revision,
    ApprovedPayrollChainImpactDto ChainImpact);

public sealed record ReviewApprovedCompensationPayrollImpactQuery(
    int EmployeeId, int PackageId, int CaseId, int PeriodId, int RowId)
    : IRequest<ApprovedCompensationPayrollImpactDto>;

public sealed class ReviewApprovedCompensationPayrollImpactQueryHandler(
    IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<ReviewApprovedCompensationPayrollImpactQuery, ApprovedCompensationPayrollImpactDto>
{
    public async Task<ApprovedCompensationPayrollImpactDto> Handle(
        ReviewApprovedCompensationPayrollImpactQuery request, CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, request.EmployeeId, request.PackageId, cancellationToken);
        var remediation = await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CaseId
                && item.EmployeeId == request.EmployeeId
                && item.CompensationPackageId == request.PackageId, cancellationToken)
            ?? throw new NotFoundException("Ücret telafi incelemesi", request.CaseId);
        if (remediation.Status != "approved" || remediation.ReviewedAtUtc is null
            || string.IsNullOrWhiteSpace(remediation.ExpertReviewReference))
            throw new ConflictException("Onaylı uzman incelemesi bulunmadan bordro etki dosyası açılamaz.");

        var row = await context.PayrollEmployees.AsNoTracking()
            .Include(item => item.Period)!.ThenInclude(period => period!.Settings)!
                .ThenInclude(settings => settings!.TaxBrackets)
            .Include(item => item.Result)
            .SingleOrDefaultAsync(item => item.Id == request.RowId
                && item.PayrollPeriodId == request.PeriodId
                && item.EmployeeId == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Bordro satırı", request.RowId);
        if (row.ApprovalStatus != PayrollApprovalStatus.Approved || row.Result is null
            || row.Period!.Status is not (PayrollPeriodStatus.Approved or PayrollPeriodStatus.Closed))
            throw new ConflictException("Bu etki dosyası yalnız onaylı veya kapatılmış dönemin onaylı satırı için hazırlanır.");

        var reviewedImpact = JsonSerializer.Deserialize<CompensationRemediationImpactDto>(
            remediation.ImpactSnapshotJson)!;
        var reviewedRow = reviewedImpact.PotentialPayrollRows.SingleOrDefault(item =>
            item.RowId == request.RowId && item.PeriodId == request.PeriodId);
        if (reviewedRow is null)
            throw new ConflictException("Satır onaylı uzman inceleme dosyasının kapsamında değil.");

        var warnings = new List<string>();
        if (reviewedRow.RecordedGrossSalary != row.GrossSalary)
            warnings.Add("Satır brütü uzman incelemesinden sonra değişti; kaynak ilişkisi yeniden incelenmeli.");
        if (remediation.PackageVersion != package.Version)
            warnings.Add("Kaynak ücret paketi uzman incelemesinden sonra değişti.");
        if (remediation.ProposedEffectiveFrom is not null
            && remediation.ProposedEffectiveFrom != package.EffectiveFrom)
            warnings.Add("Önerilen yürürlük tarihi farklı; dönem eşleşmesi ayrıca incelenmeli.");
        var periodStart = new DateOnly(row.Period.Year, row.Period.Month, 1);
        if (periodStart < package.EffectiveFrom
            || (package.EffectiveTo is not null && periodStart > package.EffectiveTo))
            warnings.Add("Bordro ayı kaynak paketin yürürlük aralığında değil.");
        if (row.GrossSalary != package.MonthlyGrossSalary
            || row.PremiumPay != package.RegularPremium
            || row.RoadAllowance != package.RoadAllowance
            || row.MealAllowance != package.MealAllowance
            || row.BenefitPay != package.OtherRegularBenefits)
            warnings.Add("Satır tutarları kaynak paketle eşleşmiyor; elle girilmiş farklar ayrıca incelenmeli.");
        if (row.StatutoryProfile != PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA)
            warnings.Add("Satırın kanuni bordro profili bu hesaplama motorunun doğrulanmış kapsamı dışında.");

        var laterRowData = await context.PayrollEmployees.AsNoTracking()
            .Where(item => item.EmployeeId == request.EmployeeId
                && item.Period!.Year == row.Period.Year
                && item.Period.Month > row.Period.Month)
            .OrderBy(item => item.Period!.Month)
            .Select(item => new { item.PayrollPeriodId, item.Id, item.Period!.Year,
                item.Period.Month, item.Period.Status, item.ApprovalStatus })
            .ToListAsync(cancellationToken);
        var laterRows = laterRowData.Select(item => new ApprovedPayrollImpactPeriodDto(
            item.PayrollPeriodId, item.Id, item.Year, item.Month,
            item.Status.ToString().ToLowerInvariant())).ToList();
        var laterApprovedData = await context.PayrollEmployees.AsNoTracking()
            .Where(item => item.EmployeeId == request.EmployeeId
                && (item.Period!.Year > row.Period.Year
                    || (item.Period.Year == row.Period.Year
                        && item.Period.Month > row.Period.Month))
                && item.ApprovalStatus == PayrollApprovalStatus.Approved)
            .OrderBy(item => item.Period!.Year).ThenBy(item => item.Period!.Month)
            .Select(item => new { item.PayrollPeriodId, item.Id, item.Period!.Year,
                item.Period.Month, item.Period.Status })
            .ToListAsync(cancellationToken);
        var laterApprovedRows = laterApprovedData.Select(item => new ApprovedPayrollImpactPeriodDto(
            item.PayrollPeriodId, item.Id, item.Year, item.Month,
            item.Status.ToString().ToLowerInvariant())).ToList();

        var result = row.Result;
        var sourceUpdatedAt = row.UpdatedAtUtc;
        var recorded = new ApprovedPayrollAmountDto(result.GrossEarnings, result.IncomeTaxBase,
            result.IncomeTax, result.NetPay, result.EmployerCost);
        ApprovedPayrollAmountDto? hypothetical = null;
        var blocking = new List<string>(warnings);
        if (warnings.Count == 0)
        {
            var values = JsonSerializer.Deserialize<CompensationPackageValuesDto>(
                remediation.ProposedValuesJson)!;
            if (row.GrossSalary == values.MonthlyGrossSalary
                && row.PremiumPay == values.RegularPremium
                && row.RoadAllowance == values.RoadAllowance
                && row.MealAllowance == values.MealAllowance
                && row.BenefitPay == values.OtherRegularBenefits)
                blocking.Add("Önerilen ücret tutarları onaylı satırla aynı.");
            CompensationPayrollApplicationRules.SetAmounts(row, values);
            var calculation = engine.Calculate(
                row.ToEngineInput(row.Period.MinimumWageIncomeTaxBaseBefore),
                row.Period.Settings!.ToEngineParameters());
            hypothetical = new(calculation.GrossEarnings, calculation.IncomeTaxBase,
                calculation.IncomeTax, calculation.NetPay, calculation.EmployerCost);
            blocking.AddRange(PayrollRowReadiness.GetBlockingReasons(row, calculation));
        }
        if (laterApprovedRows.Count > 0)
            blocking.Add("Aynı çalışanın sonraki onaylı bordro satırı var; zincirleme vergi etkisi ayrı telafi gerektirir.");
        var revision = await context.PayrollApprovedRowRevisions.AsNoTracking()
            .Where(item => item.PayrollRowId == request.RowId)
            .OrderByDescending(item => item.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (revision is not null)
            blocking.Add("Bu satır için onaylı parasal sürüm zaten var; sonraki düzeltme ayrı inceleme gerektirir.");
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { recorded, hypothetical, laterRowData, laterApprovedData,
                sourceUpdatedAt, result.CalculatedAtUtc,
                CaseVersion = remediation.Version, PackageVersion = package.Version,
                remediation.ProposedValuesJson, RevisionId = revision?.Id }))));
        var chainImpact = revision is null
            ? await ApprovedPayrollChainImpact.CalculateAsync(
                context, engine, row, hypothetical, cancellationToken)
            : new ApprovedPayrollChainImpactDto(false,
                ["İlk satırın parasal sürümü zaten var; zincirleme telafi ayrı inceleme gerektirir."],
                []);
        return new(request.PeriodId, request.RowId,
            row.Period.Status.ToString().ToLowerInvariant(), request.CaseId,
            remediation.ExpertReviewReference, remediation.ReviewedAtUtc.Value,
            hypothetical is not null, warnings, recorded, hypothetical, laterRows,
            laterApprovedRows,
            blocking.Count == 0, blocking, token,
            revision is null ? null : ApprovedPayrollRevisionRules.ToDto(revision),
            chainImpact);
    }
}
