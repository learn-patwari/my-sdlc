using System.Text.Json;
using SprintForge.Application.Audit;
using SprintForge.Application.Sdlc;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Rollback;

/// <summary>
///   Audited write-gate operation that rolls back a Jira issue creation.
///   Strategy: transition the issue to a "Won't Do" / "Cancelled" resolution.
///
///   Must be executed via AuditedOperationRunner — never called directly.
/// </summary>
public sealed class RollbackJiraIssueOperation : IAuditedOperation<string>
{
    private readonly ISdlcTool _sdlc;
    private readonly string _jiraIssueKey;
    private readonly string _originalAuditId;
    private readonly string _reason;

    public RollbackJiraIssueOperation(
        ISdlcTool sdlc,
        string jiraIssueKey,
        string originalAuditId,
        string reason)
    {
        _sdlc = sdlc;
        _jiraIssueKey = jiraIssueKey;
        _originalAuditId = originalAuditId;
        _reason = reason;
    }

    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        Module = AuditModule.Rollback,
        Action = AuditAction.Rollback,
        StartedAt = DateTimeOffset.UtcNow,
        UserName = userName,
        MachineName = machineName,
        JiraIssueKey = _jiraIssueKey,
        InputsJson = JsonSerializer.Serialize(new
        {
            TargetJiraKey = _jiraIssueKey,
            OriginalAuditId = _originalAuditId,
            Reason = _reason
        })
    };

    public async Task<Result<string>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
    {
        // Attempt to transition to "Won't Do" — if the project's workflow has this transition.
        // Transition IDs are Jira-project-specific; "won't_do" is a common well-known value.
        var transitionResult = await _sdlc.TransitionIssueAsync(
            _jiraIssueKey, "won't_do", ct).ConfigureAwait(false);

        if (transitionResult.IsFailure)
        {
            // Fallback: try generic "cancel" transition ID
            var fallback = await _sdlc.TransitionIssueAsync(
                _jiraIssueKey, "cancel", ct).ConfigureAwait(false);

            if (fallback.IsFailure)
                return Result.Failure<string>(
                    $"Rollback failed: could not transition {_jiraIssueKey} — " +
                    $"Won't Do: {transitionResult.Error}; Cancel: {fallback.Error}. " +
                    "Manually cancel the issue in Jira.");
        }

        return Result.Success(_jiraIssueKey);
    }
}
