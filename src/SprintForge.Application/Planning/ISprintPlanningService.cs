using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Planning;

namespace SprintForge.Application.Planning;

/// <summary>
///   Orchestrates the Sprint Planning workflow:
///   parse input → AI-enrich work items → validate against calendar
///   → submit batch for approval → bulk-create in Jira via write gate.
/// </summary>
public interface ISprintPlanningService
{
    /// <summary>
    ///   Parses the given markdown/text content into a sprint plan with raw work items.
    ///   No AI call, no write — pure parsing.
    /// </summary>
    Task<Result<SprintPlan>> ParseAsync(string markdownContent, SprintCalendar calendar, CancellationToken ct = default);

    /// <summary>
    ///   Enriches raw work items with AI-generated descriptions, acceptance criteria, and story-point estimates.
    ///   No write — returns enriched items for user review.
    /// </summary>
    Task<Result<IReadOnlyList<WorkItem>>> EnrichWithAiAsync(SprintPlan plan, string jiraProjectKey, CancellationToken ct = default);

    /// <summary>
    ///   Validates all items against the sprint calendar.
    ///   Returns validation results; does not block enrichment or approval submission.
    /// </summary>
    SprintValidationSummary Validate(IReadOnlyList<WorkItem> items, SprintCalendar calendar);

    /// <summary>
    ///   Queues the full work-item tree as a batch in the Approvals Center.
    ///   Each story is one approval item; its subtasks are listed in ProposedValueJson.
    /// </summary>
    Task<Result<IReadOnlyList<ApprovalRequest>>> SubmitBatchForApprovalAsync(
        IReadOnlyList<WorkItem> items,
        string jiraProjectKey,
        string correlationId,
        string userName,
        CancellationToken ct = default);

    /// <summary>
    ///   Executed after the user approves the batch in the Approvals Center.
    ///   Bulk-creates all approved items in Jira through the write gate.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> PersistApprovedBatchAsync(
        IReadOnlyList<ApprovalDecision> decisions,
        IReadOnlyList<WorkItem> items,
        string jiraProjectKey,
        string correlationId,
        string userName,
        CancellationToken ct = default);
}

public sealed record SprintPlan
{
    public required SprintCalendar Calendar { get; init; }
    public required IReadOnlyList<WorkItem> RawItems { get; init; }
    public required string SourceFormat { get; init; }
    public required DateTimeOffset ParsedAt { get; init; }
}

public sealed record SprintValidationSummary
{
    public required bool IsValid { get; init; }
    public required int TotalItems { get; init; }
    public required decimal TotalEstimateDays { get; init; }
    public required decimal CapacityDays { get; init; }
    public required IReadOnlyList<WorkItemValidationWarning> Warnings { get; init; }
}

public sealed record WorkItemValidationWarning(string LocalId, string Summary, string Message, ValidationSeverity Severity);

public enum ValidationSeverity { Info, Warning, Error }
