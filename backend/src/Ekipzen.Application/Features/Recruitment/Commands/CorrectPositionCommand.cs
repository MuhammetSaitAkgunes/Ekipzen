using Ekipzen.Application.Features.Guidance;
using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Recruitment;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Recruitment.Commands;

public sealed record CorrectPositionCommand(
    int PositionId, int ExpectedVersion, string Title, int? DepartmentId,
    int OpenCount, bool IsOpen, string Reason, int? GuidanceContentRevisionId = null) : IRequest<PositionDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "position-correct";
}

public sealed class CorrectPositionCommandValidator : AbstractValidator<CorrectPositionCommand>
{
    public CorrectPositionCommandValidator()
    {
        RuleFor(item => item.PositionId).GreaterThan(0);
        RuleFor(item => item.ExpectedVersion).GreaterThan(0);
        RuleFor(item => item.Title).NotEmpty().MaximumLength(128);
        RuleFor(item => item.OpenCount).InclusiveBetween(0, 100);
        RuleFor(item => item.OpenCount).GreaterThan(0).When(item => item.IsOpen)
            .WithMessage("Açık pozisyonun en az bir açık kadrosu olmalı.");
        RuleFor(item => item.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class CorrectPositionCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock, GuidanceOperationContext guidance)
    : IRequestHandler<CorrectPositionCommand, PositionDto>
{
    public Task<PositionDto> Handle(CorrectPositionCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync($"recruitment-position:{request.PositionId}",
            token => CorrectAsync(request, token), cancellationToken);

    private async Task<PositionDto> CorrectAsync(
        CorrectPositionCommand request, CancellationToken cancellationToken)
    {
        var position = await context.Positions
            .SingleOrDefaultAsync(item => item.Id == request.PositionId, cancellationToken)
            ?? throw new NotFoundException("Pozisyon", request.PositionId);
        if (position.Version != request.ExpectedVersion)
            throw new ConflictException("Pozisyon bu sırada değişti; güncel kaydı açıp yeniden inceleyin.");
        if (request.DepartmentId is { } departmentId
            && !await context.Departments.AnyAsync(item => item.Id == departmentId, cancellationToken))
            throw new NotFoundException("Departman", departmentId);

        var before = PositionCorrectionRules.State(position);
        var after = new PositionStateDto(request.Title.Trim(), request.DepartmentId,
            request.IsOpen, request.OpenCount);
        if (before == after)
            throw new ConflictException("Pozisyonda değişiklik yok.");

        position.Title = after.Title;
        position.DepartmentId = after.DepartmentId;
        position.IsOpen = after.IsOpen;
        position.OpenCount = after.OpenCount;
        position.Version++;
        var evidence = new PositionCorrection
        {
            PositionId = position.Id,
            FromVersion = request.ExpectedVersion,
            ToVersion = position.Version,
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after),
            Reason = request.Reason.Trim(),
            ChangedByUserId = currentUser.UserId
                ?? throw new ForbiddenAccessException("Pozisyon düzeltmesi için oturum sahibi gerekli."),
            ChangedAtUtc = DateTime.UtcNow,
        };
        guidance.Stamp(evidence);
        context.PositionCorrections.Add(evidence);
        await context.SaveChangesAsync(cancellationToken);
        var departmentName = after.DepartmentId is { } id
            ? await context.Departments.AsNoTracking()
                .Where(item => item.Id == id).Select(item => item.Name)
                .SingleAsync(cancellationToken)
            : null;
        var candidateCount = await context.Candidates.AsNoTracking()
            .CountAsync(item => item.PositionId == position.Id, cancellationToken);
        return new PositionDto(position.Id, position.Title, position.DepartmentId,
            departmentName, position.IsOpen, position.OpenCount, candidateCount, position.Version);
    }
}

public sealed record GetPositionCorrectionsQuery(int PositionId)
    : IRequest<IReadOnlyList<PositionCorrectionDto>>;

public sealed class GetPositionCorrectionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPositionCorrectionsQuery, IReadOnlyList<PositionCorrectionDto>>
{
    public async Task<IReadOnlyList<PositionCorrectionDto>> Handle(
        GetPositionCorrectionsQuery request, CancellationToken cancellationToken)
    {
        if (!await context.Positions.AnyAsync(item => item.Id == request.PositionId, cancellationToken))
            throw new NotFoundException("Pozisyon", request.PositionId);
        var items = await context.PositionCorrections.AsNoTracking()
            .Where(item => item.PositionId == request.PositionId)
            .OrderByDescending(item => item.ToVersion)
            .ToListAsync(cancellationToken);
        return items.Select(PositionCorrectionRules.ToDto).ToList();
    }
}

internal static class PositionCorrectionRules
{
    public static PositionStateDto State(Position item)
        => new(item.Title, item.DepartmentId, item.IsOpen, item.OpenCount);

    public static PositionCorrectionDto ToDto(PositionCorrection item)
        => new(item.Id, item.PositionId, item.FromVersion, item.ToVersion,
            JsonSerializer.Deserialize<PositionStateDto>(item.BeforeJson)!,
            JsonSerializer.Deserialize<PositionStateDto>(item.AfterJson)!,
            item.Reason, item.ChangedByUserId, item.ChangedAtUtc);
}
