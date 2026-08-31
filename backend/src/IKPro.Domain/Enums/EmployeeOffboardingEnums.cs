namespace IKPro.Domain.Enums;

public enum EmployeeOffboardingStatus
{
    Draft,
    Completed,
    Reversed,
}

/// <summary>
/// E-5A aşamasında tutarlar uygulama tarafından mevzuat hesabıyla üretilmez;
/// yetkili uzman veya harici bordro kaynağından alınan kapanış kaydı saklanır.
/// </summary>
public enum OffboardingSettlementSource
{
    ExternalExpert,
}
