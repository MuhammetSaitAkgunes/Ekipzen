using Ekipzen.Domain.Common;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Domain.Entities.Organization;

/// <summary>
/// Personel kartının genişletilmiş özlük bilgisi (kimlik, iletişim, iş, mali, sağlık/özlük).
/// <see cref="Employee"/> ile 1:1. Frontend personnel.js form sekmelerine karşılık gelir.
/// </summary>
public class EmployeeProfile : AuditableEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    // Kimlik
    public DateOnly? BirthDate { get; set; }
    public string? Gender { get; set; }          // Erkek | Kadın
    public string? MaritalStatus { get; set; }   // Evli | Bekar
    public string? BloodType { get; set; }       // 0 Rh+ | A Rh+ | ...
    public string? PhotoPath { get; set; }

    // İletişim
    public string? MobilePhone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? HomeAddress { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // İş & kurumsal
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public string? RehireEligibility { get; set; } // Değerlendirilmedi | Çalışılabilir | ...
    public string? ExitCode { get; set; }          // Yok | Kod-03 (İstifa)

    // Mali
    public string? Iban { get; set; }
    public string? BankName { get; set; }
    public string? SalaryType { get; set; }        // Net | Brüt
    public string? PensionStatus { get; set; }     // BES: Otomatik Katılım | İptal | Muaf
    public string? MealCard { get; set; }

    // Özlük & sağlık
    public string? TshirtSize { get; set; }
    public string? PantsSize { get; set; }
    public string? CoatSize { get; set; }
    public string? ShoeSize { get; set; }
    // Eski veri korunur; bu üç alan aktif personel API'sinden ve kart yazımından kaldırıldı.
    public bool CanWorkAtHeight { get; set; }
    public bool CanWorkNightShift { get; set; }
    public bool CanLiftHeavyLoads { get; set; }
    public string? HealthNotes { get; set; }
}
