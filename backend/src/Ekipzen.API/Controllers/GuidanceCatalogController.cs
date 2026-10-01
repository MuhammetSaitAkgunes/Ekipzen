using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Guidance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

[ApiController]
[Route("api/guidance/catalog")]
[Authorize]
public sealed class GuidanceCatalogController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GuidanceContentDto>>> Get([FromQuery] string? processKey, CancellationToken ct) =>
        Ok(await sender.Send(new GetGuidanceCatalogQuery(processKey), ct));
    [HttpGet("versions/{id:int}")]
    public async Task<ActionResult<GuidanceContentDto>> GetVersion(int id, CancellationToken ct) =>
        Ok((await sender.Send(new GetGuidanceCatalogQuery(VersionId: id), ct)).Single());
    [HttpGet("access")]
    public async Task<ActionResult<GuidanceEditorialAccessDto>> Access(CancellationToken ct) =>
        Ok(await sender.Send(new GetGuidanceEditorialAccessQuery(), ct));
    [HttpGet("readings")]
    public async Task<ActionResult<IReadOnlyList<GuidanceContentReadingDto>>> Readings(CancellationToken ct) =>
        Ok(await sender.Send(new GetMyGuidanceContentReadingsQuery(), ct));
    [HttpPost("versions/{id:int}/readings")]
    public async Task<ActionResult<GuidanceContentReadingDto>> RecordReading(int id, ReadingBody body, CancellationToken ct) =>
        Ok(await sender.Send(new RecordGuidanceContentReadingCommand(id, body.Confirmed), ct));
    [HttpGet("editorial")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    public async Task<ActionResult<IReadOnlyList<GuidanceEditorialDto>>> Editorial(CancellationToken ct) =>
        Ok(await sender.Send(new GetGuidanceEditorialQuery(), ct));
    [HttpPost("editorial")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    public async Task<ActionResult<GuidanceEditorialDto>> Create(CreateGuidanceContentCommand body, CancellationToken ct)
    {
        var result = await sender.Send(body, ct);
        return Created($"/api/guidance/catalog/editorial/{result.Content.Id}", result);
    }
    [HttpPut("editorial/{id:int}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    public async Task<ActionResult<GuidanceEditorialDto>> Update(int id, EditBody body, CancellationToken ct) =>
        Ok(await sender.Send(new UpdateGuidanceContentCommand(id, body.ExpectedRevision, body.Body), ct));
    [HttpPost("editorial/{id:int}/{operation}")]
    [Authorize(Policy = Policies.HrAdminOnly)]
    public async Task<ActionResult<GuidanceEditorialDto>> Transition(int id, string operation, TransitionBody body, CancellationToken ct) =>
        Ok(await sender.Send(new ChangeGuidanceContentStateCommand(id, body.ExpectedRevision, operation, body.Reason, body.Review), ct));
    public sealed record EditBody(int ExpectedRevision, GuidanceContentBody Body);
    public sealed record ReadingBody(bool Confirmed);
    public sealed record TransitionBody(int ExpectedRevision, string? Reason, GuidanceExpertReview? Review);
}
