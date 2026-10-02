using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Guidance;
using Ekipzen.Domain.Entities.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Departments.DeleteDepartment;

public sealed record DepartmentDeletionPreviewDto(int Id, string Name, string? Code, int EmployeeCount,
    int PositionCount, bool CanDelete, string RecordToken);
internal static class DepartmentDeletionPreview
{
    internal static async Task<DepartmentDeletionPreviewDto> ReadAsync(IApplicationDbContext db, Department department, CancellationToken ct)
    {
        var employees = await db.Employees.CountAsync(x => x.DepartmentId == department.Id, ct);
        var positions = await db.Positions.CountAsync(x => x.DepartmentId == department.Id, ct);
        return new(department.Id, department.Name, department.Code, employees, positions, employees == 0 && positions == 0,
            PreparationOperationEvidence.RecordToken("department-delete", department.Name, department.Code, employees.ToString(), positions.ToString()));
    }
}
public sealed record GetDepartmentDeletionPreviewQuery(int Id) : IRequest<DepartmentDeletionPreviewDto>;
public sealed class GetDepartmentDeletionPreviewQueryHandler(IApplicationDbContext db, IDatabaseLock gate)
    : IRequestHandler<GetDepartmentDeletionPreviewQuery, DepartmentDeletionPreviewDto>
{
    public Task<DepartmentDeletionPreviewDto> Handle(GetDepartmentDeletionPreviewQuery request, CancellationToken ct)
        => gate.ExecuteExclusiveAsync("departments", async token =>
        {
            var department = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Departman", request.Id);
            return await DepartmentDeletionPreview.ReadAsync(db, department, token);
        }, ct);
}
public sealed record DeleteDepartmentCommand(int Id, string? ExpectedRecordToken = null, int? GuidanceContentRevisionId = null)
    : IRequest, IGuidanceOperationCommand
{
    public string GuidanceOperationKey => "department-delete";
}
public sealed class DeleteDepartmentCommandHandler(IApplicationDbContext db, IDatabaseLock gate,
    ICurrentUser user, GuidanceOperationContext guidance) : IRequestHandler<DeleteDepartmentCommand>
{
    public async Task Handle(DeleteDepartmentCommand request, CancellationToken ct)
    {
        await gate.ExecuteExclusiveAsync("departments", async token =>
        {
            var department = await db.Departments.SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Departman", request.Id);
            var preview = await DepartmentDeletionPreview.ReadAsync(db, department, token);
            PreparationOperationEvidence.RequireToken(request.ExpectedRecordToken, preview.RecordToken, guidance.IsGuided);
            if (!preview.CanDelete) throw new ConflictException("Departmana bağlı personel veya pozisyon var. Önce bu bağlantıları inceleyin ve başka departmana taşıyın.");
            db.Departments.Remove(department);
            await db.SaveChangesAsync(token);
            await PreparationOperationEvidence.SaveAsync(db, guidance, user, request.GuidanceOperationKey, "department", request.Id, ["Departman kaydı"], token);
            return true;
        }, ct);
    }
}
