using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Offboarding;

public sealed record OffboardingExternalActionDto(
    int Id, string Kind, string Outcome, int? PreviousRecordId,
    string ResponsibleName, string EvidenceReference, DateOnly? ActionDate,
    decimal? RecordedAmount, string Notes, string RecordedByUserId,
    DateTime RecordedAtUtc);

public sealed record OffboardingExternalReconciliationDto(
    int SettlementRevisionId, int RevisionNumber, decimal OriginalTotal,
    decimal PreviousEffectiveTotal,
    decimal RevisedTotal, decimal CalculationDifference, bool IsCurrent,
    OffboardingExternalActionDto? Payment,
    OffboardingExternalActionDto? Notification,
    IReadOnlyList<OffboardingExternalActionDto> History);

public sealed record GetOffboardingExternalReconciliationQuery(
    int EmployeeId, int PackageId, int RemediationCaseId, int OffboardingCaseId,
    int SettlementRevisionId) : IRequest<OffboardingExternalReconciliationDto>;

public sealed class GetOffboardingExternalReconciliationQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOffboardingExternalReconciliationQuery,
        OffboardingExternalReconciliationDto>
{
    public async Task<OffboardingExternalReconciliationDto> Handle(
        GetOffboardingExternalReconciliationQuery request, CancellationToken cancellationToken)
        => (await OffboardingExternalReconciliationRules.LoadAsync(context, request.EmployeeId,
            request.PackageId, request.RemediationCaseId, request.OffboardingCaseId,
            request.SettlementRevisionId, cancellationToken)).ToDto();
}

public sealed record RecordOffboardingExternalActionCommand(
    int EmployeeId, int PackageId, int RemediationCaseId, int OffboardingCaseId,
    int SettlementRevisionId, string Kind, string Outcome,
    string ResponsibleName, string EvidenceReference, DateOnly? ActionDate,
    decimal? RecordedAmount, string Notes, int? ExpectedLatestRecordId,
    bool ConfirmRecordedStatus, int? GuidanceContentRevisionId = null)
    : IRequest<OffboardingExternalActionDto>, IGuidanceOperationCommand
{ public string GuidanceOperationKey => "offboarding-external-record"; }

public sealed class RecordOffboardingExternalActionCommandValidator
    : AbstractValidator<RecordOffboardingExternalActionCommand>
{
    public RecordOffboardingExternalActionCommandValidator()
    {
        RuleFor(item => item.Kind).Must(kind => kind is "payment" or "notification");
        RuleFor(item => item.Outcome).Must(outcome => outcome is "pending" or "completed" or "not-required");
        RuleFor(item => item.ResponsibleName)
            .Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(256);
        RuleFor(item => item.EvidenceReference).NotNull().MaximumLength(256);
        RuleFor(item => item.Notes).NotNull().MaximumLength(2000);
        RuleFor(item => item.ConfirmRecordedStatus).Equal(true);
        When(item => item.Outcome == "completed", () =>
        {
            RuleFor(item => item.EvidenceReference)
                .Must(value => !string.IsNullOrWhiteSpace(value));
            RuleFor(item => item.ActionDate).NotNull();
            When(item => item.Kind == "payment", () =>
                RuleFor(item => item.RecordedAmount).NotNull().GreaterThanOrEqualTo(0));
            When(item => item.Kind == "notification", () =>
                RuleFor(item => item.RecordedAmount).Null());
        });
        When(item => item.Outcome == "not-required", () =>
        {
            RuleFor(item => item.EvidenceReference)
                .Must(value => !string.IsNullOrWhiteSpace(value));
            RuleFor(item => item.Notes)
                .Must(value => !string.IsNullOrWhiteSpace(value));
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

public sealed class RecordOffboardingExternalActionCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock, GuidanceOperationContext guidance)
    : IRequestHandler<RecordOffboardingExternalActionCommand, OffboardingExternalActionDto>
{
    public Task<OffboardingExternalActionDto> Handle(
        RecordOffboardingExternalActionCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"employee-offboarding:{request.EmployeeId}",
            token => RecordAsync(request, token), cancellationToken);

    private async Task<OffboardingExternalActionDto> RecordAsync(
        RecordOffboardingExternalActionCommand request, CancellationToken cancellationToken)
    {
        var state = await OffboardingExternalReconciliationRules.LoadAsync(context,
            request.EmployeeId, request.PackageId, request.RemediationCaseId,
            request.OffboardingCaseId, request.SettlementRevisionId, cancellationToken);
        if (!state.IsCurrent || state.Case.Status != EmployeeOffboardingStatus.Completed
            || state.Case.Employee!.Status != EmployeeStatus.Passive
            || state.Case.Employee.TerminationDate != state.Case.TerminationDate)
            throw new ConflictException("Yalnız geçerli, tamamlanmış ayrılış parasal sürümünün dış adımları kaydedilebilir.");
        var previous = state.Records.LastOrDefault(item => item.Kind == request.Kind);
        if (previous?.Id != request.ExpectedLatestRecordId)
            throw new ConflictException("Dış işlem kaydı değişti; güncel kanıtı yeniden inceleyin.");
        var record = new EmployeeOffboardingExternalActionRecord
        {
            SettlementRevisionId = state.Revision.Id,
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
        context.EmployeeOffboardingExternalActionRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);
        await PreparationOperationEvidence.SaveAsync(context, guidance, currentUser, request.GuidanceOperationKey,
                "offboarding-external-action", record.Id, [request.Kind == "payment" ? "Ödeme kanıtı" : "Bildirim kanıtı", request.Outcome], cancellationToken);
        return OffboardingExternalReconciliationRules.ToDto(record);
    }
}

internal sealed record OffboardingExternalReconciliationState(
    EmployeeOffboardingCase Case, EmployeeOffboardingSettlementRevision Revision,
    IReadOnlyList<EmployeeOffboardingExternalActionRecord> Records,
    decimal PreviousEffectiveTotal, bool IsCurrent)
{
    public OffboardingExternalReconciliationDto ToDto()
    {
        var original = OffboardingExternalReconciliationRules.Total(Case);
        var revised = OffboardingExternalReconciliationRules.Total(Revision);
        return new(Revision.Id, Revision.RevisionNumber, original,
            PreviousEffectiveTotal, revised,
            revised - PreviousEffectiveTotal, IsCurrent,
            Records.LastOrDefault(item => item.Kind == "payment") is { } payment
                ? OffboardingExternalReconciliationRules.ToDto(payment) : null,
            Records.LastOrDefault(item => item.Kind == "notification") is { } notification
                ? OffboardingExternalReconciliationRules.ToDto(notification) : null,
            Records.OrderByDescending(item => item.Id)
                .Select(OffboardingExternalReconciliationRules.ToDto).ToList());
    }
}

internal static class OffboardingExternalReconciliationRules
{
    public static async Task<OffboardingExternalReconciliationState> LoadAsync(
        IApplicationDbContext context, int employeeId, int packageId,
        int remediationCaseId, int offboardingCaseId, int revisionId,
        CancellationToken cancellationToken)
    {
        var offboarding = await context.EmployeeOffboardingCases
            .Include(item => item.Employee)
            .SingleOrDefaultAsync(item => item.Id == offboardingCaseId
                && item.EmployeeId == employeeId
                && item.CompensationPackageId == packageId, cancellationToken)
            ?? throw new NotFoundException("İşten çıkış dosyası", offboardingCaseId);
        var revision = await context.EmployeeOffboardingSettlementRevisions
            .SingleOrDefaultAsync(item => item.Id == revisionId
                && item.OffboardingCaseId == offboardingCaseId
                && item.RemediationCaseId == remediationCaseId
                && item.Status == "approved", cancellationToken)
            ?? throw new NotFoundException("Onaylı parasal sürüm", revisionId);
        var currentId = await context.EmployeeOffboardingSettlementRevisions
            .Where(item => item.OffboardingCaseId == offboardingCaseId
                && item.Status == "approved")
            .OrderByDescending(item => item.RevisionNumber)
            .Select(item => item.Id)
            .FirstAsync(cancellationToken);
        var previousTotal = Total(offboarding);
        if (revision.BasedOnApprovedRevisionId is int previousId)
        {
            var previous = await context.EmployeeOffboardingSettlementRevisions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == previousId
                    && item.OffboardingCaseId == offboardingCaseId
                    && item.Status == "approved", cancellationToken)
                ?? throw new ConflictException("Önceki onaylı parasal sürüm bulunamadı.");
            previousTotal = Total(previous);
        }
        var records = await context.EmployeeOffboardingExternalActionRecords
            .AsNoTracking()
            .Where(item => item.SettlementRevisionId == revisionId)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return new(offboarding, revision, records, previousTotal, currentId == revisionId);
    }

    public static decimal Total(EmployeeOffboardingCase item)
        => item.UnusedLeavePayment + item.SeverancePayment + item.NoticePayment;
    public static decimal Total(EmployeeOffboardingSettlementRevision item)
        => item.UnusedLeavePayment + item.SeverancePayment + item.NoticePayment;
    public static OffboardingExternalActionDto ToDto(EmployeeOffboardingExternalActionRecord item)
        => new(item.Id, item.Kind, item.Outcome, item.PreviousRecordId,
            item.ResponsibleName, item.EvidenceReference, item.ActionDate,
            item.RecordedAmount, item.Notes, item.RecordedByUserId,
            item.RecordedAtUtc);
}
