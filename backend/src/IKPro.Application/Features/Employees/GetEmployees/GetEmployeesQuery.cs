using FluentValidation;
using IKPro.Application.Common.Interfaces;
using IKPro.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees.GetEmployees;

/// <summary>
/// Personel dizini: server-side arama (ad/ünvan/departman), departman + durum filtresi,
/// sayfalama. Sonuç istek sahibinin rol kapsamına göre daraltılır.
/// </summary>
public sealed record GetEmployeesQuery(
    string? Search = null,
    int? DepartmentId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<EmployeeListItemDto>>;

public sealed class GetEmployeesQueryValidator : AbstractValidator<GetEmployeesQuery>
{
    public GetEmployeesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status)
            .Must(s => s is null or "" or "active" or "passive")
            .WithMessage("Durum filtresi 'active' veya 'passive' olmalı.");
    }
}

public sealed class GetEmployeesQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeListItemDto>>
{
    public async Task<PagedResult<EmployeeListItemDto>> Handle(
        GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Employees
            .AsNoTracking()
            .ScopeFor(currentUser);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(e =>
                (e.FirstName + " " + e.LastName).Contains(term) ||
                e.Title.Contains(term) ||
                e.Department!.Name.Contains(term));
        }

        if (request.DepartmentId is not null)
        {
            query = query.Where(e => e.DepartmentId == request.DepartmentId);
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            var status = EmployeeMappings.ParseStatus(request.Status);
            query = query.Where(e => e.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.Title,
                e.NationalId,
                e.DepartmentId,
                DepartmentName = e.Department!.Name,
                e.Status,
                e.HireDate,
                e.TerminationDate,
            })
            .ToListAsync(cancellationToken);

        var dtos = items
            .Select(e =>
            {
                var employee = new Domain.Entities.Organization.Employee
                {
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                };
                return new EmployeeListItemDto(
                    e.Id,
                    employee.FullName,
                    e.Title,
                    EmployeeMappings.MaskNationalId(e.NationalId),
                    e.DepartmentId,
                    e.DepartmentName,
                    e.Status.ToDto(),
                    employee.Initials,
                    e.HireDate,
                    e.TerminationDate);
            })
            .ToList();

        return new PagedResult<EmployeeListItemDto>(dtos, total, request.Page, request.PageSize);
    }
}
