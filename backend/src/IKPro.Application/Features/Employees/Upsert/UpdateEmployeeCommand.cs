using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Features.Employees.GetEmployee;
using IKPro.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.Upsert;

public sealed record UpdateEmployeeCommand(int Id, EmployeeUpsertModel Model) : IRequest<EmployeeDetailDto>;

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Model).NotNull().SetValidator(new EmployeeUpsertModelValidator());
    }
}

public sealed class UpdateEmployeeCommandHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<UpdateEmployeeCommand, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .Include(e => e.Profile)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Personel", request.Id);

        var model = request.Model;
        var requestedStatus = EmployeeMappings.ParseStatus(model.Status);
        if (requestedStatus != employee.Status)
        {
            throw new ConflictException(
                "Personel durumu genel güncelleme ile değiştirilemez; ayrılış tarihi ve erişim eşitlemesi için durum işlemini kullanın.");
        }

        await EmployeeUpsertGuards.EnsureReferencesAsync(context, model, employee.Id, cancellationToken);

        employee.FirstName = model.FirstName.Trim();
        employee.LastName = model.LastName.Trim();
        employee.Title = model.Title.Trim();
        employee.NationalId = model.NationalId;
        employee.DepartmentId = model.DepartmentId;
        employee.ManagerId = model.ManagerId;
        employee.HireDate = model.HireDate;
        employee.Profile = EmployeeUpsertGuards.MapProfile(
            employee.Profile ?? new EmployeeProfile { EmployeeId = employee.Id }, model.Profile);

        await context.SaveChangesAsync(cancellationToken);

        return await sender.Send(new GetEmployeeQuery(employee.Id), cancellationToken);
    }
}
