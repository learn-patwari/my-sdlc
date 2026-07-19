using FluentAssertions;
using NSubstitute;
using SprintForge.Application.Ai;
using SprintForge.Application.Documents;
using SprintForge.Application.Sdlc;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Infrastructure.Documents;
using Xunit;

namespace SprintForge.Tests.Documents;

public sealed class SddGeneratorTests
{
    private static readonly AiCompletion SampleCompletion = new()
    {
        Content = "# 1. Overview\n\n## 1.1 Purpose\n\nPaymentService handles fund transfers.\n",
        ModelUsed = "claude-sonnet-5",
        ProviderId = "anthropic-default",
        RequestParametersJson = "{}"
    };

    private static SdlcIssue MakeSddIssue(string key, string serviceName) => new()
    {
        Key = key,
        Summary = $"SDD {serviceName}",
        Description = "Existing SDD document",
        Status = "Done",
        Assignee = "dev@example.com"
    };

    private static DocumentGenerationRequest MakeRequest(string service = "PaymentService") => new()
    {
        Kind = DocumentKind.Sdd,
        DocumentId = $"SDD-{service}-001",
        JiraProjectKey = "RBP",
        InputMarkdown = $"Service Name: {service}\n\n## Public API\n\n- processPayment(PaymentRequest): PaymentResult\n",
        TemplateId = "default-sdd",
        JiraIssueKey = "RBP-20",
        CorrelationId = Guid.NewGuid().ToString("N")
    };

    [Fact]
    public void Generator_Kind_IsSdd()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        var sdlc = Substitute.For<ISdlcTool>();
        new SddDocumentGenerator(ai, sdlc).Kind.Should().Be(DocumentKind.Sdd);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsSuccessDraft_WhenAiSucceeds()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        var sdlc = Substitute.For<ISdlcTool>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Success(SampleCompletion)));

        var generator = new SddDocumentGenerator(ai, sdlc);
        var result = await generator.GenerateAsync(MakeRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Kind.Should().Be(DocumentKind.Sdd);
        result.Value.ContentHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsFailure_WhenAiFails()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        var sdlc = Substitute.For<ISdlcTool>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Failure<AiCompletion>("Timeout")));

        var generator = new SddDocumentGenerator(ai, sdlc);
        var result = await generator.GenerateAsync(MakeRequest());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SearchExistingAsync_ReturnsMatchingIssues()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        var sdlc = Substitute.For<ISdlcTool>();
        sdlc.SearchIssuesAsync(Arg.Any<SdlcSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<SdlcIssue>>(
                [MakeSddIssue("RBP-55", "PaymentService"), MakeSddIssue("RBP-60", "OtherService")])));

        var generator = new SddDocumentGenerator(ai, sdlc);
        var result = await generator.SearchExistingAsync("PaymentService", ["RBP"]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value![0].JiraIssueKey.Should().Be("RBP-55");
    }

    [Fact]
    public async Task SearchExistingAsync_WhenJiraFails_ReturnsEmptyList()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        var sdlc = Substitute.For<ISdlcTool>();
        sdlc.SearchIssuesAsync(Arg.Any<SdlcSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<SdlcIssue>>("Jira unreachable")));

        var generator = new SddDocumentGenerator(ai, sdlc);
        var result = await generator.SearchExistingAsync("PaymentService", ["RBP"]);

        // Jira search failure must not block generation — returns empty list, not failure
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchExistingAsync_FiltersIssuesByServiceNameInSummary()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        var sdlc = Substitute.For<ISdlcTool>();
        sdlc.SearchIssuesAsync(Arg.Any<SdlcSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<SdlcIssue>>(
            [
                new SdlcIssue { Key = "RBP-1", Summary = "SDD PaymentService", Status = "Done" },
                new SdlcIssue { Key = "RBP-2", Summary = "Some unrelated ticket", Status = "Open" }
            ])));

        var generator = new SddDocumentGenerator(ai, sdlc);
        var result = await generator.SearchExistingAsync("PaymentService", ["RBP"]);

        result.Value!.Should().HaveCount(1);
        result.Value![0].JiraIssueKey.Should().Be("RBP-1");
    }

    [Fact]
    public async Task GenerateAsync_SystemPrompt_ContainsSddSections()
    {
        AiRequest? captured = null;
        var ai = Substitute.For<IAiOrchestrator>();
        var sdlc = Substitute.For<ISdlcTool>();
        ai.RunAsync(Arg.Do<AiRequest>(r => captured = r), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Success(SampleCompletion)));

        var generator = new SddDocumentGenerator(ai, sdlc);
        await generator.GenerateAsync(MakeRequest());

        captured!.SystemPrompt.Should().Contain("Software Design");
        captured.SystemPrompt.Should().Contain("TC-001");
    }
}
