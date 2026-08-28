using IKPro.Application.Common.Models;
using IKPro.Application.Common.Security;
using IKPro.Application.Features.Employees;
using IKPro.Application.Features.Employees.BulkDeactivate;
using IKPro.Application.Features.Employees.Files;
using IKPro.Application.Features.Employees.GetEmployee;
using IKPro.Application.Features.Employees.GetEmployees;
using IKPro.Application.Features.Employees.Import;
using IKPro.Application.Features.Employees.SetStatus;
using IKPro.Application.Features.Employees.Upsert;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IKPro.API.Controllers;

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
            "ikpro-personel-sablonu.xlsx");

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
            new SetEmployeeStatusCommand(id, body.Status, body.TerminationDate), cancellationToken);
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

    public sealed record SetStatusBody(string Status, DateOnly? TerminationDate = null);
}
