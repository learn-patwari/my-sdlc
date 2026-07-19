using SprintForge.Application.Audit;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Audit;

/// <summary>
///   Implements the Audit Center dashboard read views by querying IAuditService.SearchAsync.
///   Read-only — never writes audit records.
/// </summary>
public sealed class AuditDashboardService : IAuditDashboardService
{
    private readonly IAuditService _audit;

    public AuditDashboardService(IAuditService audit)
    {
        _audit = audit;
    }

    public Task<Result<IReadOnlyList<AuditRecord>>> GetTimelineAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        return SearchAsync(new AuditSearchQuery
        {
            From = from,
            To = to,
            PageSize = Math.Min(pageSize, 200),
            PageOffset = offset
        }, ct);
    }

    public async Task<Result<IReadOnlyList<AuditSession>>> GetSessionsAsync(
        int pageSize = 25,
        int offset = 0,
        CancellationToken ct = default)
    {
        // Load enough records to group them into sessions
        var records = await _audit.SearchAsync(new AuditSearchQuery
        {
            PageSize = Math.Min(pageSize * 20, 1000),
            PageOffset = 0
        }, ct).ConfigureAwait(false);

        var sessions = records
            .GroupBy(r => r.CorrelationId)
            .Select(g =>
            {
                var sorted = g.OrderBy(r => r.UtcTimestamp).ToList();
                var overallStatus = DetermineOverallStatus(sorted);
                var primaryModule = sorted
                    .GroupBy(r => r.Module)
                    .OrderByDescending(m => m.Count())
                    .First().Key;

                return new AuditSession
                {
                    CorrelationId = g.Key,
                    StartedAt = sorted.First().UtcTimestamp,
                    EndedAt = sorted.Last().UtcTimestamp,
                    InitiatingUser = sorted.First().UserName,
                    EventCount = sorted.Count,
                    PrimaryModule = primaryModule,
                    OverallStatus = overallStatus,
                    Summary = BuildSessionSummary(sorted, primaryModule)
                };
            })
            .OrderByDescending(s => s.StartedAt)
            .Skip(offset)
            .Take(pageSize)
            .ToList();

        return Result.Success<IReadOnlyList<AuditSession>>(sessions);
    }

    public Task<Result<IReadOnlyList<AuditRecord>>> GetAiPromptsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return SearchAsync(new AuditSearchQuery
        {
            Module = AuditModule.Ai,
            From = from,
            To = to,
            PageSize = Math.Min(pageSize, 200)
        }, ct);
    }

    public Task<Result<IReadOnlyList<AuditRecord>>> GetJiraChangesAsync(
        string? jiraIssueKey = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return SearchAsync(new AuditSearchQuery
        {
            Module = AuditModule.Jira,
            JiraIssueKey = jiraIssueKey,
            From = from,
            To = to,
            PageSize = Math.Min(pageSize, 200)
        }, ct);
    }

    public Task<Result<IReadOnlyList<AuditRecord>>> GetApprovalHistoryAsync(
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return SearchAsync(new AuditSearchQuery
        {
            Module = AuditModule.Approvals,
            PageSize = Math.Min(pageSize, 200)
        }, ct);
    }

    public Task<Result<IReadOnlyList<AuditRecord>>> GetRollbackHistoryAsync(
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return SearchAsync(new AuditSearchQuery
        {
            Module = AuditModule.Rollback,
            PageSize = Math.Min(pageSize, 200)
        }, ct);
    }

    public Task<Result<IReadOnlyList<AuditRecord>>> GetCorrelationChainAsync(
        string correlationId,
        CancellationToken ct = default)
    {
        return SearchAsync(new AuditSearchQuery
        {
            CorrelationId = correlationId,
            PageSize = 200
        }, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Result<IReadOnlyList<AuditRecord>>> SearchAsync(AuditSearchQuery query, CancellationToken ct)
    {
        try
        {
            var records = await _audit.SearchAsync(query, ct).ConfigureAwait(false);
            return Result.Success(records);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<AuditRecord>>($"Audit search failed: {ex.Message}");
        }
    }

    private static AuditStatus DetermineOverallStatus(IReadOnlyList<AuditRecord> records)
    {
        if (records.Any(r => r.Status == AuditStatus.Failed)) return AuditStatus.Failed;
        if (records.Any(r => r.Status == AuditStatus.Cancelled)) return AuditStatus.Cancelled;
        if (records.All(r => r.Status == AuditStatus.Completed)) return AuditStatus.Completed;
        if (records.Any(r => r.Status == AuditStatus.Started)) return AuditStatus.Started;
        return AuditStatus.Pending;
    }

    private static string BuildSessionSummary(IReadOnlyList<AuditRecord> records, AuditModule primaryModule)
    {
        var actions = records
            .GroupBy(r => r.Action)
            .Select(g => $"{g.Key} ×{g.Count()}")
            .Take(3);
        return $"{primaryModule}: {string.Join(", ", actions)}";
    }
}
