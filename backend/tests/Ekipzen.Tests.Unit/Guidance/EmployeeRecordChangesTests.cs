using Ekipzen.Application.Features.Employees.Upsert;
using Ekipzen.Domain.Entities.Organization;
using FluentAssertions;

namespace Ekipzen.Tests.Unit.Guidance;

public sealed class EmployeeRecordChangesTests
{
    [Fact]
    public void UnchangedCardAndSeparatePhotoHaveSameTokenAndNoChangedFields()
    {
        var employee = new Employee { FirstName = "Ad", LastName = "Soyad", Title = "Uzman", DepartmentId = 1 };
        var before = EmployeeRecordChanges.Capture(employee);
        var token = EmployeeRecordChanges.Token(employee);
        employee.Profile = new EmployeeProfile { PhotoPath = "new-photo.jpg" };
        EmployeeRecordChanges.Changed(before, employee).Should().BeEmpty();
        EmployeeRecordChanges.Token(employee).Should().Be(token);
    }

    [Fact]
    public void ContactOrganizationAndSensitiveCorrectionsChangeTokenButExposeOnlyFieldNames()
    {
        var employee = new Employee { FirstName = "Ad", LastName = "Soyad", Title = "Uzman", DepartmentId = 1,
            Profile = new EmployeeProfile { HealthNotes = "eski hassas bilgi" } };
        var before = EmployeeRecordChanges.Capture(employee);
        var token = EmployeeRecordChanges.Token(employee);
        employee.DepartmentId = 2; employee.ManagerId = 3;
        employee.Profile.MobilePhone = "05320000000"; employee.Profile.HealthNotes = "yeni hassas bilgi";
        EmployeeRecordChanges.Token(employee).Should().NotBe(token);
        EmployeeRecordChanges.Changed(before, employee).Should().BeEquivalentTo(
            "İş / Departman", "İş / Yönetici", "İletişim / Cep telefonu", "Özlük / Sağlık notu");
    }
}
