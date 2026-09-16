namespace Ekipzen.Application.Features.Departments;

public sealed record DepartmentDto(int Id, string Name, string? Code, int EmployeeCount);
