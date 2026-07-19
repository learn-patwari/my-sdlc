using SprintForge.Domain.Common;
using SprintForge.Domain.Configuration;

namespace SprintForge.Application.Configuration;

/// <summary>
///   Loads, saves, and lists JSON profiles stored under <c>%APPDATA%\SprintForgeBeta\profiles\</c>.
///   Profile saves are write operations and MUST flow through the Approvals Center.
/// </summary>
public interface IProfileStore
{
    Task<Result<Profile>> LoadAsync(string profileId, CancellationToken ct = default);

    /// <summary>
    ///   Persists a profile. Callers must have already queued an approved audit entry.
    ///   Secrets are stored as DPAPI handles only — never plaintext.
    /// </summary>
    Task<Result<Profile>> SaveAsync(Profile profile, CancellationToken ct = default);

    Task<Result<bool>> DeleteAsync(string profileId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ProfileSummary>>> ListAsync(CancellationToken ct = default);

    /// <summary>Validates the profile against the published JSON Schema; returns all errors.</summary>
    Task<Result<IReadOnlyList<string>>> ValidateAsync(Profile profile, CancellationToken ct = default);

    /// <summary>Returns the currently active profile, or null if none has been selected.</summary>
    Profile? ActiveProfile { get; }

    /// <summary>Sets the active profile in memory without writing to disk.</summary>
    void SetActiveProfile(Profile profile);
}

public sealed record ProfileSummary(string ProfileId, string Name, string SchemaVersion, DateTimeOffset LastModifiedAt);
