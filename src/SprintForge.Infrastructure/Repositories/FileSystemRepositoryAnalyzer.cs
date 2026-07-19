using Microsoft.Extensions.Logging;
using SprintForge.Application.Repositories;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Repositories;

/// <summary>
///   Walks a local repository file tree to identify services and build a dependency graph.
///   Supports Java, Python, TypeScript, and C# codebases.
///   No external tools required — pure file-system analysis via heuristic patterns.
/// </summary>
public sealed class FileSystemRepositoryAnalyzer : IRepositoryAnalyzer
{
    private readonly ILogger<FileSystemRepositoryAnalyzer> _logger;

    public FileSystemRepositoryAnalyzer(ILogger<FileSystemRepositoryAnalyzer> logger)
    {
        _logger = logger;
    }

    public Result<LocalRepository> Open(string localPath)
    {
        if (!Directory.Exists(localPath))
            return Result.Failure<LocalRepository>($"Repository path not found: {localPath}");

        var name = Path.GetFileName(localPath.TrimEnd(Path.DirectorySeparatorChar));
        var branch = DetectGitBranch(localPath);
        var headSha = DetectHeadSha(localPath);

        return Result.Success(new LocalRepository
        {
            RootPath = localPath,
            Name = name,
            Branch = branch,
            HeadCommitSha = headSha,
            LastScannedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<Result<DependencyGraph>> BuildDependencyGraphAsync(LocalRepository repo, CancellationToken ct = default)
    {
        _logger.LogInformation("Building dependency graph for {Repo}", repo.Name);

        var nodes = new List<DependencyNode>();
        var edges = new List<DependencyEdge>();

        var components = await DiscoverComponentsAsync(repo.RootPath, ct).ConfigureAwait(false);
        foreach (var comp in components)
        {
            nodes.Add(new DependencyNode(comp.Id, comp.Name, comp.Kind));
            foreach (var dep in comp.Dependencies)
            {
                var targetNode = components.FirstOrDefault(c => c.Name.Equals(dep, StringComparison.OrdinalIgnoreCase));
                if (targetNode is not null)
                    edges.Add(new DependencyEdge(comp.Id, targetNode.Id, null, false, false));
            }
        }

        return Result.Success(new DependencyGraph { Nodes = nodes, Edges = edges });
    }

    public async Task<Result<IReadOnlyList<CodeInsight>>> AnalyzeComplexityAsync(LocalRepository repo, CancellationToken ct = default)
    {
        _logger.LogInformation("Analyzing complexity for {Repo}", repo.Name);

        var insights = new List<CodeInsight>();
        var sourceFiles = GetSourceFiles(repo.RootPath);

        foreach (var file in sourceFiles.Take(200))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var lines = await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false);
                var complexity = EstimateCyclomaticComplexity(lines);
                var duplication = EstimateDuplication(lines);
                var debtDays = complexity > 10 ? Math.Round((complexity - 10) * 0.1, 1) : 0;

                insights.Add(new CodeInsight
                {
                    FilePath = Path.GetRelativePath(repo.RootPath, file),
                    CyclomaticComplexity = complexity,
                    DuplicationPercent = duplication,
                    TechnicalDebtDays = debtDays
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipped file {File}", file);
            }
        }

        return Result.Success<IReadOnlyList<CodeInsight>>(insights);
    }

    public async Task<Result<IReadOnlyList<ImpactedFile>>> FindImpactedFilesAsync(
        LocalRepository repo, IReadOnlyList<string> changedFiles, CancellationToken ct = default)
    {
        var impacted = new List<ImpactedFile>();
        var changed = changedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Extract class/symbol names from changed files
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in changedFiles)
        {
            var absPath = Path.IsPathRooted(file) ? file : Path.Combine(repo.RootPath, file);
            if (!File.Exists(absPath)) continue;
            var lines = await File.ReadAllLinesAsync(absPath, ct).ConfigureAwait(false);
            foreach (var sym in ExtractSymbolNames(lines, Path.GetExtension(file)))
                symbols.Add(sym);
        }

        // Scan all source files for references to changed symbols
        foreach (var file in GetSourceFiles(repo.RootPath))
        {
            ct.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(repo.RootPath, file);
            if (changed.Contains(relativePath)) continue;

            try
            {
                var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                var matchCount = symbols.Count(sym => content.Contains(sym, StringComparison.Ordinal));
                if (matchCount > 0)
                {
                    impacted.Add(new ImpactedFile(
                        relativePath,
                        $"References {matchCount} symbol(s) from changed files",
                        Math.Min(1.0, matchCount * 0.2)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipped {File}", file);
            }
        }

        return Result.Success<IReadOnlyList<ImpactedFile>>(
            impacted.OrderByDescending(f => f.ImpactScore).ToList());
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private record DiscoveredComponent(string Id, string Name, string Kind, IReadOnlyList<string> Dependencies);

    private static async Task<List<DiscoveredComponent>> DiscoverComponentsAsync(string root, CancellationToken ct)
    {
        var components = new List<DiscoveredComponent>();
        int idCounter = 0;

        foreach (var file in GetSourceFiles(root).Take(500))
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var lines = await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false);
            var componentName = IdentifyComponent(lines, ext, file);

            if (componentName is not null)
            {
                var deps = ExtractDependencies(lines, ext);
                components.Add(new DiscoveredComponent(
                    $"comp{++idCounter}",
                    componentName,
                    ClassifyKind(componentName, lines, ext),
                    deps));
            }
        }

        return components;
    }

    private static string? IdentifyComponent(string[] lines, string ext, string filePath)
    {
        // Java: @Service, @Controller, @Repository, @RestController
        if (ext == ".java")
        {
            var hasAnnotation = lines.Any(l =>
                l.Contains("@Service") || l.Contains("@Controller") ||
                l.Contains("@Repository") || l.Contains("@RestController") ||
                l.Contains("@Component"));
            if (hasAnnotation)
                return Path.GetFileNameWithoutExtension(filePath);
        }

        // C#: class XyzService, XyzController, XyzRepository
        if (ext == ".cs")
        {
            var classLine = lines.FirstOrDefault(l =>
                l.TrimStart().StartsWith("public") && l.Contains(" class "));
            if (classLine is not null)
            {
                var name = ExtractClassName(classLine);
                if (name is not null && (name.EndsWith("Service") || name.EndsWith("Controller") ||
                    name.EndsWith("Repository") || name.EndsWith("Handler") || name.EndsWith("Manager")))
                    return name;
            }
        }

        // Python: class XyzService
        if (ext == ".py")
        {
            var classLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("class "));
            if (classLine is not null)
            {
                var name = classLine.TrimStart()[6..].Split('(', ':')[0].Trim();
                if (name.EndsWith("Service") || name.EndsWith("Controller") || name.EndsWith("Repository"))
                    return name;
            }
        }

        // TypeScript: export class XyzService
        if (ext is ".ts" or ".tsx")
        {
            var classLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("export class "));
            if (classLine is not null)
            {
                var name = ExtractClassName(classLine);
                if (name is not null && (name.EndsWith("Service") || name.EndsWith("Controller") || name.EndsWith("Component")))
                    return name;
            }
        }

        return null;
    }

    private static string ClassifyKind(string name, string[] lines, string ext)
    {
        if (name.EndsWith("Controller") || name.EndsWith("Resource")) return "Controller";
        if (name.EndsWith("Repository") || name.EndsWith("Dao")) return "Repository";
        if (name.EndsWith("Service") || name.EndsWith("Manager")) return "Service";
        if (name.EndsWith("Handler") || name.EndsWith("Processor")) return "Handler";
        return "Component";
    }

    private static IReadOnlyList<string> ExtractDependencies(string[] lines, string ext)
    {
        var deps = new List<string>();

        if (ext == ".java")
        {
            // @Autowired or constructor injection field names
            foreach (var line in lines)
            {
                var m = System.Text.RegularExpressions.Regex.Match(line.Trim(),
                    @"private\s+(\w+Service|\w+Repository|\w+Client)\s+\w+");
                if (m.Success) deps.Add(m.Groups[1].Value);
            }
        }
        else if (ext == ".cs")
        {
            // Constructor parameter types
            foreach (var line in lines)
            {
                var m = System.Text.RegularExpressions.Regex.Match(line.Trim(),
                    @"I(\w+Service|I\w+Repository|\w+Client)\s+\w+");
                if (m.Success) deps.Add(m.Groups[1].Value);
            }
        }

        return deps;
    }

    private static IEnumerable<string> ExtractSymbolNames(string[] lines, string ext)
    {
        var symbols = new List<string>();
        foreach (var line in lines)
        {
            if (ext is ".java" or ".cs")
            {
                var m = System.Text.RegularExpressions.Regex.Match(line.Trim(), @"(?:class|interface|enum)\s+(\w+)");
                if (m.Success) symbols.Add(m.Groups[1].Value);
            }
            else if (ext == ".py")
            {
                var m = System.Text.RegularExpressions.Regex.Match(line.Trim(), @"(?:class|def)\s+(\w+)");
                if (m.Success) symbols.Add(m.Groups[1].Value);
            }
        }
        return symbols;
    }

    private static IEnumerable<string> GetSourceFiles(string root)
    {
        var extensions = new HashSet<string> { ".java", ".cs", ".py", ".ts", ".tsx", ".js" };
        var ignoreDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "node_modules", "bin", "obj", ".git", "target", "__pycache__", "dist", ".venv", "venv" };

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(seg => ignoreDirs.Contains(seg)));
    }

    private static double EstimateCyclomaticComplexity(string[] lines)
    {
        // Count branch keywords as a complexity proxy
        var keywords = new[] { " if ", " else ", " for ", " while ", " switch ", " case ", " catch ", " && ", " || " };
        return 1.0 + lines.Sum(l => keywords.Count(kw => l.Contains(kw, StringComparison.Ordinal)));
    }

    private static double EstimateDuplication(string[] lines)
    {
        if (lines.Length < 4) return 0;
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (nonEmpty.Count == 0) return 0;
        var distinct = nonEmpty.Distinct(StringComparer.Ordinal).Count();
        return Math.Round(100.0 * (1.0 - (double)distinct / nonEmpty.Count), 1);
    }

    private static string? ExtractClassName(string line)
    {
        var m = System.Text.RegularExpressions.Regex.Match(line, @"\bclass\s+(\w+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string DetectGitBranch(string root)
    {
        var headFile = Path.Combine(root, ".git", "HEAD");
        if (!File.Exists(headFile)) return "unknown";
        var head = File.ReadAllText(headFile).Trim();
        return head.StartsWith("ref: refs/heads/", StringComparison.Ordinal)
            ? head["ref: refs/heads/".Length..]
            : head[..7]; // detached HEAD — show short SHA
    }

    private static string DetectHeadSha(string root)
    {
        var headFile = Path.Combine(root, ".git", "HEAD");
        if (!File.Exists(headFile)) return "unknown";
        var head = File.ReadAllText(headFile).Trim();
        if (head.StartsWith("ref:", StringComparison.Ordinal))
        {
            var refPath = Path.Combine(root, ".git", head["ref: ".Length..].Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(refPath) ? File.ReadAllText(refPath).Trim()[..Math.Min(7, File.ReadAllText(refPath).Trim().Length)] : "unknown";
        }
        return head.Length >= 7 ? head[..7] : head;
    }
}
