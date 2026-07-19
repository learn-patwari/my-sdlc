using Microsoft.Extensions.Logging;
using SprintForge.Application.Repositories;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Repositories;

/// <summary>
///   GitHub REST API v3 adapter for IRepositoryProvider.
///
///   Implementation status: stub — all methods throw NotImplementedException.
///   See docs/08-integrations.md §13 (Repository Integration) for the full design.
///   Planned implementation: Phase 5 (Foundation + Integrations core) in docs/15-roadmap.md.
/// </summary>
public sealed class GitHubRepositoryProvider : IRepositoryProvider
{
    // ReSharper disable once NotAccessedField.Local
    private readonly string _baseUrl;
    // ReSharper disable once NotAccessedField.Local
    private readonly ILogger<GitHubRepositoryProvider> _logger;

    public string ProviderId => "github";
    public string DisplayName => "GitHub";

    public GitHubRepositoryProvider(string baseUrl, ILogger<GitHubRepositoryProvider> logger)
    {
        _baseUrl = baseUrl;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<RemoteRepository>>> ListRepositoriesAsync(CancellationToken ct = default)
        => throw new NotImplementedException(
            "GitHub repository listing not yet implemented. See docs/08-integrations.md §13.");

    /// <inheritdoc/>
    public Task<Result<RemoteRepository>> GetRepositoryAsync(string repoPath, CancellationToken ct = default)
        => throw new NotImplementedException(
            "GitHub GetRepository not yet implemented. See docs/08-integrations.md §13.");

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<RepoBranch>>> ListBranchesAsync(string repoPath, CancellationToken ct = default)
        => throw new NotImplementedException(
            "GitHub ListBranches not yet implemented. See docs/08-integrations.md §13.");

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<RepoCommit>>> GetRecentCommitsAsync(
        string repoPath, string branch, int limit = 20, CancellationToken ct = default)
        => throw new NotImplementedException(
            "GitHub GetRecentCommits not yet implemented. See docs/08-integrations.md §13.");

    /// <inheritdoc/>
    public Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
        => throw new NotImplementedException(
            "GitHub TestConnection not yet implemented. See docs/08-integrations.md §13.");
}
