using Ekipzen.Application.Features.Guidance;
using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Departments.UpdateDepartment;

public sealed record UpdateDepartmentCommand(int Id, string Name, string? Code, string? ExpectedRecordToken = null, int? GuidanceContentRevisionId = null)
    : IRequest<DepartmentDto>, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "department-update";
}

public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).MaximumLength(32);
    }
}

public sealed class UpdateDepartmentCommandHandler(IApplicationDbContext context, IDatabaseLock databaseLock, ICurrentUser user, GuidanceOperationContext guidance)
    : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    public Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
        => databaseLock.ExecuteExclusiveAsync("departments", async ct =>
    {
        var department = await context.Departments
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new NotFoundException("Departman", request.Id);

        PreparationOperationEvidence.RequireToken(request.ExpectedRecordToken,
            PreparationOperationEvidence.RecordToken(department.Name, department.Code), guidance.IsGuided);
        var fields = new List<string>();
        if (department.Name != request.Name.Trim()) fields.Add("Departman adı");
        if (department.Code != request.Code?.Trim()) fields.Add("Departman kodu");
        if (fields.Count == 0) return new DepartmentDto(department.Id, department.Name, department.Code,
            await context.Employees.CountAsync(e => e.DepartmentId == department.Id, ct));
        var name = request.Name.Trim();
        if (await context.Departments.AnyAsync(d => d.Name == name && d.Id != request.Id, ct))
        {
            throw new ConflictException($"'{name}' adında başka bir departman zaten var.");
        }

        department.Name = name;
        department.Code = request.Code?.Trim();
        await context.SaveChangesAsync(ct);

        var employeeCount = await context.Employees
            .CountAsync(e => e.DepartmentId == department.Id, ct);

        await PreparationOperationEvidence.SaveAsync(context, guidance, user, request.GuidanceOperationKey, "department", department.Id,
            fields, ct);
        return new DepartmentDto(department.Id, department.Name, department.Code, employeeCount);
    }, cancellationToken);
}
