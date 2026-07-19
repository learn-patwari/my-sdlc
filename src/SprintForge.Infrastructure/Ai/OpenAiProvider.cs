using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Ai;
using SprintForge.Application.Security;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Ai;

/// <summary>
///   OpenAI / Azure OpenAI / Ollama adapter (all use the OpenAI chat-completions endpoint shape).
///   Credentials are resolved from <see cref="ISecretStore"/> on each call — never cached in memory.
/// </summary>
public sealed class OpenAiProvider : IAiProvider
{
    private readonly Domain.Configuration.AiProvider _config;
    private readonly ISecretStore _secrets;
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ProviderId => _config.Id;
    public string Vendor => _config.Vendor;

    public OpenAiProvider(
        Domain.Configuration.AiProvider config,
        ISecretStore secrets,
        HttpClient http,
        ILogger<OpenAiProvider> logger)
    {
        _config = config;
        _secrets = secrets;
        _http = http;
        _logger = logger;
    }

    public async Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        var keyResult = _secrets.Retrieve(_config.ApiKeyRef);
        if (keyResult.IsFailure) return Result.Failure<AiCompletion>($"Cannot retrieve API key: {keyResult.Error}");

        _http.BaseAddress ??= new Uri(_config.Endpoint.TrimEnd('/') + '/');
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", keyResult.Value);

        var body = new OpenAiChatRequest
        {
            Model = _config.Model,
            Messages = [
                new OpenAiMessage { Role = "system", Content = request.SystemPrompt },
                new OpenAiMessage { Role = "user", Content = request.UserPrompt }
            ],
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Seed = request.Seed
        };

        try
        {
            var resp = await _http.PostAsJsonAsync("v1/chat/completions", body, JsonOpts, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Result.Failure<AiCompletion>($"OpenAI error {(int)resp.StatusCode}: {err}");
            }

            var data = await resp.Content.ReadFromJsonAsync<OpenAiChatResponse>(JsonOpts, ct).ConfigureAwait(false);
            if (data?.Choices is null || data.Choices.Count == 0)
                return Result.Failure<AiCompletion>("OpenAI returned empty choices.");

            return Result.Success(new AiCompletion
            {
                Content = data.Choices[0].Message.Content,
                ModelUsed = data.Model ?? _config.Model,
                ProviderId = ProviderId,
                PromptTokens = data.Usage?.PromptTokens,
                CompletionTokens = data.Usage?.CompletionTokens,
                RequestParametersJson = JsonSerializer.Serialize(new
                {
                    model = _config.Model,
                    temperature = request.Temperature,
                    max_tokens = request.MaxTokens,
                    seed = request.Seed
                })
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OpenAI completion failed for provider {ProviderId}.", ProviderId);
            return Result.Failure<AiCompletion>(ex.Message);
        }
    }

    public async Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
    {
        var keyResult = _secrets.Retrieve(_config.ApiKeyRef);
        if (keyResult.IsFailure) return Result.Failure<string>($"Cannot retrieve API key: {keyResult.Error}");

        _http.BaseAddress ??= new Uri(_config.Endpoint.TrimEnd('/') + '/');
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", keyResult.Value);

        try
        {
            var resp = await _http.GetAsync("v1/models", ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode
                ? Result.Success($"Connected to {_config.Vendor} ({_config.Endpoint})")
                : Result.Failure<string>($"{_config.Vendor} returned {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }
}

// --- Request / response models ---

file sealed record OpenAiChatRequest
{
    [JsonPropertyName("model")] public required string Model { get; init; }
    [JsonPropertyName("messages")] public required List<OpenAiMessage> Messages { get; init; }
    [JsonPropertyName("temperature")] public double Temperature { get; init; }
    [JsonPropertyName("max_tokens")] public int MaxTokens { get; init; }
    [JsonPropertyName("seed")] public int? Seed { get; init; }
}

file sealed record OpenAiMessage
{
    [JsonPropertyName("role")] public required string Role { get; init; }
    [JsonPropertyName("content")] public required string Content { get; init; }
}

file sealed record OpenAiChatResponse
{
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("choices")] public List<OpenAiChoice>? Choices { get; init; }
    [JsonPropertyName("usage")] public OpenAiUsage? Usage { get; init; }
}

file sealed record OpenAiChoice(
    [property: JsonPropertyName("message")] OpenAiMessage Message);

file sealed record OpenAiUsage(
    [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
    [property: JsonPropertyName("completion_tokens")] int? CompletionTokens);
