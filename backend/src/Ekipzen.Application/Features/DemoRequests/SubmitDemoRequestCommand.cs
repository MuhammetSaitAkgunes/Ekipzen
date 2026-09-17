using Ekipzen.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Ekipzen.Application.Features.DemoRequests;

/// <summary>Demo taleplerinin bildirileceği adres (DemoRequests:NotifyEmail).</summary>
public sealed record DemoRequestSettings(string NotifyEmail);

/// <summary>
/// Landing sayfasındaki demo talep formu. Kayıt oluşturmaz, hesap açmaz: talebi
/// platform sahibine e-postayla iletir; hesap operatör tarafından manuel açılır
/// (self-servis kayıt bilinçli olarak yok — bkz. AuthController xmldoc).
/// <c>Website</c> görünmez bir bal küpü alanıdır; doluysa talep sessizce yok sayılır.
/// </summary>
public sealed record SubmitDemoRequestCommand(
    string FullName,
    string Email,
    string Company,
    string CompanySize,
    string? Phone,
    string? Message,
    bool KvkkConsent,
    string? Website) : IRequest;

public sealed class SubmitDemoRequestCommandValidator : AbstractValidator<SubmitDemoRequestCommand>
{
    public static readonly string[] CompanySizes = ["1-10", "11-50", "51-200", "201-500", "500+"];

    public SubmitDemoRequestCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.").MaximumLength(256);
        RuleFor(x => x.Company).NotEmpty().WithMessage("Şirket adı zorunludur.").MaximumLength(150);
        RuleFor(x => x.CompanySize).Must(size => CompanySizes.Contains(size))
            .WithMessage("Geçerli bir çalışan aralığı seçin.");
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Message).MaximumLength(2000);
        RuleFor(x => x.KvkkConsent).Equal(true).WithMessage("KVKK aydınlatma metnini onaylamanız gerekir.");
    }
}

public sealed class SubmitDemoRequestCommandHandler(IEmailSender emailSender, DemoRequestSettings settings)
    : IRequestHandler<SubmitDemoRequestCommand>
{
    public async Task Handle(SubmitDemoRequestCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Website)) return;
        if (string.IsNullOrWhiteSpace(settings.NotifyEmail))
            throw new InvalidOperationException("DemoRequests:NotifyEmail yapılandırılmamış.");

        var subject = $"Demo talebi: {SingleLine(request.Company)}";
        var body =
            $"Yeni demo talebi\n\n" +
            $"Ad soyad: {request.FullName.Trim()}\n" +
            $"E-posta: {request.Email.Trim()}\n" +
            $"Şirket: {request.Company.Trim()}\n" +
            $"Çalışan sayısı: {request.CompanySize}\n" +
            $"Telefon: {(string.IsNullOrWhiteSpace(request.Phone) ? "-" : request.Phone.Trim())}\n" +
            $"KVKK onayı: Evet\n\n" +
            $"Mesaj:\n{(string.IsNullOrWhiteSpace(request.Message) ? "-" : request.Message.Trim())}\n";

        await emailSender.SendAsync(new EmailMessage(settings.NotifyEmail, subject, body), cancellationToken);
    }

    // Konu satırına kullanıcı girdisiyle başlık enjekte edilmesini engeller.
    private static string SingleLine(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
