using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;

namespace SprintForge.Application.Rollback;

/// <summary>
///   Rolls back previously audited write operations.
///   Every rollback is itself a new audited write (module = Rollback, action = Rollback).
///
///   Rollback strategies by operation type:
///   - Jira create → transition issue to "Won't Do" (or delete if the project allows it)
///   - Jira update → restore previous field values from the audit record's InputsJson
///   - Document version → create a new version that reverts to the previous version's content
///   - Config profile save → restore previous profile snapshot from the audit record
///
///   INVARIANT: The rollback itself goes through the write gate — audit-first, then execute.
///   If the rollback operation fails, the original write stands.
/// </summary>
public interface IRollbackService
{
    /// <summary>
    ///   Returns the list of audited write operations within the correlation that can be rolled back.
    ///   Not all operations are rollbackable (e.g., already-rolled-back, or Jira transitions).
    /// </summary>
    Task<Result<IReadOnlyList<RollbackCandidate>>> GetCandidatesAsync(string correlationId, CancellationToken ct = default);

    /// <summary>
    ///   Executes a rollback for the specified audit record.
    ///   Creates a new audit trail for the rollback operation.
    ///   Returns the rollback audit ID.
    /// </summary>
    Task<Result<string>> RollbackAsync(
        string targetAuditId,
        string reason,
        string userName,
        CancellationToken ct = default);
}

/// <summary>An audited operation that can potentially be rolled back.</summary>
public sealed record RollbackCandidate
{
    public required string AuditId { get; init; }
    public required string CorrelationId { get; init; }
    public required AuditModule Module { get; init; }
    public required AuditAction Action { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset PerformedAt { get; init; }
    public required string PerformedByUser { get; init; }

    /// <summary>Whether this operation can be automatically reversed.</summary>
    public required bool IsRollbackable { get; init; }

    /// <summary>If not rollbackable, explains why (e.g., "Jira issue was already updated by another user").</summary>
    public string? NonRollbackableReason { get; init; }

    /// <summary>What the rollback will do (user-facing description).</summary>
    public string? RollbackDescription { get; init; }
}
