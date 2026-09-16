namespace Ekipzen.Application.Features.Employees.Files;

/// <summary>
/// İzin verilen özlük evrakı uzantıları ve MIME karşılıkları — tek kaynak.
/// MIME tipi istemciden ALINMAZ: tarayıcının gönderdiği Content-Type kullanıcı
/// girdisidir ve indirme yanıtında geri verilir. Sunucu, saklanan tipi yalnızca
/// doğrulanmış uzantıdan türetir; böylece beyan ile içerik türü ayrışamaz.
/// </summary>
public static class EmployeeDocumentTypes
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    public static IReadOnlyCollection<string> AllowedExtensions => ByExtension.Keys;

    public static bool IsAllowed(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) && ByExtension.ContainsKey(Path.GetExtension(fileName));

    /// <summary>Doğrulanmış uzantının MIME karşılığı; bilinmiyorsa nötr ikili tip.</summary>
    public static string ResolveContentType(string? fileName) =>
        fileName is not null && ByExtension.TryGetValue(Path.GetExtension(fileName), out var contentType)
            ? contentType
            : "application/octet-stream";
}
