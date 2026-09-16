using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Application.Features.Payroll.Payslips;
using Ekipzen.Application.Features.Payroll.Periods;
using Ekipzen.Application.Features.Payroll.Preview;
using Ekipzen.Application.Features.Payroll.Settings;
using Ekipzen.Application.Features.Payroll.Summary;
using Ekipzen.Domain.ReadModels;
using Ekipzen.Domain.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.Controllers;

/// <summary>
/// Bordro uçları. Dönem yönetimi/hesap/ayarlar yalnız hr-admin (routes.js:
/// payroll-calculator + payroll-settings); çalışan kendi pusulalarına erişir.
/// </summary>
[ApiController]
[Route("api/payroll")]
public sealed class PayrollController(ISender sender) : ControllerBase
{
    // --- dönemler (hr-admin) ---

    [HttpGet("periods")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<IReadOnlyList<PayrollPeriodListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PayrollPeriodListItemDto>>> GetPeriods(
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPayrollPeriodsQuery(), cancellationToken));

    [HttpPost("periods")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollPeriodDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollPeriodDetailDto>> CreatePeriod(
        CreatePayrollPeriodCommand command, CancellationToken cancellationToken)
    {
        var period = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetPeriod), new { id = period.Id }, period);
    }

    [HttpGet("periods/{id:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollPeriodDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollPeriodDetailDto>> GetPeriod(int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPayrollPeriodQuery(id), cancellationToken));

    [HttpGet("periods/{id:int}/summary")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollPeriodSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollPeriodSummary>> GetPeriodSummary(
        int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPayrollPeriodSummaryQuery(id), cancellationToken));

    [HttpPut("periods/{id:int}/rows/{rowId:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollRowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRowDto>> UpdateRow(
        int id, int rowId, PayrollRowInputModel model, CancellationToken cancellationToken)
        => Ok(await sender.Send(new UpdatePayrollRowCommand(id, rowId, model), cancellationToken));

    [HttpPost("periods/{id:int}/check")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollPeriodDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollPeriodDetailDto>> RunCheck(int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RunPayrollCheckCommand(id), cancellationToken));

    [HttpPost("periods/{id:int}/rows/{rowId:int}/approve")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollRowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRowDto>> ApproveRow(
        int id, int rowId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ApprovePayrollRowCommand(id, rowId), cancellationToken));

    [HttpPost("periods/{id:int}/submit")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollPeriodDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollPeriodDetailDto>> Submit(int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new SubmitPayrollPeriodCommand(id), cancellationToken));

    // --- tekil hesap (hr-admin) ---

    [HttpPost("preview")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollCalculation>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollCalculation>> Preview(
        PreviewPayrollCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    // --- ayarlar (hr-admin, yürürlük tarihine göre versiyonlu) ---

    [HttpGet("settings")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollSettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollSettingsDto>> GetSettings(
        [FromQuery] DateOnly? asOf, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPayrollSettingsQuery(asOf), cancellationToken));

    [HttpGet("settings/drafts")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<IReadOnlyList<PayrollSettingsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PayrollSettingsDto>>> GetSettingsDrafts(
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPayrollSettingsDraftsQuery(), cancellationToken));

    [HttpPut("settings")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PayrollSettingsDto>> UpdateSettings(
        UpdatePayrollSettingsCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    [HttpPost("settings/{id:int}/approve")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<PayrollSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollSettingsDto>> ApproveSettings(
        int id, ApprovePayrollSettingsCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id) return BadRequest("Rota ve gövde mevzuat seti kimliği eşleşmiyor.");
        return Ok(await sender.Send(command, cancellationToken));
    }

    // --- çalışanın kendi bordroları (hr-admin + employee) ---

    [HttpGet("my")]
    [Authorize(Policy = Policies.PayrollAccess)]
    [ProducesResponseType<IReadOnlyList<MyPayslipDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyPayslipDto>>> GetMyPayslips(
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMyPayslipsQuery(), cancellationToken));

    [HttpGet("periods/{id:int}/rows/{rowId:int}/payslip")]
    [Authorize(Policy = Policies.PayrollAccess)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetPayslip(int id, int rowId, CancellationToken cancellationToken)
    {
        var (content, fileName) = await sender.Send(new GetPayslipPdfQuery(id, rowId), cancellationToken);
        return File(content, "application/pdf", fileName);
    }
}
