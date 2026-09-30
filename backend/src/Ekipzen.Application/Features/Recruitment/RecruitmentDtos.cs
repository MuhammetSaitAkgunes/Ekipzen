using Ekipzen.Domain.Enums;

namespace Ekipzen.Application.Features.Recruitment;

/// <summary>Aday havuzu satırı — recruitment.js candidate-item şekli.</summary>
public sealed record CandidateListItemDto(
    int Id,
    string Name,
    string AppliedRole,
    string Status,
    int Score,
    string Initials,
    DateTime AppliedAtUtc);

public sealed record CandidateSkillDto(int Id, string Name);

public sealed record CandidateExperienceDto(
    int Id, string Title, string Company, string? Period, string? Description);

public sealed record InterviewNoteDto(
    int Id, string AuthorName, string NoteType, string Text, DateTime CreatedAtUtc);

public sealed record CandidateEvaluationDto(int Id, string Criterion, int Score, int MaxScore);

public sealed record CandidateHistoryDto(int Id, string Event, DateTime OccurredAtUtc);

public sealed record IdentityProvisioningDto(
    string Status,
    string? Email,
    int AttemptCount,
    string? LastError);

/// <summary>Aday detayı — recruitment.js sekmeleri (özgeçmiş/notlar/değerlendirme/geçmiş).</summary>
public sealed record CandidateDetailDto(
    int Id,
    string Name,
    string AppliedRole,
    int? PositionId,
    string? PositionTitle,
    string Status,
    int Score,
    string Initials,
    DateTime AppliedAtUtc,
    string? Location,
    int ExperienceYears,
    string? Summary,
    IReadOnlyList<CandidateSkillDto> Skills,
    IReadOnlyList<CandidateExperienceDto> Experiences,
    IReadOnlyList<InterviewNoteDto> Notes,
    IReadOnlyList<CandidateEvaluationDto> Evaluations,
    IReadOnlyList<CandidateHistoryDto> History,
    IdentityProvisioningDto? Provisioning, int Version);

public sealed record CandidateStateDto(
    string Name, string AppliedRole, int? PositionId, string? PositionTitle,
    int Score, string? Location, int ExperienceYears, string? Summary, string Status);

public sealed record CandidateCorrectionDto(
    int Id, int CandidateId, int FromVersion, int ToVersion,
    CandidateStateDto Before, CandidateStateDto After, string Reason,
    bool EvidenceReviewConfirmed, string ChangedByUserId, string ChangedByName, DateTime ChangedAtUtc);

public sealed record PositionDto(
    int Id, string Title, int? DepartmentId, string? Department, bool IsOpen, int OpenCount, int CandidateCount,
    int Version);

public sealed record PositionStateDto(string Title, int? DepartmentId, bool IsOpen, int OpenCount);
public sealed record PositionCorrectionDto(
    int Id, int PositionId, int FromVersion, int ToVersion,
    PositionStateDto Before, PositionStateDto After, string Reason,
    string ChangedByUserId, DateTime ChangedAtUtc);

/// <summary>Funnel verisi: pipeline aşaması başına aday sayısı.</summary>
public sealed record RecruitmentFunnelDto(
    int Total, int New, int Interview, int Offer, int Rejected, int Hired);

public static class RecruitmentMappings
{
    public static IdentityProvisioningDto ToDto(
        this Domain.Entities.Identity.IdentityProvisioningOperation operation) => new(
        operation.Status switch
        {
            IdentityProvisioningStatus.Completed => "completed",
            IdentityProvisioningStatus.Failed => "failed",
            _ => "pending",
        },
        operation.Email,
        operation.AttemptCount,
        operation.LastError);

    public static bool IsStatus(string? value)
        => value is "Yeni" or "Mülakat" or "Teklif" or "Red" or "İşe Alındı";

    /// <summary>Frontend pipeline etiketleri: Yeni | Mülakat | Teklif | Red | İşe Alındı.</summary>
    public static string ToDto(this CandidateStatus status) => status switch
    {
        CandidateStatus.New => "Yeni",
        CandidateStatus.Interview => "Mülakat",
        CandidateStatus.Offer => "Teklif",
        CandidateStatus.Rejected => "Red",
        CandidateStatus.Hired => "İşe Alındı",
        _ => status.ToString(),
    };

    public static CandidateStatus ParseStatus(string value) => value switch
    {
        "Yeni" => CandidateStatus.New,
        "Mülakat" => CandidateStatus.Interview,
        "Teklif" => CandidateStatus.Offer,
        "Red" => CandidateStatus.Rejected,
        "İşe Alındı" => CandidateStatus.Hired,
        _ => throw new ArgumentException($"Geçersiz aday durumu: {value} (Yeni|Mülakat|Teklif|Red)."),
    };

    public static string InitialsOf(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpper(new System.Globalization.CultureInfo("tr-TR")),
            _ => string.Concat(parts[0][..1], parts[^1][..1]).ToUpper(new System.Globalization.CultureInfo("tr-TR")),
        };
    }
}
