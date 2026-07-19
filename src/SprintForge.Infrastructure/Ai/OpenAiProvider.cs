using SprintForge.Application.Ai;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Ai;

/// <summary>
///   OpenAI / Azure OpenAI / Ollama (OpenAI-compatible) adapter.
///   See docs/07-ai-orchestration.md for design. Implementation target: Phase 2.
/// </summary>
public sealed class OpenAiProvider : IAiProvider
{
    public string ProviderId { get; }
    public string Vendor { get; }

    public OpenAiProvider(string providerId, string vendor)
    {
        ProviderId = providerId;
        Vendor = vendor;
    }

    public Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("See docs/07-ai-orchestration.md — OpenAI adapter implementation is Phase 2.");

    public Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
        => throw new NotImplementedException("See docs/07-ai-orchestration.md — OpenAI adapter implementation is Phase 2.");
}
