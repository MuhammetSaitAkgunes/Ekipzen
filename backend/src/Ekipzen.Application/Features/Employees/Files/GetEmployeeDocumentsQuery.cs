using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Files;

/// <summary>Personelin evrak listesi (rol kapsamına tabi).</summary>
public sealed record GetEmployeeDocumentsQuery(int EmployeeId) : IRequest<IReadOnlyList<EmployeeDocumentDto>>;

public sealed class GetEmployeeDocumentsQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetEmployeeDocumentsQuery, IReadOnlyList<EmployeeDocumentDto>>
{
    public async Task<IReadOnlyList<EmployeeDocumentDto>> Handle(
        GetEmployeeDocumentsQuery request, CancellationToken cancellationToken)
    {
        await EmployeeAccessGuard.EnsureCanAccessAsync(context, currentUser, request.EmployeeId, cancellationToken);

        return await context.EmployeeDocuments
            .AsNoTracking()
            .Where(d => d.EmployeeId == request.EmployeeId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new EmployeeDocumentDto(
                d.Id, d.DocumentType, d.FileName, d.ContentType, d.SizeBytes, d.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Tekil personel erişiminde ortak kapsam kontrolü (404 → 403 sıralamasıyla).</summary>
public static class EmployeeAccessGuard
{
    public static async Task EnsureCanAccessAsync(
        IApplicationDbContext context, ICurrentUser currentUser, int employeeId, CancellationToken cancellationToken)
    {
        if (!await context.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
        {
            throw new NotFoundException("Personel", employeeId);
        }

        var inScope = await context.Employees
            .ScopeFor(currentUser)
            .AnyAsync(e => e.Id == employeeId, cancellationToken);
        if (!inScope)
        {
            throw new ForbiddenAccessException("Bu personelin kayıtlarına erişim yetkiniz yok.");
        }
    }
}
