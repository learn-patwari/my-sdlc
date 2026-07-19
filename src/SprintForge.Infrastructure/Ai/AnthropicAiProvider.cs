using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Ai;
using SprintForge.Application.Security;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Ai;

/// <summary>
///   Anthropic Messages API adapter (claude-* models).
///   See https://docs.anthropic.com/en/api/messages for endpoint contract.
///   Credentials are resolved from <see cref="ISecretStore"/> on each call.
/// </summary>
public sealed class AnthropicAiProvider : IAiProvider
{
    private const string AnthropicVersion = "2023-06-01";
    private const string ApiKeyHeader = "x-api-key";
    private const string VersionHeader = "anthropic-version";

    private readonly Domain.Configuration.AiProvider _config;
    private readonly ISecretStore _secrets;
    private readonly HttpClient _http;
    private readonly ILogger<AnthropicAiProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ProviderId => _config.Id;
    public string Vendor => "Anthropic";

    public AnthropicAiProvider(
        Domain.Configuration.AiProvider config,
        ISecretStore secrets,
        HttpClient http,
        ILogger<AnthropicAiProvider> logger)
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
        SetHeaders(keyResult.Value!);

        var body = new AnthropicRequest
        {
            Model = _config.Model,
            System = request.SystemPrompt,
            Messages = [new AnthropicMessage { Role = "user", Content = request.UserPrompt }],
            MaxTokens = request.MaxTokens,
            Temperature = request.Temperature
        };

        try
        {
            var resp = await _http.PostAsJsonAsync("v1/messages", body, JsonOpts, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Result.Failure<AiCompletion>($"Anthropic error {(int)resp.StatusCode}: {err}");
            }

            var data = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOpts, ct).ConfigureAwait(false);
            var text = data?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (text is null)
                return Result.Failure<AiCompletion>("Anthropic returned no text content.");

            return Result.Success(new AiCompletion
            {
                Content = text,
                ModelUsed = data?.Model ?? _config.Model,
                ProviderId = ProviderId,
                PromptTokens = data?.Usage?.InputTokens,
                CompletionTokens = data?.Usage?.OutputTokens,
                RequestParametersJson = JsonSerializer.Serialize(new
                {
                    model = _config.Model,
                    temperature = request.Temperature,
                    max_tokens = request.MaxTokens
                })
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Anthropic completion failed for provider {ProviderId}.", ProviderId);
            return Result.Failure<AiCompletion>(ex.Message);
        }
    }

    public async Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
    {
        var keyResult = _secrets.Retrieve(_config.ApiKeyRef);
        if (keyResult.IsFailure) return Result.Failure<string>($"Cannot retrieve API key: {keyResult.Error}");

        _http.BaseAddress ??= new Uri(_config.Endpoint.TrimEnd('/') + '/');
        SetHeaders(keyResult.Value!);

        // Anthropic has no lightweight ping — send a minimal completion as a probe
        var probe = new AnthropicRequest
        {
            Model = _config.Model,
            Messages = [new AnthropicMessage { Role = "user", Content = "ping" }],
            MaxTokens = 1,
            Temperature = 0
        };
        try
        {
            var resp = await _http.PostAsJsonAsync("v1/messages", probe, JsonOpts, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode
                ? Result.Success($"Connected to Anthropic ({_config.Model})")
                : Result.Failure<string>($"Anthropic returned {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(ex.Message);
        }
    }

    private void SetHeaders(string apiKey)
    {
        _http.DefaultRequestHeaders.Remove(ApiKeyHeader);
        _http.DefaultRequestHeaders.Remove(VersionHeader);
        _http.DefaultRequestHeaders.Add(ApiKeyHeader, apiKey);
        _http.DefaultRequestHeaders.Add(VersionHeader, AnthropicVersion);
    }
}

// --- Request / response models ---

file sealed record AnthropicRequest
{
    [JsonPropertyName("model")] public required string Model { get; init; }
    [JsonPropertyName("system")] public string? System { get; init; }
    [JsonPropertyName("messages")] public required List<AnthropicMessage> Messages { get; init; }
    [JsonPropertyName("max_tokens")] public int MaxTokens { get; init; }
    [JsonPropertyName("temperature")] public double Temperature { get; init; }
}

file sealed record AnthropicMessage
{
    [JsonPropertyName("role")] public required string Role { get; init; }
    [JsonPropertyName("content")] public required string Content { get; init; }
}

file sealed record AnthropicResponse
{
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("content")] public List<AnthropicContentBlock>? Content { get; init; }
    [JsonPropertyName("usage")] public AnthropicUsage? Usage { get; init; }
}

file sealed record AnthropicContentBlock(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text);

file sealed record AnthropicUsage(
    [property: JsonPropertyName("input_tokens")] int? InputTokens,
    [property: JsonPropertyName("output_tokens")] int? OutputTokens);
