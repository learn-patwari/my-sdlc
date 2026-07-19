namespace SprintForge.Domain.Documents;

public enum DocumentKind { Srs, Sad, Sdd, Tests, Sprint }

public enum ExportFormat { Docx, Pdf, Markdown, Html }

/// <summary>An immutable version entry for a generated document.</summary>
public sealed record DocumentVersion
{
    public required string VersionId { get; init; }
    public required string DocumentId { get; init; }
    public required DocumentKind Kind { get; init; }

    /// <summary>Monotonically increasing version number (v001, v002, …).</summary>
    public required int VersionNumber { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required string CreatedByUser { get; init; }
    public required string AuditId { get; init; }

    /// <summary>SHA-256 content hash of the primary export file (DOCX or MD).</summary>
    public required string ContentHash { get; init; }

    public required IReadOnlyList<ExportFormat> AvailableFormats { get; init; }

    /// <summary>Relative path under Working Directory (e.g., "Documents/SRS/PROJ-1234/v001/").</summary>
    public required string RelativePath { get; init; }

    public string? JiraIssueKey { get; init; }
    public string? TemplateUsed { get; init; }
}

/// <summary>Represents the result of a content-hash comparison between two document versions.</summary>
public sealed record DocumentDiff(
    string FromVersionId,
    string ToVersionId,
    bool IsIdentical,
    int AddedLines,
    int RemovedLines,
    int ModifiedLines);
