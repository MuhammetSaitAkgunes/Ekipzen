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
    DateTime OccurredAtUtc, GuidanceOperationSnapshot Content);
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
            "candidate-first-review" => ("recruitment", "first-review"),
            "leave-decision" => ("leave", "decision-effect"),
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
        var candidates = await db.CandidateReviewEvidences.AsNoTracking()
            .Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var leaves = await db.GuidanceLeaveDecisionEvidences.AsNoTracking()
            .Where(x => x.UserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var result = employees.Select(x => Item("employee-create", x.EmployeeId, "created", x.CreatedAtUtc, x.GuidanceSnapshotJson!))
            .Concat(candidates.Select(x => Item("candidate-first-review", x.CandidateId, x.DecisionStatus.ToString(), x.CreatedAtUtc, x.GuidanceSnapshotJson!)))
            .Concat(leaves.Select(x => Item("leave-decision", x.LeaveRequestId, x.DecisionStatus.ToString(), x.CreatedAtUtc, x.GuidanceSnapshotJson!)));
        return result.OrderByDescending(x => x.OccurredAtUtc).ThenBy(x => x.OperationKey).ToList();
    }
    private static GuidanceOperationEvidenceDto Item(string operation, int id, string outcome, DateTime at, string json) =>
        new(operation, id, outcome, DateTime.SpecifyKind(at, DateTimeKind.Utc), JsonSerializer.Deserialize<GuidanceOperationSnapshot>(json)!);
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
