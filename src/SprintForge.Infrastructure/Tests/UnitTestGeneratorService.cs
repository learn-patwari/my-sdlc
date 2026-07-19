using System.Text.Json;
using SprintForge.Application.Ai;
using SprintForge.Application.Approval;
using SprintForge.Application.Tests;
using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Domain.Tests;
using SprintForge.Infrastructure.Audit;

namespace SprintForge.Infrastructure.Tests;

/// <summary>
///   M7 Unit Test Generator — AI-powered test suite generation.
///   Generates test cases via AI, queues for approval, and persists through the write gate.
/// </summary>
public sealed class UnitTestGeneratorService : ITestGenerationService
{
    private readonly IAiOrchestrator _ai;
    private readonly IApprovalGate _approvalGate;
    private readonly AuditedOperationRunner _runner;
    private readonly Application.Documents.IDocumentVersionStore _store;

    public UnitTestGeneratorService(
        IAiOrchestrator ai,
        IApprovalGate approvalGate,
        AuditedOperationRunner runner,
        Application.Documents.IDocumentVersionStore store)
    {
        _ai = ai;
        _approvalGate = approvalGate;
        _runner = runner;
        _store = store;
    }

    public async Task<Result<TestSuite>> GenerateTestsAsync(TestGenerationRequest request, CancellationToken ct = default)
    {
        var aiRequest = new AiRequest
        {
            SystemPrompt = BuildSystemPrompt(request.Framework, request.Language),
            UserPrompt = BuildUserPrompt(request),
            Temperature = 0.2,
            MaxTokens = 16384,
            CorrelationId = request.CorrelationId
        };

        var completion = await _ai.RunAsync(aiRequest, ct: ct).ConfigureAwait(false);
        if (completion.IsFailure)
            return Result.Failure<TestSuite>($"AI test generation failed: {completion.Error}");

        var cases = ParseTestCasesFromAiResponse(completion.Value!.Content, request.Framework);
        var coverage = EstimateCoverage(cases);

        var suiteIdSuffix = Guid.NewGuid().ToString("N")[..8];
        var suite = new TestSuite
        {
            SuiteId = $"tests-{request.ServiceName.ToLowerInvariant().Replace(" ", "-")}-{suiteIdSuffix}",
            ServiceName = request.ServiceName,
            Framework = request.Framework,
            Language = request.Language,
            TestCases = cases,
            EstimatedCoveragePercent = coverage,
            GeneratedAt = DateTimeOffset.UtcNow,
            CorrelationId = request.CorrelationId
        };

        return Result.Success(suite);
    }

    public async Task<Result<ApprovalRequest>> SubmitForApprovalAsync(
        TestSuite suite,
        string correlationId,
        string userName,
        CancellationToken ct = default)
    {
        var request = new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid().ToString("N"),
            AuditId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            ModuleTag = "UnitTests",
            OperationDescription =
                $"Persist {suite.TestCases.Count} generated test cases for '{suite.ServiceName}' ({suite.Framework})",
            ProposedValueJson = JsonSerializer.Serialize(new
            {
                suite.SuiteId,
                suite.ServiceName,
                suite.Framework,
                suite.Language,
                TestCaseCount = suite.TestCases.Count,
                suite.EstimatedCoveragePercent
            }),
            AiExplanation =
                $"AI-generated {suite.Framework} test suite targeting {suite.EstimatedCoveragePercent:F0}% coverage. " +
                "Review the generated test cases and source code before approving.",
            QueuedAt = DateTimeOffset.UtcNow
        };

        var queued = await _approvalGate.QueueAsync(request, ct).ConfigureAwait(false);
        return Result.Success(queued);
    }

    public async Task<Result<DocumentVersion>> PersistApprovedSuiteAsync(
        ApprovalDecision decision,
        TestSuite suite,
        CancellationToken ct = default)
    {
        if (decision.Decision != ApprovalStatus.Approved)
            return Result.Failure<DocumentVersion>($"Cannot persist: approval status is {decision.Decision}.");

        var operation = new SaveTestSuiteOperation(_store, suite, decision.DecidedByUser);

        return await _runner.RunAsync(
            operation,
            suite.CorrelationId,
            decision.DecidedByUser,
            Environment.MachineName,
            ct).ConfigureAwait(false);
    }

    // ── AI prompt builders ────────────────────────────────────────────────────

    private static string BuildSystemPrompt(TestFramework framework, TestingLanguage language)
    {
        // Each case builds: preamble (interpolated) + JSON schema template (non-interpolated raw string)
        // so that JSON braces never conflict with C# interpolation syntax.
        const string jsonFooter = "\nReturn ONLY valid JSON array. No preamble or commentary.";

        return framework switch
        {
            TestFramework.XUnit or TestFramework.NUnit or TestFramework.MsTest =>
                $"You are a senior C# engineer writing {framework} unit tests following the AAA pattern (Arrange, Act, Assert).\n"
                + "Return a JSON array of test cases. Each object:\n"
                + """
                  {
                    "testName": "MethodName_Scenario_ExpectedResult",
                    "className": "ServiceNameTests",
                    "description": "One sentence describing what this test verifies.",
                    "scenario": "Given [...], When [...], Then [...]",
                    "priority": "High|Medium|Low",
                    "coveredMethods": ["MethodA", "MethodB"],
                    "sourceCode": "    [Fact]\n    public async Task TestName()\n    {\n        // Arrange\n        ...\n        // Assert\n    }"
                  }
                  """
                + jsonFooter,

            TestFramework.JUnit5 =>
                "You are a senior Java engineer writing JUnit 5 unit tests with Mockito.\nReturn a JSON array of test cases. Each object:\n"
                + """{"testName":"...", "className":"...Tests", "description":"...", "scenario":"...", "priority":"High|Medium|Low", "coveredMethods":[], "sourceCode":"    @Test\n    void testName() { ... }"}"""
                + jsonFooter,

            TestFramework.Pytest =>
                "You are a senior Python engineer writing pytest unit tests.\nReturn a JSON array of test cases. Each object:\n"
                + """{"testName":"test_...", "className":"TestClassName", "description":"...", "scenario":"...", "priority":"High|Medium|Low", "coveredMethods":[], "sourceCode":"    def test_name(...):\n        ..."}"""
                + jsonFooter,

            _ =>
                "You are a senior TypeScript engineer writing Jest/Vitest unit tests.\nReturn a JSON array of test cases. Each object:\n"
                + """{"testName":"...", "className":"ServiceTests", "description":"...", "scenario":"...", "priority":"High|Medium|Low", "coveredMethods":[], "sourceCode":"  it('...', () => { ... });"}"""
                + jsonFooter
        };
    }

    private static string BuildUserPrompt(TestGenerationRequest request)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Service: {request.ServiceName}");
        sb.AppendLine($"Framework: {request.Framework} / {request.Language}");
        if (request.TargetCoveragePercent.HasValue)
            sb.AppendLine($"Target coverage: {request.TargetCoveragePercent:F0}%");
        sb.AppendLine();
        sb.AppendLine("## API / Source");
        sb.AppendLine(request.ServiceApiMarkdown);
        if (request.SddMarkdown is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Design context (SDD excerpt)");
            sb.AppendLine(request.SddMarkdown.Length > 2000
                ? request.SddMarkdown[..2000] + "\n... [truncated]"
                : request.SddMarkdown);
        }
        return sb.ToString();
    }

    // ── Response parsing ──────────────────────────────────────────────────────

    private static IReadOnlyList<GeneratedTestCase> ParseTestCasesFromAiResponse(
        string json, TestFramework framework)
    {
        try
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start < 0 || end < 0) return FallbackTestCase(framework);

            using var doc = JsonDocument.Parse(json[start..(end + 1)]);
            var cases = new List<GeneratedTestCase>();
            int idx = 0;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var testName = element.TryGetProperty("testName", out var n) ? n.GetString() ?? $"Test{idx}" : $"Test{idx}";
                var className = element.TryGetProperty("className", out var c) ? c.GetString() ?? "GeneratedTests" : "GeneratedTests";
                var description = element.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                var scenario = element.TryGetProperty("scenario", out var s) ? s.GetString() : null;
                var priorityStr = element.TryGetProperty("priority", out var p) ? p.GetString() : "Medium";
                var sourceCode = element.TryGetProperty("sourceCode", out var sc) ? sc.GetString() ?? "" : "";

                var coveredMethods = element.TryGetProperty("coveredMethods", out var cm)
                    ? cm.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
                    : (IReadOnlyList<string>)[];

                var priority = priorityStr switch
                {
                    "High" => TestPriority.High,
                    "Low" => TestPriority.Low,
                    _ => TestPriority.Medium
                };

                cases.Add(new GeneratedTestCase
                {
                    TestId = $"TC-{++idx:D3}",
                    TestName = testName,
                    ClassName = className,
                    Description = description,
                    Scenario = scenario,
                    Priority = priority,
                    SourceCode = sourceCode,
                    CoveredMethods = coveredMethods
                });
            }

            return cases.Count > 0 ? cases : FallbackTestCase(framework);
        }
        catch
        {
            return FallbackTestCase(framework);
        }
    }

    private static IReadOnlyList<GeneratedTestCase> FallbackTestCase(TestFramework framework) =>
    [
        new GeneratedTestCase
        {
            TestId = "TC-001",
            TestName = "ShouldBeImplemented",
            ClassName = "GeneratedTests",
            Description = "Placeholder — AI response could not be parsed. Regenerate or enter test cases manually.",
            Priority = TestPriority.High,
            SourceCode = GetPlaceholderSource(framework),
            CoveredMethods = []
        }
    ];

    private static string GetPlaceholderSource(TestFramework framework) => framework switch
    {
        TestFramework.XUnit => "    [Fact]\n    public void ShouldBeImplemented()\n    {\n        // TODO: implement\n        Assert.True(false, \"Not implemented\");\n    }",
        TestFramework.NUnit => "    [Test]\n    public void ShouldBeImplemented()\n    {\n        Assert.Fail(\"Not implemented\");\n    }",
        TestFramework.JUnit5 => "    @Test\n    void shouldBeImplemented() {\n        fail(\"Not implemented\");\n    }",
        TestFramework.Pytest => "def test_should_be_implemented():\n    raise NotImplementedError(\"TODO\")",
        _ => "  it('should be implemented', () => { expect(true).toBe(false); });"
    };

    private static double EstimateCoverage(IReadOnlyList<GeneratedTestCase> cases)
    {
        if (cases.Count == 0) return 0;
        var methodCount = cases.Sum(c => c.CoveredMethods.Count);
        var uniqueMethods = cases.SelectMany(c => c.CoveredMethods).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return uniqueMethods == 0
            ? Math.Min(cases.Count * 8.0, 80.0)    // rough heuristic: 8% per test case
            : Math.Min(uniqueMethods * 15.0, 95.0); // 15% per unique covered method
    }
}
