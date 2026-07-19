using SprintForge.Domain.Audit;

namespace SprintForge.Application.Audit;

/// <summary>
///   Writes hash-chained audit records to the JSONL source-of-truth log and to the SQLite index.
///   If a flush fails the implementation MUST throw <see cref="SprintForge.Domain.Common.AuditUnavailableException"/>.
/// </summary>
public interface IAuditService
{
    /// <summary>Opens a new audit record for a started operation; returns the assigned AuditId.</summary>
    Task<string> RecordStartAsync(AuditContext context, CancellationToken ct = default);

    /// <summary>Seals the audit record with a Completed outcome and optional outputs JSON.</summary>
    Task RecordCompletedAsync(string auditId, string? outputsJson = null, CancellationToken ct = default);

    /// <summary>Seals the audit record with a Failed outcome.</summary>
    Task RecordFailedAsync(string auditId, Exception ex, CancellationToken ct = default);

    /// <summary>Seals the audit record with a Cancelled outcome.</summary>
    Task RecordCancelledAsync(string auditId, CancellationToken ct = default);

    /// <summary>Returns audit records matching the filter; page size &lt;= 200.</summary>
    Task<IReadOnlyList<AuditRecord>> SearchAsync(AuditSearchQuery query, CancellationToken ct = default);

    /// <summary>Re-derives the hash chain from the JSONL source and reports any broken links.</summary>
    Task<IntegrityReport> VerifyChainIntegrityAsync(CancellationToken ct = default);

    /// <summary>Exports filtered audit records as a JSONL file at the supplied path.</summary>
    Task ExportAsync(AuditSearchQuery query, string destinationPath, CancellationToken ct = default);
}

public sealed record AuditSearchQuery
{
    public AuditModule? Module { get; init; }
    public AuditStatus? Status { get; init; }
    public string? UserName { get; init; }
    public string? CorrelationId { get; init; }
    public string? JiraIssueKey { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? FreeText { get; init; }
    public int PageSize { get; init; } = 50;
    public int PageOffset { get; init; } = 0;
}

public sealed record IntegrityReport(bool IsValid, int RecordsChecked, IReadOnlyList<string> BrokenLinkAuditIds);
