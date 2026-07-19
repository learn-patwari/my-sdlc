namespace SprintForge.Domain.Audit;

/// <summary>Module that generated an audit event.</summary>
public enum AuditModule
{
    System,
    Srs,
    Sad,
    Sdd,
    Sprint,
    Repository,
    UnitTests,
    Jira,
    Documents,
    Ai,
    Configuration,
    Approvals,
    Security,
    Rollback
}

/// <summary>High-level action category of an audit event.</summary>
public enum AuditAction
{
    AppStart,
    AppStop,
    ProfileLoad,
    ProfileSave,
    SecretStore,
    SecretDelete,
    SrsGeneration,
    SrsVersionCreate,
    SadGeneration,
    SddGeneration,
    SddSearch,
    SprintParse,
    SprintPush,
    RepositoryScan,
    TestGeneration,
    JiraSearch,
    JiraCreate,
    JiraUpdate,
    JiraTransition,
    JiraBulkCreate,
    DocumentExport,
    AiPrompt,
    ApprovalDecision,
    Rollback,
    AuditExport,
    IntegrityCheck
}

/// <summary>Outcome status of an audited operation.</summary>
public enum AuditStatus
{
    Started,
    Completed,
    Failed,
    Cancelled,
    Queued,
    Pending,
    Approved,
    Rejected
}

/// <summary>
///   Immutable audit record written to the JSONL hash-chain log.
///   Every field that matters for reproducibility and traceability is captured here.
/// </summary>
public sealed record AuditRecord
{
    /// <summary>ULID-format unique identifier for this audit event.</summary>
    public required string AuditId { get; init; }

    /// <summary>Links related events (e.g., prompt → generation → approval → Jira write).</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Optional parent event (e.g., a batch item's parent is the batch event).</summary>
    public string? ParentAuditId { get; init; }

    public required AuditModule Module { get; init; }
    public required AuditAction Action { get; init; }
    public required AuditStatus Status { get; init; }

    /// <summary>OS login name of the acting user.</summary>
    public required string UserName { get; init; }

    /// <summary>Machine hostname for multi-machine audit aggregation.</summary>
    public required string MachineName { get; init; }

    public required DateTimeOffset UtcTimestamp { get; init; }
    public long? DurationMs { get; init; }
    public int RetryCount { get; init; }

    /// <summary>Jira issue key linked to this event (e.g., "RBP-101").</summary>
    public string? JiraIssueKey { get; init; }

    /// <summary>Free-form JSON payload capturing input parameters (never contains plaintext secrets).</summary>
    public string? InputsJson { get; init; }

    /// <summary>Free-form JSON payload capturing output results.</summary>
    public string? OutputsJson { get; init; }

    public string? ErrorMessage { get; init; }
    public string? ErrorStackTrace { get; init; }

    /// <summary>SHA-256 hash of the previous record — forms the tamper-evident hash chain.</summary>
    public required string PreviousHash { get; init; }

    /// <summary>SHA-256 hash of this record's canonical JSON (set after serialization).</summary>
    public string? SelfHash { get; init; }

    /// <summary>Optional RSA-SHA256 signature of SelfHash (requires X.509 cert config).</summary>
    public string? DigitalSignature { get; init; }
}
