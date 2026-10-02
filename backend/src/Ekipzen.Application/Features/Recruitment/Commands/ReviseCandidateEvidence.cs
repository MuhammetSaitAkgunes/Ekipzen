using Ekipzen.Application.Features.Guidance;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Recruitment.Queries;
using Ekipzen.Domain.Entities.Recruitment;
using Ekipzen.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Recruitment.Commands;

public sealed record ReviseCandidateEvidenceCommand(
    int CandidateId, int RecordId, bool IsNote, int ExpectedCandidateVersion, int ExpectedRecordVersion,
    string? NoteType, string? Text, string? Criterion, int? Score, int? MaxScore, string Reason, int? GuidanceContentRevisionId = null)
    : IRequest<CandidateDetailDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => IsNote ? "candidate-note-revise" : "candidate-evaluation-revise";
}

public sealed class ReviseCandidateEvidenceCommandValidator : AbstractValidator<ReviseCandidateEvidenceCommand>
{
    public ReviseCandidateEvidenceCommandValidator()
    {
        RuleFor(item => item.CandidateId).GreaterThan(0);
        RuleFor(item => item.RecordId).GreaterThan(0);
        RuleFor(item => item.ExpectedCandidateVersion).GreaterThan(0);
        RuleFor(item => item.ExpectedRecordVersion).GreaterThan(0);
        RuleFor(item => item.Reason).NotEmpty().MaximumLength(2000);
        When(item => item.IsNote, () =>
        {
            RuleFor(item => item.NoteType).Must(value => value is "Teknik Mülakat" or "İK Görüşmesi");
            RuleFor(item => item.Text).NotEmpty().MaximumLength(4000);
        });
        When(item => !item.IsNote, () =>
        {
            RuleFor(item => item.Criterion).NotEmpty().MaximumLength(128);
            RuleFor(item => item.MaxScore).NotNull().InclusiveBetween(1, 10);
            RuleFor(item => item.Score).NotNull().InclusiveBetween(0, 10)
                .Must((item, value) => value <= item.MaxScore)
                .WithMessage("Puan 0 ile azami puan arasında olmalıdır.");
        });
    }
}

public sealed class ReviseCandidateEvidenceCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock, ISender sender, GuidanceOperationContext guidance)
    : IRequestHandler<ReviseCandidateEvidenceCommand, CandidateDetailDto>
{
    public Task<CandidateDetailDto> Handle(ReviseCandidateEvidenceCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"recruitment-candidate:{request.CandidateId}",
            token => ReviseAsync(request, token), cancellationToken);

    private async Task<CandidateDetailDto> ReviseAsync(ReviseCandidateEvidenceCommand request, CancellationToken token)
    {
        var candidate = await context.Candidates.SingleOrDefaultAsync(item => item.Id == request.CandidateId, token)
            ?? throw new NotFoundException("Aday", request.CandidateId);
        if (candidate.Status == CandidateStatus.Hired)
            throw new ConflictException("İşe alınmış adayın başvuru geçmişi kilitli; not veya değerlendirme düzeltilemez.");
        if (candidate.Version != request.ExpectedCandidateVersion)
            throw new ConflictException("Aday kaydı değişti; güncel kaydı açıp düzeltmeyi yeniden inceleyin.");
        var original = await CandidateEvidenceVersions.OriginalAsync(context, request.CandidateId, request.RecordId, request.IsNote, token);
        var latest = await CandidateEvidenceVersions.Revisions(context, request.CandidateId, request.RecordId, request.IsNote)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(token);
        var recordVersion = latest?.Version ?? 1;
        if (recordVersion != request.ExpectedRecordVersion)
            throw new ConflictException("Not veya değerlendirme sürümü değişti; güncel sürümü yeniden inceleyin.");
        var before = latest is null ? original : CandidateEvidenceVersions.Read(latest.AfterJson);
        var after = request.IsNote
            ? new CandidateEvidenceStateDto(request.NoteType, request.Text!.Trim(), null, null, null)
            : new CandidateEvidenceStateDto(null, null, request.Criterion!.Trim(), request.Score, request.MaxScore);
        if (before == after) throw new ConflictException("Not veya değerlendirmede değişiklik yok.");
        var userId = currentUser.UserId ?? throw new ForbiddenAccessException("Düzeltme için oturum sahibi gerekli.");
        candidate.Version++;
        var evidence = new CandidateEvidenceRevision
        {
            CandidateId = candidate.Id, InterviewNoteId = request.IsNote ? request.RecordId : null,
            EvaluationId = request.IsNote ? null : request.RecordId, Version = recordVersion + 1,
            FromCandidateVersion = request.ExpectedCandidateVersion, ToCandidateVersion = candidate.Version,
            BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(after),
            Reason = request.Reason.Trim(), ChangedByUserId = userId,
            ChangedByName = currentUser.UserName ?? "İK", ChangedAtUtc = DateTime.UtcNow,
        };
        guidance.Stamp(evidence);
        context.CandidateEvidenceRevisions.Add(evidence);
        context.CandidateHistory.Add(new CandidateHistory
        {
            CandidateId = candidate.Id, OccurredAtUtc = DateTime.UtcNow,
            Event = $"{(request.IsNote ? "Mülakat notu" : "Değerlendirme")} #{request.RecordId} düzeltildi: sürüm {recordVersion} → {recordVersion + 1}",
        });
        await context.SaveChangesAsync(token);
        return await sender.Send(new GetCandidateQuery(candidate.Id), token);
    }
}

public sealed record GetCandidateEvidenceHistoryQuery(int CandidateId, int RecordId, bool IsNote)
    : IRequest<CandidateEvidenceHistoryDto>;

public sealed class GetCandidateEvidenceHistoryQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCandidateEvidenceHistoryQuery, CandidateEvidenceHistoryDto>
{
    public async Task<CandidateEvidenceHistoryDto> Handle(GetCandidateEvidenceHistoryQuery request, CancellationToken token)
    {
        var original = await CandidateEvidenceVersions.OriginalAsync(context, request.CandidateId, request.RecordId, request.IsNote, token);
        var revisions = await CandidateEvidenceVersions.Revisions(context, request.CandidateId, request.RecordId, request.IsNote)
            .AsNoTracking().OrderByDescending(item => item.Version).ToListAsync(token);
        return new(original, revisions.Select(item => new CandidateEvidenceRevisionDto(item.Id, item.Version,
            item.FromCandidateVersion, item.ToCandidateVersion, CandidateEvidenceVersions.Read(item.BeforeJson),
            CandidateEvidenceVersions.Read(item.AfterJson), item.Reason, item.ChangedByUserId,
            item.ChangedByName, item.ChangedAtUtc)).ToList());
    }
}

internal static class CandidateEvidenceVersions
{
    public static CandidateEvidenceStateDto Read(string json) => JsonSerializer.Deserialize<CandidateEvidenceStateDto>(json)!;

    public static IQueryable<CandidateEvidenceRevision> Revisions(
        IApplicationDbContext context, int candidateId, int recordId, bool isNote)
        => context.CandidateEvidenceRevisions.Where(item => item.CandidateId == candidateId
            && (isNote ? item.InterviewNoteId == recordId : item.EvaluationId == recordId));

    public static async Task<CandidateEvidenceStateDto> OriginalAsync(
        IApplicationDbContext context, int candidateId, int recordId, bool isNote, CancellationToken token)
    {
        if (isNote)
        {
            var note = await context.InterviewNotes.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == recordId && item.CandidateId == candidateId, token)
                ?? throw new NotFoundException("Mülakat notu", recordId);
            return new(note.NoteType, note.Text, null, null, null);
        }
        var evaluation = await context.CandidateEvaluations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == recordId && item.CandidateId == candidateId, token)
            ?? throw new NotFoundException("Değerlendirme", recordId);
        return new(null, null, evaluation.Criterion, evaluation.Score, evaluation.MaxScore);
    }
}
