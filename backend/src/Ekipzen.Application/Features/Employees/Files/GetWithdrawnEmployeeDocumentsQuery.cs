using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Files;

public sealed record GetWithdrawnEmployeeDocumentsQuery(int EmployeeId)
    : IRequest<IReadOnlyList<WithdrawnEmployeeDocumentDto>>;

public sealed class GetWithdrawnEmployeeDocumentsQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetWithdrawnEmployeeDocumentsQuery, IReadOnlyList<WithdrawnEmployeeDocumentDto>>
{
    public async Task<IReadOnlyList<WithdrawnEmployeeDocumentDto>> Handle(
        GetWithdrawnEmployeeDocumentsQuery request, CancellationToken cancellationToken)
    {
        await EmployeeAccessGuard.EnsureCanAccessAsync(
            context, currentUser, request.EmployeeId, cancellationToken);

        return await context.EmployeeDocuments.AsNoTracking()
            .Where(d => d.EmployeeId == request.EmployeeId && d.WithdrawnAtUtc != null)
            .OrderByDescending(d => d.WithdrawnAtUtc)
            .Select(d => new WithdrawnEmployeeDocumentDto(
                d.Id, d.DocumentType, d.FileName, d.CreatedAtUtc,
                d.WithdrawnAtUtc!.Value, d.WithdrawnByUserId,
                d.WithdrawalReason!))
            .ToListAsync(cancellationToken);
    }
}
