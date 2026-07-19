using SprintForge.Application.Ai;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Ai;

/// <summary>
///   Anthropic Claude adapter.
///   See docs/07-ai-orchestration.md for prompt strategy and reproducibility requirements.
///   Implementation target: Phase 2.
/// </summary>
public sealed class AnthropicAiProvider : IAiProvider
{
    public string ProviderId { get; }
    public string Vendor => "Anthropic";

    public AnthropicAiProvider(string providerId) => ProviderId = providerId;

    public Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/07-ai-orchestration.md — Anthropic adapter implementation is Phase 2.");

    public Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
        => throw new NotImplementedException("See docs/07-ai-orchestration.md — Anthropic adapter implementation is Phase 2.");
}
