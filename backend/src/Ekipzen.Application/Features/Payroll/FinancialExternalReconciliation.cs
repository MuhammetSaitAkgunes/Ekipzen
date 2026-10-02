using System.Globalization;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Offboarding;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Application.Features.Payroll.Periods;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Payroll;

public sealed record FinancialExternalReconciliationDto(string SourceKind, int ResourceId, decimal OriginalTotal,
    bool CanRecord, string SourceToken, OffboardingExternalActionDto? Payment,
    OffboardingExternalActionDto? Notification, IReadOnlyList<OffboardingExternalActionDto> History);

public sealed record FinancialExternalActionBody(string Kind, string Outcome, string ResponsibleName,
    string EvidenceReference, DateOnly? ActionDate, decimal? RecordedAmount, string Notes,
    int? ExpectedLatestRecordId, bool ConfirmRecordedStatus, string ExpectedSourceToken,
    int? GuidanceContentRevisionId = null);

public sealed record GetFinancialExternalReconciliationQuery(string SourceKind, int ParentId, int ResourceId)
    : IRequest<FinancialExternalReconciliationDto>;

public sealed class GetFinancialExternalReconciliationQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetFinancialExternalReconciliationQuery, FinancialExternalReconciliationDto>
{
    public Task<FinancialExternalReconciliationDto> Handle(GetFinancialExternalReconciliationQuery request, CancellationToken ct)
        => FinancialExternalReconciliation.ReadAsync(db, request.SourceKind, request.ParentId, request.ResourceId, ct);
}

public sealed record RecordFinancialExternalActionCommand(string SourceKind, int ParentId, int ResourceId,
    FinancialExternalActionBody Body) : IRequest<OffboardingExternalActionDto>, IGuidanceOperationCommand
{
    public int? GuidanceContentRevisionId => Body.GuidanceContentRevisionId;
    public string GuidanceOperationKey => "financial-external-record";
}

public sealed class RecordFinancialExternalActionCommandValidator : AbstractValidator<RecordFinancialExternalActionCommand>
{
    public RecordFinancialExternalActionCommandValidator()
    {
        RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () => {
            RuleFor(x => x.Body.ExpectedSourceToken).NotEmpty().MaximumLength(64);
            // İlk hesap ve revizyon kanıtlarının tamamlanma koşulları aynı kalır.
            RuleFor(x => x.Body).Custom((body, validation) => {
                var check = new RecordOffboardingExternalActionCommandValidator().Validate(
                    new RecordOffboardingExternalActionCommand(0, 0, 0, 0, 0, body.Kind, body.Outcome,
                        body.ResponsibleName, body.EvidenceReference, body.ActionDate, body.RecordedAmount,
                        body.Notes, body.ExpectedLatestRecordId, body.ConfirmRecordedStatus));
                foreach (var error in check.Errors) validation.AddFailure(error.PropertyName, error.ErrorMessage);
            });
        });
    }
}

public sealed class RecordFinancialExternalActionCommandHandler(IApplicationDbContext db, ICurrentUser user,
    IDatabaseLock gate, GuidanceOperationContext guidance)
    : IRequestHandler<RecordFinancialExternalActionCommand, OffboardingExternalActionDto>
{
    public Task<OffboardingExternalActionDto> Handle(RecordFinancialExternalActionCommand request, CancellationToken ct)
        => gate.ExecuteExclusiveAsync(request.SourceKind == "payroll" ? PayrollRowGuards.LockResource(request.ParentId)
            : $"employee-offboarding:{request.ParentId}", async token => {
                var state = await FinancialExternalReconciliation.ReadAsync(db, request.SourceKind,
                    request.ParentId, request.ResourceId, token);
                if (!state.CanRecord) throw new ConflictException("İlk hesap geçerli değil; güncel parasal sürümün dış kanıt akışını kullanın.");
                var body = request.Body;
                PreparationOperationEvidence.RequireToken(body.ExpectedSourceToken, state.SourceToken, true);
                var previous = body.Kind == "payment" ? state.Payment : state.Notification;
                if (previous?.Id != body.ExpectedLatestRecordId) throw new ConflictException("Dış işlem kanıtı değişti; formunuzu koruyup güncel kaydı inceleyin.");
                if (previous is not null && previous.Outcome == body.Outcome && previous.ResponsibleName == body.ResponsibleName.Trim()
                    && previous.EvidenceReference == body.EvidenceReference.Trim() && previous.ActionDate == body.ActionDate
                    && previous.RecordedAmount == body.RecordedAmount && previous.Notes == body.Notes.Trim())
                    throw new ConflictException("Aynı kanıt zaten kayıtlı; yeni işlem yapılmadı.");
                var record = new FinancialExternalActionRecord {
                    PayrollRowId = request.SourceKind == "payroll" ? request.ResourceId : null,
                    OffboardingCaseId = request.SourceKind == "offboarding" ? request.ResourceId : null,
                    Kind = body.Kind, Outcome = body.Outcome, PreviousRecordId = previous?.Id,
                    ResponsibleName = body.ResponsibleName.Trim(), EvidenceReference = body.EvidenceReference.Trim(),
                    ActionDate = body.ActionDate, RecordedAmount = body.RecordedAmount, Notes = body.Notes.Trim(),
                    RecordedByUserId = user.UserId ?? throw new ForbiddenAccessException("Oturum sahibi gerekli."),
                    RecordedAtUtc = DateTime.UtcNow,
                };
                db.FinancialExternalActionRecords.Add(record);
                await db.SaveChangesAsync(token);
                await PreparationOperationEvidence.SaveAsync(db, guidance, user, request.GuidanceOperationKey,
                    request.SourceKind + "-original-external", record.Id,
                    [body.Kind == "payment" ? "Ödeme kanıtı" : "Bildirim kanıtı", body.Outcome], token);
                return FinancialExternalReconciliation.ToDto(record);
            }, ct);
}

public static class FinancialExternalReconciliation
{
    public static async Task<FinancialExternalReconciliationDto> ReadAsync(IApplicationDbContext db,
        string kind, int parentId, int resourceId, CancellationToken ct)
    {
        decimal total;
        bool canRecord;
        string source;
        if (kind == "payroll")
        {
            var row = await db.PayrollEmployees.AsNoTracking().Include(x => x.Period).Include(x => x.Result)
                .SingleOrDefaultAsync(x => x.Id == resourceId && x.PayrollPeriodId == parentId, ct)
                ?? throw new NotFoundException("Bordro satırı", resourceId);
            total = row.Result?.NetPay ?? 0;
            canRecord = row.ApprovalStatus == PayrollApprovalStatus.Approved && row.Result is not null
                && row.Period!.Status is PayrollPeriodStatus.Approved or PayrollPeriodStatus.Closed
                && !await db.PayrollApprovedRowRevisions.AnyAsync(x => x.PayrollRowId == resourceId, ct);
            source = $"{row.Id}:{row.Result?.Id}:{row.ApprovalStatus}:{row.Period!.Status}";
        }
        else if (kind == "offboarding")
        {
            var item = await db.EmployeeOffboardingCases.AsNoTracking().Include(x => x.Employee)
                .SingleOrDefaultAsync(x => x.Id == resourceId && x.EmployeeId == parentId, ct)
                ?? throw new NotFoundException("Ayrılış dosyası", resourceId);
            total = item.UnusedLeavePayment + item.SeverancePayment + item.NoticePayment;
            canRecord = item.Status == EmployeeOffboardingStatus.Completed
                && item.Employee!.Status == EmployeeStatus.Passive && item.Employee.TerminationDate == item.TerminationDate
                && !await db.EmployeeOffboardingSettlementRevisions.AnyAsync(x => x.OffboardingCaseId == resourceId && x.Status == "approved", ct);
            source = $"{item.Id}:{item.Version}:{item.Status}:{item.Employee!.Status}:{item.Employee.TerminationDate}";
        }
        else throw new FluentValidation.ValidationException("Dış kanıt kaynağı tanımlı değil.");
        var history = await db.FinancialExternalActionRecords.AsNoTracking()
            .Where(x => kind == "payroll" ? x.PayrollRowId == resourceId : x.OffboardingCaseId == resourceId)
            .OrderByDescending(x => x.Id).ToListAsync(ct);
        var dtos = history.Select(ToDto).ToList();
        return new(kind, resourceId, total, canRecord,
            PreparationOperationEvidence.RecordToken(source, total.ToString(CultureInfo.InvariantCulture), canRecord.ToString()),
            dtos.FirstOrDefault(x => x.Kind == "payment"), dtos.FirstOrDefault(x => x.Kind == "notification"), dtos);
    }

    internal static OffboardingExternalActionDto ToDto(FinancialExternalActionRecord x)
        => new(x.Id, x.Kind, x.Outcome, x.PreviousRecordId, x.ResponsibleName, x.EvidenceReference,
            x.ActionDate, x.RecordedAmount, x.Notes, x.RecordedByUserId, x.RecordedAtUtc);
}
