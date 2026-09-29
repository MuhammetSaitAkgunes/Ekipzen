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

    [HttpGet("{id:int}/compensation-packages/{packageId:int}/correction-context")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<CompensationPackageCorrectionContextDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompensationPackageCorrectionContextDto>> GetCompensationCorrectionContext(
        int id, int packageId, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new GetCompensationPackageCorrectionContextQuery(id, packageId), cancellationToken));

    [HttpPut("{id:int}/compensation-packages/{packageId:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeEmploymentFoundationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeEmploymentFoundationDto>> CorrectCompensationPackage(
        int id, int packageId, CorrectCompensationPackageBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new CorrectEmployeeCompensationPackageCommand(
            id, packageId, body.ExpectedVersion, body.MonthlyGrossSalary,
            body.RegularPremium, body.RoadAllowance, body.MealAllowance,
            body.OtherRegularBenefits, body.SourceReference, body.Reason), cancellationToken));

    [HttpGet("{id:int}/compensation-packages/{packageId:int}/remediation-case")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<CompensationRemediationContextDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompensationRemediationContextDto>> GetCompensationRemediation(
        int id, int packageId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCompensationRemediationContextQuery(id, packageId), cancellationToken));

    [HttpPost("{id:int}/compensation-packages/{packageId:int}/remediation-case")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<CompensationRemediationCaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompensationRemediationCaseDto>> CreateCompensationRemediation(
        int id, int packageId, SaveCompensationRemediationBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(ToRemediationCommand(id, packageId, null, body), cancellationToken));

    [HttpPut("{id:int}/compensation-packages/{packageId:int}/remediation-case/{caseId:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<CompensationRemediationCaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompensationRemediationCaseDto>> UpdateCompensationRemediation(
        int id, int packageId, int caseId, SaveCompensationRemediationBody body,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(ToRemediationCommand(id, packageId, caseId, body), cancellationToken));

    [HttpPost("{id:int}/compensation-packages/{packageId:int}/remediation-case/{caseId:int}/approve")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<CompensationRemediationCaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompensationRemediationCaseDto>> ApproveCompensationRemediation(
        int id, int packageId, int caseId, ApproveCompensationRemediationBody body,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new ApproveCompensationRemediationCaseCommand(
            id, packageId, caseId, body.ExpectedCaseVersion,
            body.ExpertReviewReference, body.ReviewNotes, body.ConfirmExpertReview), cancellationToken));

    [HttpGet("{id:int}/compensation-packages/{packageId:int}/remediation-case/{caseId:int}/payroll-periods/{periodId:int}/rows/{rowId:int}/preview")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<CompensationPayrollPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompensationPayrollPreviewDto>> PreviewCompensationPayrollApplication(
        int id, int packageId, int caseId, int periodId, int rowId,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new PreviewCompensationPayrollApplicationQuery(
            id, packageId, caseId, periodId, rowId), cancellationToken));

    [HttpPost("{id:int}/compensation-packages/{packageId:int}/remediation-case/{caseId:int}/payroll-periods/{periodId:int}/rows/{rowId:int}/apply")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<CompensationPayrollApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompensationPayrollApplicationDto>> ApplyCompensationPayrollApplication(
        int id, int packageId, int caseId, int periodId, int rowId,
        ApplyCompensationPayrollBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ApplyCompensationRemediationToPayrollCommand(
            id, packageId, caseId, periodId, rowId, body.ExpectedPreviewToken,
            body.ConfirmSourceLink), cancellationToken));

    private static SaveCompensationRemediationCaseCommand ToRemediationCommand(
        int employeeId, int packageId, int? caseId, SaveCompensationRemediationBody body)
        => new(employeeId, packageId, caseId, body.ExpectedPackageVersion,
            body.ExpectedCaseVersion, body.IssueDescription, body.ProposedEffectiveFrom,
            body.MonthlyGrossSalary, body.RegularPremium, body.RoadAllowance,
            body.MealAllowance, body.OtherRegularBenefits, body.SourceReference);

    [HttpPost("{id:int}/compensation-packages/{packageId:int}/date-correction-preview")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<CompensationDateCorrectionPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CompensationDateCorrectionPreviewDto>> PreviewCompensationDateCorrection(
        int id, int packageId, PreviewCompensationDateCorrectionBody body,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new PreviewCompensationDateCorrectionQuery(
            id, packageId, body.ProposedEffectiveFrom), cancellationToken));

    [HttpPut("{id:int}/compensation-packages/{packageId:int}/effective-date")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<EmployeeEmploymentFoundationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeEmploymentFoundationDto>> CorrectCompensationDate(
        int id, int packageId, CorrectCompensationDateBody body,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new CorrectEmployeeCompensationDateCommand(
            id, packageId, body.ProposedEffectiveFrom,
            body.ExpectedPackageVersion, body.ExpectedPreviousPackageId,
            body.ExpectedPreviousPackageVersion, body.Reason), cancellationToken));

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

    [HttpGet("{id:int}/compensation-packages/{packageId:int}/remediation-case/{remediationCaseId:int}/offboarding/{caseId:int}/settlement-revisions")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<OffboardingSettlementRevisionContextDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OffboardingSettlementRevisionContextDto>> GetOffboardingSettlementRevisions(
        int id, int packageId, int remediationCaseId, int caseId,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOffboardingSettlementRevisionContextQuery(
            id, packageId, remediationCaseId, caseId), cancellationToken));

    [HttpPost("{id:int}/compensation-packages/{packageId:int}/remediation-case/{remediationCaseId:int}/offboarding/{caseId:int}/settlement-revisions")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<OffboardingSettlementRevisionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OffboardingSettlementRevisionDto>> OpenOffboardingSettlementRevision(
        int id, int packageId, int remediationCaseId, int caseId,
        OpenOffboardingSettlementRevisionBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new OpenOffboardingSettlementRevisionCommand(
            id, packageId, remediationCaseId, caseId, body.ExpectedOffboardingVersion),
            cancellationToken));

    [HttpPut("{id:int}/compensation-packages/{packageId:int}/remediation-case/{remediationCaseId:int}/offboarding/{caseId:int}/settlement-revisions/{revisionId:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<OffboardingSettlementRevisionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OffboardingSettlementRevisionDto>> SaveOffboardingSettlementRevision(
        int id, int packageId, int remediationCaseId, int caseId, int revisionId,
        SaveOffboardingSettlementRevisionBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new SaveOffboardingSettlementRevisionCommand(
            id, packageId, remediationCaseId, caseId, revisionId,
            body.ExpectedRevisionVersion, body.UnusedLeaveDays,
            body.UnusedLeavePayment, body.SeverancePayment, body.NoticePayment,
            body.Reason, body.SettlementReference, body.SettlementNotes), cancellationToken));

    [HttpPost("{id:int}/compensation-packages/{packageId:int}/remediation-case/{remediationCaseId:int}/offboarding/{caseId:int}/settlement-revisions/{revisionId:int}/approve")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<OffboardingSettlementRevisionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OffboardingSettlementRevisionDto>> ApproveOffboardingSettlementRevision(
        int id, int packageId, int remediationCaseId, int caseId, int revisionId,
        ApproveOffboardingSettlementRevisionBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ApproveOffboardingSettlementRevisionCommand(
            id, packageId, remediationCaseId, caseId, revisionId,
            body.ExpectedRevisionVersion, body.ExpertReviewReference, body.ReviewNotes,
            body.ConfirmFinancialReview, body.ConfirmExternalReconciliation), cancellationToken));

    [HttpGet("{id:int}/compensation-packages/{packageId:int}/remediation-case/{remediationCaseId:int}/offboarding/{caseId:int}/settlement-revisions/{revisionId:int}/external-reconciliation")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<OffboardingExternalReconciliationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OffboardingExternalReconciliationDto>> GetOffboardingExternalReconciliation(
        int id, int packageId, int remediationCaseId, int caseId, int revisionId,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOffboardingExternalReconciliationQuery(
            id, packageId, remediationCaseId, caseId, revisionId), cancellationToken));

    [HttpPost("{id:int}/compensation-packages/{packageId:int}/remediation-case/{remediationCaseId:int}/offboarding/{caseId:int}/settlement-revisions/{revisionId:int}/external-reconciliation")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<OffboardingExternalActionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OffboardingExternalActionDto>> RecordOffboardingExternalAction(
        int id, int packageId, int remediationCaseId, int caseId, int revisionId,
        RecordOffboardingExternalActionBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RecordOffboardingExternalActionCommand(
            id, packageId, remediationCaseId, caseId, revisionId,
            body.Kind, body.Outcome, body.ResponsibleName, body.EvidenceReference,
            body.ActionDate, body.RecordedAmount, body.Notes,
            body.ExpectedLatestRecordId, body.ConfirmRecordedStatus), cancellationToken));


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

    [HttpGet("{id:int}/documents/withdrawn")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<IReadOnlyList<WithdrawnEmployeeDocumentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WithdrawnEmployeeDocumentDto>>> GetWithdrawnDocuments(
        int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetWithdrawnEmployeeDocumentsQuery(id), cancellationToken));

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

    [HttpPost("{id:int}/documents/{documentId:int}/withdraw")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<WithdrawnEmployeeDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WithdrawnEmployeeDocumentDto>> WithdrawDocument(
        int id, int documentId, WithdrawDocumentBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new WithdrawEmployeeDocumentCommand(id, documentId, body.Reason), cancellationToken));

    public sealed record WithdrawDocumentBody(string Reason);

    public sealed record SaveCompensationPackageBody(
        DateOnly EffectiveFrom,
        decimal MonthlyGrossSalary,
        decimal RegularPremium,
        decimal RoadAllowance,
        decimal MealAllowance,
        decimal OtherRegularBenefits,
        string SourceReference);

    public sealed record CorrectCompensationPackageBody(
        int ExpectedVersion,
        decimal MonthlyGrossSalary,
        decimal RegularPremium,
        decimal RoadAllowance,
        decimal MealAllowance,
        decimal OtherRegularBenefits,
        string SourceReference,
        string Reason);

    public sealed record SaveCompensationRemediationBody(
        int ExpectedPackageVersion, int? ExpectedCaseVersion, string IssueDescription,
        DateOnly? ProposedEffectiveFrom, decimal MonthlyGrossSalary, decimal RegularPremium,
        decimal RoadAllowance, decimal MealAllowance, decimal OtherRegularBenefits,
        string SourceReference);

    public sealed record ApproveCompensationRemediationBody(
        int ExpectedCaseVersion, string ExpertReviewReference, string ReviewNotes,
        bool ConfirmExpertReview);

    public sealed record ApplyCompensationPayrollBody(
        string ExpectedPreviewToken, bool ConfirmSourceLink);

    public sealed record PreviewCompensationDateCorrectionBody(DateOnly ProposedEffectiveFrom);

    public sealed record CorrectCompensationDateBody(
        DateOnly ProposedEffectiveFrom, int ExpectedPackageVersion,
        int? ExpectedPreviousPackageId, int? ExpectedPreviousPackageVersion,
        string Reason);

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

    public sealed record OpenOffboardingSettlementRevisionBody(int ExpectedOffboardingVersion);
    public sealed record SaveOffboardingSettlementRevisionBody(
        int ExpectedRevisionVersion, decimal UnusedLeaveDays, decimal UnusedLeavePayment,
        decimal SeverancePayment, decimal NoticePayment, string Reason,
        string SettlementReference, string? SettlementNotes);
    public sealed record ApproveOffboardingSettlementRevisionBody(
        int ExpectedRevisionVersion, string ExpertReviewReference, string ReviewNotes,
        bool ConfirmFinancialReview, bool ConfirmExternalReconciliation);
    public sealed record RecordOffboardingExternalActionBody(
        string Kind, string Outcome, string ResponsibleName, string EvidenceReference,
        DateOnly? ActionDate, decimal? RecordedAmount, string Notes,
        int? ExpectedLatestRecordId, bool ConfirmRecordedStatus);

    public sealed record SetStatusBody(
        string Status,
        DateOnly? TerminationDate = null,
        int? SuccessorManagerId = null,
        DateOnly? RehireDate = null);
}
