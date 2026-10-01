using FluentValidation;
using System.Text.RegularExpressions;

namespace Ekipzen.Application.Features.Employees.Upsert;

/// <summary>
/// Personel kartı oluşturma/güncelleme gövdesi — personnel.js form sekmeleriyle birebir.
/// Enum alanları frontend değerlerini alır (status: active|passive, çalışma şekli Türkçe).
/// </summary>
public sealed record EmployeeUpsertModel(
    string FirstName,
    string LastName,
    string Title,
    int DepartmentId,
    DateOnly HireDate,
    string? NationalId = null,
    int? ManagerId = null,
    string Status = "active",
    string ContractType = "indefinite",
    EmployeeProfileUpsertModel? Profile = null,
    int? GuidanceContentRevisionId = null);

public sealed record EmployeeProfileUpsertModel(
    // Kimlik
    DateOnly? BirthDate = null,
    string? Gender = null,
    string? MaritalStatus = null,
    string? BloodType = null,
    // İletişim
    string? MobilePhone = null,
    string? PersonalEmail = null,
    string? HomeAddress = null,
    string? EmergencyContactName = null,
    string? EmergencyContactRelation = null,
    string? EmergencyContactPhone = null,
    // İş & kurumsal
    string? EmploymentType = null,
    string? RehireEligibility = null,
    string? ExitCode = null,
    // Mali
    string? Iban = null,
    string? BankName = null,
    string? SalaryType = null,
    string? PensionStatus = null,
    string? MealCard = null,
    // Özlük & sağlık
    string? TshirtSize = null,
    string? PantsSize = null,
    string? CoatSize = null,
    string? ShoeSize = null,
    bool CanWorkAtHeight = false,
    bool CanWorkNightShift = false,
    bool CanLiftHeavyLoads = false,
    string? HealthNotes = null);

public sealed class EmployeeUpsertModelValidator : AbstractValidator<EmployeeUpsertModel>
{
    private static readonly Regex NationalIdRegex = new(@"^\d{11}$", RegexOptions.Compiled);
    private static readonly Regex IbanRegex = new(@"^TR\d{24}$", RegexOptions.Compiled);

    public EmployeeUpsertModelValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.HireDate).NotEmpty();

        RuleFor(x => x.NationalId)
            .Must(id => id is null || NationalIdRegex.IsMatch(id))
            .WithMessage("TC Kimlik No 11 haneli rakam olmalı.");

        RuleFor(x => x.Status)
            .Must(s => s is "active" or "passive")
            .WithMessage("Durum 'active' veya 'passive' olmalı.");

        RuleFor(x => x.ContractType)
            .Must(value => value is "indefinite" or "fixed-term")
            .WithMessage("Sözleşme türü 'indefinite' veya 'fixed-term' olmalı.");

        When(x => x.Profile is not null, () =>
        {
            RuleFor(x => x.Profile!.PersonalEmail)
                .EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Profile!.PersonalEmail));

            RuleFor(x => x.Profile!.Iban)
                .Must(iban => iban is null || IbanRegex.IsMatch(iban.Replace(" ", "")))
                .WithMessage("IBAN 'TR' ile başlayan 26 karakter olmalı.");

            RuleFor(x => x.Profile!.EmploymentType)
                .Must(t => t is null or "" or "Tam Zamanlı" or "Yarı Zamanlı" or "Uzaktan")
                .WithMessage("Çalışma şekli: Tam Zamanlı | Yarı Zamanlı | Uzaktan.");

            RuleFor(x => x.Profile!.MobilePhone).MaximumLength(32);
            RuleFor(x => x.Profile!.HealthNotes).MaximumLength(2000);
        });
    }
}
