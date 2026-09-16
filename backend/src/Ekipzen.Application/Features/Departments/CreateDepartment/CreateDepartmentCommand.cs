using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Departments.CreateDepartment;

public sealed record CreateDepartmentCommand(string Name, string? Code) : IRequest<DepartmentDto>;

public sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).MaximumLength(32);
    }
}

public sealed class CreateDepartmentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await context.Departments.AnyAsync(d => d.Name == name, cancellationToken))
        {
            throw new ConflictException($"'{name}' adında bir departman zaten var.");
        }

        var department = new Department { Name = name, Code = request.Code?.Trim() };
        context.Departments.Add(department);
        await context.SaveChangesAsync(cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Code, 0);
    }
}
