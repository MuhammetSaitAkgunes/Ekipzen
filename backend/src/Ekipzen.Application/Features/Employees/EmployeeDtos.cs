using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Domain.Constants;
using Ekipzen.Domain.Entities.Organization;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Application.Features.Employees;

/// <summary>Directory satırı — personnel.js tablo kolonlarıyla birebir (TC maskeli).</summary>
public sealed record EmployeeListItemDto(
    int Id,
    string Name,
    string Title,
    string? NationalIdMasked,
    int DepartmentId,
    string Department,
    string Status,
    string Initials,
    DateOnly HireDate,
    DateOnly? TerminationDate);

/// <summary>Personel kartının profil grupları (personnel.js form sekmeleri).</summary>
public sealed record EmployeeProfileDto(
    // Kimlik
    DateOnly? BirthDate,
    string? Gender,
    string? MaritalStatus,
    string? BloodType,
    string? PhotoPath,
    // İletişim
    string? MobilePhone,
    string? PersonalEmail,
    string? HomeAddress,
    string? EmergencyContactName,
    string? EmergencyContactRelation,
    string? EmergencyContactPhone,
    // İş & kurumsal
    string EmploymentType,
    string? RehireEligibility,
    string? ExitCode,
    // Mali
    string? Iban,
    string? BankName,
    string? SalaryType,
    string? PensionStatus,
    string? MealCard,
    // Özlük & sağlık
    string? TshirtSize,
    string? PantsSize,
    string? CoatSize,
    string? ShoeSize,
    bool CanWorkAtHeight,
    bool CanWorkNightShift,
    bool CanLiftHeavyLoads,
    string? HealthNotes);

public sealed record EmployeeDocumentDto(
    int Id,
    string DocumentType,
    string FileName,
    string? ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);

/// <summary>Tam personel kartı. TC yalnız hr-admin için açık döner.</summary>
public sealed record EmployeeDetailDto(
    int Id,
    string FirstName,
    string LastName,
    string Name,
    string Initials,
    string Title,
    string? NationalId,
    string Status,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string ContractType,
    int DepartmentId,
    string Department,
    int? ManagerId,
    string? ManagerName,
    EmployeeProfileDto Profile,
    IReadOnlyList<EmployeeDocumentDto> Documents);

/// <summary>Frontend değerleri ↔ enum eşlemeleri (status: active|passive, çalışma şekli Türkçe etiket).</summary>
public static class EmployeeMappings
{
    public static string ToDto(this EmployeeStatus status)
        => status == EmployeeStatus.Active ? "active" : "passive";

    public static EmployeeStatus ParseStatus(string value) => value switch
    {
        "active" => EmployeeStatus.Active,
        "passive" => EmployeeStatus.Passive,
        _ => throw new ArgumentException($"Geçersiz durum: {value} (active|passive bekleniyor)."),
    };

    public static string ToDto(this EmploymentType type) => type switch
    {
        EmploymentType.FullTime => "Tam Zamanlı",
        EmploymentType.PartTime => "Yarı Zamanlı",
        EmploymentType.Remote => "Uzaktan",
        _ => type.ToString(),
    };

    public static EmploymentType ParseEmploymentType(string? value) => value switch
    {
        null or "" or "Tam Zamanlı" => EmploymentType.FullTime,
        "Yarı Zamanlı" => EmploymentType.PartTime,
        "Uzaktan" => EmploymentType.Remote,
        _ => throw new ArgumentException($"Geçersiz çalışma şekli: {value}."),
    };

    /// <summary>Directory'deki maskeli TC gösterimi (mockData: "123*****").</summary>
    public static string? MaskNationalId(string? nationalId)
        => string.IsNullOrEmpty(nationalId)
            ? null
            : nationalId[..Math.Min(3, nationalId.Length)] + "*****";
}

/// <summary>
/// Rol bazlı personel kapsamı: hr-admin → herkes; manager → kendi ekibi + kendisi;
/// employee → yalnız kendisi (routes.js kapsam ilkesinin sorgu karşılığı).
/// </summary>
public static class EmployeeScopeExtensions
{
    public static IQueryable<Employee> ScopeFor(this IQueryable<Employee> query, ICurrentUser user)
    {
        if (user.Roles.Contains(Roles.HrAdmin))
        {
            return query;
        }

        var selfId = user.EmployeeId ?? -1;
        return user.Roles.Contains(Roles.Manager)
            ? query.Where(e => e.ManagerId == selfId || e.Id == selfId)
            : query.Where(e => e.Id == selfId);
    }
}
