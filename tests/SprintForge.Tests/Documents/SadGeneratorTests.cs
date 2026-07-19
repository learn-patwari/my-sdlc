using FluentAssertions;
using NSubstitute;
using SprintForge.Application.Ai;
using SprintForge.Application.Documents;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Infrastructure.Documents;
using Xunit;

namespace SprintForge.Tests.Documents;

public sealed class SadGeneratorTests
{
    private static readonly AiCompletion SampleCompletion = new()
    {
        Content = "# 1. Introduction\n\n## 1.1 Purpose\n\nThis SAD defines the architecture.\n",
        ModelUsed = "claude-sonnet-5",
        ProviderId = "anthropic-default",
        RequestParametersJson = "{}"
    };

    private static DocumentGenerationRequest MakeRequest() => new()
    {
        Kind = DocumentKind.Sad,
        DocumentId = "SAD-PROJ-001",
        JiraProjectKey = "RBP",
        InputMarkdown = "- Payment Service\n- Account Service\n- Notification Service\n- PostgreSQL Database\n",
        TemplateId = "default-sad",
        JiraIssueKey = "RBP-10",
        CorrelationId = Guid.NewGuid().ToString("N")
    };

    [Fact]
    public async Task GenerateAsync_ReturnsSuccessDraft_WhenAiSucceeds()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Success(SampleCompletion)));

        var generator = new SadDocumentGenerator(ai, new DrawioWriter());
        var result = await generator.GenerateAsync(MakeRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Kind.Should().Be(DocumentKind.Sad);
        result.Value.ContentMarkdown.Should().Contain("1. Introduction");
        result.Value.ContentHash.Should().HaveLength(64);
        result.Value.TemplateUsed.Should().Be("default-sad");
    }

    [Fact]
    public async Task GenerateAsync_IncludesDrawioXmlInAdditionalFiles()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Success(SampleCompletion)));

        var generator = new SadDocumentGenerator(ai, new DrawioWriter());
        var result = await generator.GenerateAsync(MakeRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.AdditionalFiles.Should().NotBeNull();
        result.Value.AdditionalFiles!.Should().ContainKey("architecture.drawio");
        result.Value.AdditionalFiles["architecture.drawio"].Should().Contain("<mxGraphModel");
    }

    [Fact]
    public async Task GenerateAsync_DrawioXml_ContainsServicesFromInputMarkdown()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Success(SampleCompletion)));

        var generator = new SadDocumentGenerator(ai, new DrawioWriter());
        var result = await generator.GenerateAsync(MakeRequest());

        var drawioXml = result.Value!.AdditionalFiles!["architecture.drawio"];
        drawioXml.Should().Contain("Payment Service");
    }

    [Fact]
    public async Task GenerateAsync_ReturnsFailure_WhenAiFails()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Any<AiRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Failure<AiCompletion>("Provider unavailable")));

        var generator = new SadDocumentGenerator(ai, new DrawioWriter());
        var result = await generator.GenerateAsync(MakeRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AI generation failed");
    }

    [Fact]
    public void Generator_Kind_IsSad()
    {
        var ai = Substitute.For<IAiOrchestrator>();
        new SadDocumentGenerator(ai, new DrawioWriter()).Kind.Should().Be(DocumentKind.Sad);
    }

    [Fact]
    public async Task GenerateAsync_SystemPrompt_MentionsArchitectureDocument()
    {
        AiRequest? captured = null;
        var ai = Substitute.For<IAiOrchestrator>();
        ai.RunAsync(Arg.Do<AiRequest>(r => captured = r), Arg.Any<string?>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromResult(Result.Success(SampleCompletion)));

        var generator = new SadDocumentGenerator(ai, new DrawioWriter());
        await generator.GenerateAsync(MakeRequest());

        captured!.SystemPrompt.Should().Contain("Software Architecture");
        captured.Temperature.Should().Be(0.2);
    }
}
