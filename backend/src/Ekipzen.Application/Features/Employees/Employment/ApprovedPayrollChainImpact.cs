using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Payroll;
using Ekipzen.Domain.Entities.Payroll;
using Ekipzen.Domain.Enums;
using Ekipzen.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Employees.Employment;

public sealed record ApprovedPayrollChainRowDto(
    int PeriodId, int RowId, int Year, int Month,
    decimal RecordedPreviousTaxBase, decimal? ProjectedPreviousTaxBase,
    ApprovedPayrollAmountDto? Recorded, ApprovedPayrollAmountDto? Projected,
    string? BlockingReason);

public sealed record ApprovedPayrollChainImpactDto(
    bool CanEstimate, IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<ApprovedPayrollChainRowDto> LaterApprovedRows,
    string PreviewToken);

internal static class ApprovedPayrollChainImpact
{
    public static async Task<ApprovedPayrollChainImpactDto> CalculateAsync(
        IApplicationDbContext context, IPayrollEngine engine,
        PayrollEmployee source, ApprovedPayrollAmountDto? proposed,
        string sourcePreviewToken,
        CancellationToken cancellationToken)
    {
        var laterRows = await context.PayrollEmployees.AsNoTracking()
            .Where(row => row.EmployeeId == source.EmployeeId
                && row.Period!.Year == source.Period!.Year
                && row.Period.Month > source.Period.Month
                && row.ApprovalStatus == PayrollApprovalStatus.Approved)
            .Include(row => row.Period)!.ThenInclude(period => period!.Settings)!
                .ThenInclude(settings => settings!.TaxBrackets)
            .Include(row => row.Result)
            .OrderBy(row => row.Period!.Month)
            .ToListAsync(cancellationToken);
        if (laterRows.Count == 0)
            return new(true, [], [], string.Empty);

        var issues = new List<string>();
        var projectedRows = new List<ApprovedPayrollChainRowDto>();
        decimal? projectedCarry = proposed is null || source.Result is null
            ? null : source.PreviousTaxBase + proposed.IncomeTaxBase;
        decimal? recordedCarry = source.Result is null
            ? null : source.PreviousTaxBase + source.Result.IncomeTaxBase;
        if (projectedCarry is null)
            issues.Add("İlk bordro ayı için güvenilir öneri hesabı bulunmadan zincirleme etki hesaplanamaz.");

        var revisions = await ApprovedPayrollRevisionRules.LatestByRowAsync(context,
            laterRows.Select(row => row.Id).ToArray(), cancellationToken);
        foreach (var row in laterRows)
        {
            ApprovedPayrollAmountDto? recorded = row.Result is null
                ? null : new ApprovedPayrollAmountDto(row.Result.GrossEarnings,
                    row.Result.IncomeTaxBase, row.Result.IncomeTax,
                    row.Result.NetPay, row.Result.EmployerCost);
            var reason = GetBlockingReason(row, recordedCarry,
                revisions.ContainsKey(row.Id), engine);
            if (reason is null && projectedCarry is null)
                reason = "önceki ay doğrulanamadığından zincirleme tahmin durduruldu.";
            if (reason is not null)
                issues.Add($"{row.Period!.Year}/{row.Period.Month:00} bordrosu: {reason}");
            ApprovedPayrollAmountDto? projected = null;
            var projectedBefore = projectedCarry;
            if (reason is null && projectedCarry is not null)
            {
                var calculation = engine.Calculate(row.ToEngineInput(
                    row.Period!.MinimumWageIncomeTaxBaseBefore)
                    with { PreviousTaxBase = projectedCarry.Value },
                    row.Period.Settings!.ToEngineParameters());
                projected = new(calculation.GrossEarnings, calculation.IncomeTaxBase,
                    calculation.IncomeTax, calculation.NetPay, calculation.EmployerCost);
                projectedCarry += calculation.IncomeTaxBase;
            }
            else
            {
                projectedCarry = null;
            }
            projectedRows.Add(new(row.PayrollPeriodId, row.Id,
                row.Period!.Year, row.Period.Month,
                row.PreviousTaxBase, projectedBefore, recorded, projected, reason));
            recordedCarry = recorded is null
                ? null : row.PreviousTaxBase + recorded.IncomeTaxBase;
        }
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new {
                SourcePreviewToken = sourcePreviewToken,
                Rows = laterRows.Select(row => new {
                    row.Id, row.UpdatedAtUtc, row.ApprovalStatus,
                    ResultUpdatedAtUtc = row.Result?.UpdatedAtUtc,
                    ResultCalculatedAtUtc = row.Result?.CalculatedAtUtc,
                    PeriodStatus = row.Period!.Status,
                    SettingsVersion = row.Period.Settings?.Version,
                    SettingsUpdatedAtUtc = row.Period.Settings?.UpdatedAtUtc,
                    Parameters = row.Period.Settings?.ToEngineParameters(),
                    Input = row.ToEngineInput(row.Period.MinimumWageIncomeTaxBaseBefore),
                }),
                ProjectedRows = projectedRows,
            }))));
        return new(issues.Count == 0, issues, projectedRows, token);
    }

    private static string? GetBlockingReason(PayrollEmployee row, decimal? recordedCarry,
        bool hasRevision, IPayrollEngine engine)
    {
        if (row.Period!.Status is not (PayrollPeriodStatus.Approved or PayrollPeriodStatus.Closed)
            || row.Result is null || row.Period.Settings is null)
            return "onaylı dönem sonucu eksik veya tutarsız.";
        if (hasRevision)
            return "bu ayın ayrı parasal sürümü var; birden çok düzeltme birlikte uzman incelemesi gerektirir.";
        if (recordedCarry is null)
            return "önceki onaylı sonuç bulunamadığından vergi matrahı zinciri doğrulanamıyor.";
        if (row.StatutoryProfile != PayrollStatutoryProfile.DomesticPrivateSectorStandardFourA)
            return "kanuni profil doğrulanmış hesaplama kapsamı dışında.";
        if (row.PreviousTaxBase != recordedCarry)
            return "saklı önceki vergi matrahı önceki onaylı sonuçla uyuşmuyor.";
        try
        {
            var baseline = engine.Calculate(row.ToEngineInput(
                row.Period.MinimumWageIncomeTaxBaseBefore),
                row.Period.Settings.ToEngineParameters());
            if (baseline.GrossEarnings != row.Result.GrossEarnings
                || baseline.IncomeTaxBase != row.Result.IncomeTaxBase
                || baseline.IncomeTax != row.Result.IncomeTax
                || baseline.NetPay != row.Result.NetPay
                || baseline.EmployerCost != row.Result.EmployerCost)
                return "güncel parametreyle yeniden hesaplama saklı onaylı sonuçla uyuşmuyor.";
        }
        catch (ArgumentException)
        {
            return "saklı bordro girdileri doğrulanmış hesaplama kapsamı dışında.";
        }
        return null;
    }
}
