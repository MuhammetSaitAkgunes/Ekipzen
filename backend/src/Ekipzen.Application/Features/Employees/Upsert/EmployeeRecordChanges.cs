using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ekipzen.Domain.Entities.Organization;

namespace Ekipzen.Application.Features.Employees.Upsert;

public static class EmployeeRecordChanges
{
    private static readonly IReadOnlyDictionary<string, string> ProfileLabels = new Dictionary<string, string>
    {
        ["BirthDate"] = "Kimlik / Doğum tarihi", ["Gender"] = "Kimlik / Cinsiyet", ["MaritalStatus"] = "Kimlik / Medeni durum", ["BloodType"] = "Kimlik / Kan grubu",
        ["MobilePhone"] = "İletişim / Cep telefonu", ["PersonalEmail"] = "İletişim / E-posta", ["HomeAddress"] = "İletişim / Adres",
        ["EmergencyContactName"] = "İletişim / Acil durum kişisi", ["EmergencyContactRelation"] = "İletişim / Acil durum yakınlığı", ["EmergencyContactPhone"] = "İletişim / Acil durum telefonu",
        ["EmploymentType"] = "İş / Çalışma şekli", ["RehireEligibility"] = "İş / Yeniden işe alım uygunluğu", ["ExitCode"] = "İş / Profil çıkış kodu",
        ["Iban"] = "Mali / IBAN", ["BankName"] = "Mali / Banka", ["SalaryType"] = "Mali / Ücret türü", ["PensionStatus"] = "Mali / Emeklilik", ["MealCard"] = "Mali / Yemek kartı",
        ["TshirtSize"] = "Özlük / Tişört", ["PantsSize"] = "Özlük / Pantolon", ["CoatSize"] = "Özlük / Mont", ["ShoeSize"] = "Özlük / Ayakkabı",
        ["HealthNotes"] = "Özlük / Sağlık notu",
    };

    public static SortedDictionary<string, string> Capture(Employee employee)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Add(string label, object? value) => values.Add(label, JsonSerializer.Serialize(value));
        Add("Kimlik / Ad", employee.FirstName); Add("Kimlik / Soyad", employee.LastName); Add("Kimlik / TC", employee.NationalId);
        Add("İş / Unvan", employee.Title); Add("İş / Departman", employee.DepartmentId); Add("İş / Yönetici", employee.ManagerId);
        Add("İş / İşe giriş", employee.HireDate); Add("İş / Sözleşme", employee.ContractType); Add("İş / Durum", employee.Status);
        var profile = employee.Profile ?? new EmployeeProfile();
        foreach (var (property, label) in ProfileLabels)
            Add(label, typeof(EmployeeProfile).GetProperty(property)!.GetValue(profile));
        return values;
    }
    public static string Token(Employee employee) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Capture(employee)))));
    public static string[] Changed(SortedDictionary<string, string> before, Employee after) => Capture(after)
        .Where(x => before[x.Key] != x.Value).Select(x => x.Key).ToArray();
}
