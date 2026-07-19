using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Application.Documents;

/// <summary>
///   Orchestrates the full SAD workflow:
///   generate (AI + draw.io XML, no write) → submit for approval → persist (write gate).
/// </summary>
public interface ISadService
{
    /// <summary>
    ///   Generates SAD markdown + draw.io architecture diagram from a brief description.
    ///   No write occurs — returns an in-memory draft with the diagram XML embedded in AdditionalFiles.
    /// </summary>
    Task<Result<DocumentDraft>> GenerateDraftAsync(SadGenerationRequest request, CancellationToken ct = default);

    Task<Result<ApprovalRequest>> SubmitForApprovalAsync(DocumentDraft draft, string correlationId, string userName, CancellationToken ct = default);

    Task<Result<DocumentVersion>> PersistApprovedDraftAsync(ApprovalDecision decision, DocumentDraft draft, CancellationToken ct = default);
}

public sealed record SadGenerationRequest
{
    public required string DocumentId { get; init; }
    public required string JiraProjectKey { get; init; }
    public required string ServiceName { get; init; }

    /// <summary>Brief description of the architecture scope (user input).</summary>
    public required string InputMarkdown { get; init; }

    /// <summary>Identified services to include in the diagram (from repo analysis or user input).</summary>
    public IReadOnlyList<string> IdentifiedServices { get; init; } = [];

    public string? ArchitectureStyle { get; init; }
    public string? TechStack { get; init; }
    public string? JiraIssueKey { get; init; }
    public string? TemplateId { get; init; }
    public required string CorrelationId { get; init; }
}
