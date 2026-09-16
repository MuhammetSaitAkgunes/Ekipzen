using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Employees.Upsert;
using Ekipzen.Domain.Entities.Organization;
using MediatR;

namespace Ekipzen.Application.Features.Employees.Import;

/// <summary>Geçerli satırları kaydeder; hatalı ve mükerrer satırları atlar.</summary>
public sealed record ImportEmployeesCommand(Stream Dosya) : IRequest<ImportResultDto>;

public sealed class ImportEmployeesCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ImportEmployeesCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(
        ImportEmployeesCommand request, CancellationToken cancellationToken)
    {
        var (satirlar, ayristirmaSorunlari) = EmployeeImportParser.Ayristir(request.Dosya);
        if (ayristirmaSorunlari.Count > 0)
        {
            return new ImportResultDto(0, 0, ayristirmaSorunlari);
        }

        // Önizlemeyle AYNI doğrulama: farklı sonuç üretmesi mümkün değil.
        var (departmanlar, tcler) = await ImportLookups.YukleAsync(context, cancellationToken);
        var sonuc = EmployeeImportValidator.Dogrula(satirlar, departmanlar, tcler);

        foreach (var model in sonuc.Gecerli)
        {
            var employee = new Employee
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Title = model.Title,
                NationalId = model.NationalId,
                DepartmentId = model.DepartmentId,
                HireDate = model.HireDate,
                Status = EmployeeMappings.ParseStatus(model.Status),
                ContractType = EmployeeEmploymentMappings.ParseContractType(model.ContractType),
                Profile = EmployeeUpsertGuards.MapProfile(new EmployeeProfile(), model.Profile),
            };
            employee.EmploymentPeriods.Add(new EmployeeEmploymentPeriod
            {
                StartDate = employee.HireDate,
                ContractType = employee.ContractType,
            });
            context.Employees.Add(employee);
        }

        // Tek SaveChanges → tek transaction. Beklenmedik bir veritabanı hatasında
        // HİÇBİRİ kaydedilmez; yarım aktarım durumu oluşmaz.
        if (sonuc.Gecerli.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        var atlanan = satirlar.Count - sonuc.Gecerli.Count;
        return new ImportResultDto(sonuc.Gecerli.Count, atlanan, sonuc.Sorunlar);
    }
}
