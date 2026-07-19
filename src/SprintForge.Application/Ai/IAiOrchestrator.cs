using SprintForge.Domain.Common;

namespace SprintForge.Application.Ai;

/// <summary>
///   Routes AI requests to the configured default provider with automatic fallback.
///   Every prompt/completion pair is persisted via <see cref="Application.Audit.IAuditService"/>.
/// </summary>
public interface IAiOrchestrator
{
    /// <summary>Executes a prompt against the default (or named) provider, with fallback on transient failure.</summary>
    Task<Result<AiCompletion>> RunAsync(AiRequest request, string? overrideProviderId = null, CancellationToken ct = default);

    /// <summary>Returns all registered provider IDs and their health status.</summary>
    IReadOnlyList<AiProviderStatus> GetProviderStatuses();
}

/// <summary>Registry of all <see cref="IAiProvider"/> instances; resolved by the DI container.</summary>
public interface IAiProviderRegistry
{
    IAiProvider? GetProvider(string providerId);
    IReadOnlyList<IAiProvider> GetAll();
}

public sealed record AiProviderStatus(string ProviderId, string Vendor, bool IsAvailable, string? LastError);
