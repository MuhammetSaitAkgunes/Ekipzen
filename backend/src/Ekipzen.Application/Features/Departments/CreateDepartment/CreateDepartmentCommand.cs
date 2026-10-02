using Ekipzen.Application.Features.Guidance;
using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Departments.CreateDepartment;

public sealed record CreateDepartmentCommand(string Name, string? Code, int? GuidanceContentRevisionId = null)
    : IRequest<DepartmentDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "department-create";
}

public sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).MaximumLength(32);
    }
}

public sealed class CreateDepartmentCommandHandler(IApplicationDbContext context, IDatabaseLock databaseLock, ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync("departments", async ct =>
    {
        var name = request.Name.Trim();

        if (await context.Departments.AnyAsync(d => d.Name == name, ct))
        {
            throw new ConflictException($"'{name}' adında bir departman zaten var.");
        }

        var department = new Department { Name = name, Code = request.Code?.Trim() };
        context.Departments.Add(department);
        await context.SaveChangesAsync(ct);

        await PreparationOperationEvidence.SaveAsync(context, guidance, user, request.GuidanceOperationKey, "department", department.Id,
            ["Departman adı", "Departman kodu"], ct);
        return new DepartmentDto(department.Id, department.Name, department.Code, 0);
    }, cancellationToken);
}
