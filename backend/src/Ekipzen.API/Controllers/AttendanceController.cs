using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Attendance;
using Ekipzen.Application.Features.Attendance.Commands;
using Ekipzen.Application.Features.Attendance.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Mesai &amp; puantaj uçları (routes.js: /attendance hr-admin+manager). Canlı yoklama
/// panosu, aylık puantaj, manuel giriş + satır düzenleme ve aylık özet (SQL view).
/// Manager kapsamı handler'larda ekibiyle sınırlanır.
/// </summary>
[ApiController]
[Route("api/attendance")]
[Authorize(Policy = Policies.Management)]
public sealed class AttendanceController(ISender sender) : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType<IReadOnlyList<LiveBoardCardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LiveBoardCardDto>>> GetLiveBoard(
        [FromQuery] DateOnly? date, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetLiveBoardQuery(date), cancellationToken));

    [HttpGet]
    [ProducesResponseType<TimesheetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TimesheetDto>> GetTimesheet(
        [FromQuery] int employeeId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTimesheetQuery(employeeId, year, month), cancellationToken));

    [HttpPost]
    [ProducesResponseType<TimesheetRowDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetRowDto>> Create(
        CreateEntryBody body, CancellationToken cancellationToken)
    {
        var row = await sender.Send(
            new CreateAttendanceEntryCommand(body.EmployeeId, body.Model), cancellationToken);
        return CreatedAtAction(
            nameof(GetTimesheet),
            new { employeeId = body.EmployeeId, year = row.WorkDate.Year, month = row.WorkDate.Month },
            row);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<TimesheetRowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetRowDto>> Update(
        int id, AttendanceEntryModel model, CancellationToken cancellationToken)
        => Ok(await sender.Send(new UpdateAttendanceEntryCommand(id, model), cancellationToken));

    [HttpGet("summary")]
    [ProducesResponseType<IReadOnlyList<AttendanceSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AttendanceSummaryDto>>> GetSummary(
        [FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMonthlyAttendanceSummaryQuery(year, month), cancellationToken));

    /// <summary>Manuel giriş gövdesi: personel + gün verileri.</summary>
    public sealed record CreateEntryBody(int EmployeeId, AttendanceEntryModel Model);
}
