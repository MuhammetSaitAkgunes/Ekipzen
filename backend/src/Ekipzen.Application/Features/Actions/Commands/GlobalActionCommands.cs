using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Actions;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Actions.Commands;

// --- oluştur ---

public sealed record CreateGlobalActionCommand(
    string Title,
    string Source,
    string Owner,
    string? SourceRoute = null,
    string? Due = null,
    string Priority = "medium",
    string? RecommendedAction = null) : IRequest<GlobalActionDto>;

public sealed class CreateGlobalActionCommandValidator : AbstractValidator<CreateGlobalActionCommand>
{
    public CreateGlobalActionCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Source).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Owner).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SourceRoute).MaximumLength(64);
        RuleFor(x => x.Due).MaximumLength(64);
        RuleFor(x => x.RecommendedAction).MaximumLength(512);
        RuleFor(x => x.Priority)
            .Must(ActionsMappings.IsPriority)
            .WithMessage("Öncelik: high | medium | low.");
    }
}

public sealed class CreateGlobalActionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateGlobalActionCommand, GlobalActionDto>
{
    public async Task<GlobalActionDto> Handle(
        CreateGlobalActionCommand request, CancellationToken cancellationToken)
    {
        var action = new GlobalAction
        {
            Title = request.Title.Trim(),
            Source = request.Source.Trim(),
            SourceRoute = request.SourceRoute,
            Owner = request.Owner.Trim(),
            Due = request.Due,
            Priority = ActionsMappings.ParsePriority(request.Priority),
            Status = ActionStatus.Open,
            RecommendedAction = request.RecommendedAction,
        };

        context.GlobalActions.Add(action);
        await context.SaveChangesAsync(cancellationToken);
        return action.ToDto();
    }
}

// --- güncelle ---

public sealed record UpdateGlobalActionCommand(
    int Id,
    string Title,
    string Source,
    string Owner,
    string? SourceRoute,
    string? Due,
    string Priority,
    string? RecommendedAction) : IRequest<GlobalActionDto>;

public sealed class UpdateGlobalActionCommandValidator : AbstractValidator<UpdateGlobalActionCommand>
{
    public UpdateGlobalActionCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Source).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Owner).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Priority)
            .Must(ActionsMappings.IsPriority)
            .WithMessage("Öncelik: high | medium | low.");
    }
}

public sealed class UpdateGlobalActionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateGlobalActionCommand, GlobalActionDto>
{
    public async Task<GlobalActionDto> Handle(
        UpdateGlobalActionCommand request, CancellationToken cancellationToken)
    {
        var action = await context.GlobalActions
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Aksiyon", request.Id);

        action.Title = request.Title.Trim();
        action.Source = request.Source.Trim();
        action.SourceRoute = request.SourceRoute;
        action.Owner = request.Owner.Trim();
        action.Due = request.Due;
        action.Priority = ActionsMappings.ParsePriority(request.Priority);
        action.RecommendedAction = request.RecommendedAction;

        await context.SaveChangesAsync(cancellationToken);
        return action.ToDto();
    }
}

// --- durum geçişi (open → week → done, ileri yönlü) ---

public sealed record SetGlobalActionStatusCommand(int Id, string Status) : IRequest<GlobalActionDto>;

public sealed class SetGlobalActionStatusCommandValidator
    : AbstractValidator<SetGlobalActionStatusCommand>
{
    public SetGlobalActionStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .Must(ActionsMappings.IsStatus)
            .WithMessage("Aksiyon durumu: open | week | done.");
    }
}

public sealed class SetGlobalActionStatusCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SetGlobalActionStatusCommand, GlobalActionDto>
{
    public async Task<GlobalActionDto> Handle(
        SetGlobalActionStatusCommand request, CancellationToken cancellationToken)
    {
        var action = await context.GlobalActions
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Aksiyon", request.Id);

        var target = ActionsMappings.ParseStatus(request.Status);
        if (action.Status == target)
        {
            throw new ConflictException($"Aksiyon zaten '{target.ToDto()}' durumunda.");
        }

        // Yaşam döngüsü ileri yönlüdür (plan: open→week→done); tamamlanan aksiyon yeniden açılamaz.
        if (target < action.Status)
        {
            throw new ConflictException(
                $"Geriye dönük durum geçişi yapılamaz ({action.Status.ToDto()} → {target.ToDto()}).");
        }

        action.Status = target;
        if (target == ActionStatus.Done)
        {
            action.Due = "Tamamlandı";
        }

        await context.SaveChangesAsync(cancellationToken);
        return action.ToDto();
    }
}

// --- sil ---

public sealed record DeleteGlobalActionCommand(int Id) : IRequest;

public sealed class DeleteGlobalActionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteGlobalActionCommand>
{
    public async Task Handle(DeleteGlobalActionCommand request, CancellationToken cancellationToken)
    {
        var action = await context.GlobalActions
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Aksiyon", request.Id);

        context.GlobalActions.Remove(action);
        await context.SaveChangesAsync(cancellationToken);
    }
}
