using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Upsert;

public sealed record EmployeeManagerChoiceDto(int Id, string Name, string Title);
public sealed record GetEmployeeManagerChoicesQuery(int? ExceptEmployeeId) : IRequest<IReadOnlyList<EmployeeManagerChoiceDto>>;
public sealed class GetEmployeeManagerChoicesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeeManagerChoicesQuery, IReadOnlyList<EmployeeManagerChoiceDto>>
{
    public async Task<IReadOnlyList<EmployeeManagerChoiceDto>> Handle(GetEmployeeManagerChoicesQuery request, CancellationToken ct)
        => await db.Employees.AsNoTracking().Where(x => x.Status == EmployeeStatus.Active
            && x.UserId != null && x.Id != request.ExceptEmployeeId).OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .Select(x => new EmployeeManagerChoiceDto(x.Id, x.FirstName + " " + x.LastName, x.Title)).ToListAsync(ct);
}
public sealed record EmployeePersonnelHistoryDto(int Id, string OperationKey, int? DocumentId,
    DateTime OccurredAtUtc, IReadOnlyList<string> ChangedFields, string? Reason,
    int? EmploymentPeriodId = null, int? CompensationPackageId = null, int? RelatedPackageId = null, int? FromVersion = null, int? ToVersion = null);
public sealed record GetEmployeePersonnelHistoryQuery(int EmployeeId) : IRequest<IReadOnlyList<EmployeePersonnelHistoryDto>>;
public sealed class GetEmployeePersonnelHistoryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeePersonnelHistoryQuery, IReadOnlyList<EmployeePersonnelHistoryDto>>
{
    public async Task<IReadOnlyList<EmployeePersonnelHistoryDto>> Handle(GetEmployeePersonnelHistoryQuery request, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.Id == request.EmployeeId, ct)) throw new NotFoundException("Personel", request.EmployeeId);
        var rows = await db.GuidancePersonnelEvidences.AsNoTracking().Where(x => x.EmployeeId == request.EmployeeId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Take(100).ToListAsync(ct);
        return rows.Select(x => new EmployeePersonnelHistoryDto(x.Id, x.OperationKey, x.DocumentId,
            DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc), JsonSerializer.Deserialize<string[]>(x.ChangedFieldsJson)!, x.Reason,
            x.EmploymentPeriodId, x.CompensationPackageId, x.RelatedPackageId, x.FromVersion, x.ToVersion)).ToArray();
    }
}
