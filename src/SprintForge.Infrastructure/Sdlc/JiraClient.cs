using SprintForge.Application.Sdlc;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Sdlc;

/// <summary>
///   Jira REST API v3 client implemented via <see cref="HttpClient"/> + Polly.
///   See docs/08-integrations.md §12 for the full design.
/// </summary>
public sealed class JiraClient : ISdlcTool
{
    public string ToolId => "jira";
    public string DisplayName => "Jira REST v3";

    public Task<Result<IReadOnlyList<SdlcIssue>>> SearchIssuesAsync(SdlcSearchQuery query, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/08-integrations.md §12 — Jira adapter implementation is Phase 2.");

    public Task<Result<SdlcIssue>> GetIssueAsync(string issueKey, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/08-integrations.md §12 — Jira adapter implementation is Phase 2.");

    public Task<Result<IReadOnlyList<SdlcIssue>>> GetSprintIssuesAsync(string sprintId, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/08-integrations.md §12 — Jira adapter implementation is Phase 2.");

    public Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
        => throw new NotImplementedException("See docs/08-integrations.md §12 — Jira adapter implementation is Phase 2.");

    public Task<Result<string>> CreateIssueAsync(SdlcIssueCreate request, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/08-integrations.md §12 — Jira adapter implementation is Phase 2.");

    public Task<Result<string>> UpdateIssueAsync(string issueKey, SdlcIssueUpdate update, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/08-integrations.md §12 — Jira adapter implementation is Phase 2.");

    public Task<Result<bool>> TransitionIssueAsync(string issueKey, string transitionId, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/08-integrations.md §12 — Jira adapter implementation is Phase 2.");

    public Task<Result<IReadOnlyList<string>>> BulkCreateIssuesAsync(IReadOnlyList<SdlcIssueCreate> requests, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/08-integrations.md §12 — Jira adapter implementation is Phase 2.");
}
