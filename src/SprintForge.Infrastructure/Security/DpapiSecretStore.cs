using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Security;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Security;

/// <summary>
///   Encrypts secrets with Windows DPAPI (CurrentUser scope).
///   Ciphertext is stored as Base64 in a JSON index file under the app data directory.
///   Plaintext NEVER leaves this class — callers receive only the handle.
///
///   On non-Windows platforms this store throws <see cref="PlatformNotSupportedException"/> on any write —
///   the WPF host is Windows-only; the net8.0 Infrastructure project builds on Linux for CI but DPAPI
///   calls are gated at runtime.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private const string HandlePrefix = "dpapi:";
    private readonly string _indexPath;
    private readonly ILogger<DpapiSecretStore> _logger;
    private readonly object _fileLock = new();

    public DpapiSecretStore(string indexPath, ILogger<DpapiSecretStore> logger)
    {
        _indexPath = indexPath;
        _logger = logger;
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
    }

    public Result<string> Store(string handle, string plaintext)
    {
        if (!handle.StartsWith(HandlePrefix, StringComparison.Ordinal))
            return Result.Failure<string>($"Handle must start with '{HandlePrefix}'.");

        try
        {
            var ciphertext = Encrypt(plaintext);
            var index = LoadIndex();
            index[handle] = ciphertext;
            SaveIndex(index);
            _logger.LogInformation("Secret stored for handle {Handle}.", handle);
            return Result.Success(handle);
        }
        catch (PlatformNotSupportedException ex)
        {
            return Result.Failure<string>($"DPAPI not available on this platform: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store secret for handle {Handle}.", handle);
            return Result.Failure<string>($"Failed to store secret: {ex.Message}");
        }
    }

    public Result<string> Retrieve(string handle)
    {
        try
        {
            var index = LoadIndex();
            if (!index.TryGetValue(handle, out var ciphertext))
                throw new SecretNotFoundException(handle);

            return Result.Success(Decrypt(ciphertext));
        }
        catch (SecretNotFoundException)
        {
            return Result.Failure<string>($"Secret '{handle}' not found.");
        }
        catch (PlatformNotSupportedException ex)
        {
            return Result.Failure<string>($"DPAPI not available on this platform: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret for handle {Handle}.", handle);
            return Result.Failure<string>($"Failed to retrieve secret: {ex.Message}");
        }
    }

    public Result<bool> Delete(string handle)
    {
        try
        {
            var index = LoadIndex();
            var removed = index.Remove(handle);
            if (removed) SaveIndex(index);
            return Result.Success(removed);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>($"Failed to delete secret: {ex.Message}");
        }
    }

    public bool Exists(string handle) => LoadIndex().ContainsKey(handle);

    public IReadOnlyList<string> ListHandles() => LoadIndex().Keys.ToList();

    // --- internals ---

    private static string Encrypt(string plaintext)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");

        var data = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string Decrypt(string ciphertext)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");

        var data = Convert.FromBase64String(ciphertext);
        var decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    private Dictionary<string, string> LoadIndex()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_indexPath)) return [];
            var json = File.ReadAllText(_indexPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
    }

    private void SaveIndex(Dictionary<string, string> index)
    {
        lock (_fileLock)
        {
            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_indexPath, json, Encoding.UTF8);
        }
    }
}
