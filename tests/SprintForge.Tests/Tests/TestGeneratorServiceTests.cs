using FluentAssertions;
using NSubstitute;
using SprintForge.Application.Ai;
using SprintForge.Application.Approval;
using SprintForge.Application.Audit;
using SprintForge.Application.Documents;
using SprintForge.Application.Tests;
using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Domain.Tests;
using SprintForge.Infrastructure.Audit;
using SprintForge.Infrastructure.Tests;
using Microsoft.Extensions.Logging.Abstractions;

namespace SprintForge.Tests.Tests;

public sealed class TestGeneratorServiceTests
{
    private readonly IAiOrchestrator _ai = Substitute.For<IAiOrchestrator>();
    private readonly IApprovalGate _gate = Substitute.For<IApprovalGate>();
    private readonly IAuditService _auditSvc = Substitute.For<IAuditService>();
    private readonly IDocumentVersionStore _store = Substitute.For<IDocumentVersionStore>();

    private ITestGenerationService CreateSut()
    {
        var runner = new AuditedOperationRunner(_auditSvc, NullLogger<AuditedOperationRunner>.Instance);
        return new UnitTestGeneratorService(_ai, _gate, runner, _store);
    }

    private static AiCompletion MakeCompletion(string content) => new()
    {
        Content = content,
        ModelUsed = "claude-sonnet-4-6",
        ProviderId = "anthropic",
        RequestParametersJson = "{}"
    };

    private static TestGenerationRequest MakeRequest(TestFramework framework = TestFramework.XUnit) => new()
    {
        ServiceName = "PaymentService",
        Framework = framework,
        Language = TestingLanguage.CSharp,
        ServiceApiMarkdown = "public Task<Result<Receipt>> PayAsync(PaymentRequest req);",
        JiraProjectKey = "RBP",
        CorrelationId = Guid.NewGuid().ToString("N")
    };

    [Fact]
    public async Task GenerateTestsAsync_ValidAiResponse_ReturnsSuite()
    {
        var json = """
            [
              {
                "testName": "PayAsync_ValidRequest_ReturnsReceipt",
                "className": "PaymentServiceTests",
                "description": "Verifies that a valid payment request returns a receipt.",
                "scenario": "Given a valid payment request, When PayAsync is called, Then a receipt is returned",
                "priority": "High",
                "coveredMethods": ["PayAsync"],
                "sourceCode": "    [Fact]\n    public async Task PayAsync_ValidRequest_ReturnsReceipt() { }"
              }
            ]
            """;
        _ai.RunAsync(Arg.Any<AiRequest>(), ct: Arg.Any<CancellationToken>())
            .Returns(Result.Success(MakeCompletion(json)));

        var result = await CreateSut().GenerateTestsAsync(MakeRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.TestCases.Should().HaveCount(1);
        result.Value.TestCases[0].TestName.Should().Be("PayAsync_ValidRequest_ReturnsReceipt");
        result.Value.TestCases[0].Priority.Should().Be(TestPriority.High);
        result.Value.ServiceName.Should().Be("PaymentService");
        result.Value.Framework.Should().Be(TestFramework.XUnit);
    }

    [Fact]
    public async Task GenerateTestsAsync_AiFailure_ReturnsFailure()
    {
        _ai.RunAsync(Arg.Any<AiRequest>(), ct: Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AiCompletion>("AI provider unavailable"));

        var result = await CreateSut().GenerateTestsAsync(MakeRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AI test generation failed");
    }

    [Fact]
    public async Task GenerateTestsAsync_MalformedJson_ReturnsFallbackTestCase()
    {
        _ai.RunAsync(Arg.Any<AiRequest>(), ct: Arg.Any<CancellationToken>())
            .Returns(Result.Success(MakeCompletion("not json at all")));

        var result = await CreateSut().GenerateTestsAsync(MakeRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.TestCases.Should().HaveCount(1);
        result.Value.TestCases[0].TestId.Should().Be("TC-001");
        result.Value.TestCases[0].TestName.Should().Be("ShouldBeImplemented");
    }

    [Fact]
    public async Task SubmitForApprovalAsync_QueuesToApprovalGate()
    {
        var suite = new TestSuite
        {
            SuiteId = "tests-payment-abc",
            ServiceName = "PaymentService",
            Framework = TestFramework.XUnit,
            Language = TestingLanguage.CSharp,
            TestCases = [],
            GeneratedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
        _gate.QueueAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ApprovalRequest
            {
                ApprovalId = "ap-1",
                AuditId = "au-1",
                CorrelationId = suite.CorrelationId,
                ModuleTag = "UnitTests",
                OperationDescription = "test",
                ProposedValueJson = "{}",
                QueuedAt = DateTimeOffset.UtcNow
            }));

        var result = await CreateSut().SubmitForApprovalAsync(suite, suite.CorrelationId, "user1");

        result.IsSuccess.Should().BeTrue();
        result.Value!.ModuleTag.Should().Be("UnitTests");
        await _gate.Received(1).QueueAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PersistApprovedSuiteAsync_RejectedDecision_ReturnsFailure()
    {
        var suite = new TestSuite
        {
            SuiteId = "tests-x",
            ServiceName = "X",
            Framework = TestFramework.XUnit,
            Language = TestingLanguage.CSharp,
            TestCases = [],
            GeneratedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
        var decision = new ApprovalDecision
        {
            ApprovalId = "ap-1",
            Decision = ApprovalStatus.Rejected,
            DecidedAt = DateTimeOffset.UtcNow,
            DecidedByUser = "user1"
        };

        var result = await CreateSut().PersistApprovedSuiteAsync(decision, suite);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Rejected");
    }

    [Fact]
    public async Task PersistApprovedSuiteAsync_Approved_CallsWriteGate()
    {
        var suite = new TestSuite
        {
            SuiteId = "tests-pay",
            ServiceName = "PaymentService",
            Framework = TestFramework.XUnit,
            Language = TestingLanguage.CSharp,
            TestCases = [],
            GeneratedAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
        var decision = new ApprovalDecision
        {
            ApprovalId = "ap-1",
            Decision = ApprovalStatus.Approved,
            DecidedAt = DateTimeOffset.UtcNow,
            DecidedByUser = "user1"
        };

        _auditSvc.RecordStartAsync(Arg.Any<AuditContext>(), Arg.Any<CancellationToken>())
            .Returns("audit-001");
        _auditSvc.RecordCompletedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var storedVersion = new DocumentVersion
        {
            VersionId = "v1",
            DocumentId = suite.SuiteId,
            Kind = DocumentKind.Tests,
            VersionNumber = 1,
            ContentHash = "abc123",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUser = "user1",
            AuditId = "audit-001",
            AvailableFormats = [ExportFormat.Markdown],
            RelativePath = "Documents/Tests/tests-pay/v001/"
        };
        _store.SaveDraftAsync(Arg.Any<DocumentDraft>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(storedVersion));

        var result = await CreateSut().PersistApprovedSuiteAsync(decision, suite);

        result.IsSuccess.Should().BeTrue();
        await _auditSvc.Received(1).RecordStartAsync(Arg.Any<AuditContext>(), Arg.Any<CancellationToken>());
        await _store.Received(1).SaveDraftAsync(Arg.Any<DocumentDraft>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
