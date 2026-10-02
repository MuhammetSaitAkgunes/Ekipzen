using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Departments;
using Ekipzen.Application.Features.Departments.CreateDepartment;
using Ekipzen.Application.Features.Departments.DeleteDepartment;
using Ekipzen.Application.Features.Departments.GetDepartments;
using Ekipzen.Application.Features.Departments.UpdateDepartment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

/// <summary>Departman CRUD. Okuma: yönetim rolleri; mutasyon: yalnız hr-admin.</summary>
[ApiController]
[Route("api/departments")]
public sealed class DepartmentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Management)]
    [ProducesResponseType<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetDepartmentsQuery(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var department = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, department);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Update(
        int id, UpdateDepartmentBody body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new UpdateDepartmentCommand(id, body.Name, body.Code, body.ExpectedRecordToken, body.GuidanceContentRevisionId), cancellationToken));

    [HttpGet("{id:int}/deletion-preview")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType<DepartmentDeletionPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DepartmentDeletionPreviewDto>> DeletionPreview(int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetDepartmentDeletionPreviewQuery(id), cancellationToken));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, [FromQuery] string? expectedRecordToken, [FromQuery] int? guidanceContentRevisionId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDepartmentCommand(id, expectedRecordToken, guidanceContentRevisionId), cancellationToken);
        return NoContent();
    }

    /// <summary>Gövde modeli: id route'tan gelir.</summary>
    public sealed record UpdateDepartmentBody(string Name, string? Code, string? ExpectedRecordToken = null, int? GuidanceContentRevisionId = null);
}
