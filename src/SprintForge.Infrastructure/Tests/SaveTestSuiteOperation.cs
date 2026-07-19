using System.Text.Json;
using SprintForge.Application.Audit;
using SprintForge.Application.Documents;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Domain.Tests;

namespace SprintForge.Infrastructure.Tests;

/// <summary>
///   Audited write-gate operation that converts a TestSuite into a DocumentVersion
///   and persists it through the FileSystemDocumentVersionStore.
///   Must be executed via AuditedOperationRunner — never called directly.
/// </summary>
public sealed class SaveTestSuiteOperation : IAuditedOperation<DocumentVersion>
{
    private readonly IDocumentVersionStore _store;
    private readonly TestSuite _suite;
    private readonly string _createdByUser;

    public SaveTestSuiteOperation(IDocumentVersionStore store, TestSuite suite, string createdByUser)
    {
        _store = store;
        _suite = suite;
        _createdByUser = createdByUser;
    }

    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        Module = AuditModule.UnitTests,
        Action = AuditAction.TestGeneration,
        StartedAt = DateTimeOffset.UtcNow,
        UserName = userName,
        MachineName = machineName,
        InputsJson = JsonSerializer.Serialize(new
        {
            _suite.SuiteId,
            _suite.ServiceName,
            _suite.Framework,
            _suite.Language,
            TestCaseCount = _suite.TestCases.Count,
            _suite.EstimatedCoveragePercent
        })
    };

    public Task<Result<DocumentVersion>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
    {
        var draft = new DocumentDraft
        {
            DocumentId = _suite.SuiteId,
            Kind = DocumentKind.Tests,
            ContentMarkdown = BuildMarkdownSummary(_suite),
            ContentHash = ComputeHash(_suite),
            TemplateUsed = $"{_suite.Framework}/{_suite.Language}",
            CorrelationId = _suite.CorrelationId,
            GeneratedAt = _suite.GeneratedAt,
            AdditionalFiles = BuildTestFiles(_suite)
        };

        return _store.SaveDraftAsync(draft, ctx.AuditId, _createdByUser, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildMarkdownSummary(TestSuite suite)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Unit Tests — {suite.ServiceName}");
        sb.AppendLine();
        sb.AppendLine($"**Framework:** {suite.Framework}  ");
        sb.AppendLine($"**Language:** {suite.Language}  ");
        sb.AppendLine($"**Estimated Coverage:** {suite.EstimatedCoveragePercent:F1}%  ");
        sb.AppendLine($"**Generated:** {suite.GeneratedAt:yyyy-MM-dd HH:mm} UTC  ");
        sb.AppendLine();
        sb.AppendLine("## Test Cases");
        sb.AppendLine();

        foreach (var tc in suite.TestCases.OrderBy(t => t.Priority))
        {
            sb.AppendLine($"### {tc.TestName}");
            sb.AppendLine($"**Priority:** {tc.Priority}  ");
            sb.AppendLine($"**Class:** `{tc.ClassName}`  ");
            if (tc.Scenario is not null)
                sb.AppendLine($"**Scenario:** {tc.Scenario}  ");
            sb.AppendLine();
            sb.AppendLine(tc.Description);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static IReadOnlyDictionary<string, string> BuildTestFiles(TestSuite suite)
    {
        var files = new Dictionary<string, string>();
        var byClass = suite.TestCases.GroupBy(tc => tc.ClassName);

        foreach (var group in byClass)
        {
            var filename = GetTestFilename(group.Key, suite.Framework);
            var code = BuildTestFile(group.Key, group.ToList(), suite);
            files[filename] = code;
        }

        return files;
    }

    private static string GetTestFilename(string className, TestFramework framework) => framework switch
    {
        TestFramework.XUnit or TestFramework.NUnit or TestFramework.MsTest => $"{className}.cs",
        TestFramework.JUnit5 => $"{className}.java",
        TestFramework.Pytest => $"test_{ToSnakeCase(className)}.py",
        TestFramework.Jest or TestFramework.Vitest or TestFramework.Mocha => $"{className}.test.ts",
        _ => $"{className}.test.txt"
    };

    private static string BuildTestFile(string className, List<GeneratedTestCase> cases, TestSuite suite)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(GetFileHeader(className, suite));
        sb.AppendLine();
        foreach (var tc in cases)
            sb.AppendLine(tc.SourceCode);
        sb.AppendLine(GetFileFooter(suite.Framework));
        return sb.ToString();
    }

    private static string GetFileHeader(string className, TestSuite suite) => suite.Framework switch
    {
        TestFramework.XUnit => $"namespace {suite.ServiceName}.Tests;\n\npublic sealed class {className}\n{{",
        TestFramework.NUnit => $"namespace {suite.ServiceName}.Tests;\n\n[TestFixture]\npublic sealed class {className}\n{{",
        TestFramework.JUnit5 => $"import org.junit.jupiter.api.*;\n\nclass {className} {{",
        TestFramework.Pytest => $"import pytest\n\n",
        TestFramework.Jest => $"import {{ describe, it, expect }} from '@jest/globals';\n\ndescribe('{className}', () => {{",
        TestFramework.Vitest => $"import {{ describe, it, expect }} from 'vitest';\n\ndescribe('{className}', () => {{",
        _ => $"// {className}\n{{"
    };

    private static string GetFileFooter(TestFramework framework) => framework switch
    {
        TestFramework.Jest or TestFramework.Vitest => "});",
        TestFramework.JUnit5 => "}",
        TestFramework.Pytest => "",
        _ => "}"
    };

    private static string ToSnakeCase(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", "_$1").TrimStart('_').ToLowerInvariant();

    private static string ComputeHash(TestSuite suite)
    {
        var json = JsonSerializer.Serialize(new { suite.SuiteId, suite.ServiceName, Count = suite.TestCases.Count });
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }
}
