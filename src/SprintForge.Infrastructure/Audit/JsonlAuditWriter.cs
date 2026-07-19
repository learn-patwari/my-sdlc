using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Audit;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Audit;

/// <summary>
///   Appends hash-chained records to a JSONL file.
///   Thread-safe: a <see cref="SemaphoreSlim"/> guards all writes to preserve ordering.
///   If the file cannot be written <see cref="AuditUnavailableException"/> is thrown — callers MUST NOT proceed.
/// </summary>
public sealed class JsonlAuditWriter : IDisposable
{
    private readonly string _filePath;
    private readonly ILogger<JsonlAuditWriter> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private string _lastHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonlAuditWriter(string filePath, ILogger<JsonlAuditWriter> logger)
    {
        _filePath = filePath;
        _logger = logger;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        _lastHash = ReadLastHashFromFile();
    }

    public async Task<AuditRecord> AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var withPrev = record with { PreviousHash = _lastHash };
            var canonical = JsonSerializer.Serialize(withPrev, SerializerOptions);
            var selfHash = ComputeSha256(canonical);
            var final = withPrev with { SelfHash = selfHash };
            var line = JsonSerializer.Serialize(final, SerializerOptions);

            await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, Encoding.UTF8, ct)
                .ConfigureAwait(false);

            _lastHash = selfHash;
            return final;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "Audit JSONL write failed for record {AuditId}. Operation MUST be blocked.", record.AuditId);
            throw new AuditUnavailableException($"Audit sink unavailable — cannot write record {record.AuditId}.", ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<AuditRecord>> ReadAllAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return [];
        var lines = await File.ReadAllLinesAsync(_filePath, Encoding.UTF8, ct).ConfigureAwait(false);
        var records = new List<AuditRecord>(lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var record = JsonSerializer.Deserialize<AuditRecord>(line, SerializerOptions);
            if (record is not null) records.Add(record);
        }
        return records;
    }

    public async Task<IntegrityReport> VerifyChainAsync(CancellationToken ct = default)
    {
        var records = await ReadAllAsync(ct).ConfigureAwait(false);
        var broken = new List<string>();
        var expectedPrev = "0000000000000000000000000000000000000000000000000000000000000000";

        foreach (var r in records)
        {
            if (r.PreviousHash != expectedPrev)
                broken.Add(r.AuditId);

            var canonical = JsonSerializer.Serialize(r with { SelfHash = null, DigitalSignature = null }, SerializerOptions);
            expectedPrev = ComputeSha256(canonical);
        }
        return new IntegrityReport(broken.Count == 0, records.Count, broken);
    }

    private string ReadLastHashFromFile()
    {
        if (!File.Exists(_filePath)) return _lastHash;
        string? lastLine = null;
        foreach (var line in File.ReadLines(_filePath, Encoding.UTF8))
            if (!string.IsNullOrWhiteSpace(line)) lastLine = line;

        if (lastLine is null) return _lastHash;
        var last = JsonSerializer.Deserialize<AuditRecord>(lastLine, SerializerOptions);
        return last?.SelfHash ?? _lastHash;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose() => _writeLock.Dispose();
}
