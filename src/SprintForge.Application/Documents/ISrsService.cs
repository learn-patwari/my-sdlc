using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Application.Documents;

/// <summary>
///   Orchestrates the full SRS workflow:
///   generate (AI, no write) → submit for approval (Approvals Center) → persist (write gate).
/// </summary>
public interface ISrsService
{
    /// <summary>
    ///   Calls the AI orchestrator with the SRS prompt template and returns an in-memory draft.
    ///   No audit record, no write, no approval — pure generation.
    /// </summary>
    Task<Result<DocumentDraft>> GenerateDraftAsync(SrsGenerationRequest request, CancellationToken ct = default);

    /// <summary>
    ///   Enqueues the draft in the Approvals Center and returns immediately.
    ///   The caller polls IApprovalGate.WaitForDecisionAsync or the UI reacts to the badge.
    /// </summary>
    Task<Result<ApprovalRequest>> SubmitForApprovalAsync(DocumentDraft draft, string correlationId, string userName, CancellationToken ct = default);

    /// <summary>
    ///   Called after the user approves in the Approvals Center.
    ///   Runs through the audit write-gate and persists the versioned document.
    /// </summary>
    Task<Result<DocumentVersion>> PersistApprovedDraftAsync(ApprovalDecision decision, DocumentDraft draft, CancellationToken ct = default);
}

public sealed record SrsGenerationRequest
{
    public required string DocumentId { get; init; }
    public required string JiraProjectKey { get; init; }
    public required string InputMarkdown { get; init; }
    public string? JiraIssueKey { get; init; }
    public string? TemplateId { get; init; }
    public required string CorrelationId { get; init; }
}
