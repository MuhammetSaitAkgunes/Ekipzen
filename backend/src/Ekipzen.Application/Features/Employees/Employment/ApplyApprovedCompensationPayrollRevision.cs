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

public sealed record ApprovedPayrollRevisionDto(
    int Id, int RevisionNumber, int PayrollRowId, int RemediationCaseId,
    PayrollRowDto Before, PayrollRowDto After, string Reason,
    string ExpertReviewReference, string ReviewNotes,
    string ApprovedByUserId, DateTime ApprovedAtUtc);

public sealed record ApplyApprovedCompensationPayrollRevisionCommand(
    int EmployeeId, int PackageId, int CaseId, int PeriodId, int RowId,
    string ExpectedPreviewToken, string ExpertReviewReference, string ReviewNotes,
    bool ConfirmSourceLink, bool ConfirmExternalReconciliation)
    : IRequest<ApprovedPayrollRevisionDto>;

public sealed class ApplyApprovedCompensationPayrollRevisionCommandValidator
    : AbstractValidator<ApplyApprovedCompensationPayrollRevisionCommand>
{
    public ApplyApprovedCompensationPayrollRevisionCommandValidator()
    {
        RuleFor(item => item.ExpectedPreviewToken).NotEmpty().Length(64);
        RuleFor(item => item.ExpertReviewReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.ReviewNotes).NotEmpty().MaximumLength(2000);
        RuleFor(item => item.ConfirmSourceLink).Equal(true);
        RuleFor(item => item.ConfirmExternalReconciliation).Equal(true);
    }
}

public sealed class ApplyApprovedCompensationPayrollRevisionCommandHandler(
    IApplicationDbContext context, IPayrollEngine engine, ICurrentUser currentUser,
    IDatabaseLock databaseLock)
    : IRequestHandler<ApplyApprovedCompensationPayrollRevisionCommand, ApprovedPayrollRevisionDto>
{
    public Task<ApprovedPayrollRevisionDto> Handle(
        ApplyApprovedCompensationPayrollRevisionCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync(PayrollRowGuards.LockResource(request.PeriodId),
            token => ApplyAsync(request, token), cancellationToken);

    private async Task<ApprovedPayrollRevisionDto> ApplyAsync(
        ApplyApprovedCompensationPayrollRevisionCommand request, CancellationToken cancellationToken)
    {
        var preview = await new ReviewApprovedCompensationPayrollImpactQueryHandler(context, engine)
            .Handle(new(request.EmployeeId, request.PackageId, request.CaseId,
                request.PeriodId, request.RowId), cancellationToken);
        if (!preview.CanApply)
            throw new ConflictException(string.Join(" ", preview.BlockingReasons));
        if (!string.Equals(preview.PreviewToken, request.ExpectedPreviewToken,
                StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Onaylı bordro etki dosyası değişti; yeniden önizleyin.");

        var row = await context.PayrollEmployees.AsNoTracking()
            .Include(item => item.Period)!.ThenInclude(period => period!.Settings)!
                .ThenInclude(settings => settings!.TaxBrackets)
            .Include(item => item.Employee)!.ThenInclude(employee => employee!.Department)
            .Include(item => item.Result)
            .SingleAsync(item => item.Id == request.RowId && item.PayrollPeriodId == request.PeriodId
                && item.EmployeeId == request.EmployeeId, cancellationToken);
        var remediation = await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .SingleAsync(item => item.Id == request.CaseId, cancellationToken);
        var parameters = row.Period!.Settings!.ToEngineParameters();
        var before = PayrollPeriodProjector.ToRow(row, engine, parameters,
            row.Period.MinimumWageIncomeTaxBaseBefore);
        var values = JsonSerializer.Deserialize<CompensationPackageValuesDto>(
            remediation.ProposedValuesJson)!;
        CompensationPayrollApplicationRules.SetAmounts(row, values);
        var calculation = engine.Calculate(
            row.ToEngineInput(row.Period.MinimumWageIncomeTaxBaseBefore), parameters);
        PayrollRowReadiness.EnsureApprovable(row, calculation);
        row.ApprovalStatus = PayrollApprovalStatus.Control;
        var after = PayrollPeriodProjector.ToRow(row, engine, parameters,
            row.Period.MinimumWageIncomeTaxBaseBefore)
            with { ApprovalStatus = PayrollApprovalStatus.Approved.ToDto() };

        var revision = new PayrollApprovedRowRevision
        {
            PayrollRowId = row.Id,
            RemediationCaseId = request.CaseId,
            RevisionNumber = 1,
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
        return ApprovedPayrollRevisionRules.ToDto(revision);
    }
}

public static class ApprovedPayrollRevisionRules
{
    public static ApprovedPayrollRevisionDto ToDto(PayrollApprovedRowRevision item)
        => new(item.Id, item.RevisionNumber, item.PayrollRowId, item.RemediationCaseId,
            JsonSerializer.Deserialize<PayrollRowDto>(item.BeforeJson)!,
            JsonSerializer.Deserialize<PayrollRowDto>(item.AfterJson)!,
            item.Reason, item.ExpertReviewReference, item.ReviewNotes,
            item.ApprovedByUserId, item.ApprovedAtUtc);

    public static PayrollRowDto EffectiveRow(PayrollApprovedRowRevision item)
        => JsonSerializer.Deserialize<PayrollRowDto>(item.AfterJson)!;

    public static async Task<Dictionary<int, PayrollApprovedRowRevision>> LatestByRowAsync(
        IApplicationDbContext context, IReadOnlyCollection<int> rowIds,
        CancellationToken cancellationToken)
    {
        if (rowIds.Count == 0) return [];
        var revisions = await context.PayrollApprovedRowRevisions.AsNoTracking()
            .Where(item => rowIds.Contains(item.PayrollRowId))
            .OrderBy(item => item.RevisionNumber)
            .ToListAsync(cancellationToken);
        return revisions.GroupBy(item => item.PayrollRowId)
            .ToDictionary(group => group.Key, group => group.Last());
    }
}
