using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Recruitment;
using Ekipzen.Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Actions.Queries;

/// <summary>
/// Birleşik arama (layout.js global search'ün veri ucu): personel + aksiyon + aday.
/// Personel rol kapsamlıdır (ScopeFor), aksiyonlar tüm rollere açıktır (routes.js /actions),
/// adaylar yalnız hr-admin'e döner. Sayfa (route) sonuçları frontend'in kendi işidir.
/// </summary>
public sealed record GlobalSearchQuery(string Query, int Take = 20)
    : IRequest<IReadOnlyList<SearchResultDto>>;

public sealed class GlobalSearchQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GlobalSearchQuery, IReadOnlyList<SearchResultDto>>
{
    public async Task<IReadOnlyList<SearchResultDto>> Handle(
        GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var term = request.Query.Trim();
        if (term.Length < 2)
        {
            return [];
        }

        var take = Math.Clamp(request.Take, 1, 50);
        var results = new List<SearchResultDto>();

        var employees = await context.Employees
            .Include(e => e.Department)
            .ScopeFor(currentUser)
            .Where(e => (e.FirstName + " " + e.LastName).Contains(term) || e.Title.Contains(term))
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Take(take)
            .ToListAsync(cancellationToken);
        results.AddRange(employees.Select(e => new SearchResultDto(
            "Personel", e.FullName, $"{e.Title} · {e.Department?.Name}", "personnel", e.Id)));

        var actions = await context.GlobalActions
            .Where(a => a.Title.Contains(term) || a.Owner.Contains(term) || a.Source.Contains(term))
            .OrderBy(a => a.Priority).ThenByDescending(a => a.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
        results.AddRange(actions.Select(a => new SearchResultDto(
            "Aksiyon", a.Title, $"{a.Source} · {a.Due ?? "-"}", "actions", a.Id)));

        if (currentUser.Roles.Contains(Roles.HrAdmin))
        {
            var candidates = await context.Candidates
                .Where(c => c.Name.Contains(term) || c.AppliedRole.Contains(term))
                .OrderByDescending(c => c.AppliedAtUtc)
                .Take(take)
                .ToListAsync(cancellationToken);
            results.AddRange(candidates.Select(c => new SearchResultDto(
                "Aday", c.Name, $"{c.AppliedRole} · {c.Status.ToDto()}", "recruitment", c.Id)));
        }

        return results.Take(take).ToList();
    }
}
