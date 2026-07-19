using Microsoft.EntityFrameworkCore;
using SprintForge.Domain.Audit;

namespace SprintForge.Infrastructure.Data;

/// <summary>
///   Rebuildable SQLite index for the audit log.
///   The JSONL files are the source of truth; this DB is a queryable projection.
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecordIndex> AuditRecords => Set<AuditRecordIndex>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AuditRecordIndex>(e =>
        {
            e.HasKey(r => r.AuditId);
            e.HasIndex(r => r.CorrelationId);
            e.HasIndex(r => r.Module);
            e.HasIndex(r => r.Action);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.UtcTimestamp);
            e.HasIndex(r => r.UserName);
            e.HasIndex(r => r.JiraIssueKey);
            e.Property(r => r.Module).HasConversion<string>();
            e.Property(r => r.Action).HasConversion<string>();
            e.Property(r => r.Status).HasConversion<string>();
        });
    }
}

/// <summary>Flattened, indexed projection of an <see cref="AuditRecord"/> for SQLite queries.</summary>
public sealed class AuditRecordIndex
{
    public required string AuditId { get; set; }
    public required string CorrelationId { get; set; }
    public string? ParentAuditId { get; set; }
    public required AuditModule Module { get; set; }
    public required AuditAction Action { get; set; }
    public required AuditStatus Status { get; set; }
    public required string UserName { get; set; }
    public required string MachineName { get; set; }
    public required DateTimeOffset UtcTimestamp { get; set; }
    public long? DurationMs { get; set; }
    public string? JiraIssueKey { get; set; }
    public string? ErrorMessage { get; set; }
    public required string PreviousHash { get; set; }
    public string? SelfHash { get; set; }
}
