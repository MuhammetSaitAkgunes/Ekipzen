using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record PayrollRevisionExternalActionDto(
    int Id, string Kind, string Outcome, int? PreviousRecordId,
    string ResponsibleName, string EvidenceReference, DateOnly? ActionDate,
    decimal? RecordedAmount, string Notes, string RecordedByUserId,
    DateTime RecordedAtUtc);

public sealed record PayrollRevisionExternalReconciliationDto(
    int RevisionId, int RevisionNumber, decimal OriginalNetPay,
    decimal RevisedNetPay, decimal CalculatedNetDifference, bool IsCurrent,
    PayrollRevisionExternalActionDto? Payment,
    PayrollRevisionExternalActionDto? Notification,
    IReadOnlyList<PayrollRevisionExternalActionDto> History);

public sealed record GetPayrollRevisionExternalReconciliationQuery(
    int EmployeeId, int PackageId, int CaseId, int PeriodId, int RowId, int RevisionId)
    : IRequest<PayrollRevisionExternalReconciliationDto>;

public sealed class GetPayrollRevisionExternalReconciliationQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollRevisionExternalReconciliationQuery,
        PayrollRevisionExternalReconciliationDto>
{
    public async Task<PayrollRevisionExternalReconciliationDto> Handle(
        GetPayrollRevisionExternalReconciliationQuery request, CancellationToken cancellationToken)
        => (await PayrollRevisionExternalReconciliationRules.LoadAsync(context, request,
            cancellationToken)).ToDto();
}

public sealed record RecordPayrollRevisionExternalActionCommand(
    int EmployeeId, int PackageId, int CaseId, int PeriodId, int RowId, int RevisionId,
    string Kind, string Outcome, string ResponsibleName, string EvidenceReference,
    DateOnly? ActionDate, decimal? RecordedAmount, string Notes,
    int? ExpectedLatestRecordId, bool ConfirmRecordedStatus)
    : IRequest<PayrollRevisionExternalActionDto>;

public sealed class RecordPayrollRevisionExternalActionCommandValidator
    : AbstractValidator<RecordPayrollRevisionExternalActionCommand>
{
    public RecordPayrollRevisionExternalActionCommandValidator()
    {
        RuleFor(item => item.Kind).Must(kind => kind is "payment" or "notification");
        RuleFor(item => item.Outcome).Must(outcome => outcome is "pending" or "completed" or "not-required");
        RuleFor(item => item.ResponsibleName).Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(256);
        RuleFor(item => item.EvidenceReference).NotNull().MaximumLength(256);
        RuleFor(item => item.Notes).NotNull().MaximumLength(2000);
        RuleFor(item => item.ConfirmRecordedStatus).Equal(true);
        When(item => item.Outcome == "completed", () =>
        {
            RuleFor(item => item.EvidenceReference).Must(value => !string.IsNullOrWhiteSpace(value));
            RuleFor(item => item.ActionDate).NotNull();
            When(item => item.Kind == "payment", () =>
                RuleFor(item => item.RecordedAmount).NotNull().GreaterThanOrEqualTo(0));
            When(item => item.Kind == "notification", () =>
                RuleFor(item => item.RecordedAmount).Null());
        });
        When(item => item.Outcome == "not-required", () =>
        {
            RuleFor(item => item.EvidenceReference).Must(value => !string.IsNullOrWhiteSpace(value));
            RuleFor(item => item.Notes).Must(value => !string.IsNullOrWhiteSpace(value));
            RuleFor(item => item.ActionDate).Null();
            RuleFor(item => item.RecordedAmount).Null();
        });
        When(item => item.Outcome == "pending", () =>
        {
            RuleFor(item => item.EvidenceReference).Empty();
            RuleFor(item => item.ActionDate).Null();
            RuleFor(item => item.RecordedAmount).Null();
        });
    }
}

public sealed class RecordPayrollRevisionExternalActionCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<RecordPayrollRevisionExternalActionCommand, PayrollRevisionExternalActionDto>
{
    public Task<PayrollRevisionExternalActionDto> Handle(
        RecordPayrollRevisionExternalActionCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"payroll-row-external:{request.RowId}",
            token => RecordAsync(request, token), cancellationToken);

    private async Task<PayrollRevisionExternalActionDto> RecordAsync(
        RecordPayrollRevisionExternalActionCommand request, CancellationToken cancellationToken)
    {
        var state = await PayrollRevisionExternalReconciliationRules.LoadAsync(context,
            new(request.EmployeeId, request.PackageId, request.CaseId,
                request.PeriodId, request.RowId, request.RevisionId), cancellationToken);
        if (!state.IsCurrent || state.Row.ApprovalStatus != PayrollApprovalStatus.Approved
            || state.Row.Period!.Status is not (PayrollPeriodStatus.Approved or PayrollPeriodStatus.Closed))
            throw new ConflictException("Yalnız geçerli, onaylı bordro parasal sürümünün dış adımları kaydedilebilir.");
        var previous = state.Records.LastOrDefault(item => item.Kind == request.Kind);
        if (previous?.Id != request.ExpectedLatestRecordId)
            throw new ConflictException("Dış işlem kaydı değişti; güncel kanıtı yeniden inceleyin.");
        var record = new PayrollApprovedRowExternalActionRecord
        {
            RevisionId = state.Revision.Id,
            Kind = request.Kind,
            Outcome = request.Outcome,
            PreviousRecordId = previous?.Id,
            ResponsibleName = request.ResponsibleName.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            ActionDate = request.ActionDate,
            RecordedAmount = request.RecordedAmount,
            Notes = request.Notes.Trim(),
            RecordedByUserId = currentUser.UserId
                ?? throw new ForbiddenAccessException("Dış işlem kanıtı için kullanıcı kimliği gereklidir."),
            RecordedAtUtc = DateTime.UtcNow,
        };
        if (previous is not null && previous.Outcome == record.Outcome
            && previous.ResponsibleName == record.ResponsibleName
            && previous.EvidenceReference == record.EvidenceReference
            && previous.ActionDate == record.ActionDate
            && previous.RecordedAmount == record.RecordedAmount
            && previous.Notes == record.Notes)
            throw new ConflictException("Aynı kanıt zaten geçerli; değişiklik olmadan yeni kayıt açılamaz.");
        context.PayrollApprovedRowExternalActionRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);
        return PayrollRevisionExternalReconciliationRules.ToDto(record);
    }
}

internal sealed record PayrollRevisionExternalReconciliationState(
    PayrollEmployee Row, PayrollApprovedRowRevision Revision,
    IReadOnlyList<PayrollApprovedRowExternalActionRecord> Records, bool IsCurrent)
{
    public PayrollRevisionExternalReconciliationDto ToDto()
    {
        var revision = ApprovedPayrollRevisionRules.ToDto(Revision);
        return new(Revision.Id, Revision.RevisionNumber,
            revision.Before.NetPay, revision.After.NetPay,
            revision.After.NetPay - revision.Before.NetPay, IsCurrent,
            Records.LastOrDefault(item => item.Kind == "payment") is { } payment
                ? PayrollRevisionExternalReconciliationRules.ToDto(payment) : null,
            Records.LastOrDefault(item => item.Kind == "notification") is { } notification
                ? PayrollRevisionExternalReconciliationRules.ToDto(notification) : null,
            Records.OrderByDescending(item => item.Id)
                .Select(PayrollRevisionExternalReconciliationRules.ToDto).ToList());
    }
}

internal static class PayrollRevisionExternalReconciliationRules
{
    public static async Task<PayrollRevisionExternalReconciliationState> LoadAsync(
        IApplicationDbContext context, GetPayrollRevisionExternalReconciliationQuery request,
        CancellationToken cancellationToken)
    {
        var remediation = await context.EmployeeCompensationRemediationCases.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CaseId
                && item.EmployeeId == request.EmployeeId
                && item.CompensationPackageId == request.PackageId, cancellationToken)
            ?? throw new NotFoundException("Ücret telafi dosyası", request.CaseId);
        if (remediation.Status != "approved")
            throw new ConflictException("Onaylı ücret telafi dosyası bulunamadı.");
        var row = await context.PayrollEmployees.AsNoTracking()
            .Include(item => item.Period)
            .SingleOrDefaultAsync(item => item.Id == request.RowId
                && item.PayrollPeriodId == request.PeriodId
                && item.EmployeeId == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("Bordro satırı", request.RowId);
        var revision = await context.PayrollApprovedRowRevisions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.RevisionId
                && item.PayrollRowId == request.RowId
                && item.RemediationCaseId == request.CaseId, cancellationToken)
            ?? throw new NotFoundException("Bordro parasal sürümü", request.RevisionId);
        var currentId = await context.PayrollApprovedRowRevisions.AsNoTracking()
            .Where(item => item.PayrollRowId == request.RowId)
            .OrderByDescending(item => item.RevisionNumber)
            .Select(item => item.Id)
            .FirstAsync(cancellationToken);
        var records = await context.PayrollApprovedRowExternalActionRecords.AsNoTracking()
            .Where(item => item.RevisionId == request.RevisionId)
            .OrderBy(item => item.Id).ToListAsync(cancellationToken);
        return new(row, revision, records, currentId == revision.Id
            && row.ApprovalStatus == PayrollApprovalStatus.Approved
            && row.Period!.Status is PayrollPeriodStatus.Approved or PayrollPeriodStatus.Closed);
    }

    public static PayrollRevisionExternalActionDto ToDto(PayrollApprovedRowExternalActionRecord item)
        => new(item.Id, item.Kind, item.Outcome, item.PreviousRecordId,
            item.ResponsibleName, item.EvidenceReference, item.ActionDate,
            item.RecordedAmount, item.Notes, item.RecordedByUserId,
            item.RecordedAtUtc);
}
