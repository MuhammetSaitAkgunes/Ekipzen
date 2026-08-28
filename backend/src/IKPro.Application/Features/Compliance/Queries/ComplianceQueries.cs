using FluentValidation;
using IKPro.Application.Common.Interfaces;
using IKPro.Domain.Constants;
using IKPro.Domain.Enums;
using IKPro.Domain.ReadModels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IKPro.Application.Features.Compliance.Queries;

/// <summary>
/// Uyum belgeleri listesi: durum/seviye/arama filtreli, rol kapsamlı
/// (hr-admin → hepsi, manager → ekibi + kendisi).
/// </summary>
public sealed record GetComplianceDocumentsQuery(
    string? Status = null,
    string? Level = null,
    string? Search = null) : IRequest<IReadOnlyList<ComplianceDocumentDto>>;

public sealed class GetComplianceDocumentsQueryValidator
    : AbstractValidator<GetComplianceDocumentsQuery>
{
    public GetComplianceDocumentsQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || ComplianceMappings.IsStatus(s))
            .WithMessage("Belge durumu filtresi: Eksik | İncelemede | Süresi Yaklaşıyor | Tamamlandı.");
        RuleFor(x => x.Level)
            .Must(l => string.IsNullOrWhiteSpace(l) || ComplianceMappings.IsLevel(l))
            .WithMessage("Risk seviyesi filtresi: high | medium | low.");
    }
}

public sealed class GetComplianceDocumentsQueryHandler(
    IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetComplianceDocumentsQuery, IReadOnlyList<ComplianceDocumentDto>>
{
    public async Task<IReadOnlyList<ComplianceDocumentDto>> Handle(
        GetComplianceDocumentsQuery request, CancellationToken cancellationToken)
    {
        var query = context.ComplianceDocuments
            .Include(d => d.Employee)!.ThenInclude(e => e!.Department)
            .ScopeFor(currentUser);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ComplianceMappings.ParseStatus(request.Status);
            query = query.Where(d => d.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Level))
        {
            var level = ComplianceMappings.ParseLevel(request.Level);
            query = query.Where(d => d.Level == level);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(d =>
                d.DocumentName.Contains(term) ||
                (d.Employee!.FirstName + " " + d.Employee.LastName).Contains(term) ||
                (d.OwnerName != null && d.OwnerName.Contains(term)));
        }

        var documents = await query
            .OrderBy(d => d.DueDate == null).ThenBy(d => d.DueDate)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return documents.Select(d => d.ToDto(today)).ToList();
    }
}

/// <summary>
/// Denetim hazırlık özeti. hr-admin → şirket geneli <c>vw_ComplianceReadiness</c> (hızlı yol);
/// manager → belge tablosuyla aynı kapsam (yalnız ekibi) — KPI'lar tabloyla tutarlı olur.
/// </summary>
public sealed record GetComplianceReadinessQuery : IRequest<ComplianceReadinessDto>;

public sealed class GetComplianceReadinessQueryHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetComplianceReadinessQuery, ComplianceReadinessDto>
{
    public async Task<ComplianceReadinessDto> Handle(
        GetComplianceReadinessQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Roles.Contains(Roles.HrAdmin))
        {
            var global = await context.ComplianceReadiness
                .FirstOrDefaultAsync(cancellationToken)
                ?? new ComplianceReadiness { DocumentComplianceScore = 100, ReadinessScore = 100 };
            return global.ToDto();
        }

        var scoped = await context.ComplianceDocuments
            .ScopeFor(currentUser)
            .Select(d => new { d.Status, HasOwner = d.OwnerName != null && d.OwnerName != "" })
            .ToListAsync(cancellationToken);

        return ComplianceReadinessAggregator
            .From(scoped.Select(x => (x.Status, x.HasOwner)))
            .ToDto();
    }
}

/// <summary>
/// <c>vw_ComplianceReadiness</c> sayım/skor formülünün uygulama karşılığı — rol kapsamlı
/// (manager) hesaplarda kullanılır. View SQL'iyle birebir tutulmalıdır.
/// </summary>
public static class ComplianceReadinessAggregator
{
    public static ComplianceReadiness From(IEnumerable<(ComplianceStatus Status, bool HasOwner)> documents)
    {
        var items = documents.ToList();
        var total = items.Count;
        var completed = items.Count(d => d.Status == ComplianceStatus.Completed);
        var missing = items.Count(d => d.Status == ComplianceStatus.Missing);
        var dueSoon = items.Count(d => d.Status == ComplianceStatus.DueSoon);
        var inReview = items.Count(d => d.Status == ComplianceStatus.InReview);
        var owned = items.Count(d => d.HasOwner);

        return new ComplianceReadiness
        {
            TotalCount = total,
            CompletedCount = completed,
            MissingCount = missing,
            DueSoonCount = dueSoon,
            InReviewCount = inReview,
            OwnedCount = owned,
            DocumentComplianceScore = total == 0
                ? 100
                : (int)Math.Round(100.0 * completed / total, MidpointRounding.AwayFromZero),
            ReadinessScore = total == 0
                ? 100
                : Math.Clamp(100 - missing * 6 - dueSoon * 3 - inReview * 2, 0, 100),
        };
    }
}

public static class ComplianceReadinessMappings
{
    public static ComplianceReadinessDto ToDto(this ComplianceReadiness r)
    {
        // Oranlar boş tabloda %100 kabul edilir (risk yok).
        var dueSoonControl = r.TotalCount == 0
            ? 100 : 100 - (int)Math.Round(100.0 * r.DueSoonCount / r.TotalCount);
        var ownerClarity = r.TotalCount == 0
            ? 100 : (int)Math.Round(100.0 * r.OwnedCount / r.TotalCount);

        // dashboard.js auditChecklist kalemlerinin veri kaynaklı karşılıkları.
        var checklist = new List<AuditChecklistItemDto>
        {
            new("Personel dosyası bütünlüğü", r.DocumentComplianceScore,
                ChecklistLevel(r.DocumentComplianceScore)),
            new("Süresi yaklaşan evrak kontrolü", dueSoonControl, ChecklistLevel(dueSoonControl)),
            new("Sorumlu atama netliği", ownerClarity, ChecklistLevel(ownerClarity)),
            new("Denetim klasörü hazırlığı", r.ReadinessScore, ChecklistLevel(r.ReadinessScore)),
        };

        var recommendedActions = new List<string>();
        if (r.MissingCount > 0)
        {
            recommendedActions.Add($"Eksik {r.MissingCount} evrakı bugün kapat");
        }
        if (r.DueSoonCount > 0)
        {
            recommendedActions.Add($"Süresi yaklaşan {r.DueSoonCount} evrak için sorumlu ataması yap");
        }
        if (r.OwnedCount < r.TotalCount)
        {
            recommendedActions.Add("Sorumlusu olmayan evraklara owner ata");
        }
        if (recommendedActions.Count == 0)
        {
            recommendedActions.Add("Denetim klasörü kontrol listesini haftalık takip et");
        }

        return new ComplianceReadinessDto(
            r.TotalCount,
            r.CompletedCount,
            r.MissingCount,
            r.DueSoonCount,
            r.InReviewCount,
            r.OwnedCount,
            r.DocumentComplianceScore,
            r.ReadinessScore,
            r.ReadinessScore >= 80 ? "Düşük" : r.ReadinessScore >= 60 ? "Orta" : "Yüksek",
            checklist,
            recommendedActions);
    }

    private static string ChecklistLevel(int value)
        => value >= 85 ? "low" : value >= 70 ? "medium" : "high";
}
