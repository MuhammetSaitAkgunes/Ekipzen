using FluentValidation;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.BulkDeactivate;

/// <summary>Seçili personelleri pasife alır (personnel.js "Pasife al" toplu aksiyonu).</summary>
public sealed record BulkDeactivateEmployeesCommand(IReadOnlyList<int> Ids) : IRequest<int>;

public sealed class BulkDeactivateEmployeesCommandValidator : AbstractValidator<BulkDeactivateEmployeesCommand>
{
    public BulkDeactivateEmployeesCommandValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().WithMessage("En az bir personel seçilmeli.");
    }
}

public sealed class BulkDeactivateEmployeesCommandHandler(
    IApplicationDbContext context,
    IIdentityService identityService)
    : IRequestHandler<BulkDeactivateEmployeesCommand, int>
{
    public async Task<int> Handle(BulkDeactivateEmployeesCommand request, CancellationToken cancellationToken)
    {
        // ExecuteUpdate yerine change tracker kullanılır: audit interceptor + trigger devrede kalsın.
        var employees = await context.Employees
            .Where(e => request.Ids.Contains(e.Id) && e.Status == EmployeeStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var employee in employees)
        {
            employee.Status = EmployeeStatus.Passive;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Login'i olan personellerin Identity hesabını da kapat ve refresh
        // token'larını iptal et. Login'i olmayan personeller no-op'tur.
        foreach (var employee in employees)
        {
            await identityService.SetEmployeeAccessAsync(employee.Id, false, cancellationToken);
        }

        return employees.Count;
    }
}
