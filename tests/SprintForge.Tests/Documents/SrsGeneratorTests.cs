using FluentAssertions;
using NSubstitute;
using SprintForge.Application.Ai;
using SprintForge.Application.Documents;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Infrastructure.Documents;
using Xunit;

namespace SprintForge.Tests.Documents;

public sealed class SrsGeneratorTests
{
    private static readonly AiCompletion SampleCompletion = new()
    {
        Content = "# 1. Introduction\n\n## 1.1 Purpose\n\nThis SRS defines requirements.\n",
        ModelUsed = "claude-sonnet-5",
        ProviderId = "anthropic-default",
        RequestParametersJson = "{}"
    };

    private static DocumentGenerationRequest MakeRequest() => new()
    {
        Kind = DocumentKind.Srs,
        DocumentId = "PROJ-SRS-001",
        JiraProjectKey = "RBP",
        InputMarkdown = "## Requirements\n\n- FR-01: The system shall authenticate users.\n",
        TemplateId = "default-srs",
        JiraIssueKey = "RBP-42",
        CorrelationId = Guid.NewGuid().ToString("N")
    };

    [Fact]
    public async Task GenerateAsync_ReturnsSuccessDraft_WhenAiSucceeds()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Success(SampleCompletion)));

        var generator = new SrsDocumentGenerator(ai);
        var result = await generator.GenerateAsync(MakeRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Kind.Should().Be(DocumentKind.Srs);
        result.Value.ContentMarkdown.Should().Contain("1. Introduction");
        result.Value.ContentHash.Should().HaveLength(64, "SHA-256 hex digest is always 64 chars");
        result.Value.TemplateUsed.Should().Be("default-srs");
        result.Value.JiraIssueKey.Should().Be("RBP-42");
    }

    [Fact]
    public async Task GenerateAsync_ReturnsFailure_WhenAiFails()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Failure<AiCompletion>("AI provider timeout")));

        var generator = new SrsDocumentGenerator(ai);
        var result = await generator.GenerateAsync(MakeRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AI generation failed");
    }

    [Fact]
    public void Generator_Kind_IsSrs()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        new SrsDocumentGenerator(ai).Kind.Should().Be(DocumentKind.Srs);
    }

    [Fact]
    public async Task GenerateAsync_ContentHash_DifferentForDifferentContent()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(
              Task.FromResult(Result.Success(SampleCompletion with { Content = "Content A" })),
              Task.FromResult(Result.Success(SampleCompletion with { Content = "Content B" })));

        var generator = new SrsDocumentGenerator(ai);
        var r1 = await generator.GenerateAsync(MakeRequest());
        var r2 = await generator.GenerateAsync(MakeRequest());

        r1.Value!.ContentHash.Should().NotBe(r2.Value!.ContentHash);
    }

    [Fact]
    public async Task GenerateAsync_PassesUserPrompt_ContainingProjectKey()
    {
        AiRequest? capturedRequest = null;
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Do<AiRequest>(r => capturedRequest = r), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Success(SampleCompletion)));

        var generator = new SrsDocumentGenerator(ai);
        await generator.GenerateAsync(MakeRequest());

        capturedRequest.Should().NotBeNull();
        capturedRequest!.UserPrompt.Should().Contain("RBP");
        capturedRequest.SystemPrompt.Should().Contain("IEEE 830");
        capturedRequest.Temperature.Should().Be(0.2);
    }
}
