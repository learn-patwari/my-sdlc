using SprintForge.Domain.Common;

namespace SprintForge.Application.Ai;

/// <summary>Single AI vendor integration (OpenAI, Azure OpenAI, Anthropic, Gemini, Ollama).</summary>
public interface IAiProvider
{
    /// <summary>Matches the <c>Id</c> field in the profile's AI provider configuration.</summary>
    string ProviderId { get; }

    string Vendor { get; }

    /// <summary>Sends a prompt and returns the completion along with reproducibility metadata.</summary>
    Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct = default);

    /// <summary>Tests connectivity and authentication; returns error description on failure.</summary>
    Task<Result<string>> TestConnectionAsync(CancellationToken ct = default);
}

public sealed record AiRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public double Temperature { get; init; } = 0.2;
    public int MaxTokens { get; init; } = 8192;
    public int? Seed { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed record AiCompletion
{
    public required string Content { get; init; }
    public required string ModelUsed { get; init; }
    public required string ProviderId { get; init; }
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }

    /// <summary>Captured request parameters for audit reproducibility.</summary>
    public required string RequestParametersJson { get; init; }

    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}
