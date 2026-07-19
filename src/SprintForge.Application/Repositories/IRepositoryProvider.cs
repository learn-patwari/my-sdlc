using SprintForge.Domain.Common;

namespace SprintForge.Application.Repositories;

/// <summary>Source-control hosting provider (GitHub, GitLab, Bitbucket, or local).</summary>
public interface IRepositoryProvider
{
    string ProviderId { get; }
    string DisplayName { get; }

    Task<Result<IReadOnlyList<RemoteRepository>>> ListRepositoriesAsync(CancellationToken ct = default);
    Task<Result<RemoteRepository>> GetRepositoryAsync(string repoPath, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RepoBranch>>> ListBranchesAsync(string repoPath, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RepoCommit>>> GetRecentCommitsAsync(string repoPath, string branch, int limit = 20, CancellationToken ct = default);
    Task<Result<string>> TestConnectionAsync(CancellationToken ct = default);
}

public sealed record RemoteRepository
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string DefaultBranch { get; init; }
    public required string CloneUrl { get; init; }
    public bool IsPrivate { get; init; }
}

public sealed record RepoBranch(string Name, string HeadCommitSha, bool IsDefault, bool IsProtected);

public sealed record RepoCommit(string Sha, string Message, string Author, DateTimeOffset CommittedAt);
