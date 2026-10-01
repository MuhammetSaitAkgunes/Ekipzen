using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ekipzen.Tests.Integration;

// Existing lifecycle tests submit the revision actually returned by GET.
// Conflict tests deliberately send their original token without this helper.
internal static class ReviewedPayrollRequests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    public static async Task<HttpResponseMessage> PutReviewedPayrollRowAsync(this HttpClient client, string path, object body)
        => await client.PutAsJsonAsync(path, await WithRevision(client, path, body), Options);
    public static async Task<HttpResponseMessage> ApproveReviewedPayrollRowAsync(this HttpClient client, string path, object? body = null)
        => await client.PostAsJsonAsync(path, await WithRevision(client, path[..^"/approve".Length], body), Options);
    public static async Task<HttpResponseMessage> PutReviewedComplianceAsync(this HttpClient client, string path, object body)
        => await client.PutAsJsonAsync(path, await WithComplianceRevision(client, path, body), Options);
    public static async Task<HttpResponseMessage> PatchReviewedComplianceAsync(this HttpClient client, string path, object body)
        => await client.PatchAsJsonAsync(path, await WithComplianceRevision(client, path, body), Options);
    private static async Task<JsonObject> WithComplianceRevision(HttpClient client, string path, object body)
    {
        var payload = JsonSerializer.SerializeToNode(body, Options)!.AsObject();
        var id = int.Parse(path.Split('/')[4]);
        var response = await client.GetAsync("/api/compliance/documents");
        var revision = response.IsSuccessStatusCode
            ? JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray().SingleOrDefault(row => row?["id"]?.GetValue<int>() == id)?["revision"]?.GetValue<string>() : null;
        payload["expectedRevision"] = revision ?? "unavailable";
        return payload;
    }
    public static async Task<HttpResponseMessage> InitializeReviewedSetupAsync(this HttpClient client, string path, object? body = null)
    {
        var payload = body is null ? new JsonObject() : JsonSerializer.SerializeToNode(body, Options)!.AsObject();
        var preview = await client.GetAsync(path + "/preview");
        payload["expectedPreviewToken"] = preview.IsSuccessStatusCode ? JsonNode.Parse(await preview.Content.ReadAsStringAsync())?["token"]?.GetValue<string>() : "unavailable";
        return await client.PostAsJsonAsync(path, payload, Options);
    }
    private static async Task<JsonObject> WithRevision(HttpClient client, string path, object? body)
    {
        var payload = body is null ? new JsonObject() : JsonSerializer.SerializeToNode(body, Options)!.AsObject();
        var marker = path.LastIndexOf("/rows/", StringComparison.Ordinal);
        var response = await client.GetAsync(path[..marker]);
        var id = int.Parse(path[(marker + 6)..]);
        var revision = response.IsSuccessStatusCode
            ? JsonNode.Parse(await response.Content.ReadAsStringAsync())?["rows"]?.AsArray()
                .SingleOrDefault(row => row?["id"]?.GetValue<int>() == id)?["revision"]?.GetValue<string>() : null;
        payload["expectedRevision"] = revision ?? "unavailable";
        return payload;
    }
}
