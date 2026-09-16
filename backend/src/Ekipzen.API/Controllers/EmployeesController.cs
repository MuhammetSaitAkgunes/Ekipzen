using Ekipzen.Application.Common.Models;
using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Employees;
using Ekipzen.Application.Features.Employees.BulkDeactivate;
using Ekipzen.Application.Features.Employees.Employment;
using Ekipzen.Application.Features.Employees.Files;
using Ekipzen.Application.Features.Employees.GetEmployee;
using Ekipzen.Application.Features.Employees.GetEmployees;
using Ekipzen.Application.Features.Employees.Import;
using Ekipzen.Application.Features.Employees.Offboarding;
using Ekipzen.Application.Features.Employees.SetStatus;
using Ekipzen.Application.Features.Employees.Upsert;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Personel yönetimi: directory (arama/filtre/sayfalama), tam kart CRUD,
/// toplu pasife alma, durum, foto ve özlük evrakları. Okuma uçları yönetim
/// rollerine açık (manager kapsamı handler'da daraltılır); mutasyonlar hr-admin.
/// </summary>
[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// İçe aktarma şablonunu indirir. Başlıklar sunucudan üretilir ki
    /// kullanıcının dosyası ayrıştırıcıyla garantili uyumlu olsun.
    /// </summary>
    [HttpGet("import/template")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DownloadImportTemplate()
        => File(EmployeeImportTemplate.Olustur(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "ekipzen-personel-sablonu.xlsx");

    /// <summary>
    /// Dosyayı doğrular ve rapor döner; HİÇBİR ŞEY kaydetmez. Aktarımdan önce
    /// kullanıcının hatalı satırları görüp düzeltmesi içindir.
    /// </summary>
    [HttpPost("import/preview")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<ImportPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportPreviewDto>> PreviewImport(
        IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await sender.Send(new PreviewEmployeeImportCommand(stream), cancellationToken));
    }

    /// <summary>
    /// Geçerli satırları kaydeder; hatalı ve mükerrer satırları atlar.
    /// Önizlemeyle aynı doğrulamayı kullanır, bu yüzden sonuçlar ayrışamaz.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<ImportResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportResultDto>> Import(
        IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await sender.Send(new ImportEmployeesCommand(stream), cancellationToken));
    }

    [HttpGet]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<PagedResult<EmployeeListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? departmentId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await sender.Send(
            new GetEmployeesQuery(search, departmentId, status, page, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<EmployeeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetEmployeeQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDetailDto>> Create(
        EmployeeUpsertModel model, CancellationToken cancellationToken)
    {
        var employee = await sender.Send(new CreateEmployeeCommand(model), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, employee);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDetailDto>> Update(
        int id, EmployeeUpsertModel model, CancellationToken cancellationToken)
        => Ok(await sender.Send(new UpdateEmployeeCommand(id, model), cancellationToken));

    [HttpGet("{id:int}/employment-foundation")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeEmploymentFoundationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmploymentFoundationDto>> GetEmploymentFoundation(
        int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetEmployeeEmploymentFoundationQuery(id), cancellationToken));

    [HttpPost("{id:int}/compensation-packages")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeEmploymentFoundationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeEmploymentFoundationDto>> SaveCompensationPackage(
        int id, SaveCompensationPackageBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new SaveEmployeeCompensationPackageCommand(
            id,
            body.EffectiveFrom,
            body.MonthlyGrossSalary,
            body.RegularPremium,
            body.RoadAllowance,
            body.MealAllowance,
            body.OtherRegularBenefits,
            body.SourceReference), cancellationToken));

    [HttpPost("{id:int}/rehire")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeEmploymentFoundationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeEmploymentFoundationDto>> Rehire(
        int id, RehireEmployeeBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new RehireEmployeeCommand(id, body.RehireDate, body.ContractType), cancellationToken));

    [HttpGet("{id:int}/offboarding-context")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeOffboardingContextDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeOffboardingContextDto>> GetOffboardingContext(
        int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetEmployeeOffboardingContextQuery(id), cancellationToken));
    [HttpPut("{id:int}/offboarding-draft")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeOffboardingCaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeOffboardingCaseDto>> SaveOffboardingDraft(
        int id, SaveOffboardingDraftBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new SaveEmployeeOffboardingDraftCommand(
            id,
            body.ExpectedVersion,
            body.TerminationDate,
            body.ExitCode,
            body.ExitReason,
            body.TerminationReason,
            body.NoticeHandling,
            body.SuccessorManagerId,
            body.UnusedLeaveDays,
            body.UnusedLeavePayment,
            body.SeverancePayment,
            body.NoticePayment,
            body.SettlementReference,
            body.SettlementNotes), cancellationToken));

    [HttpPost("{id:int}/offboarding/{caseId:int}/calculation-draft")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeOffboardingCaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeOffboardingCaseDto>> GenerateOffboardingCalculationDraft(
        int id, int caseId, GenerateOffboardingCalculationDraftBody body,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GenerateTerminationCalculationDraftCommand(
            id,
            caseId,
            body.ExpectedVersion,
            body.ConfirmSeveranceEligibility,
            body.ConfirmNoticeEligibility,
            body.ExpertReviewReference), cancellationToken));

    [HttpPost("{id:int}/offboarding/{caseId:int}/complete")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeOffboardingCaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeOffboardingCaseDto>> CompleteOffboarding(
        int id, int caseId, OffboardingVersionBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new CompleteEmployeeOffboardingCommand(id, caseId, body.ExpectedVersion),
            cancellationToken));

    [HttpPost("{id:int}/offboarding/{caseId:int}/reverse")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeOffboardingCaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeOffboardingCaseDto>> ReverseOffboarding(
        int id, int caseId, ReverseOffboardingBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new ReverseEmployeeOffboardingCommand(
                id, caseId, body.ExpectedVersion, body.Reason),
            cancellationToken));


    [HttpPost("bulk-deactivate")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkDeactivate(
        BulkDeactivateEmployeesCommand command, CancellationToken cancellationToken)
    {
        var count = await sender.Send(command, cancellationToken);
        return Ok(new { deactivated = count });
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        int id, SetStatusBody body, CancellationToken cancellationToken)
    {
        await sender.Send(
            new SetEmployeeStatusCommand(
                id, body.Status, body.TerminationDate, body.SuccessorManagerId, body.RehireDate), cancellationToken);
        return NoContent();
    }

    // --- dosyalar ---

    [HttpPost("{id:int}/photo")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [RequestSizeLimit(4 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadPhoto(int id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var photoPath = await sender.Send(
            new UploadEmployeePhotoCommand(id, stream, file.FileName, file.Length), cancellationToken);
        return Ok(new { photoPath });
    }

    [HttpGet("{id:int}/documents")]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<IReadOnlyList<EmployeeDocumentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDocumentDto>>> GetDocuments(
        int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetEmployeeDocumentsQuery(id), cancellationToken));

    [HttpPost("{id:int}/documents")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType<EmployeeDocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDocumentDto>> UploadDocument(
        int id, IFormFile file, [FromForm] string documentType, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var document = await sender.Send(
            new UploadEmployeeDocumentCommand(id, documentType, stream, file.FileName, file.Length),
            cancellationToken);
        return CreatedAtAction(nameof(GetDocuments), new { id }, document);
    }

    [HttpGet("{id:int}/documents/{documentId:int}")]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(
        int id, int documentId, CancellationToken cancellationToken)
    {
        var download = await sender.Send(
            new DownloadEmployeeDocumentQuery(id, documentId), cancellationToken);
        return File(download.Content, download.ContentType, download.FileName);
    }

    public sealed record SaveCompensationPackageBody(
        DateOnly EffectiveFrom,
        decimal MonthlyGrossSalary,
        decimal RegularPremium,
        decimal RoadAllowance,
        decimal MealAllowance,
        decimal OtherRegularBenefits,
        string SourceReference);

    public sealed record RehireEmployeeBody(DateOnly RehireDate, string ContractType);

    public sealed record SaveOffboardingDraftBody(
        int? ExpectedVersion,
        DateOnly TerminationDate,
        string ExitCode,
        string ExitReason,
        string TerminationReason,
        string NoticeHandling,
        int? SuccessorManagerId,
        decimal UnusedLeaveDays,
        decimal UnusedLeavePayment,
        decimal SeverancePayment,
        decimal NoticePayment,
        string SettlementReference,
        string? SettlementNotes = null);

    public sealed record GenerateOffboardingCalculationDraftBody(
        int ExpectedVersion,
        bool ConfirmSeveranceEligibility,
        bool ConfirmNoticeEligibility,
        string ExpertReviewReference);

    public sealed record OffboardingVersionBody(int ExpectedVersion);

    public sealed record ReverseOffboardingBody(int ExpectedVersion, string Reason);

    public sealed record SetStatusBody(
        string Status,
        DateOnly? TerminationDate = null,
        int? SuccessorManagerId = null,
        DateOnly? RehireDate = null);
}
