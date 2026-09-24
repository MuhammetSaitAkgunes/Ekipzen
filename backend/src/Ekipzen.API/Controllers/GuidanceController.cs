using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Guidance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

[ApiController]
[Route("api/guidance/employee-entry")]
[Authorize(Policy = Policies.HrAdminOnly)]
public sealed class GuidanceController(ISender sender) : ControllerBase
{
    [HttpGet("bookmark")]
    [ProducesResponseType<GuidanceBookmarkDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GuidanceBookmarkDto>> GetBookmark(CancellationToken ct) =>
        Ok(await sender.Send(new GetPersonnelEntryBookmarkQuery(), ct));

    [HttpPut("bookmark")]
    [ProducesResponseType<GuidanceBookmarkDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GuidanceBookmarkDto>> SaveBookmark(
        SavePersonnelEntryBookmarkCommand command, CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    [HttpGet("employees/{employeeId:int}/evidence")]
    [ProducesResponseType<EmployeeCreationEvidenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeCreationEvidenceDto>> GetEmployeeCreationEvidence(
        int employeeId, CancellationToken ct) =>
        Ok(await sender.Send(new GetEmployeeCreationEvidenceQuery(employeeId), ct));
}
