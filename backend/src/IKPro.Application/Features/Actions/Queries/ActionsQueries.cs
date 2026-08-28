using FluentValidation;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Actions.Queries;

/// <summary>
/// Aksiyon listesi: öncelik/kaynak/sahip/durum filtreli (actions.js filtre barı).
/// Öncelik sırasına (high→low) ve son kayda göre sıralanır.
/// </summary>
public sealed record GetGlobalActionsQuery(
    string? Priority = null,
    string? Source = null,
    string? Owner = null,
    string? Status = null) : IRequest<IReadOnlyList<GlobalActionDto>>;

public sealed class GetGlobalActionsQueryValidator : AbstractValidator<GetGlobalActionsQuery>
{
    public GetGlobalActionsQueryValidator()
    {
        RuleFor(x => x.Priority)
            .Must(p => string.IsNullOrWhiteSpace(p) || ActionsMappings.IsPriority(p))
            .WithMessage("Öncelik filtresi: high | medium | low.");
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || ActionsMappings.IsStatus(s))
            .WithMessage("Durum filtresi: open | week | done.");
    }
}

public sealed class GetGlobalActionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetGlobalActionsQuery, IReadOnlyList<GlobalActionDto>>
{
    public async Task<IReadOnlyList<GlobalActionDto>> Handle(
        GetGlobalActionsQuery request, CancellationToken cancellationToken)
    {
        var query = context.GlobalActions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            var priority = ActionsMappings.ParsePriority(request.Priority);
            query = query.Where(a => a.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            query = query.Where(a => a.Source == request.Source);
        }

        if (!string.IsNullOrWhiteSpace(request.Owner))
        {
            query = query.Where(a => a.Owner == request.Owner);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ActionsMappings.ParseStatus(request.Status);
            query = query.Where(a => a.Status == status);
        }

        var actions = await query
            .OrderBy(a => a.Priority).ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        return actions.Select(a => a.ToDto()).ToList();
    }
}

/// <summary>Sidebar rozeti: tamamlanmamış aksiyon sayısı (layout.js getOpenActionCount).</summary>
public sealed record GetActionBadgeQuery : IRequest<ActionBadgeDto>;

public sealed class GetActionBadgeQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetActionBadgeQuery, ActionBadgeDto>
{
    public async Task<ActionBadgeDto> Handle(
        GetActionBadgeQuery request, CancellationToken cancellationToken)
        => new(await context.GlobalActions
            .CountAsync(a => a.Status != ActionStatus.Done, cancellationToken));
}

/// <summary>
/// Denetim izi (append-only AuditLogs; kritik tablo trigger'ları + auth olayları doldurur).
/// Modül/arama filtreli, en yeni kayıt önce; take ile sınırlandırılır.
/// </summary>
public sealed record GetAuditLogsQuery(
    string? Module = null,
    string? Search = null,
    int Take = 50) : IRequest<IReadOnlyList<AuditLogDto>>;

public sealed class GetAuditLogsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(
        GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Module))
        {
            query = query.Where(l => l.Module == request.Module);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(l =>
                l.Actor.Contains(term) ||
                l.Action.Contains(term) ||
                (l.Detail != null && l.Detail.Contains(term)) ||
                (l.EntityName != null && l.EntityName.Contains(term)));
        }

        var take = Math.Clamp(request.Take, 1, 200);
        return await query
            .OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id)
            .Take(take)
            .Select(l => new AuditLogDto(
                l.Id, l.Actor, l.Action, l.Module, l.Detail,
                l.EntityName, l.EntityId, l.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
