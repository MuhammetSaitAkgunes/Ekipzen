using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Employees.Upsert;
using Ekipzen.Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.GetEmployee;

/// <summary>Tam personel kartı. Kapsam dışı taleplerde 403 döner.</summary>
public sealed record GetEmployeeQuery(int Id) : IRequest<EmployeeDetailDto>;

public sealed class GetEmployeeQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetEmployeeQuery, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(GetEmployeeQuery request, CancellationToken cancellationToken)
    {
        var exists = await context.Employees
            .AnyAsync(e => e.Id == request.Id, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Personel", request.Id);
        }

        var employee = await context.Employees
            .AsNoTracking()
            .ScopeFor(currentUser)
            .Include(e => e.Department)
            .Include(e => e.Manager)
            .Include(e => e.Profile)
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new ForbiddenAccessException("Bu personel kartına erişim yetkiniz yok.");

        var profile = employee.Profile ?? new Domain.Entities.Organization.EmployeeProfile();

        // TC yalnız hr-admin'e açık döner; diğer roller maskeli görür.
        var nationalId = currentUser.Roles.Contains(Roles.HrAdmin)
            ? employee.NationalId
            : EmployeeMappings.MaskNationalId(employee.NationalId);

        return new EmployeeDetailDto(
            employee.Id,
            employee.FirstName,
            employee.LastName,
            employee.FullName,
            employee.Initials,
            employee.Title,
            nationalId,
            employee.Status.ToDto(),
            employee.HireDate,
            employee.TerminationDate,
            employee.ContractType.ToDto(),
            employee.DepartmentId,
            employee.Department?.Name ?? string.Empty,
            employee.ManagerId,
            employee.Manager?.FullName,
            new EmployeeProfileDto(
                profile.BirthDate,
                profile.Gender,
                profile.MaritalStatus,
                profile.BloodType,
                profile.PhotoPath,
                profile.MobilePhone,
                profile.PersonalEmail,
                profile.HomeAddress,
                profile.EmergencyContactName,
                profile.EmergencyContactRelation,
                profile.EmergencyContactPhone,
                profile.EmploymentType.ToDto(),
                profile.RehireEligibility,
                profile.ExitCode,
                profile.Iban,
                profile.BankName,
                profile.SalaryType,
                profile.PensionStatus,
                profile.MealCard,
                profile.TshirtSize,
                profile.PantsSize,
                profile.CoatSize,
                profile.ShoeSize,
                profile.CanWorkAtHeight,
                profile.CanWorkNightShift,
                profile.CanLiftHeavyLoads,
                profile.HealthNotes),
            employee.Documents
                .Where(d => d.WithdrawnAtUtc is null)
                .OrderByDescending(d => d.CreatedAtUtc)
                .Select(d => new EmployeeDocumentDto(
                    d.Id, d.DocumentType, d.FileName, d.ContentType, d.SizeBytes, d.CreatedAtUtc))
                .ToList(), currentUser.Roles.Contains(Roles.HrAdmin) ? EmployeeRecordChanges.Token(employee) : null);
    }
}
