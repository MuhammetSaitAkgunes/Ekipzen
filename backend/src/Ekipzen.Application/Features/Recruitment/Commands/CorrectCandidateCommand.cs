using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Recruitment.Queries;
using Ekipzen.Domain.Entities.Recruitment;
using Ekipzen.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Recruitment.Commands;

public sealed record CorrectCandidateCommand(
    int CandidateId, int ExpectedVersion, string Name, string AppliedRole,
    int? PositionId, int? ExpectedPositionVersion, int Score, string? Location,
    int ExperienceYears, string? Summary, string Reason, bool ConfirmExistingEvidenceReview)
    : IRequest<CandidateDetailDto>;

public sealed class CorrectCandidateCommandValidator : AbstractValidator<CorrectCandidateCommand>
{
    public CorrectCandidateCommandValidator()
    {
        RuleFor(item => item.CandidateId).GreaterThan(0);
        RuleFor(item => item.ExpectedVersion).GreaterThan(0);
        RuleFor(item => item.Name).NotEmpty().MaximumLength(200);
        RuleFor(item => item.AppliedRole).NotEmpty().MaximumLength(128);
        RuleFor(item => item.PositionId).GreaterThan(0).When(item => item.PositionId is not null);
        RuleFor(item => item.ExpectedPositionVersion).GreaterThan(0)
            .When(item => item.ExpectedPositionVersion is not null);
        RuleFor(item => item.Score).InclusiveBetween(0, 100);
        RuleFor(item => item.Location).MaximumLength(128);
        RuleFor(item => item.ExperienceYears).InclusiveBetween(0, 60);
        RuleFor(item => item.Summary).MaximumLength(2000);
        RuleFor(item => item.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class CorrectCandidateCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock, ISender sender)
    : IRequestHandler<CorrectCandidateCommand, CandidateDetailDto>
{
    public async Task<CandidateDetailDto> Handle(
        CorrectCandidateCommand request, CancellationToken cancellationToken)
    {
        var source = await context.Candidates.AsNoTracking()
            .Where(item => item.Id == request.CandidateId)
            .Select(item => new { item.PositionId }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Aday", request.CandidateId);
        var resources = new List<string> { $"recruitment-candidate:{request.CandidateId}" };
        if (source.PositionId is { } previousId) resources.Add($"recruitment-position:{previousId}");
        if (request.PositionId is { } nextId) resources.Add($"recruitment-position:{nextId}");
        return await databaseLock.ExecuteExclusiveAsync(resources,
            token => CorrectAsync(request, source.PositionId, token), cancellationToken);
    }

    private async Task<CandidateDetailDto> CorrectAsync(
        CorrectCandidateCommand request, int? previousPositionId, CancellationToken cancellationToken)
    {
        var candidate = await context.Candidates.Include(item => item.Position)
            .SingleAsync(item => item.Id == request.CandidateId, cancellationToken);
        if (candidate.Version != request.ExpectedVersion || candidate.PositionId != previousPositionId)
            throw new ConflictException("Aday kaydı bu sırada değişti; güncel kaydı açıp yeniden inceleyin.");
        if (candidate.Status == CandidateStatus.Hired)
            throw new ConflictException("İşe alınmış adayın başvuru kaydı kilitli; düzeltmeyi çalışan kartında değerlendirin.");

        var roleChanged = request.AppliedRole.Trim() != candidate.AppliedRole
            || request.PositionId != candidate.PositionId;
        if (roleChanged && !request.ConfirmExistingEvidenceReview)
            throw new ValidationException([new ValidationFailure(nameof(request.ConfirmExistingEvidenceReview),
                "Rol veya pozisyon değişiminde mevcut not ve değerlendirmelerin yeni role uygunluğunu kontrol edin.")]);

        Position? targetPosition = candidate.Position;
        if (request.PositionId != candidate.PositionId)
        {
            targetPosition = request.PositionId is { } targetId
                ? await context.Positions.SingleOrDefaultAsync(item => item.Id == targetId, cancellationToken)
                    ?? throw new NotFoundException("Pozisyon", targetId)
                : null;
            if (targetPosition is not null)
            {
                if (request.ExpectedPositionVersion is null)
                    throw new ValidationException([new ValidationFailure(nameof(request.ExpectedPositionVersion),
                        "Yeni pozisyonun incelenen sürümü gerekli.")]);
                if (targetPosition.Version != request.ExpectedPositionVersion)
                    throw new ConflictException("Hedef pozisyon değişti; güncel pozisyonu yeniden seçin.");
                if (!targetPosition.IsOpen || targetPosition.OpenCount < 1)
                    throw new ConflictException("Hedef pozisyon kapalı veya kalan kadrosu yok; aday bağlantısı değiştirilemez.");
            }
        }

        var before = CandidateCorrectionRules.State(candidate);
        var after = new CandidateStateDto(request.Name.Trim(), request.AppliedRole.Trim(),
            request.PositionId, targetPosition?.Title, request.Score,
            string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            request.ExperienceYears, string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim(),
            candidate.Status.ToDto());
        if (before == after) throw new ConflictException("Aday kaydında değişiklik yok.");

        candidate.Name = after.Name;
        candidate.AppliedRole = after.AppliedRole;
        candidate.Position = targetPosition;
        candidate.PositionId = after.PositionId;
        candidate.Score = after.Score;
        candidate.Location = after.Location;
        candidate.ExperienceYears = after.ExperienceYears;
        candidate.Summary = after.Summary;
        candidate.Version++;
        context.CandidateCorrections.Add(new CandidateCorrection
        {
            CandidateId = candidate.Id,
            FromVersion = request.ExpectedVersion, ToVersion = candidate.Version,
            BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(after),
            Reason = request.Reason.Trim(), EvidenceReviewConfirmed = roleChanged,
            ChangedByUserId = currentUser.UserId
                ?? throw new ForbiddenAccessException("Aday düzeltmesi için oturum sahibi gerekli."),
            ChangedByName = currentUser.UserName ?? "İK", ChangedAtUtc = DateTime.UtcNow,
        });
        context.CandidateHistory.Add(new CandidateHistory
        {
            CandidateId = candidate.Id,
            Event = $"Aday kaydı düzeltildi: sürüm {request.ExpectedVersion} → {candidate.Version}",
            OccurredAtUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
        return await sender.Send(new GetCandidateQuery(candidate.Id), cancellationToken);
    }
}

public sealed record GetCandidateCorrectionsQuery(int CandidateId)
    : IRequest<IReadOnlyList<CandidateCorrectionDto>>;

public sealed class GetCandidateCorrectionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCandidateCorrectionsQuery, IReadOnlyList<CandidateCorrectionDto>>
{
    public async Task<IReadOnlyList<CandidateCorrectionDto>> Handle(
        GetCandidateCorrectionsQuery request, CancellationToken cancellationToken)
    {
        if (!await context.Candidates.AnyAsync(item => item.Id == request.CandidateId, cancellationToken))
            throw new NotFoundException("Aday", request.CandidateId);
        var items = await context.CandidateCorrections.AsNoTracking()
            .Where(item => item.CandidateId == request.CandidateId)
            .OrderByDescending(item => item.ToVersion).ToListAsync(cancellationToken);
        return items.Select(item => new CandidateCorrectionDto(item.Id, item.CandidateId,
            item.FromVersion, item.ToVersion,
            JsonSerializer.Deserialize<CandidateStateDto>(item.BeforeJson)!,
            JsonSerializer.Deserialize<CandidateStateDto>(item.AfterJson)!, item.Reason,
            item.EvidenceReviewConfirmed, item.ChangedByUserId, item.ChangedByName, item.ChangedAtUtc)).ToList();
    }
}

internal static class CandidateCorrectionRules
{
    public static CandidateStateDto State(Candidate item)
        => new(item.Name, item.AppliedRole, item.PositionId, item.Position?.Title,
            item.Score, item.Location, item.ExperienceYears, item.Summary, item.Status.ToDto());
}
