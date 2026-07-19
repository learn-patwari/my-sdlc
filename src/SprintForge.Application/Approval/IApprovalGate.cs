using SprintForge.Domain.Approvals;

namespace SprintForge.Application.Approval;

/// <summary>
///   Queues a write operation into the Approvals Center and waits (non-blocking, via polling/signal)
///   until the user decides. Never shows a modal — the Approvals Center screen is the only UI entry point.
/// </summary>
public interface IApprovalGate
{
    /// <summary>
    ///   Enqueues the request in the Approvals Center and returns immediately with the queued item.
    ///   Callers then await <see cref="WaitForDecisionAsync"/>.
    /// </summary>
    Task<ApprovalRequest> QueueAsync(ApprovalRequest request, CancellationToken ct = default);

    /// <summary>
    ///   Awaits a user decision on the given approval item.
    ///   Returns when the user has acted (Approved, Rejected, etc.) or <paramref name="ct"/> is cancelled.
    /// </summary>
    Task<ApprovalDecision> WaitForDecisionAsync(string approvalId, CancellationToken ct = default);

    /// <summary>Records the user's decision and transitions the item's status.</summary>
    Task ApplyDecisionAsync(ApprovalDecision decision, CancellationToken ct = default);

    /// <summary>Returns all pending (unresolved) items, ordered by QueuedAt ascending.</summary>
    Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>Returns paginated history of all decided items.</summary>
    Task<IReadOnlyList<ApprovalRequest>> GetHistoryAsync(int pageSize = 50, int offset = 0, CancellationToken ct = default);

    /// <summary>Cancels all pending items that belong to the same batch.</summary>
    Task CancelBatchAsync(string batchId, CancellationToken ct = default);
}
