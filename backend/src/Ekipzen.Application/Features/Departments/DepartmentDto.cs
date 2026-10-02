using Ekipzen.Application.Features.Guidance;
namespace Ekipzen.Application.Features.Departments;

public sealed record DepartmentDto(int Id, string Name, string? Code, int EmployeeCount)
{
    public string RecordToken => PreparationOperationEvidence.RecordToken(Name, Code);
}
