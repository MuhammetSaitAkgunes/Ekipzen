using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Application.Features.Compliance;
using Ekipzen.Application.Features.Compliance.Queries;
using Ekipzen.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ekipzen.Application.Features.Dashboard.Queries;

/// <summary>
/// Uyum risk merkezi özeti (dashboard.js complianceMetrics karşılığı).
/// Skorlar vw_ComplianceReadiness view'ından okunur (Faz 9); kayıt ve son tarih
/// listeleri ComplianceDocuments tablosundan derlenir.
/// </summary>
public sealed record GetComplianceRiskQuery : IRequest<ComplianceRiskDto>;

public sealed class GetComplianceRiskQueryHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<GetComplianceRiskQuery, ComplianceRiskDto>
{
    public async Task<ComplianceRiskDto> Handle(
        GetComplianceRiskQuery request, CancellationToken cancellationToken)
    {
        var readiness = await sender.Send(new GetComplianceReadinessQuery(), cancellationToken);

        var documents = await context.ComplianceDocuments
            .Include(d => d.Employee)!.ThenInclude(e => e!.Department)
            .OrderBy(d => d.DueDate == null).ThenBy(d => d.DueDate)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var records = documents
            .Select(d =>
            {
                var dto = d.ToDto(today);
                return new ComplianceRecordDto(
                    dto.Id, dto.Employee, dto.Dept, dto.Document, dto.Owner,
                    dto.DueLabel, dto.Status, dto.Level);
            })
            .ToList();

        // Yaklaşan son tarihler: tamamlanmamış evraklar belge adına göre gruplanır.
        var deadlines = documents
            .Where(d => d.Status != ComplianceStatus.Completed)
            .GroupBy(d => d.DocumentName)
            .Select(g =>
            {
                var earliest = g.OrderBy(d => d.DueDate == null).ThenBy(d => d.DueDate).First();
                var level = g.Any(d => d.Level == RiskLevel.High) ? "high"
                    : g.Any(d => d.Level == RiskLevel.Medium) ? "medium" : "low";
                return new ComplianceDeadlineDto(
                    g.Key, g.Count(),
                    ComplianceMappings.DueLabel(earliest.Status, earliest.DueDate, today),
                    earliest.OwnerName, level);
            })
            .OrderBy(d => d.Level == "high" ? 0 : d.Level == "medium" ? 1 : 2)
            .ToList();

        return new ComplianceRiskDto(
            readiness.DocumentComplianceScore,
            readiness.MissingCount,
            readiness.DueSoonCount,
            readiness.ReadinessRisk,
            readiness.ReadinessScore,
            records,
            deadlines);
    }
}
