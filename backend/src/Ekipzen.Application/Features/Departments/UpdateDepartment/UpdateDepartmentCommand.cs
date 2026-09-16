using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Departments.UpdateDepartment;

public sealed record UpdateDepartmentCommand(int Id, string Name, string? Code) : IRequest<DepartmentDto>;

public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).MaximumLength(32);
    }
}

public sealed class UpdateDepartmentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await context.Departments
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Departman", request.Id);

        var name = request.Name.Trim();
        if (await context.Departments.AnyAsync(d => d.Name == name && d.Id != request.Id, cancellationToken))
        {
            throw new ConflictException($"'{name}' adında başka bir departman zaten var.");
        }

        department.Name = name;
        department.Code = request.Code?.Trim();
        await context.SaveChangesAsync(cancellationToken);

        var employeeCount = await context.Employees
            .CountAsync(e => e.DepartmentId == department.Id, cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Code, employeeCount);
    }
}
