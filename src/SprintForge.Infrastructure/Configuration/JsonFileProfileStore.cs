using System.Text.Json;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Configuration;
using SprintForge.Domain.Common;
using SprintForge.Domain.Configuration;

namespace SprintForge.Infrastructure.Configuration;

/// <summary>
///   Stores profiles as JSON files at <c>{rootPath}/{profileId}.json</c>.
///   Profile saves are write operations — callers must have already cleared the Approvals Center.
///   Secrets are stored as DPAPI handles only; this class never touches plaintext values.
/// </summary>
public sealed class JsonFileProfileStore : IProfileStore
{
    private readonly string _rootPath;
    private readonly ILogger<JsonFileProfileStore> _logger;
    private Profile? _activeProfile;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonFileProfileStore(string rootPath, ILogger<JsonFileProfileStore> logger)
    {
        _rootPath = rootPath;
        _logger = logger;
        Directory.CreateDirectory(rootPath);
    }

    public Profile? ActiveProfile => _activeProfile;

    public void SetActiveProfile(Profile profile) => _activeProfile = profile;

    public async Task<Result<Profile>> LoadAsync(string profileId, CancellationToken ct = default)
    {
        var path = ProfilePath(profileId);
        if (!File.Exists(path))
            return Result.Failure<Profile>($"Profile '{profileId}' not found at {path}.");

        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var profile = JsonSerializer.Deserialize<Profile>(json, SerializerOptions);
            if (profile is null)
                return Result.Failure<Profile>($"Profile '{profileId}' deserialised as null.");

            _logger.LogInformation("Loaded profile {ProfileId} ({Name}).", profile.ProfileId, profile.Name);
            return Result.Success(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile {ProfileId}.", profileId);
            return Result.Failure<Profile>(ex.Message);
        }
    }

    public async Task<Result<Profile>> SaveAsync(Profile profile, CancellationToken ct = default)
    {
        AssertNoPlaintext(profile);
        var path = ProfilePath(profile.ProfileId);
        try
        {
            var json = JsonSerializer.Serialize(profile, SerializerOptions);
            await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
            _logger.LogInformation("Saved profile {ProfileId}.", profile.ProfileId);
            return Result.Success(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save profile {ProfileId}.", profile.ProfileId);
            return Result.Failure<Profile>(ex.Message);
        }
    }

    public Task<Result<bool>> DeleteAsync(string profileId, CancellationToken ct = default)
    {
        var path = ProfilePath(profileId);
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return Task.FromResult(Result.Success(true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<bool>(ex.Message));
        }
    }

    public Task<Result<IReadOnlyList<ProfileSummary>>> ListAsync(CancellationToken ct = default)
    {
        try
        {
            var summaries = Directory.GetFiles(_rootPath, "*.json")
                .Select(f =>
                {
                    try
                    {
                        var json = File.ReadAllText(f);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        return new ProfileSummary(
                            root.GetProperty("profileId").GetString() ?? Path.GetFileNameWithoutExtension(f),
                            root.GetProperty("name").GetString() ?? "Unknown",
                            root.TryGetProperty("schemaVersion", out var sv) ? sv.GetString() ?? "1.0" : "1.0",
                            root.TryGetProperty("lastModifiedAt", out var lm)
                                ? DateTimeOffset.Parse(lm.GetString()!)
                                : DateTimeOffset.MinValue);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(s => s is not null)
                .Cast<ProfileSummary>()
                .ToList();

            return Task.FromResult(Result.Success<IReadOnlyList<ProfileSummary>>(summaries));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<ProfileSummary>>(ex.Message));
        }
    }

    public Task<Result<IReadOnlyList<string>>> ValidateAsync(Profile profile, CancellationToken ct = default)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.ProfileId)) errors.Add("ProfileId is required.");
        if (string.IsNullOrWhiteSpace(profile.Name)) errors.Add("Name is required.");
        if (!profile.Jira.ApiTokenRef.StartsWith("dpapi:", StringComparison.Ordinal))
            errors.Add("Jira.ApiTokenRef must be a DPAPI handle (starts with 'dpapi:').");
        foreach (var p in profile.Ai.Providers)
        {
            if (!p.ApiKeyRef.StartsWith("dpapi:", StringComparison.Ordinal))
                errors.Add($"AI provider '{p.Id}' ApiKeyRef must be a DPAPI handle.");
        }
        return Task.FromResult(Result.Success<IReadOnlyList<string>>(errors));
    }

    private string ProfilePath(string profileId) => Path.Combine(_rootPath, $"{profileId}.json");

    private static void AssertNoPlaintext(Profile profile)
    {
        // Belt-and-suspenders guard: the store must never persist plaintext secrets.
        if (!profile.Jira.ApiTokenRef.StartsWith("dpapi:", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"BUG: Attempted to save profile with plaintext Jira token. ProfileId={profile.ProfileId}");

        foreach (var p in profile.Ai.Providers)
        {
            if (!p.ApiKeyRef.StartsWith("dpapi:", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"BUG: AI provider '{p.Id}' has plaintext ApiKeyRef. ProfileId={profile.ProfileId}");
        }
    }
}
