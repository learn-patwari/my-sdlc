using Microsoft.Extensions.Logging;
using SprintForge.Application.Audit;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Audit;

/// <summary>
///   THE write gate. Every write operation runs through here.
///
///   Contract:
///   1. Pre-record the audit entry (Started). If this throws, the operation is BLOCKED — we never proceed.
///   2. Execute the operation.
///   3. Seal the record (Completed / Failed / Cancelled).
///
///   If the audit sink throws at step 1, <see cref="AuditUnavailableException"/> propagates to the caller.
///   Operations MUST NOT be invoked any other way (enforced by DI — no unwrapped write service is registered).
/// </summary>
public sealed class AuditedOperationRunner
{
    private readonly IAuditService _audit;
    private readonly ILogger<AuditedOperationRunner> _logger;

    public AuditedOperationRunner(IAuditService audit, ILogger<AuditedOperationRunner> logger)
    {
        _audit = audit;
        _logger = logger;
    }

    public async Task<Result<TResult>> RunAsync<TResult>(
        IAuditedOperation<TResult> operation,
        string correlationId,
        string userName,
        string machineName,
        CancellationToken ct = default)
    {
        var ctx = operation.BuildAuditContext(correlationId, userName, machineName);

        // STEP 1 — pre-record. Throws AuditUnavailableException if sink is down; operation is BLOCKED.
        string auditId;
        try
        {
            auditId = await _audit.RecordStartAsync(ctx, ct).ConfigureAwait(false);
        }
        catch (AuditUnavailableException ex)
        {
            _logger.LogCritical(ex,
                "BLOCKED: Operation {Action}/{Module} cannot proceed — audit sink unavailable.",
                ctx.Action, ctx.Module);
            throw;
        }

        // STEP 2 — execute
        Result<TResult> result;
        try
        {
            result = await operation.ExecuteAsync(ctx, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await TrySealAsync(() => _audit.RecordCancelledAsync(auditId, CancellationToken.None)).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await TrySealAsync(() => _audit.RecordFailedAsync(auditId, ex, CancellationToken.None)).ConfigureAwait(false);
            return Result.Failure<TResult>($"Operation failed: {ex.Message}");
        }

        // STEP 3 — seal
        if (result.IsSuccess)
            await TrySealAsync(() => _audit.RecordCompletedAsync(auditId, ct: CancellationToken.None)).ConfigureAwait(false);
        else
            await TrySealAsync(() =>
            {
                var fakeEx = new InvalidOperationException(result.Error);
                return _audit.RecordFailedAsync(auditId, fakeEx, CancellationToken.None);
            }).ConfigureAwait(false);

        return result;
    }

    private async Task TrySealAsync(Func<Task> seal)
    {
        try { await seal().ConfigureAwait(false); }
        catch (Exception ex)
        {
            // Sealing failure is logged but does NOT re-block the caller — the write already happened.
            _logger.LogError(ex, "Failed to seal audit record. Manual reconciliation required.");
        }
    }
}
