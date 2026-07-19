using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Configuration;
using SprintForge.Application.Sdlc;
using SprintForge.Application.Security;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Sdlc;

/// <summary>
///   Jira REST API v3 client.
///   Credentials are resolved per-request from the active profile's DPAPI-backed secret store.
///   All write methods here execute the raw HTTP call; callers MUST wrap them in
///   an <see cref="Application.Audit.IAuditedOperation{T}"/> via JiraWriteOperations.
/// </summary>
public sealed class JiraClient : ISdlcTool
{
    private readonly HttpClient _http;
    private readonly IProfileStore _profiles;
    private readonly ISecretStore _secrets;
    private readonly ILogger<JiraClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string ToolId => "jira";
    public string DisplayName => "Jira REST v3";

    public JiraClient(HttpClient http, IProfileStore profiles, ISecretStore secrets, ILogger<JiraClient> logger)
    {
        _http = http;
        _profiles = profiles;
        _secrets = secrets;
        _logger = logger;
    }

    // ── Read operations ──────────────────────────────────────────────────────

    public async Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
    {
        var init = InitRequest();
        if (init.IsFailure) return Result.Failure<string>(init.Error!);

        try
        {
            var resp = await _http.GetAsync("rest/api/3/myself", ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Result.Failure<string>($"Jira returned {(int)resp.StatusCode}: {resp.ReasonPhrase}");

            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var display = doc.RootElement.TryGetProperty("displayName", out var dn) ? dn.GetString() : "unknown";
            return Result.Success($"Connected as {display}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jira TestConnection failed.");
            return Result.Failure<string>(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<SdlcIssue>>> SearchIssuesAsync(SdlcSearchQuery query, CancellationToken ct = default)
    {
        var init = InitRequest();
        if (init.IsFailure) return Result.Failure<IReadOnlyList<SdlcIssue>>(init.Error!);

        try
        {
            var jql = BuildJql(query);
            var fields = ResolveFields();
            var url = $"rest/api/3/search?jql={Uri.EscapeDataString(jql)}&fields={fields}&maxResults={query.MaxResults}";
            var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Result.Failure<IReadOnlyList<SdlcIssue>>($"Jira search failed: {(int)resp.StatusCode}");

            var data = await resp.Content.ReadFromJsonAsync<JiraSearchResponse>(JsonOpts, ct).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<SdlcIssue>>(data?.Issues.Select(MapIssue).ToList() ?? []);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jira SearchIssues failed.");
            return Result.Failure<IReadOnlyList<SdlcIssue>>(ex.Message);
        }
    }

    public async Task<Result<SdlcIssue>> GetIssueAsync(string issueKey, CancellationToken ct = default)
    {
        var init = InitRequest();
        if (init.IsFailure) return Result.Failure<SdlcIssue>(init.Error!);

        try
        {
            var resp = await _http.GetAsync($"rest/api/3/issue/{issueKey}?fields={ResolveFields()}", ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Result.Failure<SdlcIssue>($"Issue {issueKey} not found: {(int)resp.StatusCode}");

            var dto = await resp.Content.ReadFromJsonAsync<JiraIssueDto>(JsonOpts, ct).ConfigureAwait(false);
            return dto is null
                ? Result.Failure<SdlcIssue>("Empty response from Jira.")
                : Result.Success(MapIssue(dto));
        }
        catch (Exception ex)
        {
            return Result.Failure<SdlcIssue>(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<SdlcIssue>>> GetSprintIssuesAsync(string sprintId, CancellationToken ct = default)
    {
        var init = InitRequest();
        if (init.IsFailure) return Result.Failure<IReadOnlyList<SdlcIssue>>(init.Error!);

        try
        {
            var url = $"rest/agile/1.0/sprint/{sprintId}/issue?fields={ResolveFields()}";
            var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return Result.Failure<IReadOnlyList<SdlcIssue>>($"Sprint {sprintId} issues failed: {(int)resp.StatusCode}");

            var data = await resp.Content.ReadFromJsonAsync<JiraSearchResponse>(JsonOpts, ct).ConfigureAwait(false);
            return Result.Success<IReadOnlyList<SdlcIssue>>(data?.Issues.Select(MapIssue).ToList() ?? []);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<SdlcIssue>>(ex.Message);
        }
    }

    // ── Write operations (raw HTTP — always called from audited operations) ──

    public async Task<Result<string>> CreateIssueAsync(SdlcIssueCreate request, CancellationToken ct = default)
    {
        var init = InitRequest();
        if (init.IsFailure) return Result.Failure<string>(init.Error!);

        try
        {
            var body = BuildCreateBody(request);
            var resp = await _http.PostAsJsonAsync("rest/api/3/issue", body, JsonOpts, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Result.Failure<string>($"Jira create failed {(int)resp.StatusCode}: {err}");
            }
            var created = await resp.Content.ReadFromJsonAsync<JiraCreateResponse>(JsonOpts, ct).ConfigureAwait(false);
            return created?.Key is not null
                ? Result.Success(created.Key)
                : Result.Failure<string>("No key in Jira create response.");
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }

    public async Task<Result<string>> UpdateIssueAsync(string issueKey, SdlcIssueUpdate update, CancellationToken ct = default)
    {
        var init = InitRequest();
        if (init.IsFailure) return Result.Failure<string>(init.Error!);

        try
        {
            var fields = BuildUpdateFields(update);
            var resp = await _http.PutAsJsonAsync($"rest/api/3/issue/{issueKey}", new { fields }, JsonOpts, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode
                ? Result.Success(issueKey)
                : Result.Failure<string>($"Update {issueKey} failed: {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }

    public async Task<Result<bool>> TransitionIssueAsync(string issueKey, string transitionId, CancellationToken ct = default)
    {
        var init = InitRequest();
        if (init.IsFailure) return Result.Failure<bool>(init.Error!);

        try
        {
            var body = new JiraTransitionRequest(new JiraTransitionIdDto(transitionId));
            var resp = await _http.PostAsJsonAsync($"rest/api/3/issue/{issueKey}/transitions", body, JsonOpts, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode
                ? Result.Success(true)
                : Result.Failure<bool>($"Transition {issueKey} failed: {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> BulkCreateIssuesAsync(IReadOnlyList<SdlcIssueCreate> requests, CancellationToken ct = default)
    {
        var init = InitRequest();
        if (init.IsFailure) return Result.Failure<IReadOnlyList<string>>(init.Error!);

        try
        {
            var issueUpdates = requests.Select(r => new JiraCreateRequest(BuildCreateBody(r).Fields)).ToList();
            var bulkBody = new JiraBulkCreateRequest(issueUpdates);
            var resp = await _http.PostAsJsonAsync("rest/api/3/issue/bulk", bulkBody, JsonOpts, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Result.Failure<IReadOnlyList<string>>($"Bulk create failed {(int)resp.StatusCode}: {err}");
            }
            var bulk = await resp.Content.ReadFromJsonAsync<JiraBulkCreateResponse>(JsonOpts, ct).ConfigureAwait(false);
            var keys = bulk?.Issues?.Select(i => i.Key).ToList() ?? [];
            return Result.Success<IReadOnlyList<string>>(keys);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<string>>(ex.Message);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>Configures the HttpClient's BaseAddress and Authorization header from the active profile.</summary>
    private Result<bool> InitRequest()
    {
        var profile = _profiles.ActiveProfile;
        if (profile is null)
            return Result.Failure<bool>("No active profile. Load a profile before making Jira requests.");

        var tokenResult = _secrets.Retrieve(profile.Jira.ApiTokenRef);
        if (tokenResult.IsFailure)
            return Result.Failure<bool>($"Cannot retrieve Jira API token: {tokenResult.Error}");

        _http.BaseAddress = new Uri(profile.Jira.ServerUrl.TrimEnd('/') + '/');
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{profile.Jira.Username}:{tokenResult.Value}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return Result.Success(true);
    }

    private static string BuildJql(SdlcSearchQuery query)
    {
        if (query.Jql is not null) return query.Jql;
        var parts = new List<string>();
        if (query.ProjectKeys.Count > 0)
            parts.Add($"project in ({string.Join(",", query.ProjectKeys)})");
        if (query.Status is not null)
            parts.Add($"status = \"{query.Status}\"");
        if (query.Assignee is not null)
            parts.Add($"assignee = \"{query.Assignee}\"");
        if (query.Summary is not null)
            parts.Add($"summary ~ \"{query.Summary}\"");
        return parts.Count > 0 ? string.Join(" AND ", parts) : "ORDER BY created DESC";
    }

    private string ResolveFields()
    {
        var storyPointsField = _profiles.ActiveProfile?.Jira.FieldMappings.StoryPoints ?? "story_points";
        var epicField = _profiles.ActiveProfile?.Jira.FieldMappings.EpicLink ?? "customfield_10014";
        return $"summary,description,status,assignee,priority,issuetype,labels,created,updated,{storyPointsField},{epicField}";
    }

    private JiraCreateRequest BuildCreateBody(SdlcIssueCreate r)
    {
        var profile = _profiles.ActiveProfile;
        var spField = profile?.Jira.FieldMappings.StoryPoints ?? "story_points";
        var epicField = profile?.Jira.FieldMappings.EpicLink ?? "customfield_10014";

        var fields = new Dictionary<string, object?>
        {
            ["project"] = new { key = r.ProjectKey },
            ["issuetype"] = new { name = r.IssueType },
            ["summary"] = r.Summary
        };

        if (r.Description is not null)
            fields["description"] = AdfHelper.ToAdf(r.Description);
        if (r.Priority is not null)
            fields["priority"] = new { name = r.Priority };
        if (r.Assignee is not null)
            fields["assignee"] = new { accountId = r.Assignee };
        if (r.StoryPoints.HasValue)
            fields[spField] = r.StoryPoints.Value;
        if (r.EpicKey is not null)
            fields[epicField] = r.EpicKey;
        if (r.ParentKey is not null)
            fields["parent"] = new { key = r.ParentKey };
        if (r.Labels.Count > 0)
            fields["labels"] = r.Labels;
        if (r.Components.Count > 0)
            fields["components"] = r.Components.Select(c => new { name = c });

        return new JiraCreateRequest(fields);
    }

    private Dictionary<string, object?> BuildUpdateFields(SdlcIssueUpdate u)
    {
        var profile = _profiles.ActiveProfile;
        var spField = profile?.Jira.FieldMappings.StoryPoints ?? "story_points";
        var fields = new Dictionary<string, object?>();
        if (u.Summary is not null) fields["summary"] = u.Summary;
        if (u.Description is not null) fields["description"] = AdfHelper.ToAdf(u.Description);
        if (u.Priority is not null) fields["priority"] = new { name = u.Priority };
        if (u.Assignee is not null) fields["assignee"] = new { accountId = u.Assignee };
        if (u.StoryPoints.HasValue) fields[spField] = u.StoryPoints.Value;
        if (u.Labels is not null) fields["labels"] = u.Labels;
        return fields;
    }

    private SdlcIssue MapIssue(JiraIssueDto dto)
    {
        var profile = _profiles.ActiveProfile;
        var spField = profile?.Jira.FieldMappings.StoryPoints ?? "story_points";
        var epicField = profile?.Jira.FieldMappings.EpicLink ?? "customfield_10014";
        int? storyPoints = null;
        string? epicKey = null;
        if (dto.Fields.Extra is not null)
        {
            if (dto.Fields.Extra.TryGetValue(spField, out var sp) && sp.ValueKind == JsonValueKind.Number)
                storyPoints = sp.GetInt32();
            if (dto.Fields.Extra.TryGetValue(epicField, out var ek) && ek.ValueKind == JsonValueKind.String)
                epicKey = ek.GetString();
        }
        return new SdlcIssue
        {
            Key = dto.Key,
            Summary = dto.Fields.Summary ?? string.Empty,
            Description = AdfHelper.ExtractText(dto.Fields.Description),
            Status = dto.Fields.Status?.Name ?? "Unknown",
            Assignee = dto.Fields.Assignee?.DisplayName,
            Priority = dto.Fields.Priority?.Name,
            IssueType = dto.Fields.IssueType?.Name,
            StoryPoints = storyPoints,
            EpicKey = epicKey,
            Labels = dto.Fields.Labels ?? [],
            CreatedAt = dto.Fields.Created,
            UpdatedAt = dto.Fields.Updated
        };
    }
}
