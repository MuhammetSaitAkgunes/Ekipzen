using System.Text.Json;
using Ekipzen.Application.Common.Exceptions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Employees.Upsert;
using Ekipzen.Domain.Entities.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Import;

public sealed record ImportEmployeesCommand(Stream Dosya, Guid ImportRequestId, string ExpectedPreviewToken)
    : IRequest<ImportResultDto>;

public sealed class ImportEmployeesCommandValidator : AbstractValidator<ImportEmployeesCommand>
{
    public ImportEmployeesCommandValidator()
    {
        RuleFor(x => x.ImportRequestId).NotEmpty();
        RuleFor(x => x.ExpectedPreviewToken).NotEmpty().Length(64);
    }
}

public sealed class ImportEmployeesCommandHandler(
    IApplicationDbContext context, ICurrentUser currentUser, IDatabaseLock databaseLock)
    : IRequestHandler<ImportEmployeesCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(ImportEmployeesCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Oturum sahibi belirlenemedi.");
        var file = await ImportPreparation.ReadAsync(request.Dosya, ct);
        return await databaseLock.ExecuteExclusiveAsync("employee-import", async token =>
        {
            // İlk kayıt artık TC'leri değiştirmiş olabilir; önce kalıcı sonuç aranır.
            var receipt = await context.EmployeeImportReceipts.AsNoTracking().SingleOrDefaultAsync(
                x => x.UserId == userId && x.ImportRequestId == request.ImportRequestId, token);
            if (receipt is not null)
            {
                if (receipt.FileFingerprint != file.Fingerprint || receipt.PreviewToken != request.ExpectedPreviewToken)
                    throw new ConflictException("Bu aktarım zaten tamamlandı. Aynı gönderimle farklı dosya veya önizleme uygulanamaz; önce kayıtlı sonucu açın.");
                return JsonSerializer.Deserialize<ImportResultDto>(receipt.ResultJson)!;
            }
            var prepared = await ImportPreparation.PrepareAsync(file, context, token);
            if (prepared.Preview.PreviewToken is null || prepared.Preview.PreviewToken != request.ExpectedPreviewToken)
                throw new ConflictException("Dosya veya personel/departman kaynağı önizlemeden sonra değişti. Hiçbir kayıt oluşturulmadı; dosyayı yeniden önizleyip kontrol edin.");

            var created = new Dictionary<int, Employee>();
            foreach (var row in prepared.Rows.Where(x => x.Model is not null))
            {
                var model = row.Model!;
                var employee = new Employee
                {
                    FirstName = model.FirstName, LastName = model.LastName, Title = model.Title,
                    NationalId = model.NationalId, DepartmentId = model.DepartmentId,
                    HireDate = model.HireDate, Status = EmployeeMappings.ParseStatus(model.Status),
                    ContractType = EmployeeEmploymentMappings.ParseContractType(model.ContractType),
                    Profile = EmployeeUpsertGuards.MapProfile(new EmployeeProfile(), model.Profile),
                };
                employee.EmploymentPeriods.Add(new EmployeeEmploymentPeriod
                {
                    StartDate = employee.HireDate, ContractType = employee.ContractType,
                });
                context.Employees.Add(employee);
                created.Add(row.Preview.SatirNo, employee);
            }
            await context.SaveChangesAsync(token);
            var results = prepared.Preview.Satirlar.Select(row => created.TryGetValue(row.SatirNo, out var employee)
                ? row with { Durum = "created", EmployeeId = employee.Id, Aciklama = "Personel ve istihdam kaydı oluşturuldu; karttan kontrol edip eksik bilgileri tamamlayın." }
                : row).ToArray();
            var result = new ImportResultDto(created.Count, prepared.Preview.ToplamSatir - created.Count,
                prepared.Preview.Sorunlar, results, request.ImportRequestId);
            context.EmployeeImportReceipts.Add(new EmployeeImportReceipt
            {
                UserId = userId, ImportRequestId = request.ImportRequestId,
                FileFingerprint = file.Fingerprint, PreviewToken = request.ExpectedPreviewToken,
                ResultJson = JsonSerializer.Serialize(result),
            });
            // Sonuç kaydı başarısızsa aynı transaction personel kayıtlarını da geri alır.
            await context.SaveChangesAsync(token);
            return result;
        }, ct);
    }
}

public sealed record GetEmployeeImportResultQuery(Guid RequestId) : IRequest<ImportResultDto?>;
public sealed class GetEmployeeImportResultQueryHandler(IApplicationDbContext context, ICurrentUser user)
    : IRequestHandler<GetEmployeeImportResultQuery, ImportResultDto?>
{
    public async Task<ImportResultDto?> Handle(GetEmployeeImportResultQuery request, CancellationToken ct)
    {
        var json = await context.EmployeeImportReceipts.AsNoTracking()
            .Where(x => x.UserId == user.UserId && x.ImportRequestId == request.RequestId)
            .Select(x => x.ResultJson).SingleOrDefaultAsync(ct);
        return json is null ? null : JsonSerializer.Deserialize<ImportResultDto>(json);
    }
}
