using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Application.Documents;

/// <summary>Generates a versioned document (SRS, SAD, SDD, etc.) and persists it under the working directory.</summary>
public interface IDocumentGenerator
{
    DocumentKind Kind { get; }

    /// <summary>
    ///   Generates document content from <paramref name="request"/> using the AI orchestrator and the relevant template.
    ///   Returns a draft; the caller must route it through the Approvals Center before persisting.
    /// </summary>
    Task<Result<DocumentDraft>> GenerateAsync(DocumentGenerationRequest request, CancellationToken ct = default);
}

/// <summary>Persists, versions, exports, and compares generated document drafts.</summary>
public interface IDocumentVersionStore
{
    Task<Result<DocumentVersion>> SaveDraftAsync(DocumentDraft draft, string auditId, string createdByUser, CancellationToken ct = default);
    Task<Result<DocumentVersion>> GetVersionAsync(string documentId, int versionNumber, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DocumentVersion>>> GetVersionHistoryAsync(string documentId, CancellationToken ct = default);
    Task<Result<DocumentDiff>> DiffVersionsAsync(string documentId, int fromVersion, int toVersion, CancellationToken ct = default);
    Task<Result<string>> ExportAsync(string versionId, ExportFormat format, string destinationPath, CancellationToken ct = default);
}

public sealed record DocumentGenerationRequest
{
    public required DocumentKind Kind { get; init; }
    public required string DocumentId { get; init; }
    public required string JiraProjectKey { get; init; }
    public required string InputMarkdown { get; init; }
    public string? TemplateId { get; init; }
    public string? JiraIssueKey { get; init; }
    public required string CorrelationId { get; init; }
}

public sealed record DocumentDraft
{
    public required string DocumentId { get; init; }
    public required DocumentKind Kind { get; init; }
    public required string ContentMarkdown { get; init; }
    public required string ContentHash { get; init; }
    public string? TemplateUsed { get; init; }
    public string? JiraIssueKey { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
}
