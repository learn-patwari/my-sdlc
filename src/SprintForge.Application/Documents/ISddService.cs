using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Application.Documents;

/// <summary>
///   Orchestrates the full SDD workflow:
///   search existing SDDs → generate (AI, no write) → submit for approval → persist (write gate).
///
///   INVARIANT: search-before-create — callers MUST call SearchExistingSddsAsync first
///   and surface results to the user before generating a new SDD.
/// </summary>
public interface ISddService
{
    /// <summary>
    ///   Searches all configured Jira projects for existing SDD documents matching the service name.
    ///   Returns an empty list when no matches are found; never blocks generation.
    /// </summary>
    Task<Result<IReadOnlyList<ExistingSddMatch>>> SearchExistingSddsAsync(string serviceName, IReadOnlyList<string> projectKeys, CancellationToken ct = default);

    /// <summary>
    ///   Generates a new SDD document via AI. Call after the user has reviewed SearchExistingSddsAsync results.
    ///   No write occurs.
    /// </summary>
    Task<Result<DocumentDraft>> GenerateDraftAsync(SddGenerationRequest request, CancellationToken ct = default);

    Task<Result<ApprovalRequest>> SubmitForApprovalAsync(DocumentDraft draft, string correlationId, string userName, CancellationToken ct = default);

    Task<Result<DocumentVersion>> PersistApprovedDraftAsync(ApprovalDecision decision, DocumentDraft draft, CancellationToken ct = default);
}

public sealed record SddGenerationRequest
{
    public required string DocumentId { get; init; }
    public required string JiraProjectKey { get; init; }
    public required string ServiceName { get; init; }

    /// <summary>Public API surface of the service (methods, signatures, etc.) from repo analysis.</summary>
    public string? ServiceApiMarkdown { get; init; }

    /// <summary>Optional dependency summary from repo analysis.</summary>
    public string? DependencySummary { get; init; }

    public string? JiraIssueKey { get; init; }
    public string? TemplateId { get; init; }
    public required string CorrelationId { get; init; }
}

public sealed record ExistingSddMatch
{
    public required string JiraIssueKey { get; init; }
    public required string Summary { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? ProjectKey { get; init; }
}
