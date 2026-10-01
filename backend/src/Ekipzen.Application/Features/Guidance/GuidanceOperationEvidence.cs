using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Guidance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Guidance;

public interface IGuidanceOperationCommand
{
    int? GuidanceContentRevisionId { get; }
    string GuidanceOperationKey { get; }
}

public sealed record GuidanceOperationSnapshot(int ContentRevisionId, string ContentKey, string ProcessKey,
    string StepKey, int ContentVersion, string Title);
public sealed record GuidanceOperationEvidenceDto(string OperationKey, int ResourceId, string Outcome,
    DateTime OccurredAtUtc, GuidanceOperationSnapshot Content, string EvidenceKey, GuidanceSetupChangesDto? SetupChanges = null,
    GuidancePersonnelChangesDto? PersonnelChanges = null);
public sealed record GuidancePersonnelChangesDto(int EmployeeId, int? DocumentId, IReadOnlyList<string> ChangedFields, string? Reason,
    int? EmploymentPeriodId = null, int? CompensationPackageId = null, int? RelatedPackageId = null, int? FromVersion = null, int? ToVersion = null);
public sealed record GuidanceSetupChangesDto(bool AddedCompanyProfile, int AddedLeaveTypeCount, int AddedHolidayCount, bool AddedPayrollSettings);
public sealed record GuidanceOperationHistoryDto(GuidanceOperationEvidenceDto Evidence, bool IsCurrent);

public sealed class GuidanceOperationContext
{
    internal GuidanceOperationSnapshot? Snapshot { get; set; }
    public bool IsGuided => Snapshot is not null;
    // Çağrı yerleri yalnız yeni işlem kanıtlarıdır; eski kanıtı sonradan damgalamaz.
    public void Stamp(IGuidanceOperationEvidence evidence)
    {
        if (Snapshot is not null) evidence.GuidanceSnapshotJson = JsonSerializer.Serialize(Snapshot);
    }
}

public sealed class GuidanceOperationBehavior<TRequest, TResponse>(IPlatformDbContext platform,
    IGuidanceCatalogLock catalogGate, ISender sender, ICurrentUser user, GuidanceOperationContext context)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IGuidanceOperationCommand command) return await next();
        if (command.GuidanceContentRevisionId is null)
        {
            var previous = context.Snapshot;
            context.Snapshot = null;
            try { return await next(); }
            finally { context.Snapshot = previous; }
        }
        var id = command.GuidanceContentRevisionId.Value;
        if (id <= 0) throw new ValidationException("Rehber içerik sürümü pozitif olmalıdır.");
        var (process, step) = command.GuidanceOperationKey switch
        {
            "employee-create" => ("personnel-entry", "tab-is"),
            "employee-update" => ("personnel", "record-review"),
            "employee-rehire" => ("personnel", "rehire"),
            "employee-compensation-create" or "employee-compensation-correct" or "employee-compensation-date" => ("personnel-entry", "tab-mali"),
            "employee-document-upload" or "employee-document-withdraw" => ("personnel-entry", "tab-evrak"),
            "candidate-first-review" => ("recruitment", "first-review"),
            "leave-decision" => ("leave", "decision-effect"),
            "payroll-period-create" => ("payroll", "period-create"),
            "payroll-row-approve" or "payroll-period-submit" => ("payroll", "approval"),
            "offboarding-complete" => ("offboarding", "completion"),
            "offboarding-reverse" => ("offboarding", "reversal-review"),
            "tenant-setup" => ("company", "initial-setup"),
            "attendance-create" => ("attendance", "entry-review"),
            "attendance-update" => ("attendance", "entry-correction"),
            "compliance-create" => ("compliance", "record-review"),
            "compliance-update" or "compliance-owner" => ("compliance", "record-correction"),
            "compliance-status" => ("compliance", "status-review"),
            _ => throw new ConflictException("Bu işlem için sürümlü rehber bağlantısı tanımlı değil."),
        };
        GuidancePublishedSteps.RequireAccess(process, step, user);
        var key = await platform.GuidanceContentRevisions.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.ContentKey).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Rehber sürümü", id);
        // Diğer rehber yazımları gibi merkezî yayın kilidi önce, işin kiracı kilidi sonra.
        return await catalogGate.ExecuteAsync(key, async lockedCt =>
        {
            var content = (await sender.Send(new GetGuidanceCatalogQuery(VersionId: id), lockedCt)).Single();
            if (!content.IsCurrent) throw new ConflictException("Seçilen rehber sürümü güncel değil. Formunuz korunur; güncel sürümü açıkça seçin veya rehber bağlantısını kaldırın.");
            if (content.ProcessKey != process || content.Body.StepKey != step || content.ContentKey != $"{process}-{step}")
                throw new ConflictException("Seçilen rehber sürümü bu gerçek işlemin adımına ait değil.");
            var previous = context.Snapshot;
            context.Snapshot = new(content.Id, content.ContentKey, content.ProcessKey,
                step, content.ContentVersion, content.Body.Title);
            try { return await next(); }
            finally { context.Snapshot = previous; }
        }, ct);
    }
}

public static class GuidanceOperationEvidenceReader
{
    public static async Task<IReadOnlyList<GuidanceOperationEvidenceDto>> ReadAsync(IApplicationDbContext db,
        string userId, CancellationToken ct)
    {
        var employees = await db.EmployeeCreationEvidences.AsNoTracking()
            .Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var personnel = await db.GuidancePersonnelEvidences.AsNoTracking()
            .Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var candidates = await db.CandidateReviewEvidences.AsNoTracking()
            .Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var leaves = await db.GuidanceLeaveDecisionEvidences.AsNoTracking()
            .Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var setups = await db.GuidanceSetupEvidences.AsNoTracking().Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var attendance = await db.GuidanceAttendanceEvidences.AsNoTracking().Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var compliance = await db.GuidanceComplianceEvidences.AsNoTracking().Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var periods = await db.GuidancePayrollPeriodEvidences.AsNoTracking().Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var rows = await db.GuidancePayrollRowEvidences.AsNoTracking().Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var offboarding = await db.GuidanceOffboardingEvidences.AsNoTracking().Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var result = employees.Select(x => Item("employee-create", x.Id, x.EmployeeId, "created", x.CreatedAtUtc, x.GuidanceSnapshotJson!))
            .Concat(personnel.Select(x => Item(x.OperationKey, x.Id, x.DocumentId ?? x.CompensationPackageId ?? x.EmploymentPeriodId ?? x.EmployeeId,
                x.OperationKey switch { "employee-document-upload" => "uploaded", "employee-document-withdraw" => "withdrawn",
                    "employee-rehire" => "rehired", "employee-compensation-create" => "created", _ => "updated" },
                x.CreatedAtUtc, x.GuidanceSnapshotJson!) with
                { PersonnelChanges = new(x.EmployeeId, x.DocumentId, JsonSerializer.Deserialize<string[]>(x.ChangedFieldsJson)!, x.Reason,
                    x.EmploymentPeriodId, x.CompensationPackageId, x.RelatedPackageId, x.FromVersion, x.ToVersion) }))
            .Concat(candidates.Select(x => Item("candidate-first-review", x.Id, x.CandidateId, x.DecisionStatus.ToString(), x.CreatedAtUtc, x.GuidanceSnapshotJson!)))
            .Concat(leaves.Select(x => Item("leave-decision", x.Id, x.LeaveRequestId, x.DecisionStatus.ToString(), x.CreatedAtUtc, x.GuidanceSnapshotJson!)))
            .Concat(setups.Select(x => Item("tenant-setup", x.Id, x.TemplateYear, "initialized", x.CreatedAtUtc, x.GuidanceSnapshotJson!) with
                { SetupChanges = new(x.AddedCompanyProfile, x.AddedLeaveTypeCount, x.AddedHolidayCount, x.AddedPayrollSettings) }))
            .Concat(attendance.Select(x => Item(x.OperationKey, x.Id, x.AttendanceRecordId, x.OperationKey == "attendance-create" ? "created" : "updated", x.CreatedAtUtc, x.GuidanceSnapshotJson!)))
            .Concat(compliance.Select(x => Item(x.OperationKey, x.Id, x.ComplianceDocumentId, x.Outcome, x.CreatedAtUtc, x.GuidanceSnapshotJson!)))
            .Concat(periods.Select(x => Item(x.OperationKey, x.Id, x.PayrollPeriodId, x.OperationKey == "payroll-period-create" ? "draft-created" : "approved", x.CreatedAtUtc, x.GuidanceSnapshotJson!)))
            .Concat(rows.Select(x => Item("payroll-row-approve", x.Id, x.PayrollEmployeeId, "approved", x.CreatedAtUtc, x.GuidanceSnapshotJson!)))
            .Concat(offboarding.Select(x => Item(x.OperationKey, x.Id, x.OffboardingCaseId, x.OperationKey == "offboarding-complete" ? "completed" : "reversed", x.CreatedAtUtc, x.GuidanceSnapshotJson!)));
        return result.OrderByDescending(x => x.OccurredAtUtc).ThenBy(x => x.OperationKey).ToList();
    }
    private static GuidanceOperationEvidenceDto Item(string operation, int evidenceId, int id, string outcome, DateTime at, string json) =>
        new(operation, id, outcome, DateTime.SpecifyKind(at, DateTimeKind.Utc), JsonSerializer.Deserialize<GuidanceOperationSnapshot>(json)!, $"{operation}:{evidenceId}");
}

public sealed record GetMyGuidanceOperationsQuery : IRequest<IReadOnlyList<GuidanceOperationHistoryDto>>;
public sealed class GetMyGuidanceOperationsQueryHandler(IApplicationDbContext db, ICurrentUser user, ISender sender)
    : IRequestHandler<GetMyGuidanceOperationsQuery, IReadOnlyList<GuidanceOperationHistoryDto>>
{
    public async Task<IReadOnlyList<GuidanceOperationHistoryDto>> Handle(GetMyGuidanceOperationsQuery request, CancellationToken ct)
    {
        var id = PersonnelEntryGuide.RequireUserId(user);
        var items = await GuidanceOperationEvidenceReader.ReadAsync(db, id, ct);
        var current = (await sender.Send(new GetGuidanceCatalogQuery(), ct)).Select(x => x.Id).ToHashSet();
        return items.Select(x => new GuidanceOperationHistoryDto(x, current.Contains(x.Content.ContentRevisionId))).ToList();
    }
}
