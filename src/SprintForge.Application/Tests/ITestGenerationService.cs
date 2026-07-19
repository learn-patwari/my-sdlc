using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Domain.Tests;

namespace SprintForge.Application.Tests;

/// <summary>
///   Orchestrates the full M7 Unit Test Generation workflow:
///   generate (AI, no write) → submit for approval (Approvals Center) → persist (write gate).
///
///   Supports C# (xUnit/NUnit), Java (JUnit 5), Python (pytest), TypeScript/JavaScript (Jest/Vitest).
///
///   INVARIANT: No test file is written until the user explicitly approves in the Approvals Center
///   and the write gate pre-records the audit entry successfully.
/// </summary>
public interface ITestGenerationService
{
    /// <summary>
    ///   Generates a test suite for the specified service using AI.
    ///   Returns a draft; no file is written yet.
    /// </summary>
    Task<Result<TestSuite>> GenerateTestsAsync(TestGenerationRequest request, CancellationToken ct = default);

    /// <summary>
    ///   Queues the test suite for approval in the Approvals Center.
    ///   Returns the queued ApprovalRequest; no file is written yet.
    /// </summary>
    Task<Result<ApprovalRequest>> SubmitForApprovalAsync(TestSuite suite, string correlationId, string userName, CancellationToken ct = default);

    /// <summary>
    ///   Persists the approved test suite to the document version store through the write gate.
    ///   Blocked if the user has not approved or if the audit sink is unavailable.
    /// </summary>
    Task<Result<DocumentVersion>> PersistApprovedSuiteAsync(ApprovalDecision decision, TestSuite suite, CancellationToken ct = default);
}

/// <summary>Parameters for test suite generation.</summary>
public sealed record TestGenerationRequest
{
    public required string ServiceName { get; init; }
    public required TestFramework Framework { get; init; }
    public required TestingLanguage Language { get; init; }

    /// <summary>
    ///   Source code snippets or API surface to generate tests for.
    ///   Typically extracted from the repository analyzer or SDD document.
    /// </summary>
    public required string ServiceApiMarkdown { get; init; }

    /// <summary>Optional SDD document markdown for richer context.</summary>
    public string? SddMarkdown { get; init; }

    /// <summary>Optional minimum coverage target (0–100). AI will aim to meet or exceed this.</summary>
    public double? TargetCoveragePercent { get; init; }

    public required string JiraProjectKey { get; init; }
    public string? JiraIssueKey { get; init; }
    public required string CorrelationId { get; init; }
}
