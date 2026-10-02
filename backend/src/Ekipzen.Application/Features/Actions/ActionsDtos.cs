using Ekipzen.Domain.Entities.Actions;
using Ekipzen.Application.Features.Guidance;
using System.Globalization;
using Ekipzen.Domain.Enums;

namespace Ekipzen.Application.Features.Actions;

/// <summary>Aksiyon kartı — mockData.js globalActions şekli (actions.js renderActionCard).</summary>
public sealed record GlobalActionDto(
    int Id,
    string Title,
    string Source,
    string? SourceRoute,
    string Owner,
    string? Due,
    string Priority,
    string Status,
    string? Action, string? Revision = null);

/// <summary>Denetim izi satırı — mockData.js auditLogs şekli (+ ham UTC zaman).</summary>
public sealed record AuditLogDto(
    int Id,
    string Actor,
    string Action,
    string Module,
    string? Detail,
    string? EntityName,
    string? EntityId,
    DateTime CreatedAtUtc);

/// <summary>Sidebar açık-aksiyon rozeti (layout.js getOpenActionCount karşılığı).</summary>
public sealed record ActionBadgeDto(int OpenCount);

/// <summary>Birleşik arama sonucu — layout.js getGlobalSearchIndex öğe şekli.</summary>
public sealed record SearchResultDto(
    string Type,
    string Label,
    string Hint,
    string RouteKey,
    int EntityId);

public static class ActionsMappings
{
    public static bool IsPriority(string? value)
        => value is "high" or "medium" or "low";

    public static bool IsStatus(string? value)
        => value is "open" or "week" or "done";

    /// <summary>Frontend öncelik etiketi: high | medium | low.</summary>
    public static string ToDto(this ActionPriority priority) => priority switch
    {
        ActionPriority.High => "high",
        ActionPriority.Medium => "medium",
        _ => "low",
    };

    public static ActionPriority ParsePriority(string value) => value switch
    {
        "high" => ActionPriority.High,
        "medium" => ActionPriority.Medium,
        "low" => ActionPriority.Low,
        _ => throw new ArgumentException($"Geçersiz öncelik: {value} (high|medium|low)."),
    };

    /// <summary>Frontend durum etiketi: open | week | done.</summary>
    public static string ToDto(this ActionStatus status) => status switch
    {
        ActionStatus.Open => "open",
        ActionStatus.Week => "week",
        _ => "done",
    };

    public static ActionStatus ParseStatus(string value) => value switch
    {
        "open" => ActionStatus.Open,
        "week" => ActionStatus.Week,
        "done" => ActionStatus.Done,
        _ => throw new ArgumentException($"Geçersiz aksiyon durumu: {value} (open|week|done)."),
    };

    public static string Revision(GlobalAction action) => PreparationOperationEvidence.RecordToken(
        action.Id.ToString(CultureInfo.InvariantCulture), action.Title, action.Source, action.SourceRoute,
        action.Owner, action.Due, action.Priority.ToString(), action.Status.ToString(), action.RecommendedAction,
        action.CreatedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture), action.UpdatedAtUtc?.Ticks.ToString(CultureInfo.InvariantCulture));

    public static GlobalActionDto ToDto(this GlobalAction action) => new(
        action.Id,
        action.Title,
        action.Source,
        action.SourceRoute,
        action.Owner,
        action.Due,
        action.Priority.ToDto(),
        action.Status.ToDto(),
        action.RecommendedAction, Revision(action));
}
