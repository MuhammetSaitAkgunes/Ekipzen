using Ekipzen.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using static Ekipzen.Application.Features.Guidance.GuidanceOperationEvidenceReader;

namespace Ekipzen.Application.Features.Guidance;

internal static class GuidanceRecruitmentEvidenceReader
{
    public static async Task<IReadOnlyList<GuidanceOperationEvidenceDto>> ReadAsync(
        IApplicationDbContext db, string userId, CancellationToken ct)
    {
        // Kaynak sadece o yazımda seçilir. Güncel aday görünümünden eski geçmiş yeniden damgalanmaz.
        var positions = await db.PositionCorrections.AsNoTracking()
            .Where(x => x.ChangedByUserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var candidates = await db.CandidateCorrections.AsNoTracking()
            .Where(x => x.ChangedByUserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var notes = await db.InterviewNotes.AsNoTracking()
            .Where(x => x.AuthorUserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);
        var evaluations = await db.CandidateEvaluations.AsNoTracking()
            .Where(x => x.GuidanceUserId == userId && x.GuidanceSnapshotJson != null && x.GuidanceOccurredAtUtc != null).ToListAsync(ct);
        var revisions = await db.CandidateEvidenceRevisions.AsNoTracking()
            .Where(x => x.ChangedByUserId == userId && x.GuidanceSnapshotJson != null).ToListAsync(ct);

        return positions.Select(x => Item("position-correct", x.Id, x.PositionId, "corrected", x.ChangedAtUtc, x.GuidanceSnapshotJson!) with {
                RecruitmentChanges = new(null, x.PositionId, "position", x.Id, x.FromVersion, x.ToVersion, x.Reason),
            }).Concat(candidates.Select(x => Item("candidate-correct", x.Id, x.CandidateId, "corrected", x.ChangedAtUtc, x.GuidanceSnapshotJson!) with {
                RecruitmentChanges = new(x.CandidateId, null, "candidate", x.Id, x.FromVersion, x.ToVersion, x.Reason),
            })).Concat(notes.Select(x => Item("candidate-note-create", x.Id, x.CandidateId, "created", x.CreatedAtUtc, x.GuidanceSnapshotJson!) with {
                RecruitmentChanges = new(x.CandidateId, null, "note", x.Id, null, 1, null),
            })).Concat(evaluations.Select(x => Item("candidate-evaluation-create", x.Id, x.CandidateId, "created", x.GuidanceOccurredAtUtc!.Value, x.GuidanceSnapshotJson!) with {
                RecruitmentChanges = new(x.CandidateId, null, "evaluation", x.Id, null, 1, null),
            })).Concat(revisions.Select(x => Item(x.InterviewNoteId.HasValue ? "candidate-note-revise" : "candidate-evaluation-revise",
                x.Id, x.CandidateId, "revised", x.ChangedAtUtc, x.GuidanceSnapshotJson!) with {
                RecruitmentChanges = new(x.CandidateId, null, x.InterviewNoteId.HasValue ? "note" : "evaluation",
                    x.InterviewNoteId ?? x.EvaluationId!.Value, x.Version - 1, x.Version, x.Reason, x.FromCandidateVersion, x.ToCandidateVersion),
            })).ToList();
    }
}
