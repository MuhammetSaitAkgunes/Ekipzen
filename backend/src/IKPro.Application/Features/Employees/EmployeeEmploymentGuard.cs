using IKPro.Application.Common.Exceptions;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Employees;

/// <summary>
/// Personel-bağlı tarihli işlemlerin istihdam aralığı içinde kalmasını sağlar.
/// Geçmiş düzeltmeler, ayrılış tarihi dahil olmak üzere korunur; işe giriş öncesi
/// ve ayrılış sonrası kayıtlar engellenir.
/// </summary>
public static class EmployeeEmploymentGuard
{
    public static async Task EnsureDateIsWithinEmploymentAsync(
        IApplicationDbContext context,
        int employeeId,
        DateOnly operationDate,
        CancellationToken cancellationToken)
    {
        var employment = await context.Employees
            .Where(employee => employee.Id == employeeId)
            .Select(employee => new
            {
                employee.HireDate,
                employee.TerminationDate,
                employee.Status,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Personel", employeeId);

        if (operationDate < employment.HireDate)
        {
            throw new ConflictException("İşe giriş tarihinden önce personel işlemi oluşturulamaz.");
        }

        if (employment.TerminationDate is not null && operationDate > employment.TerminationDate)
        {
            throw new ConflictException("İşten ayrılma tarihinden sonra personel işlemi oluşturulamaz.");
        }

        if (employment.Status == EmployeeStatus.Passive && employment.TerminationDate is null)
        {
            throw new ConflictException(
                "Pasif personelin ayrılış tarihi eksik; tarihli işlemden önce personel kaydını düzeltin.");
        }
    }
}
