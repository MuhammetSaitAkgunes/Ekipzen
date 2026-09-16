using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.ReadModels;

/// <summary>
/// Denetim hazırlık skoru okuma-modeli (SQL view: vw_ComplianceReadiness, tek satır).
/// Skor formülleri dashboard.js complianceMetrics ile hizalıdır:
///   evrak uyum skoru = 100 * tamamlanan / toplam (boşsa 100)
///   hazırlık skoru   = 100 - eksik*6 - süresiYaklaşan*3 - incelemede*2 (0..100)
/// </summary>
public class ComplianceReadiness : ITenantScoped
{
    /// <summary>Sahip kiracı (view TenantId kolonundan; global filtre bununla izole eder).</summary>
    public int TenantId { get; set; }

    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int MissingCount { get; set; }
    public int DueSoonCount { get; set; }
    public int InReviewCount { get; set; }

    /// <summary>Sorumlusu atanmış kayıt sayısı (sorumlu atama netliği göstergesi).</summary>
    public int OwnedCount { get; set; }

    public int DocumentComplianceScore { get; set; }
    public int ReadinessScore { get; set; }
}
