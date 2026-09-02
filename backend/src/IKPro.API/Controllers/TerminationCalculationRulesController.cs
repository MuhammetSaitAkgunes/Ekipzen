using IKPro.Application.Common.Security;
using IKPro.Application.Features.Employees.Offboarding;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IKPro.API.Controllers;

/// <summary>
/// İşten çıkış taslak hesap motorunun uzman onaylı, tarih etkili kural sürümleri.
/// Onaylanan sürüm değiştirilemez; yeni değerler yeni sürüm olarak açılır.
/// </summary>
[ApiController]
[Route("api/termination-calculation-rules")]
[Authorize(Policy = Policies.HrAdminOnly)]
public sealed class TerminationCalculationRulesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TerminationCalculationRuleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TerminationCalculationRuleDto>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetTerminationCalculationRulesQuery(), cancellationToken));

    [HttpPost]
    [ProducesResponseType<TerminationCalculationRuleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TerminationCalculationRuleDto>> Create(
        SaveTerminationCalculationRuleBody body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(body.ToCommand(), cancellationToken);
        return Created($"/api/termination-calculation-rules/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<TerminationCalculationRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TerminationCalculationRuleDto>> Update(
        int id,
        SaveTerminationCalculationRuleBody body,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(body.ToCommand(id), cancellationToken));

    [HttpPost("{id:int}/approve")]
    [ProducesResponseType<TerminationCalculationRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TerminationCalculationRuleDto>> Approve(
        int id,
        ApproveTerminationCalculationRuleBody body,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(
            new ApproveTerminationCalculationRuleCommand(id, body.ExpectedVersion),
            cancellationToken));

    public sealed record SaveTerminationCalculationRuleBody(
        int? ExpectedVersion,
        DateOnly EffectiveFrom,
        decimal DailyWageDivisor,
        decimal SeveranceDaysPerYear,
        int MinimumSeveranceServiceDays,
        decimal SeveranceCeiling,
        int NoticeDaysUnderSixMonths,
        int NoticeDaysSixToEighteenMonths,
        int NoticeDaysEighteenToThirtySixMonths,
        int NoticeDaysOverThirtySixMonths,
        int NoticeFirstThresholdMonths,
        int NoticeSecondThresholdMonths,
        int NoticeThirdThresholdMonths,
        bool IncludeRegularPremiumInDressedWage,
        bool IncludeRoadAllowanceInDressedWage,
        bool IncludeMealAllowanceInDressedWage,
        bool IncludeOtherRegularBenefitsInDressedWage,
        string SourceReference,
        string? ReviewNotes)
    {
        public SaveTerminationCalculationRuleCommand ToCommand(int? id = null) => new(
            id,
            ExpectedVersion,
            EffectiveFrom,
            DailyWageDivisor,
            SeveranceDaysPerYear,
            MinimumSeveranceServiceDays,
            SeveranceCeiling,
            NoticeDaysUnderSixMonths,
            NoticeDaysSixToEighteenMonths,
            NoticeDaysEighteenToThirtySixMonths,
            NoticeDaysOverThirtySixMonths,
            NoticeFirstThresholdMonths,
            NoticeSecondThresholdMonths,
            NoticeThirdThresholdMonths,
            IncludeRegularPremiumInDressedWage,
            IncludeRoadAllowanceInDressedWage,
            IncludeMealAllowanceInDressedWage,
            IncludeOtherRegularBenefitsInDressedWage,
            SourceReference,
            ReviewNotes);
    }

    public sealed record ApproveTerminationCalculationRuleBody(int ExpectedVersion);
}
