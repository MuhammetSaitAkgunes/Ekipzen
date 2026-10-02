using Ekipzen.Application.Features.Guidance;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Application.Features.Payroll.Periods;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record ApplyApprovedCompensationPayrollNextRevisionCommand(
    int EmployeeId, int PackageId, int CaseId, int PeriodId, int RowId,
    int ExpectedCurrentRevisionId, string ExpectedPreviewToken,
    string ExpertReviewReference, string ReviewNotes,
    bool ConfirmSourceLink, bool ConfirmExternalReconciliation, int? GuidanceContentRevisionId = null)
    : IRequest<ApprovedPayrollRevisionDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "payroll-revision-next"; }

public sealed class ApplyApprovedCompensationPayrollNextRevisionCommandValidator
    : AbstractValidator<ApplyApprovedCompensationPayrollNextRevisionCommand>
{
    public ApplyApprovedCompensationPayrollNextRevisionCommandValidator()
    {
        RuleFor(item => item.ExpectedCurrentRevisionId).GreaterThan(0);
        RuleFor(item => item.ExpectedPreviewToken).NotEmpty().Length(64);
        RuleFor(item => item.ExpertReviewReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.ReviewNotes).NotEmpty().MaximumLength(2000);
        RuleFor(item => item.ConfirmSourceLink).Equal(true);
        RuleFor(item => item.ConfirmExternalReconciliation).Equal(true);
    }
}

public sealed class ApplyApprovedCompensationPayrollNextRevisionCommandHandler(
    IApplicationDbContext context, IPayrollEngine engine, ICurrentUser currentUser,
    IDatabaseLock databaseLock, GuidanceOperationContext guidance)
    : IRequestHandler<ApplyApprovedCompensationPayrollNextRevisionCommand, ApprovedPayrollRevisionDto>
{
    public async Task<ApprovedPayrollRevisionDto> Handle(
        ApplyApprovedCompensationPayrollNextRevisionCommand request,
        CancellationToken cancellationToken)
    {
        var source = await context.PayrollPeriods.AsNoTracking()
            .Where(period => period.Id == request.PeriodId)
            .Select(period => new { period.Year, period.Month })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Bordro dönemi", request.PeriodId);
        var periodIds = await RelevantPeriodIdsAsync(source.Year, source.Month, cancellationToken);
        var resources = periodIds.Select(PayrollRowGuards.LockResource)
            .Append("payroll-period-create")
            .Append($"employee-compensation:{request.EmployeeId}").ToArray();
        return await databaseLock.ExecuteExclusiveAsync(resources, async token =>
        {
            var currentIds = await RelevantPeriodIdsAsync(source.Year, source.Month, token);
            if (!periodIds.SequenceEqual(currentIds))
                throw new ConflictException("Bordro dönemleri değişti; etki dosyasını yeniden açın.");
            return await ApplyLockedAsync(request, token);
        }, cancellationToken);
    }

    private async Task<int[]> RelevantPeriodIdsAsync(int year, int month,
        CancellationToken cancellationToken)
        => await context.PayrollPeriods.AsNoTracking()
            .Where(period => period.Year > year
                || (period.Year == year && period.Month >= month))
            .Select(period => period.Id).OrderBy(id => id)
            .ToArrayAsync(cancellationToken);

    private async Task<ApprovedPayrollRevisionDto> ApplyLockedAsync(
        ApplyApprovedCompensationPayrollNextRevisionCommand request,
        CancellationToken cancellationToken)
    {
        var preview = await new ReviewApprovedCompensationPayrollImpactQueryHandler(context, engine)
            .Handle(new(request.EmployeeId, request.PackageId, request.CaseId,
                request.PeriodId, request.RowId), cancellationToken);
        if (!preview.CanApplyNext || preview.Revision?.Id != request.ExpectedCurrentRevisionId)
            throw new ConflictException("Geçerli parasal sürüm veya uzman dosyası değişti; yeniden önizleyin.");
        if (!string.Equals(preview.PreviewToken, request.ExpectedPreviewToken,
                StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Onaylı bordro etki dosyası değişti; yeniden önizleyin.");

        var row = await context.PayrollEmployees.AsNoTracking()
            .Include(item => item.Period)!.ThenInclude(period => period!.Settings)!
                .ThenInclude(settings => settings!.TaxBrackets)
            .Include(item => item.Employee)!.ThenInclude(employee => employee!.Department)
            .Include(item => item.Result)
            .SingleAsync(item => item.Id == request.RowId
                && item.PayrollPeriodId == request.PeriodId
                && item.EmployeeId == request.EmployeeId, cancellationToken);
        var current = await context.PayrollApprovedRowRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == request.ExpectedCurrentRevisionId
                && item.PayrollRowId == request.RowId, cancellationToken);
        var before = ApprovedPayrollRevisionRules.EffectiveRow(current)
            with { RevisionNumber = current.RevisionNumber };
        ApprovedPayrollRevisionRules.ApplyEffectiveInputs(row, before);
        var remediation = await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .SingleAsync(item => item.Id == request.CaseId, cancellationToken);
        var values = JsonSerializer.Deserialize<CompensationPackageValuesDto>(
            remediation.ProposedValuesJson)!;
        CompensationPayrollApplicationRules.SetAmounts(row, values);
        var parameters = row.Period!.Settings!.ToEngineParameters();
        var calculated = engine.Calculate(
            row.ToEngineInput(row.Period.MinimumWageIncomeTaxBaseBefore), parameters);
        PayrollRowReadiness.EnsureApprovable(row, calculated);
        if (preview.Hypothetical is not { } expected
            || calculated.GrossEarnings != expected.GrossEarnings
            || calculated.IncomeTaxBase != expected.IncomeTaxBase
            || calculated.IncomeTax != expected.IncomeTax
            || calculated.NetPay != expected.NetPay
            || calculated.EmployerCost != expected.EmployerCost)
            throw new ConflictException("Yeniden hesaplanan bordro etki dosyasıyla uyuşmuyor.");
        row.ApprovalStatus = PayrollApprovalStatus.ReadyForApproval;
        var after = PayrollPeriodProjector.ToRow(row, engine, parameters,
            row.Period.MinimumWageIncomeTaxBaseBefore)
            with { ApprovalStatus = PayrollApprovalStatus.Approved.ToDto(),
                RevisionNumber = current.RevisionNumber + 1 };
        var revision = new PayrollApprovedRowRevision
        {
            PayrollRowId = row.Id,
            RemediationCaseId = request.CaseId,
            BasedOnRevisionId = current.Id,
            RevisionNumber = current.RevisionNumber + 1,
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after),
            Reason = remediation.IssueDescription,
            ExpertReviewReference = request.ExpertReviewReference.Trim(),
            ReviewNotes = request.ReviewNotes.Trim(),
            ApprovedByUserId = currentUser.UserId
                ?? throw new ForbiddenAccessException("Bordro parasal sürüm onayı için oturum sahibi gerekli."),
            ApprovedAtUtc = DateTime.UtcNow,
        };
        context.PayrollApprovedRowRevisions.Add(revision);
        await context.SaveChangesAsync(cancellationToken);
        await PreparationOperationEvidence.SaveAsync(context, guidance, currentUser, request.GuidanceOperationKey,
                "payroll-revision", revision.Id, ["Önceki sürüm korunarak yeni parasal sürüm"], cancellationToken);
        return ApprovedPayrollRevisionRules.ToDto(revision);
    }
}
