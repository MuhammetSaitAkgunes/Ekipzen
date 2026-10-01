using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Guidance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

[ApiController]
[Route("api/guidance/help-preferences")]
[Authorize(Policy = Policies.Management)]
public sealed class GuidanceHelpController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GuidanceHelpPreferenceDto>>> Get(CancellationToken ct) =>
        Ok(await sender.Send(new GetGuidanceHelpPreferencesQuery(), ct));
    [HttpPut("{processKey}")]
    public async Task<ActionResult<GuidanceHelpPreferenceDto>> Save(string processKey, HelpPreferenceBody body, CancellationToken ct) =>
        Ok(await sender.Send(new SaveGuidanceHelpPreferenceCommand(processKey, body.Mode, body.ExpectedRevision), ct));
    public sealed record HelpPreferenceBody(string Mode, int? ExpectedRevision);
}
