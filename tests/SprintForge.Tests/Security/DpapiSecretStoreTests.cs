using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SprintForge.Infrastructure.Security;
using Xunit;

namespace SprintForge.Tests.Security;

public sealed class DpapiSecretStoreTests : IDisposable
{
    private readonly string _indexPath = Path.Combine(Path.GetTempPath(), $"dpapi-test-{Guid.NewGuid():N}.json");
    private readonly DpapiSecretStore _store;

    public DpapiSecretStoreTests()
        => _store = new DpapiSecretStore(_indexPath, NullLogger<DpapiSecretStore>.Instance);

    [Fact(DisplayName = "Store returns failure on non-Windows platforms", Skip = "Windows-only: run on a Windows CI agent.")]
    public void Store_OnNonWindows_ReturnsFailure_SkippedOnWindows()
    {
        // This test verifies behaviour when DPAPI is unavailable.
        // Run on Linux CI — DPAPI calls return Failure, not throw.
        var result = _store.Store("dpapi:test-handle", "super-secret");

        if (OperatingSystem.IsWindows())
            result.IsSuccess.Should().BeTrue("DPAPI available on Windows");
        else
            result.IsFailure.Should().BeTrue("DPAPI not available on non-Windows");
    }

    [Fact(DisplayName = "Round-trip store and retrieve returns original plaintext (Windows only)",
          Skip = "Windows-only DPAPI: run on a Windows CI agent.")]
    public void RoundTrip_StoreAndRetrieve_ReturnsOriginalPlaintext()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "Skipped — DPAPI unavailable on this platform.");
            return;
        }

        const string handle = "dpapi:test-roundtrip";
        const string secret = "my-super-secret-token-xyz";

        var storeResult = _store.Store(handle, secret);
        storeResult.IsSuccess.Should().BeTrue();

        var retrieveResult = _store.Retrieve(handle);
        retrieveResult.IsSuccess.Should().BeTrue();
        retrieveResult.Value.Should().Be(secret);
    }

    [Fact(DisplayName = "Handle must start with dpapi: prefix")]
    public void Store_WithInvalidHandle_ReturnsFailure()
    {
        var result = _store.Store("plain-handle", "secret");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("dpapi:");
    }

    [Fact(DisplayName = "Exists returns false for unknown handle")]
    public void Exists_UnknownHandle_ReturnsFalse()
        => _store.Exists("dpapi:ghost").Should().BeFalse();

    [Fact(DisplayName = "Retrieve non-existent handle returns failure (non-Windows: platform error)")]
    public void Retrieve_NonExistentHandle_ReturnsFailure()
    {
        var result = _store.Retrieve("dpapi:does-not-exist");
        result.IsFailure.Should().BeTrue();
    }

    public void Dispose()
    {
        if (File.Exists(_indexPath)) File.Delete(_indexPath);
    }
}
