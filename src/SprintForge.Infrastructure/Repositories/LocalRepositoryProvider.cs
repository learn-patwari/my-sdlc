using Microsoft.Extensions.Logging;
using SprintForge.Application.Repositories;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Repositories;

/// <summary>
///   Scans a local root directory for Git repositories.
///   ProviderId = "local" — used when the working directory contains one or more cloned repos.
/// </summary>
public sealed class LocalRepositoryProvider : IRepositoryProvider
{
    private readonly string _rootPath;
    private readonly ILogger<LocalRepositoryProvider> _logger;

    public string ProviderId => "local";
    public string DisplayName => "Local File System";

    public LocalRepositoryProvider(string rootPath, ILogger<LocalRepositoryProvider> logger)
    {
        _rootPath = rootPath;
        _logger = logger;
    }

    public Task<Result<IReadOnlyList<RemoteRepository>>> ListRepositoriesAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_rootPath))
            return Task.FromResult(Result.Failure<IReadOnlyList<RemoteRepository>>($"Root path not found: {_rootPath}"));

        var repos = new List<RemoteRepository>();

        // The root itself may be a git repo, or subdirectories may be repos
        if (IsGitRepo(_rootPath))
            repos.Add(BuildRemoteRepository(_rootPath));

        foreach (var dir in Directory.EnumerateDirectories(_rootPath))
        {
            ct.ThrowIfCancellationRequested();
            if (IsGitRepo(dir))
                repos.Add(BuildRemoteRepository(dir));
        }

        _logger.LogInformation("Found {Count} local repositories under {Root}", repos.Count, _rootPath);
        return Task.FromResult(Result.Success<IReadOnlyList<RemoteRepository>>(repos));
    }

    public Task<Result<RemoteRepository>> GetRepositoryAsync(string repoPath, CancellationToken ct = default)
    {
        var absPath = Path.IsPathRooted(repoPath) ? repoPath : Path.Combine(_rootPath, repoPath);
        if (!Directory.Exists(absPath))
            return Task.FromResult(Result.Failure<RemoteRepository>($"Repository not found: {absPath}"));
        if (!IsGitRepo(absPath))
            return Task.FromResult(Result.Failure<RemoteRepository>($"Not a git repository: {absPath}"));
        return Task.FromResult(Result.Success(BuildRemoteRepository(absPath)));
    }

    public Task<Result<IReadOnlyList<RepoBranch>>> ListBranchesAsync(string repoPath, CancellationToken ct = default)
    {
        var absPath = Path.IsPathRooted(repoPath) ? repoPath : Path.Combine(_rootPath, repoPath);
        var refsHeads = Path.Combine(absPath, ".git", "refs", "heads");
        if (!Directory.Exists(refsHeads))
            return Task.FromResult(Result.Success<IReadOnlyList<RepoBranch>>([]));

        var currentBranch = ReadHeadBranch(absPath);
        var branches = new List<RepoBranch>();

        foreach (var file in Directory.EnumerateFiles(refsHeads, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetRelativePath(refsHeads, file).Replace(Path.DirectorySeparatorChar, '/');
            var sha = File.ReadAllText(file).Trim();
            var shortSha = sha.Length >= 7 ? sha[..7] : sha;
            branches.Add(new RepoBranch(name, shortSha, name == currentBranch, false));
        }

        return Task.FromResult(Result.Success<IReadOnlyList<RepoBranch>>(branches));
    }

    public Task<Result<IReadOnlyList<RepoCommit>>> GetRecentCommitsAsync(
        string repoPath, string branch, int limit = 20, CancellationToken ct = default)
    {
        // Pure filesystem commit reading is complex (pack files, etc.)
        // Return empty list rather than a partial/incorrect implementation
        _logger.LogDebug("GetRecentCommitsAsync for local repos requires git CLI; returning empty list");
        return Task.FromResult(Result.Success<IReadOnlyList<RepoCommit>>([]));
    }

    public Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
    {
        return Directory.Exists(_rootPath)
            ? Task.FromResult(Result.Success($"Local provider OK — root: {_rootPath}"))
            : Task.FromResult(Result.Failure<string>($"Root path does not exist: {_rootPath}"));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool IsGitRepo(string path) =>
        Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git"));

    private static RemoteRepository BuildRemoteRepository(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        var branch = ReadHeadBranch(path);
        return new RemoteRepository
        {
            FullPath = path,
            Name = name,
            Description = null,
            DefaultBranch = branch,
            CloneUrl = path,
            IsPrivate = false
        };
    }

    private static string ReadHeadBranch(string repoRoot)
    {
        var headFile = Path.Combine(repoRoot, ".git", "HEAD");
        if (!File.Exists(headFile)) return "main";
        var head = File.ReadAllText(headFile).Trim();
        return head.StartsWith("ref: refs/heads/", StringComparison.Ordinal)
            ? head["ref: refs/heads/".Length..]
            : "detached";
    }
}
