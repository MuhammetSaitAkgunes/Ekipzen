using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Lightweight, unauthenticated endpoints for smoke-testing that the API is up.
/// </summary>
[ApiController]
[Route("api")]
public sealed class MetaController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new
    {
        service = "Ekipzen API",
        status = "ok",
        version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
        timeUtc = DateTime.UtcNow
    });
}
