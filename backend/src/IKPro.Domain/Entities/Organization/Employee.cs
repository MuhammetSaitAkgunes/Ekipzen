using IKPro.Domain.Common;
using IKPro.Domain.Enums;

namespace IKPro.Domain.Entities.Organization;

/// <summary>
/// Çalışan / personel — directory'nin kanonik kaydı. Genişletilmiş özlük bilgisi
/// <see cref="EmployeeProfile"/> içinde 1:1 tutulur. Kimlik doğrulama kullanıcısına
/// (Identity) <see cref="UserId"/> ile bağlanır (Domain saf kalsın diye string FK).
/// </summary>
public class Employee : AuditableEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>TC Kimlik No (maskeleme sunum katmanında yapılır).</summary>
    public string? NationalId { get; set; }

    public string Title { get; set; } = string.Empty;
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public DateOnly HireDate { get; set; }
    public EmploymentContractType ContractType { get; set; } = EmploymentContractType.Indefinite;

    /// <summary>Son çalışma günü; yalnız pasif personelde doludur.</summary>
    public DateOnly? TerminationDate { get; set; }

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>Yönetici (kendi kendine referans; ekip kapsamı için).</summary>
    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }

    /// <summary>Bağlı Identity kullanıcısı (varsa).</summary>
    public string? UserId { get; set; }

    public EmployeeProfile? Profile { get; set; }
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
    public ICollection<EmployeeEmploymentPeriod> EmploymentPeriods { get; set; } = new List<EmployeeEmploymentPeriod>();

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>Baş harfler (avatar), ör. "AY".</summary>
    public string Initials =>
        string.Concat(
            (FirstName.Length > 0 ? FirstName[0].ToString() : string.Empty),
            (LastName.Length > 0 ? LastName[0].ToString() : string.Empty))
        .ToUpperInvariant();
}
