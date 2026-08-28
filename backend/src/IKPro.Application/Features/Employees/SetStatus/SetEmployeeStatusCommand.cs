using FluentValidation;
using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.SetStatus;

public sealed record SetEmployeeStatusCommand(int Id, string Status) : IRequest;

public sealed class SetEmployeeStatusCommandValidator : AbstractValidator<SetEmployeeStatusCommand>
{
    public SetEmployeeStatusCommandValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is "active" or "passive")
            .WithMessage("Durum 'active' veya 'passive' olmalı.");
    }
}

public sealed class SetEmployeeStatusCommandHandler(
    IApplicationDbContext context,
    IIdentityService identityService)
    : IRequestHandler<SetEmployeeStatusCommand>
{
    public async Task Handle(SetEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Personel", request.Id);

        employee.Status = EmployeeMappings.ParseStatus(request.Status);
        await context.SaveChangesAsync(cancellationToken);

        // Personel durumu yalnız İK tablosunda kalırsa işten çıkarılan kullanıcı
        // yeni token almaya devam eder. Önce personeli kaydediyoruz: ikinci adım
        // aksarsa HTTP erişim kapısı yine pasif personeli anında reddeder; güvenli
        // yönde başarısız olur ve işlem tekrarlandığında kimlik tarafı tamamlanır.
        await identityService.SetEmployeeAccessAsync(
            employee.Id,
            employee.Status == EmployeeStatus.Active,
            cancellationToken);
    }
}
