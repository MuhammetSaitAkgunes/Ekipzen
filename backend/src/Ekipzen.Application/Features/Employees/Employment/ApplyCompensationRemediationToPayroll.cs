using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Application.Features.Payroll.Periods;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record CompensationPayrollApplicationDto(
    int Id, int RemediationCaseId, int PayrollRowId, PayrollRowDto Before,
    PayrollRowDto After, string ActorUserId, DateTime AppliedAtUtc);

public sealed record CompensationPayrollPreviewDto(
    int RowId, int PeriodId, bool CanApply, IReadOnlyList<string> BlockingReasons,
    string PreviewToken, PayrollRowDto Before, PayrollRowDto? After,
    CompensationPayrollApplicationDto? Application);

public sealed record PreviewCompensationPayrollApplicationQuery(
    int EmployeeId, int PackageId, int CaseId, int PeriodId, int RowId)
    : IRequest<CompensationPayrollPreviewDto>;

public sealed class PreviewCompensationPayrollApplicationQueryHandler(
    IApplicationDbContext context, IPayrollEngine engine)
    : IRequestHandler<PreviewCompensationPayrollApplicationQuery, CompensationPayrollPreviewDto>
{
    public Task<CompensationPayrollPreviewDto> Handle(
        PreviewCompensationPayrollApplicationQuery request, CancellationToken cancellationToken)
        => CompensationPayrollApplicationRules.PreviewAsync(context, engine,
            request.EmployeeId, request.PackageId, request.CaseId, request.PeriodId,
            request.RowId, cancellationToken);
}

public sealed record ApplyCompensationRemediationToPayrollCommand(
    int EmployeeId, int PackageId, int CaseId, int PeriodId, int RowId,
    string ExpectedPreviewToken, bool ConfirmSourceLink)
    : IRequest<CompensationPayrollApplicationDto>;

public sealed class ApplyCompensationRemediationToPayrollCommandValidator
    : AbstractValidator<ApplyCompensationRemediationToPayrollCommand>
{
    public ApplyCompensationRemediationToPayrollCommandValidator()
    {
        RuleFor(item => item.ExpectedPreviewToken).NotEmpty().Length(64);
        RuleFor(item => item.ConfirmSourceLink).Equal(true);
    }
}

public sealed class ApplyCompensationRemediationToPayrollCommandHandler(
    IApplicationDbContext context, IPayrollEngine engine, ICurrentUser currentUser,
    IDatabaseLock databaseLock)
    : IRequestHandler<ApplyCompensationRemediationToPayrollCommand, CompensationPayrollApplicationDto>
{
    public Task<CompensationPayrollApplicationDto> Handle(
        ApplyCompensationRemediationToPayrollCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync([
                $"employee-compensation:{request.EmployeeId}",
                PayrollRowGuards.LockResource(request.PeriodId)],
            token => ApplyAsync(request, token), cancellationToken);

    private async Task<CompensationPayrollApplicationDto> ApplyAsync(
        ApplyCompensationRemediationToPayrollCommand request, CancellationToken cancellationToken)
    {
        var preview = await CompensationPayrollApplicationRules.PreviewAsync(
            context, engine, request.EmployeeId, request.PackageId, request.CaseId,
            request.PeriodId, request.RowId, cancellationToken);
        if (!preview.CanApply)
            throw new ConflictException(string.Join(" ", preview.BlockingReasons));
        if (!string.Equals(preview.PreviewToken, request.ExpectedPreviewToken,
                StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Bordro önizlemesi güncelliğini yitirdi; yeniden önizleyin.");

        var row = await PayrollRowGuards.GetMutableRowAsync(
            context, request.PeriodId, request.RowId, cancellationToken);
        var remediation = await context.EmployeeCompensationRemediationCases
            .SingleAsync(item => item.Id == request.CaseId, cancellationToken);
        var values = JsonSerializer.Deserialize<CompensationPackageValuesDto>(
            remediation.ProposedValuesJson)!;
        CompensationPayrollApplicationRules.SetAmounts(row, values);
        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, row.Period!, [row], cancellationToken);
        await PayrollCumulativeTaxBase.RefreshAsync(
            context, [row], row.Period!.Year, row.Period.Month, cancellationToken);
        var parameters = row.Period.Settings!.ToEngineParameters();
        var calculation = engine.Calculate(
            row.ToEngineInput(row.Period.MinimumWageIncomeTaxBaseBefore), parameters);
        row.ApprovalStatus = PayrollRowReadiness.IsReady(row, calculation)
            ? PayrollApprovalStatus.Control : PayrollApprovalStatus.MissingData;
        var after = PayrollPeriodProjector.ToRow(
            row, engine, parameters, row.Period.MinimumWageIncomeTaxBaseBefore);
        var application = new EmployeeCompensationPayrollApplication
        {
            RemediationCaseId = request.CaseId,
            PayrollRowId = request.RowId,
            BeforeJson = JsonSerializer.Serialize(preview.Before),
            AfterJson = JsonSerializer.Serialize(after),
            ActorUserId = currentUser.UserId
                ?? throw new InvalidOperationException("Bordro telafisi için oturum sahibi gerekli."),
            AppliedAtUtc = DateTime.UtcNow,
        };
        context.EmployeeCompensationPayrollApplications.Add(application);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Bordro satırı değişti; önizlemeyi yenileyin.");
        }
        return CompensationPayrollApplicationRules.ToDto(application);
    }
}

internal static class CompensationPayrollApplicationRules
{
    public static async Task<CompensationPayrollPreviewDto> PreviewAsync(
        IApplicationDbContext context, IPayrollEngine engine, int employeeId,
        int packageId, int caseId, int periodId, int rowId,
        CancellationToken cancellationToken)
    {
        var package = await CompensationPackageCorrectionRules.LoadAsync(
            context, employeeId, packageId, cancellationToken);
        var remediation = await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == caseId && item.EmployeeId == employeeId
                && item.CompensationPackageId == packageId, cancellationToken)
            ?? throw new NotFoundException("Ücret telafi incelemesi", caseId);
        var row = await context.PayrollEmployees
            .Include(item => item.Period)!.ThenInclude(item => item!.Settings)!
                .ThenInclude(item => item!.TaxBrackets)
            .Include(item => item.Employee)!.ThenInclude(item => item!.Department)
            .Include(item => item.Result)
            .SingleOrDefaultAsync(item => item.Id == rowId && item.PayrollPeriodId == periodId
                && item.EmployeeId == employeeId, cancellationToken)
            ?? throw new NotFoundException("Bordro satırı", rowId);
        var existing = await context.EmployeeCompensationPayrollApplications.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RemediationCaseId == caseId
                && item.PayrollRowId == rowId, cancellationToken);
        var values = JsonSerializer.Deserialize<CompensationPackageValuesDto>(
            remediation.ProposedValuesJson)!;
        var reasons = new List<string>();
        if (remediation.Status != "approved")
            reasons.Add("Uzman inceleme dosyası henüz onaylanmadı.");
        if (await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .AnyAsync(item => item.CompensationPackageId == packageId
                && item.Id > caseId, cancellationToken))
            reasons.Add("Bu dosyadan sonra yeni uzman inceleme dosyası açılmış; eski dosya yeni bordro uygulaması için kullanılamaz.");
        if (remediation.PackageVersion != package.Version)
            reasons.Add("Ücret paketi inceleme sonrasında değişti.");
        var reviewedImpact = JsonSerializer.Deserialize<CompensationRemediationImpactDto>(
            remediation.ImpactSnapshotJson)!;
        var reviewedRow = reviewedImpact.PotentialPayrollRows.SingleOrDefault(item =>
            item.RowId == rowId && item.PeriodId == periodId);
        if (reviewedRow is null)
            reasons.Add("Bu bordro satırı onaylı uzman incelemesinin kapsamına alınmamış.");
        else if (reviewedRow.RecordedGrossSalary != row.GrossSalary)
            reasons.Add("Bordro brütü uzman incelemesinden sonra değişti; yeni inceleme gerekir.");
        if (remediation.ProposedEffectiveFrom is not null
            && remediation.ProposedEffectiveFrom != package.EffectiveFrom)
            reasons.Add("Önerilen yürürlük tarihi farklı; tarih etkisi ayrı incelenmelidir.");
        var periodStart = new DateOnly(row.Period!.Year, row.Period.Month, 1);
        if (periodStart < package.EffectiveFrom ||
            (package.EffectiveTo is not null && periodStart > package.EffectiveTo))
            reasons.Add("Bordro dönemi bu paketin yürürlük aralığında değil.");
        if (row.Period.Status is PayrollPeriodStatus.Approved or PayrollPeriodStatus.Closed)
            reasons.Add("Onaylanmış veya kapanmış bordro dönemi değiştirilemez.");
        if (row.ApprovalStatus == PayrollApprovalStatus.Approved)
            reasons.Add("Onaylanmış bordro satırı değiştirilemez.");
        if (row.GrossSalary != package.MonthlyGrossSalary
            || row.PremiumPay != package.RegularPremium
            || row.RoadAllowance != package.RoadAllowance
            || row.MealAllowance != package.MealAllowance
            || row.BenefitPay != package.OtherRegularBenefits)
            reasons.Add("Satırın düzenli ücret alanları kaynak paketle eşleşmiyor; elle girilmiş farkları uzman incelemelidir.");
        if (existing is not null)
            reasons.Add("Bu inceleme dosyası bu bordro satırına zaten uygulandı.");
        if (row.GrossSalary == values.MonthlyGrossSalary
            && row.PremiumPay == values.RegularPremium
            && row.RoadAllowance == values.RoadAllowance
            && row.MealAllowance == values.MealAllowance
            && row.BenefitPay == values.OtherRegularBenefits)
            reasons.Add("Önerilen tutarlar bordro satırındaki tutarlarla aynı.");

        await PayrollRowReadiness.RefreshTimesheetCompletenessAsync(
            context, row.Period, [row], cancellationToken);
        await PayrollCumulativeTaxBase.RefreshAsync(
            context, [row], row.Period.Year, row.Period.Month, cancellationToken);
        var parameters = row.Period.Settings!.ToEngineParameters();
        var before = PayrollPeriodProjector.ToRow(
            row, engine, parameters, row.Period.MinimumWageIncomeTaxBaseBefore);
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { before, row.CreatedAtUtc, row.UpdatedAtUtc,
                PeriodStatus = row.Period.Status,
                PackageVersion = package.Version, CaseVersion = remediation.Version,
                CaseStatus = remediation.Status, remediation.ProposedValuesJson }))));
        PayrollRowDto? after = null;
        if (reasons.Count == 0)
        {
            var original = new CompensationPackageValuesDto(row.GrossSalary, row.PremiumPay,
                row.RoadAllowance, row.MealAllowance, row.BenefitPay, string.Empty);
            try
            {
                SetAmounts(row, values);
                after = PayrollPeriodProjector.ToRow(
                    row, engine, parameters, row.Period.MinimumWageIncomeTaxBaseBefore);
            }
            finally
            {
                SetAmounts(row, original);
            }
        }
        return new(rowId, periodId, reasons.Count == 0, reasons, token, before, after,
            existing is null ? null : ToDto(existing));
    }

    public static void SetAmounts(PayrollEmployee row, CompensationPackageValuesDto values)
    {
        row.GrossSalary = values.MonthlyGrossSalary;
        row.PremiumPay = values.RegularPremium;
        row.RoadAllowance = values.RoadAllowance;
        row.MealAllowance = values.MealAllowance;
        row.BenefitPay = values.OtherRegularBenefits;
    }

    public static CompensationPayrollApplicationDto ToDto(
        EmployeeCompensationPayrollApplication item)
        => new(item.Id, item.RemediationCaseId, item.PayrollRowId,
            JsonSerializer.Deserialize<PayrollRowDto>(item.BeforeJson)!,
            JsonSerializer.Deserialize<PayrollRowDto>(item.AfterJson)!,
            item.ActorUserId, item.AppliedAtUtc);
}
