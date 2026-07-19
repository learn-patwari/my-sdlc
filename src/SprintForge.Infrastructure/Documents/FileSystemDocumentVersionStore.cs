using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Documents;
using SprintForge.Domain.Documents;
using SprintForge.Infrastructure.Documents.Exporters;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Persists document drafts as versioned directories under the working directory.
///   Layout: {root}/Documents/{Kind}/{DocumentId}/v{NNN}/
///   Each version directory contains: document.md, document.docx, document.html, version.json
/// </summary>
public sealed class FileSystemDocumentVersionStore : IDocumentVersionStore
{
    private readonly string _root;
    private readonly ILogger<FileSystemDocumentVersionStore> _logger;

    public FileSystemDocumentVersionStore(string workingDirectory, ILogger<FileSystemDocumentVersionStore> logger)
    {
        _root = Path.Combine(workingDirectory, "Documents");
        _logger = logger;
    }

    public async Task<Result<DocumentVersion>> SaveDraftAsync(
        DocumentDraft draft,
        string auditId,
        string createdByUser,
        CancellationToken ct = default)
    {
        try
        {
            var history = await GetVersionHistoryAsync(draft.DocumentId, ct).ConfigureAwait(false);
            int nextVersion = history.IsSuccess ? history.Value!.Count + 1 : 1;

            var kindDir = Path.Combine(_root, draft.Kind.ToString(), SanitizeName(draft.DocumentId));
            var versionDir = Path.Combine(kindDir, $"v{nextVersion:D3}");
            Directory.CreateDirectory(versionDir);

            var mdPath = Path.Combine(versionDir, "document.md");
            var docxPath = Path.Combine(versionDir, "document.docx");
            var htmlPath = Path.Combine(versionDir, "document.html");

            await File.WriteAllTextAsync(mdPath, draft.ContentMarkdown, Encoding.UTF8, ct).ConfigureAwait(false);
            DocxExporter.Export(draft.ContentMarkdown, draft.DocumentId, docxPath);
            await HtmlExporter.ExportAsync(draft.ContentMarkdown, draft.DocumentId, htmlPath, ct).ConfigureAwait(false);

            var relativePath = Path.Combine("Documents", draft.Kind.ToString(), SanitizeName(draft.DocumentId), $"v{nextVersion:D3}");

            var version = new DocumentVersion
            {
                VersionId = Guid.NewGuid().ToString("N"),
                DocumentId = draft.DocumentId,
                Kind = draft.Kind,
                VersionNumber = nextVersion,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUser = createdByUser,
                AuditId = auditId,
                ContentHash = draft.ContentHash,
                AvailableFormats = new[] { ExportFormat.Markdown, ExportFormat.Docx, ExportFormat.Html },
                RelativePath = relativePath,
                JiraIssueKey = draft.JiraIssueKey,
                TemplateUsed = draft.TemplateUsed
            };

            var versionJson = System.Text.Json.JsonSerializer.Serialize(version, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(versionDir, "version.json"), versionJson, Encoding.UTF8, ct).ConfigureAwait(false);

            _logger.LogInformation("Saved {Kind} document {DocumentId} as v{Version}", draft.Kind, draft.DocumentId, nextVersion);
            return Result.Success(version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save document {DocumentId}", draft.DocumentId);
            return Result.Failure<DocumentVersion>($"Failed to save document: {ex.Message}");
        }
    }

    public async Task<Result<DocumentVersion>> GetVersionAsync(string documentId, int versionNumber, CancellationToken ct = default)
    {
        foreach (var kindName in Enum.GetNames<DocumentKind>())
        {
            var versionFile = Path.Combine(_root, kindName, SanitizeName(documentId), $"v{versionNumber:D3}", "version.json");
            if (File.Exists(versionFile))
            {
                var json = await File.ReadAllTextAsync(versionFile, ct).ConfigureAwait(false);
                var version = System.Text.Json.JsonSerializer.Deserialize<DocumentVersion>(json);
                return version is not null
                    ? Result.Success(version)
                    : Result.Failure<DocumentVersion>("Corrupted version.json");
            }
        }
        return Result.Failure<DocumentVersion>($"Version v{versionNumber:D3} of {documentId} not found.");
    }

    public async Task<Result<IReadOnlyList<DocumentVersion>>> GetVersionHistoryAsync(string documentId, CancellationToken ct = default)
    {
        var versions = new List<DocumentVersion>();
        foreach (var kindName in Enum.GetNames<DocumentKind>())
        {
            var docDir = Path.Combine(_root, kindName, SanitizeName(documentId));
            if (!Directory.Exists(docDir)) continue;

            foreach (var vDir in Directory.EnumerateDirectories(docDir).OrderBy(d => d))
            {
                ct.ThrowIfCancellationRequested();
                var versionFile = Path.Combine(vDir, "version.json");
                if (!File.Exists(versionFile)) continue;
                var json = await File.ReadAllTextAsync(versionFile, ct).ConfigureAwait(false);
                var v = System.Text.Json.JsonSerializer.Deserialize<DocumentVersion>(json);
                if (v is not null) versions.Add(v);
            }
        }
        return Result.Success<IReadOnlyList<DocumentVersion>>(versions.OrderBy(v => v.VersionNumber).ToList());
    }

    public async Task<Result<DocumentDiff>> DiffVersionsAsync(string documentId, int fromVersion, int toVersion, CancellationToken ct = default)
    {
        var from = await GetVersionAsync(documentId, fromVersion, ct).ConfigureAwait(false);
        if (from.IsFailure) return Result.Failure<DocumentDiff>(from.Error!);
        var to = await GetVersionAsync(documentId, toVersion, ct).ConfigureAwait(false);
        if (to.IsFailure) return Result.Failure<DocumentDiff>(to.Error!);

        if (from.Value!.ContentHash == to.Value!.ContentHash)
            return Result.Success(new DocumentDiff(from.Value.VersionId, to.Value.VersionId, true, 0, 0, 0));

        var fromPath = GetMarkdownPath(from.Value);
        var toPath = GetMarkdownPath(to.Value);

        if (!File.Exists(fromPath) || !File.Exists(toPath))
            return Result.Failure<DocumentDiff>("Source files not found for diff.");

        var fromLines = await File.ReadAllLinesAsync(fromPath, ct).ConfigureAwait(false);
        var toLines = await File.ReadAllLinesAsync(toPath, ct).ConfigureAwait(false);

        var (added, removed) = CountLineDelta(fromLines, toLines);
        return Result.Success(new DocumentDiff(from.Value.VersionId, to.Value.VersionId, false, added, removed, 0));
    }

    public async Task<Result<string>> ExportAsync(string versionId, ExportFormat format, string destinationPath, CancellationToken ct = default)
    {
        var versionFile = FindVersionFileById(versionId);
        if (versionFile is null)
            return Result.Failure<string>($"Version {versionId} not found.");

        var json = await File.ReadAllTextAsync(versionFile, ct).ConfigureAwait(false);
        var version = System.Text.Json.JsonSerializer.Deserialize<DocumentVersion>(json);
        if (version is null) return Result.Failure<string>("Corrupted version.json");

        var versionDir = Path.GetDirectoryName(versionFile)!;
        var ext = format switch { ExportFormat.Docx => "docx", ExportFormat.Html => "html", _ => "md" };
        var sourcePath = Path.Combine(versionDir, $"document.{ext}");

        if (!File.Exists(sourcePath))
            return Result.Failure<string>($"Export file for format {format} not found.");

        File.Copy(sourcePath, destinationPath, overwrite: true);
        return Result.Success(destinationPath);
    }

    private string GetMarkdownPath(DocumentVersion version)
    {
        var versionDir = Path.Combine(_root, version.Kind.ToString(), SanitizeName(version.DocumentId), $"v{version.VersionNumber:D3}");
        return Path.Combine(versionDir, "document.md");
    }

    private string? FindVersionFileById(string versionId)
    {
        foreach (var file in Directory.EnumerateFiles(_root, "version.json", SearchOption.AllDirectories))
        {
            var json = File.ReadAllText(file);
            if (json.Contains(versionId)) return file;
        }
        return null;
    }

    private static (int added, int removed) CountLineDelta(string[] from, string[] to)
    {
        var fromSet = new HashSet<string>(from);
        var toSet = new HashSet<string>(to);
        var added = to.Count(l => !fromSet.Contains(l));
        var removed = from.Count(l => !toSet.Contains(l));
        return (added, removed);
    }

    private static string SanitizeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
