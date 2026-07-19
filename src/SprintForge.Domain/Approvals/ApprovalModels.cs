namespace SprintForge.Domain.Approvals;

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Executing,
    Completed,
    Failed
}

/// <summary>A single item queued in the Approvals Center, representing a write operation awaiting user decision.</summary>
public sealed record ApprovalRequest
{
    public required string ApprovalId { get; init; }
    public required string AuditId { get; init; }
    public required string CorrelationId { get; init; }
    public string? BatchId { get; init; }

    public required string OperationDescription { get; init; }
    public required string ModuleTag { get; init; }

    /// <summary>JSON snapshot of the current state (before the proposed change).</summary>
    public string? CurrentValueJson { get; init; }

    /// <summary>JSON snapshot of the AI-proposed state.</summary>
    public required string ProposedValueJson { get; init; }

    /// <summary>Human-readable AI explanation of why this change is being proposed.</summary>
    public string? AiExplanation { get; init; }

    /// <summary>Relative paths of files affected by this write.</summary>
    public IReadOnlyList<string> AffectedFiles { get; init; } = [];

    public ApprovalStatus Status { get; init; } = ApprovalStatus.Pending;
    public required DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public string? DecisionComment { get; init; }
    public string? DecidedByUser { get; init; }
}

/// <summary>Decision recorded when a user acts on an approval item.</summary>
public sealed record ApprovalDecision
{
    public required string ApprovalId { get; init; }
    public required ApprovalStatus Decision { get; init; }
    public string? Comment { get; init; }

    /// <summary>If the user chose Modify, this carries the edited proposed value.</summary>
    public string? ModifiedValueJson { get; init; }
    public required DateTimeOffset DecidedAt { get; init; }
    public required string DecidedByUser { get; init; }
}
