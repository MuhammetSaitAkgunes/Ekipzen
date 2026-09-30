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

public sealed record ApprovedPayrollRevisionBatchRowDto(int PeriodId, ApprovedPayrollRevisionDto Revision);
public sealed record ApprovedPayrollRevisionBatchDto(
    int Id, int EmployeeId, int SourcePayrollRowId, int Year,
    string ExpertReviewReference, string ReviewNotes, string ApprovedByUserId,
    DateTime ApprovedAtUtc, IReadOnlyList<ApprovedPayrollRevisionBatchRowDto> Rows);

public sealed record ApplyApprovedCompensationPayrollChainCommand(
    int EmployeeId, int PackageId, int CaseId, int PeriodId, int RowId,
    string ExpectedPreviewToken, string ExpertReviewReference, string ReviewNotes,
    bool ConfirmSourceLink, bool ConfirmChainImpact, bool ConfirmExternalReconciliation)
    : IRequest<ApprovedPayrollRevisionBatchDto>;

public sealed class ApplyApprovedCompensationPayrollChainCommandValidator
    : AbstractValidator<ApplyApprovedCompensationPayrollChainCommand>
{
    public ApplyApprovedCompensationPayrollChainCommandValidator()
    {
        RuleFor(item => item.ExpectedPreviewToken).NotEmpty().Length(64);
        RuleFor(item => item.ExpertReviewReference).NotEmpty().MaximumLength(256);
        RuleFor(item => item.ReviewNotes).NotEmpty().MaximumLength(2000);
        RuleFor(item => item.ConfirmSourceLink).Equal(true);
        RuleFor(item => item.ConfirmChainImpact).Equal(true);
        RuleFor(item => item.ConfirmExternalReconciliation).Equal(true);
    }
}

public sealed class ApplyApprovedCompensationPayrollChainCommandHandler(
    IApplicationDbContext context, IPayrollEngine engine, ICurrentUser currentUser,
    IDatabaseLock databaseLock)
    : IRequestHandler<ApplyApprovedCompensationPayrollChainCommand, ApprovedPayrollRevisionBatchDto>
{
    public async Task<ApprovedPayrollRevisionBatchDto> Handle(
        ApplyApprovedCompensationPayrollChainCommand request, CancellationToken cancellationToken)
    {
        var source = await context.PayrollPeriods.AsNoTracking()
            .Where(period => period.Id == request.PeriodId)
            .Select(period => new { period.Year, period.Month })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Bordro dönemi", request.PeriodId);
        var periodIds = await RelevantPeriodIdsAsync(
            source.Year, source.Month, cancellationToken);
        var resources = periodIds.Select(PayrollRowGuards.LockResource)
            .Append("payroll-period-create").ToArray();
        return await databaseLock.ExecuteExclusiveAsync(resources, async token =>
        {
            var currentIds = await RelevantPeriodIdsAsync(
                source.Year, source.Month, token);
            if (!periodIds.SequenceEqual(currentIds))
                throw new ConflictException("Bordro dönemleri değişti; zincirleme etki dosyasını yeniden açın.");
            return await ApplyLockedAsync(request, source.Year, token);
        }, cancellationToken);
    }

    private async Task<int[]> RelevantPeriodIdsAsync(int year, int month,
        CancellationToken cancellationToken)
        => await context.PayrollPeriods.AsNoTracking()
            .Where(period => period.Year > year
                || (period.Year == year && period.Month >= month))
            .Select(period => period.Id).OrderBy(id => id)
            .ToArrayAsync(cancellationToken);

    private async Task<ApprovedPayrollRevisionBatchDto> ApplyLockedAsync(
        ApplyApprovedCompensationPayrollChainCommand request, int year,
        CancellationToken cancellationToken)
    {
        var impact = await new ReviewApprovedCompensationPayrollImpactQueryHandler(context, engine)
            .Handle(new(request.EmployeeId, request.PackageId, request.CaseId,
                request.PeriodId, request.RowId), cancellationToken);
        if (!impact.CanApplyChain)
            throw new ConflictException("Zincirleme uygulama için doğrulanmış ve engelsiz etki dosyası gerekli.");
        if (!string.Equals(impact.ChainImpact.PreviewToken, request.ExpectedPreviewToken,
                StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Zincirleme etki dosyası değişti; yeniden önizleyin.");

        var projected = impact.ChainImpact.LaterApprovedRows;
        var rowIds = projected.Select(item => item.RowId).Prepend(request.RowId).ToArray();
        var rows = await context.PayrollEmployees.AsNoTracking()
            .Where(item => rowIds.Contains(item.Id) && item.EmployeeId == request.EmployeeId)
            .Include(item => item.Period)!.ThenInclude(period => period!.Settings)!
                .ThenInclude(settings => settings!.TaxBrackets)
            .Include(item => item.Employee)!.ThenInclude(employee => employee!.Department)
            .Include(item => item.Result)
            .ToListAsync(cancellationToken);
        if (rows.Count != rowIds.Length)
            throw new ConflictException("Etkilenen bordro satırları değişti; etki dosyasını yeniden açın.");
        var byId = rows.ToDictionary(item => item.Id);
        var existing = await context.PayrollApprovedRowRevisions.AsNoTracking()
            .AnyAsync(item => rowIds.Contains(item.PayrollRowId), cancellationToken);
        if (existing)
            throw new ConflictException("Etkilenen aylardan birinde parasal sürüm var; yeniden inceleme gerekli.");

        var remediation = await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .SingleAsync(item => item.Id == request.CaseId, cancellationToken);
        var values = JsonSerializer.Deserialize<CompensationPackageValuesDto>(
            remediation.ProposedValuesJson)!;
        var actor = currentUser.UserId
            ?? throw new ForbiddenAccessException("Bordro parasal sürüm onayı için oturum sahibi gerekli.");
        var now = DateTime.UtcNow;
        var batch = new PayrollApprovedRevisionBatch
        {
            EmployeeId = request.EmployeeId,
            RemediationCaseId = request.CaseId,
            SourcePayrollRowId = request.RowId,
            Year = year,
            PreviewToken = impact.ChainImpact.PreviewToken,
            ExpertReviewReference = request.ExpertReviewReference.Trim(),
            ReviewNotes = request.ReviewNotes.Trim(),
            ApprovedByUserId = actor,
            ApprovedAtUtc = now,
        };
        context.PayrollApprovedRevisionBatches.Add(batch);
        var revisions = new List<(int PeriodId, PayrollApprovedRowRevision Revision)>();
        foreach (var rowId in rowIds)
        {
            var row = byId[rowId];
            var parameters = row.Period!.Settings!.ToEngineParameters();
            var before = PayrollPeriodProjector.ToRow(row, engine, parameters,
                row.Period.MinimumWageIncomeTaxBaseBefore);
            if (rowId == request.RowId)
                CompensationPayrollApplicationRules.SetAmounts(row, values);
            else
            {
                var projection = projected.Single(item => item.RowId == rowId);
                row.PreviousTaxBase = projection.ProjectedPreviousTaxBase
                    ?? throw new ConflictException("Sonraki ayın vergi matrahı tahmin edilemedi.");
            }
            var calculated = engine.Calculate(
                row.ToEngineInput(row.Period.MinimumWageIncomeTaxBaseBefore), parameters);
            PayrollRowReadiness.EnsureApprovable(row, calculated);
            var expected = rowId == request.RowId ? impact.Hypothetical
                : projected.Single(item => item.RowId == rowId).Projected;
            if (expected is null || calculated.GrossEarnings != expected.GrossEarnings
                || calculated.IncomeTaxBase != expected.IncomeTaxBase
                || calculated.IncomeTax != expected.IncomeTax
                || calculated.NetPay != expected.NetPay
                || calculated.EmployerCost != expected.EmployerCost)
                throw new ConflictException("Yeniden hesaplanan bordro etki dosyasıyla uyuşmuyor.");
            row.ApprovalStatus = PayrollApprovalStatus.ReadyForApproval;
            var after = PayrollPeriodProjector.ToRow(row, engine, parameters,
                row.Period.MinimumWageIncomeTaxBaseBefore)
                with { ApprovalStatus = PayrollApprovalStatus.Approved.ToDto() };
            var revision = new PayrollApprovedRowRevision
            {
                Batch = batch,
                PayrollRowId = row.Id,
                RemediationCaseId = request.CaseId,
                RevisionNumber = 1,
                BeforeJson = JsonSerializer.Serialize(before),
                AfterJson = JsonSerializer.Serialize(after),
                Reason = remediation.IssueDescription,
                ExpertReviewReference = batch.ExpertReviewReference,
                ReviewNotes = batch.ReviewNotes,
                ApprovedByUserId = actor,
                ApprovedAtUtc = now,
            };
            context.PayrollApprovedRowRevisions.Add(revision);
            revisions.Add((row.PayrollPeriodId, revision));
        }
        await context.SaveChangesAsync(cancellationToken);
        return new(batch.Id, request.EmployeeId, request.RowId, year,
            batch.ExpertReviewReference, batch.ReviewNotes, actor, now,
            revisions.Select(item => new ApprovedPayrollRevisionBatchRowDto(
                item.PeriodId, ApprovedPayrollRevisionRules.ToDto(item.Revision))).ToList());
    }
}
