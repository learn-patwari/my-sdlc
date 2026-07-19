using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Audit;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;
using SprintForge.Infrastructure.Data;

namespace SprintForge.Infrastructure.Audit;

public sealed class AuditService : IAuditService
{
    private readonly JsonlAuditWriter _writer;
    private readonly AuditDbContext _db;
    private readonly ILogger<AuditService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AuditService(JsonlAuditWriter writer, AuditDbContext db, ILogger<AuditService> logger)
    {
        _writer = writer;
        _db = db;
        _logger = logger;
    }

    public async Task<string> RecordStartAsync(AuditContext context, CancellationToken ct = default)
    {
        var record = new AuditRecord
        {
            AuditId = context.AuditId,
            CorrelationId = context.CorrelationId,
            ParentAuditId = context.ParentAuditId,
            Module = context.Module,
            Action = context.Action,
            Status = AuditStatus.Started,
            UserName = context.UserName,
            MachineName = context.MachineName,
            UtcTimestamp = DateTimeOffset.UtcNow,
            JiraIssueKey = context.JiraIssueKey,
            InputsJson = context.InputsJson,
            PreviousHash = string.Empty // set by writer
        };

        // This throws AuditUnavailableException if the sink is down — callers are blocked.
        var written = await _writer.AppendAsync(record, ct).ConfigureAwait(false);
        await IndexRecordAsync(written, ct).ConfigureAwait(false);
        return written.AuditId;
    }

    public async Task RecordCompletedAsync(string auditId, string? outputsJson = null, CancellationToken ct = default)
    {
        var startRecord = await _db.AuditRecords.FindAsync([auditId], ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No started record found for AuditId={auditId}");

        var durationMs = (long)(DateTimeOffset.UtcNow - startRecord.UtcTimestamp).TotalMilliseconds;
        var record = BuildUpdate(auditId, startRecord, AuditStatus.Completed, durationMs, null, null, outputsJson);
        var written = await _writer.AppendAsync(record, ct).ConfigureAwait(false);
        await UpdateIndexStatusAsync(written, ct).ConfigureAwait(false);
    }

    public async Task RecordFailedAsync(string auditId, Exception ex, CancellationToken ct = default)
    {
        var startRecord = await _db.AuditRecords.FindAsync([auditId], ct).ConfigureAwait(false);
        var durationMs = startRecord is null ? 0L
            : (long)(DateTimeOffset.UtcNow - startRecord.UtcTimestamp).TotalMilliseconds;

        var record = BuildUpdate(auditId, startRecord, AuditStatus.Failed, durationMs, ex.Message, ex.StackTrace, null);
        var written = await _writer.AppendAsync(record, ct).ConfigureAwait(false);
        await UpdateIndexStatusAsync(written, ct).ConfigureAwait(false);
    }

    public async Task RecordCancelledAsync(string auditId, CancellationToken ct = default)
    {
        var startRecord = await _db.AuditRecords.FindAsync([auditId], ct).ConfigureAwait(false);
        var durationMs = startRecord is null ? 0L
            : (long)(DateTimeOffset.UtcNow - startRecord.UtcTimestamp).TotalMilliseconds;

        var record = BuildUpdate(auditId, startRecord, AuditStatus.Cancelled, durationMs, null, null, null);
        var written = await _writer.AppendAsync(record, ct).ConfigureAwait(false);
        await UpdateIndexStatusAsync(written, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditRecord>> SearchAsync(AuditSearchQuery query, CancellationToken ct = default)
    {
        var q = _db.AuditRecords.AsNoTracking();
        if (query.Module.HasValue) q = q.Where(r => r.Module == query.Module.Value);
        if (query.Status.HasValue) q = q.Where(r => r.Status == query.Status.Value);
        if (query.UserName is not null) q = q.Where(r => r.UserName == query.UserName);
        if (query.CorrelationId is not null) q = q.Where(r => r.CorrelationId == query.CorrelationId);
        if (query.JiraIssueKey is not null) q = q.Where(r => r.JiraIssueKey == query.JiraIssueKey);
        if (query.From.HasValue) q = q.Where(r => r.UtcTimestamp >= query.From.Value);
        if (query.To.HasValue) q = q.Where(r => r.UtcTimestamp <= query.To.Value);
        if (query.FreeText is not null) q = q.Where(r => r.ErrorMessage != null && r.ErrorMessage.Contains(query.FreeText));

        var indexed = await q
            .OrderByDescending(r => r.UtcTimestamp)
            .Skip(query.PageOffset)
            .Take(query.PageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        return indexed.Select(MapFromIndex).ToList();
    }

    public Task<IntegrityReport> VerifyChainIntegrityAsync(CancellationToken ct = default)
        => _writer.VerifyChainAsync(ct);

    public async Task ExportAsync(AuditSearchQuery query, string destinationPath, CancellationToken ct = default)
    {
        var records = await SearchAsync(query, ct).ConfigureAwait(false);
        await File.WriteAllLinesAsync(
            destinationPath,
            records.Select(r => JsonSerializer.Serialize(r, JsonOpts)),
            ct).ConfigureAwait(false);
    }

    // --- private helpers ---

    private AuditRecord BuildUpdate(string auditId, AuditRecordIndex? start, AuditStatus status,
        long durationMs, string? error, string? stack, string? outputs)
    {
        return new AuditRecord
        {
            AuditId = auditId,
            CorrelationId = start?.CorrelationId ?? auditId,
            ParentAuditId = start?.ParentAuditId,
            Module = start?.Module ?? AuditModule.System,
            Action = start?.Action ?? AuditAction.AppStart,
            Status = status,
            UserName = start?.UserName ?? Environment.UserName,
            MachineName = start?.MachineName ?? Environment.MachineName,
            UtcTimestamp = DateTimeOffset.UtcNow,
            DurationMs = durationMs,
            JiraIssueKey = start?.JiraIssueKey,
            OutputsJson = outputs,
            ErrorMessage = error,
            ErrorStackTrace = stack,
            PreviousHash = string.Empty // set by writer
        };
    }

    private async Task IndexRecordAsync(AuditRecord r, CancellationToken ct)
    {
        _db.AuditRecords.Add(MapToIndex(r));
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task UpdateIndexStatusAsync(AuditRecord r, CancellationToken ct)
    {
        var existing = await _db.AuditRecords.FindAsync([r.AuditId], ct).ConfigureAwait(false);
        if (existing is not null)
        {
            existing.Status = r.Status;
            existing.DurationMs = r.DurationMs;
            existing.ErrorMessage = r.ErrorMessage;
            existing.SelfHash = r.SelfHash;
        }
        else
        {
            _db.AuditRecords.Add(MapToIndex(r));
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static AuditRecordIndex MapToIndex(AuditRecord r) => new()
    {
        AuditId = r.AuditId,
        CorrelationId = r.CorrelationId,
        ParentAuditId = r.ParentAuditId,
        Module = r.Module,
        Action = r.Action,
        Status = r.Status,
        UserName = r.UserName,
        MachineName = r.MachineName,
        UtcTimestamp = r.UtcTimestamp,
        DurationMs = r.DurationMs,
        JiraIssueKey = r.JiraIssueKey,
        ErrorMessage = r.ErrorMessage,
        PreviousHash = r.PreviousHash,
        SelfHash = r.SelfHash
    };

    private static AuditRecord MapFromIndex(AuditRecordIndex i) => new()
    {
        AuditId = i.AuditId,
        CorrelationId = i.CorrelationId,
        ParentAuditId = i.ParentAuditId,
        Module = i.Module,
        Action = i.Action,
        Status = i.Status,
        UserName = i.UserName,
        MachineName = i.MachineName,
        UtcTimestamp = i.UtcTimestamp,
        DurationMs = i.DurationMs,
        JiraIssueKey = i.JiraIssueKey,
        ErrorMessage = i.ErrorMessage,
        PreviousHash = i.PreviousHash,
        SelfHash = i.SelfHash
    };
}
