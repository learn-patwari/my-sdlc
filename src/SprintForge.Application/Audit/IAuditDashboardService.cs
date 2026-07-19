using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;

namespace SprintForge.Application.Audit;

/// <summary>
///   Provides aggregated read views over the audit log for the Audit Center dashboard.
///   All methods are read-only; no audit record is created by this service.
///
///   Backed by the SQLite index (AuditDbContext) for fast queries.
///   The JSONL file remains the source of truth; the SQLite DB is a rebuildable index.
/// </summary>
public interface IAuditDashboardService
{
    /// <summary>Timeline view — all recent audit events, newest first.</summary>
    Task<Result<IReadOnlyList<AuditRecord>>> GetTimelineAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 50,
        int offset = 0,
        CancellationToken ct = default);

    /// <summary>Sessions view — events grouped by CorrelationId, newest session first.</summary>
    Task<Result<IReadOnlyList<AuditSession>>> GetSessionsAsync(
        int pageSize = 25,
        int offset = 0,
        CancellationToken ct = default);

    /// <summary>AI Prompts view — all AiPrompt events with prompt/response content.</summary>
    Task<Result<IReadOnlyList<AuditRecord>>> GetAiPromptsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Jira Changes view — all Jira write events (create, update, transition, bulk).</summary>
    Task<Result<IReadOnlyList<AuditRecord>>> GetJiraChangesAsync(
        string? jiraIssueKey = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Approvals view — all approval decisions with their linked audit records.</summary>
    Task<Result<IReadOnlyList<AuditRecord>>> GetApprovalHistoryAsync(
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Rollback view — all rollback operations.</summary>
    Task<Result<IReadOnlyList<AuditRecord>>> GetRollbackHistoryAsync(
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Returns all audit events that share the specified CorrelationId, in chronological order.</summary>
    Task<Result<IReadOnlyList<AuditRecord>>> GetCorrelationChainAsync(
        string correlationId,
        CancellationToken ct = default);
}

/// <summary>A session in the Audit Center sessions view — all events sharing a CorrelationId.</summary>
public sealed record AuditSession
{
    public required string CorrelationId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public required string InitiatingUser { get; init; }
    public required int EventCount { get; init; }
    public required AuditModule PrimaryModule { get; init; }
    public required AuditStatus OverallStatus { get; init; }

    /// <summary>Summary of the session's operations for the sessions list view.</summary>
    public required string Summary { get; init; }
}
