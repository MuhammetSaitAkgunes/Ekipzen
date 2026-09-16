using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Departments.GetDepartments;

public sealed record GetDepartmentsQuery : IRequest<IReadOnlyList<DepartmentDto>>;

public sealed class GetDepartmentsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<IReadOnlyList<DepartmentDto>> Handle(
        GetDepartmentsQuery request, CancellationToken cancellationToken)
        => await context.Departments
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.Employees.Count))
            .ToListAsync(cancellationToken);
}
