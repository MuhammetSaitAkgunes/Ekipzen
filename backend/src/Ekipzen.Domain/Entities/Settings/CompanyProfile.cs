using Ekipzen.Domain.Common;

namespace Ekipzen.Domain.Entities.Settings;

/// <summary>Şirket profili / markalama (tekil kayıt).</summary>
public class CompanyProfile : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? SystemEmail { get; set; }
    public string? Phone { get; set; }
    public string? HeadquartersAddress { get; set; }
    public string? LogoPath { get; set; }
}
