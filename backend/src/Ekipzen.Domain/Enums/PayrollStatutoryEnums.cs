namespace Ekipzen.Domain.Enums;

/// <summary>Motorun mevzuat kapsamı açıkça onaylanmış çalışan profili.</summary>
public enum PayrollStatutoryProfile
{
    Unspecified,
    DomesticPrivateSectorStandardFourA,
}

/// <summary>Nakit yemek yardımının vergi/SGK sınıflandırması.</summary>
public enum PayrollMealTreatment
{
    Unspecified,
    CashExemptWhereNoMealProvided,
    FullyTaxable,
}

/// <summary>2026'da resmî maliyet tablosunda yer alan genel işveren SGK indirimleri.</summary>
public enum EmployerSgkDiscountType
{
    None,
    OtherSectorTwoPoints,
    ManufacturingFivePoints,
}

/// <summary>193 sayılı Kanunun 31. maddesi kapsamındaki aylık engellilik indirimi sınıfı.</summary>
public enum PayrollDisabilityReliefType
{
    Unspecified,
    None,
    FirstDegree,
    SecondDegree,
    ThirdDegree,
}

public enum PayrollSettingsStatus
{
    Draft,
    Approved,
}
