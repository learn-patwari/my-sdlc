using SprintForge.Domain.Common;

namespace SprintForge.Application.Sdlc;

/// <summary>
///   Abstraction over a project-tracking backend (Jira, Azure DevOps, GitHub Projects, etc.).
///   All write operations MUST flow through the write-gate — <see cref="ISdlcTool"/> provides the
///   raw capability; callers wrap it in an <see cref="Application.Audit.IAuditedOperation{T}"/>.
/// </summary>
public interface ISdlcTool
{
    string ToolId { get; }
    string DisplayName { get; }

    // --- READ (no approval gate required) ---

    Task<Result<IReadOnlyList<SdlcIssue>>> SearchIssuesAsync(SdlcSearchQuery query, CancellationToken ct = default);
    Task<Result<SdlcIssue>> GetIssueAsync(string issueKey, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SdlcIssue>>> GetSprintIssuesAsync(string sprintId, CancellationToken ct = default);
    Task<Result<string>> TestConnectionAsync(CancellationToken ct = default);

    // --- WRITE (must only be called from an approved IAuditedOperation) ---

    Task<Result<string>> CreateIssueAsync(SdlcIssueCreate request, CancellationToken ct = default);
    Task<Result<string>> UpdateIssueAsync(string issueKey, SdlcIssueUpdate update, CancellationToken ct = default);
    Task<Result<bool>> TransitionIssueAsync(string issueKey, string transitionId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> BulkCreateIssuesAsync(IReadOnlyList<SdlcIssueCreate> requests, CancellationToken ct = default);
}

public sealed record SdlcSearchQuery
{
    public IReadOnlyList<string> ProjectKeys { get; init; } = [];
    public string? Jql { get; init; }
    public string? Summary { get; init; }
    public string? Assignee { get; init; }
    public string? Status { get; init; }
    public int MaxResults { get; init; } = 50;
}

public sealed record SdlcIssue
{
    public required string Key { get; init; }
    public required string Summary { get; init; }
    public string? Description { get; init; }
    public required string Status { get; init; }
    public string? Assignee { get; init; }
    public string? Priority { get; init; }
    public string? IssueType { get; init; }
    public int? StoryPoints { get; init; }
    public string? EpicKey { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? RawJson { get; init; }
}

public sealed record SdlcIssueCreate
{
    public required string ProjectKey { get; init; }
    public required string IssueType { get; init; }
    public required string Summary { get; init; }
    public string? Description { get; init; }
    public string? Priority { get; init; }
    public string? Assignee { get; init; }
    public int? StoryPoints { get; init; }
    public string? EpicKey { get; init; }
    public string? ParentKey { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<string> Components { get; init; } = [];
}

public sealed record SdlcIssueUpdate
{
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? Priority { get; init; }
    public string? Assignee { get; init; }
    public int? StoryPoints { get; init; }
    public IReadOnlyList<string>? Labels { get; init; }
}
