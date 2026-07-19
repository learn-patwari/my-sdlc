using SprintForge.Domain.Common;

namespace SprintForge.Application.Repositories;

/// <summary>Performs local analysis of a cloned repository: dependency graph, complexity, coverage estimation.</summary>
public interface IRepositoryAnalyzer
{
    /// <summary>Opens the local repository at the given path.</summary>
    Result<LocalRepository> Open(string localPath);

    Task<Result<DependencyGraph>> BuildDependencyGraphAsync(LocalRepository repo, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CodeInsight>>> AnalyzeComplexityAsync(LocalRepository repo, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ImpactedFile>>> FindImpactedFilesAsync(LocalRepository repo, IReadOnlyList<string> changedFiles, CancellationToken ct = default);
}

public sealed record LocalRepository
{
    public required string RootPath { get; init; }
    public required string Name { get; init; }
    public required string Branch { get; init; }
    public required string HeadCommitSha { get; init; }
    public required DateTimeOffset LastScannedAt { get; init; }
}

public sealed record DependencyGraph
{
    public required IReadOnlyList<DependencyNode> Nodes { get; init; }
    public required IReadOnlyList<DependencyEdge> Edges { get; init; }
}

public sealed record DependencyNode(string Id, string Name, string Kind);
public sealed record DependencyEdge(string FromId, string ToId, string? Label, bool IsAsync, bool IsExternal);

public sealed record CodeInsight
{
    public required string FilePath { get; init; }
    public required double CyclomaticComplexity { get; init; }
    public required double DuplicationPercent { get; init; }
    public double? TestCoveragePercent { get; init; }
    public required double TechnicalDebtDays { get; init; }
}

public sealed record ImpactedFile(string FilePath, string Reason, double ImpactScore);
