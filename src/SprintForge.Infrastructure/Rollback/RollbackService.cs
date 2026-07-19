using Microsoft.Extensions.Logging;
using SprintForge.Application.Audit;
using SprintForge.Application.Rollback;
using SprintForge.Application.Sdlc;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;
using SprintForge.Infrastructure.Audit;

namespace SprintForge.Infrastructure.Rollback;

/// <summary>
///   Orchestrates rollback of previously audited write operations.
///   Every rollback is itself an audited write (Module = Rollback, Action = Rollback).
///
///   Currently supports rolling back:
///   - JiraCreate → transitions issue to "Won't Do" / "Cancel"
///   - JiraBulkCreate → transitions each created issue
///
///   Document version rollback and config profile rollback: listed as candidates
///   but marked non-rollbackable until Phase 7 (doc store rollback is a new version create).
/// </summary>
public sealed class RollbackService : IRollbackService
{
    private readonly IAuditService _auditService;
    private readonly ISdlcTool _sdlc;
    private readonly AuditedOperationRunner _runner;
    private readonly ILogger<RollbackService> _logger;

    private static readonly HashSet<AuditAction> RollbackableJiraActions = new()
    {
        AuditAction.JiraCreate,
        AuditAction.JiraBulkCreate
    };

    public RollbackService(
        IAuditService auditService,
        ISdlcTool sdlc,
        AuditedOperationRunner runner,
        ILogger<RollbackService> logger)
    {
        _auditService = auditService;
        _sdlc = sdlc;
        _runner = runner;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<RollbackCandidate>>> GetCandidatesAsync(
        string correlationId,
        CancellationToken ct = default)
    {
        var query = new AuditSearchQuery
        {
            CorrelationId = correlationId,
            PageSize = 200
        };

        var records = await _auditService.SearchAsync(query, ct).ConfigureAwait(false);
        var candidates = records
            .Where(r => r.Status == AuditStatus.Completed)
            .Select(r => BuildCandidate(r))
            .OrderByDescending(c => c.PerformedAt)
            .ToList();

        return Result.Success<IReadOnlyList<RollbackCandidate>>(candidates);
    }

    public async Task<Result<string>> RollbackAsync(
        string targetAuditId,
        string reason,
        string userName,
        CancellationToken ct = default)
    {
        // Fetch the original record
        var query = new AuditSearchQuery { CorrelationId = string.Empty, PageSize = 1, FreeText = targetAuditId };
        var records = await _auditService.SearchAsync(new AuditSearchQuery { PageSize = 200 }, ct)
            .ConfigureAwait(false);
        var original = records.FirstOrDefault(r => r.AuditId == targetAuditId);

        if (original is null)
            return Result.Failure<string>($"Audit record '{targetAuditId}' not found.");

        if (original.Status != AuditStatus.Completed)
            return Result.Failure<string>($"Cannot rollback: record status is '{original.Status}' (expected Completed).");

        if (!RollbackableJiraActions.Contains(original.Action))
            return Result.Failure<string>(
                $"Rollback of {original.Action} is not supported in this release. " +
                "See docs/10-audit-approval-framework.md §Rollback for the planned implementation.");

        // Extract Jira key(s) from OutputsJson
        var jiraKeys = ExtractJiraKeysFromOutputs(original.OutputsJson);
        if (jiraKeys.Count == 0)
            return Result.Failure<string>("No Jira keys found in the original audit record's outputs. Cannot rollback.");

        _logger.LogInformation(
            "Rolling back {Action} for audit {AuditId}: {Count} Jira issue(s)",
            original.Action, targetAuditId, jiraKeys.Count);

        var rollbackSuffix = Guid.NewGuid().ToString("N")[..8];
        var rollbackCorrelation = $"rb-{rollbackSuffix}-{original.CorrelationId}";
        var rolledBack = new List<string>();

        foreach (var key in jiraKeys)
        {
            ct.ThrowIfCancellationRequested();
            var op = new RollbackJiraIssueOperation(_sdlc, key, targetAuditId, reason);
            var result = await _runner.RunAsync(op, rollbackCorrelation, userName, Environment.MachineName, ct)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                _logger.LogWarning("Failed to rollback Jira issue {Key}: {Error}", key, result.Error);
                // Continue rolling back remaining issues; report partial success
            }
            else
            {
                rolledBack.Add(key);
            }
        }

        if (rolledBack.Count == 0)
            return Result.Failure<string>("All rollback transitions failed. See logs for details.");

        var summary = $"Rolled back {rolledBack.Count}/{jiraKeys.Count} Jira issue(s): {string.Join(", ", rolledBack)}";
        _logger.LogInformation("{Summary}", summary);
        return Result.Success(summary);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static RollbackCandidate BuildCandidate(AuditRecord record)
    {
        var isRollbackable = RollbackableJiraActions.Contains(record.Action);
        var nonRollbackableReason = isRollbackable
            ? null
            : $"Rollback of {record.Action} is not yet supported (Phase 7). " +
              "See docs/10-audit-approval-framework.md §Rollback.";

        return new RollbackCandidate
        {
            AuditId = record.AuditId,
            CorrelationId = record.CorrelationId,
            Module = record.Module,
            Action = record.Action,
            Description = BuildDescription(record),
            PerformedAt = record.UtcTimestamp,
            PerformedByUser = record.UserName,
            IsRollbackable = isRollbackable,
            NonRollbackableReason = nonRollbackableReason,
            RollbackDescription = isRollbackable
                ? $"Transition all Jira issues created in this operation to 'Won't Do'."
                : null
        };
    }

    private static string BuildDescription(AuditRecord record) =>
        $"{record.Action} [{record.Module}] by {record.UserName} at {record.UtcTimestamp:yyyy-MM-dd HH:mm} UTC";

    private static IReadOnlyList<string> ExtractJiraKeysFromOutputs(string? outputsJson)
    {
        if (string.IsNullOrWhiteSpace(outputsJson)) return [];
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(outputsJson);
            var root = doc.RootElement;

            // Handle array of keys: ["RBP-101", "RBP-102"]
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                return root.EnumerateArray()
                    .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

            // Handle object with a Keys array: {"Keys": ["RBP-101"]}
            if (root.TryGetProperty("Keys", out var keys) && keys.ValueKind == System.Text.Json.JsonValueKind.Array)
                return keys.EnumerateArray()
                    .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

            // Handle single key: {"Key": "RBP-101"}
            if (root.TryGetProperty("Key", out var key) && key.ValueKind == System.Text.Json.JsonValueKind.String)
                return [key.GetString()!];

            return [];
        }
        catch
        {
            return [];
        }
    }
}
