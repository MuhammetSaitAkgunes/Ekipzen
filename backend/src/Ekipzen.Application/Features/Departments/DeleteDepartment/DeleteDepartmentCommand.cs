using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Departments.DeleteDepartment;

public sealed record DeleteDepartmentCommand(int Id) : IRequest;

public sealed class DeleteDepartmentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteDepartmentCommand>
{
    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await context.Departments
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Departman", request.Id);

        var hasEmployees = await context.Employees
            .AnyAsync(e => e.DepartmentId == request.Id, cancellationToken);
        if (hasEmployees)
        {
            throw new ConflictException(
                "Bu departmanda kayıtlı personel var; önce personelleri başka departmana taşıyın.");
        }

        context.Departments.Remove(department);
        await context.SaveChangesAsync(cancellationToken);
    }
}
