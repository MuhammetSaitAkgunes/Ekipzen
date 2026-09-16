using Ekipzen.Application.Common.Security;
using Ekipzen.Application.Features.Dashboard;
using Ekipzen.Application.Features.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Analitik &amp; risk merkezi uçları — routes.js: /dashboard ve /risk/* hr-admin + manager;
/// /overview tüm roller. Metrikler rol kapsamına göre daralır (manager → kendi ekibi).
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Policies.Management)]
public sealed class DashboardController(ISender sender) : ControllerBase
{
    /// <summary>Risk merkezi ana metrikleri (dashboard.js getDashboardMetrics).</summary>
    [HttpGet("metrics")]
    [ProducesResponseType<DashboardMetricsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardMetricsDto>> GetMetrics(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetDashboardMetricsQuery(), cancellationToken));

    [HttpGet("attrition")]
    [ProducesResponseType<RiskDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskDetailDto>> GetAttrition(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAttritionDetailQuery(), cancellationToken));

    [HttpGet("burnout")]
    [ProducesResponseType<RiskDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskDetailDto>> GetBurnout(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetBurnoutDetailQuery(), cancellationToken));

    [HttpGet("manager-load")]
    [ProducesResponseType<ManagerLoadDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagerLoadDto>> GetManagerLoad(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetManagerLoadQuery(), cancellationToken));

    [HttpGet("employee-voice")]
    [ProducesResponseType<EmployeeVoiceDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeVoiceDto>> GetEmployeeVoice(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetEmployeeVoiceQuery(), cancellationToken));

    [HttpGet("compliance")]
    [ProducesResponseType<ComplianceRiskDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ComplianceRiskDto>> GetCompliance(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetComplianceRiskQuery(), cancellationToken));
}

/// <summary>
/// Genel durum KPI'ları — routes.js /overview tüm rollere açıktır, bu yüzden
/// Management policy'li DashboardController'dan ayrı tutulur.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class OverviewController(ISender sender) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<OverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOverviewQuery(), cancellationToken));
}
