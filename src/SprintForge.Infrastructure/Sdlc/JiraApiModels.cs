using System.Text.Json;
using System.Text.Json.Serialization;

namespace SprintForge.Infrastructure.Sdlc;

// --- Search response ---

internal sealed record JiraSearchResponse(
    [property: JsonPropertyName("issues")] List<JiraIssueDto> Issues,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("startAt")] int StartAt,
    [property: JsonPropertyName("maxResults")] int MaxResults);

// --- Issue ---

internal sealed record JiraIssueDto(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("fields")] JiraFieldsDto Fields);

internal sealed record JiraFieldsDto
{
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("description")] public JsonElement? Description { get; init; }
    [JsonPropertyName("status")] public JiraStatusDto? Status { get; init; }
    [JsonPropertyName("assignee")] public JiraUserDto? Assignee { get; init; }
    [JsonPropertyName("priority")] public JiraNamedDto? Priority { get; init; }
    [JsonPropertyName("issuetype")] public JiraNamedDto? IssueType { get; init; }
    [JsonPropertyName("labels")] public List<string>? Labels { get; init; }
    [JsonPropertyName("created")] public DateTimeOffset? Created { get; init; }
    [JsonPropertyName("updated")] public DateTimeOffset? Updated { get; init; }

    // Custom fields resolved at runtime from profile field mappings
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

internal sealed record JiraStatusDto([property: JsonPropertyName("name")] string Name);
internal sealed record JiraUserDto([property: JsonPropertyName("displayName")] string DisplayName,
                                    [property: JsonPropertyName("accountId")] string? AccountId);
internal sealed record JiraNamedDto([property: JsonPropertyName("name")] string Name);

// --- Create issue ---

internal sealed record JiraCreateRequest([property: JsonPropertyName("fields")] Dictionary<string, object?> Fields);

internal sealed record JiraCreateResponse(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("id")] string Id);

// --- Bulk create ---

internal sealed record JiraBulkCreateRequest(
    [property: JsonPropertyName("issueUpdates")] List<JiraCreateRequest> IssueUpdates);

internal sealed record JiraBulkCreateResponse(
    [property: JsonPropertyName("issues")] List<JiraCreateResponse>? Issues,
    [property: JsonPropertyName("errors")] List<JsonElement>? Errors);

// --- Transitions ---

internal sealed record JiraTransitionsResponse(
    [property: JsonPropertyName("transitions")] List<JiraTransitionDto> Transitions);

internal sealed record JiraTransitionDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);

internal sealed record JiraTransitionRequest(
    [property: JsonPropertyName("transition")] JiraTransitionIdDto Transition);

internal sealed record JiraTransitionIdDto([property: JsonPropertyName("id")] string Id);

// --- ADF helpers ---

internal static class AdfHelper
{
    /// <summary>Wraps plain text in the minimal Atlassian Document Format paragraph structure.</summary>
    internal static object ToAdf(string text) => new
    {
        type = "doc",
        version = 1,
        content = new[]
        {
            new
            {
                type = "paragraph",
                content = new[] { new { type = "text", text } }
            }
        }
    };

    /// <summary>Extracts plain text from an ADF JsonElement by collecting all "text" leaf nodes.</summary>
    internal static string ExtractText(JsonElement? element)
    {
        if (element is null) return string.Empty;
        var sb = new System.Text.StringBuilder();
        ExtractTextRecursive(element.Value, sb);
        return sb.ToString().Trim();
    }

    private static void ExtractTextRecursive(JsonElement el, System.Text.StringBuilder sb)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("type", out var type) && type.GetString() == "text"
                && el.TryGetProperty("text", out var text))
            {
                sb.Append(text.GetString());
                return;
            }
            if (el.TryGetProperty("content", out var content))
                ExtractTextRecursive(content, sb);
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in el.EnumerateArray())
            {
                ExtractTextRecursive(child, sb);
                sb.Append(' ');
            }
        }
    }
}
