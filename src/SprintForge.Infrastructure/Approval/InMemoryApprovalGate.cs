using System.Collections.Concurrent;
using SprintForge.Application.Approval;
using SprintForge.Domain.Approvals;

namespace SprintForge.Infrastructure.Approval;

/// <summary>
///   In-process approval queue. The WPF Approvals Center screen binds to this singleton to render
///   pending items. A production implementation would use a persistent SQLite queue so items survive
///   restarts — this in-memory version satisfies the scaffold.
/// </summary>
public sealed class InMemoryApprovalGate : IApprovalGate
{
    private readonly ConcurrentDictionary<string, ApprovalRequest> _queue = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ApprovalDecision>> _waiters = new();

    public Task<ApprovalRequest> QueueAsync(ApprovalRequest request, CancellationToken ct = default)
    {
        _queue[request.ApprovalId] = request;
        _waiters.TryAdd(request.ApprovalId, new TaskCompletionSource<ApprovalDecision>(
            TaskCreationOptions.RunContinuationsAsynchronously));
        return Task.FromResult(request);
    }

    public async Task<ApprovalDecision> WaitForDecisionAsync(string approvalId, CancellationToken ct = default)
    {
        if (!_waiters.TryGetValue(approvalId, out var tcs))
            throw new InvalidOperationException($"Approval item {approvalId} not found.");

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        return await tcs.Task.ConfigureAwait(false);
    }

    public Task ApplyDecisionAsync(ApprovalDecision decision, CancellationToken ct = default)
    {
        if (_queue.TryGetValue(decision.ApprovalId, out var existing))
            _queue[decision.ApprovalId] = existing with { Status = decision.Decision, DecidedAt = decision.DecidedAt };

        if (_waiters.TryRemove(decision.ApprovalId, out var tcs))
            tcs.TrySetResult(decision);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(CancellationToken ct = default)
    {
        var pending = _queue.Values
            .Where(r => r.Status == ApprovalStatus.Pending)
            .OrderBy(r => r.QueuedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<ApprovalRequest>>(pending);
    }

    public Task<IReadOnlyList<ApprovalRequest>> GetHistoryAsync(int pageSize = 50, int offset = 0, CancellationToken ct = default)
    {
        var history = _queue.Values
            .Where(r => r.Status != ApprovalStatus.Pending)
            .OrderByDescending(r => r.DecidedAt)
            .Skip(offset).Take(pageSize)
            .ToList();
        return Task.FromResult<IReadOnlyList<ApprovalRequest>>(history);
    }

    public Task CancelBatchAsync(string batchId, CancellationToken ct = default)
    {
        foreach (var item in _queue.Values.Where(r => r.BatchId == batchId && r.Status == ApprovalStatus.Pending))
        {
            _queue[item.ApprovalId] = item with { Status = ApprovalStatus.Cancelled };
            if (_waiters.TryRemove(item.ApprovalId, out var tcs))
            {
                var decision = new ApprovalDecision
                {
                    ApprovalId = item.ApprovalId,
                    Decision = ApprovalStatus.Cancelled,
                    DecidedAt = DateTimeOffset.UtcNow,
                    DecidedByUser = "System"
                };
                tcs.TrySetResult(decision);
            }
        }
        return Task.CompletedTask;
    }
}
