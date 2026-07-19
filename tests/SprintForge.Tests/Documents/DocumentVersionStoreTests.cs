using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SprintForge.Application.Documents;
using SprintForge.Domain.Documents;
using SprintForge.Infrastructure.Documents;
using Xunit;

namespace SprintForge.Tests.Documents;

public sealed class DocumentVersionStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemDocumentVersionStore _store;

    public DocumentVersionStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spf-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new FileSystemDocumentVersionStore(_tempDir, NullLogger<FileSystemDocumentVersionStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static DocumentDraft MakeDraft(string? id = null) => new()
    {
        DocumentId = id ?? "TEST-001",
        Kind = DocumentKind.Srs,
        ContentMarkdown = "# Test SRS\n\n## 1. Introduction\n\nThis is a test document.\n",
        ContentHash = "abc123def456abc123def456abc123def456abc123def456abc123def456abc1",
        TemplateUsed = "default-srs",
        JiraIssueKey = "RBP-42",
        CorrelationId = Guid.NewGuid().ToString("N"),
        GeneratedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task SaveDraft_CreatesVersionDirectory()
    {
        var draft = MakeDraft();

        var result = await _store.SaveDraftAsync(draft, auditId: "audit-001", createdByUser: "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.VersionNumber.Should().Be(1);
        result.Value.DocumentId.Should().Be("TEST-001");
        result.Value.Kind.Should().Be(DocumentKind.Srs);
        result.Value.AuditId.Should().Be("audit-001");
        result.Value.CreatedByUser.Should().Be("tester");
        result.Value.AvailableFormats.Should().Contain(ExportFormat.Markdown);
        result.Value.AvailableFormats.Should().Contain(ExportFormat.Docx);
        result.Value.AvailableFormats.Should().Contain(ExportFormat.Html);
    }

    [Fact]
    public async Task SaveDraft_Twice_IncrementsVersionNumber()
    {
        var draft = MakeDraft();

        var v1 = await _store.SaveDraftAsync(draft, "audit-001", "tester");
        var v2 = await _store.SaveDraftAsync(draft, "audit-002", "tester");

        v1.IsSuccess.Should().BeTrue();
        v2.IsSuccess.Should().BeTrue();
        v1.Value!.VersionNumber.Should().Be(1);
        v2.Value!.VersionNumber.Should().Be(2);
    }

    [Fact]
    public async Task SaveDraft_WritesMarkdownAndDocxAndHtmlFiles()
    {
        var draft = MakeDraft();

        var result = await _store.SaveDraftAsync(draft, "audit-001", "tester");

        result.IsSuccess.Should().BeTrue();
        var versionDir = Path.Combine(_tempDir, "Documents", "Srs", "TEST-001", "v001");
        File.Exists(Path.Combine(versionDir, "document.md")).Should().BeTrue();
        File.Exists(Path.Combine(versionDir, "document.docx")).Should().BeTrue();
        File.Exists(Path.Combine(versionDir, "document.html")).Should().BeTrue();
        File.Exists(Path.Combine(versionDir, "version.json")).Should().BeTrue();
    }

    [Fact]
    public async Task GetVersionHistory_ReturnsAllVersionsOrdered()
    {
        var draft = MakeDraft();
        await _store.SaveDraftAsync(draft, "a1", "user1");
        await _store.SaveDraftAsync(draft, "a2", "user2");
        await _store.SaveDraftAsync(draft, "a3", "user3");

        var history = await _store.GetVersionHistoryAsync("TEST-001");

        history.IsSuccess.Should().BeTrue();
        history.Value!.Should().HaveCount(3);
        history.Value![0].VersionNumber.Should().Be(1);
        history.Value![2].VersionNumber.Should().Be(3);
    }

    [Fact]
    public async Task GetVersion_ReturnsCorrectVersion()
    {
        var draft = MakeDraft();
        await _store.SaveDraftAsync(draft, "a1", "tester");
        await _store.SaveDraftAsync(draft, "a2", "tester");

        var v2 = await _store.GetVersionAsync("TEST-001", 2);

        v2.IsSuccess.Should().BeTrue();
        v2.Value!.VersionNumber.Should().Be(2);
        v2.Value.AuditId.Should().Be("a2");
    }

    [Fact]
    public async Task GetVersion_NotFound_ReturnsFailure()
    {
        var result = await _store.GetVersionAsync("NONEXISTENT-999", 1);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DiffVersions_IdenticalContent_ReturnsIsIdenticalTrue()
    {
        var draft = MakeDraft();
        await _store.SaveDraftAsync(draft, "a1", "tester");
        await _store.SaveDraftAsync(draft, "a2", "tester");

        var diff = await _store.DiffVersionsAsync("TEST-001", 1, 2);

        diff.IsSuccess.Should().BeTrue();
        diff.Value!.IsIdentical.Should().BeTrue();
        diff.Value.AddedLines.Should().Be(0);
        diff.Value.RemovedLines.Should().Be(0);
    }

    [Fact]
    public async Task DiffVersions_DifferentContent_CountsLineDelta()
    {
        var draft1 = MakeDraft() with
        {
            ContentMarkdown = "# Version 1\n\nOriginal content.\n",
            ContentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        };
        var draft2 = MakeDraft() with
        {
            ContentMarkdown = "# Version 2\n\nChanged content.\n\nNew paragraph added.\n",
            ContentHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
        };

        await _store.SaveDraftAsync(draft1, "a1", "tester");
        await _store.SaveDraftAsync(draft2, "a2", "tester");

        var diff = await _store.DiffVersionsAsync("TEST-001", 1, 2);

        diff.IsSuccess.Should().BeTrue();
        diff.Value!.IsIdentical.Should().BeFalse();
        diff.Value.AddedLines.Should().BeGreaterThan(0);
        diff.Value.RemovedLines.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportAsync_Markdown_CopiesFileToDestination()
    {
        var draft = MakeDraft();
        var saved = await _store.SaveDraftAsync(draft, "a1", "tester");
        var destPath = Path.Combine(_tempDir, "exported.md");

        var result = await _store.ExportAsync(saved.Value!.VersionId, ExportFormat.Markdown, destPath);

        result.IsSuccess.Should().BeTrue();
        File.Exists(destPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(destPath);
        content.Should().Contain("Test SRS");
    }
}
