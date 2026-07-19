using SprintForge.Domain.Common;

namespace SprintForge.Application.Security;

/// <summary>
///   Encrypts and decrypts secrets using Windows DPAPI (per-user scope).
///   Profiles reference secrets via opaque handles (e.g., <c>dpapi:jira-token-acme</c>).
///   Plaintext never touches disk or any log/audit output.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    ///   Encrypts <paramref name="plaintext"/> and stores it under <paramref name="handle"/>.
    ///   Returns the handle for storage in profiles.
    /// </summary>
    Result<string> Store(string handle, string plaintext);

    /// <summary>Retrieves and decrypts the secret for the given handle.</summary>
    Result<string> Retrieve(string handle);

    /// <summary>Permanently deletes the secret for the given handle.</summary>
    Result<bool> Delete(string handle);

    /// <summary>Returns true if a secret exists for the given handle.</summary>
    bool Exists(string handle);

    /// <summary>Returns all stored handles (never values).</summary>
    IReadOnlyList<string> ListHandles();
}
