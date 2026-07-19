using System.Text.Json;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Ai;
using SprintForge.Application.Audit;
using SprintForge.Application.Configuration;
using SprintForge.Application.Security;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Ai;

/// <summary>
///   Routes AI requests to the configured default provider with automatic fallback.
///   Every prompt/completion pair is persisted as an <see cref="AuditAction.AiPrompt"/> record.
///   Providers are instantiated on demand from the active profile — no stale config risk.
/// </summary>
public sealed class AiOrchestrator : IAiOrchestrator
{
    private readonly IProfileStore _profiles;
    private readonly ISecretStore _secrets;
    private readonly IAuditService _audit;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AiOrchestrator> _logger;
    private readonly ILogger<OpenAiProvider> _openAiLogger;
    private readonly ILogger<AnthropicAiProvider> _anthropicLogger;

    public AiOrchestrator(
        IProfileStore profiles,
        ISecretStore secrets,
        IAuditService audit,
        IHttpClientFactory httpFactory,
        ILogger<AiOrchestrator> logger,
        ILogger<OpenAiProvider> openAiLogger,
        ILogger<AnthropicAiProvider> anthropicLogger)
    {
        _profiles = profiles;
        _secrets = secrets;
        _audit = audit;
        _httpFactory = httpFactory;
        _logger = logger;
        _openAiLogger = openAiLogger;
        _anthropicLogger = anthropicLogger;
    }

    public async Task<Result<AiCompletion>> RunAsync(
        AiRequest request,
        string? overrideProviderId = null,
        CancellationToken ct = default)
    {
        var profile = _profiles.ActiveProfile;
        if (profile is null)
            return Result.Failure<AiCompletion>("No active profile. Load a profile before running AI requests.");

        var providerId = overrideProviderId ?? profile.Ai.DefaultProviderId;
        var provider = ResolveProvider(profile, providerId);
        if (provider is null)
            return Result.Failure<AiCompletion>($"AI provider '{providerId}' not found in active profile.");

        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString("N");
        var ctx = new AuditContext
        {
            AuditId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            UserName = Environment.UserName,
            MachineName = Environment.MachineName,
            Module = AuditModule.Ai,
            Action = AuditAction.AiPrompt,
            StartedAt = DateTimeOffset.UtcNow,
            InputsJson = JsonSerializer.Serialize(new
            {
                providerId,
                systemPrompt = request.SystemPrompt[..Math.Min(200, request.SystemPrompt.Length)] + "…",
                userPromptLength = request.UserPrompt.Length,
                temperature = request.Temperature,
                maxTokens = request.MaxTokens,
                seed = request.Seed
            })
        };

        var auditId = await _audit.RecordStartAsync(ctx, ct).ConfigureAwait(false);

        var result = await provider.CompleteAsync(request, ct).ConfigureAwait(false);

        if (result.IsFailure && profile.Ai.FallbackProviderId is not null && profile.Ai.FallbackProviderId != providerId)
        {
            _logger.LogWarning("Provider {ProviderId} failed ({Error}), trying fallback {FallbackId}.",
                providerId, result.Error, profile.Ai.FallbackProviderId);

            var fallback = ResolveProvider(profile, profile.Ai.FallbackProviderId);
            if (fallback is not null)
                result = await fallback.CompleteAsync(request, ct).ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            await _audit.RecordCompletedAsync(auditId,
                JsonSerializer.Serialize(new
                {
                    modelUsed = result.Value!.ModelUsed,
                    promptTokens = result.Value.PromptTokens,
                    completionTokens = result.Value.CompletionTokens
                }), ct).ConfigureAwait(false);
        }
        else
        {
            await _audit.RecordFailedAsync(auditId, new Exception(result.Error), ct).ConfigureAwait(false);
        }

        return result;
    }

    public IReadOnlyList<AiProviderStatus> GetProviderStatuses()
    {
        var profile = _profiles.ActiveProfile;
        if (profile is null) return [];
        return profile.Ai.Providers
            .Select(p => new AiProviderStatus(p.Id, p.Vendor, _secrets.Exists(p.ApiKeyRef), null))
            .ToList();
    }

    private IAiProvider? ResolveProvider(Domain.Configuration.Profile profile, string providerId)
    {
        var config = profile.Ai.Providers.FirstOrDefault(p => p.Id == providerId);
        if (config is null) return null;

        return config.Vendor.Equals("Anthropic", StringComparison.OrdinalIgnoreCase)
            ? new AnthropicAiProvider(config, _secrets, _httpFactory.CreateClient($"ai-{config.Id}"), _anthropicLogger)
            : new OpenAiProvider(config, _secrets, _httpFactory.CreateClient($"ai-{config.Id}"), _openAiLogger);
    }
}
