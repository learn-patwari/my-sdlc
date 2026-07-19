using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SprintForge.Application.Repositories;
using SprintForge.Infrastructure.Repositories;

namespace SprintForge.Tests.Repositories;

public sealed class RepositoryAnalyzerTests : IDisposable
{
    private readonly string _tmpRoot = Path.Combine(Path.GetTempPath(), $"sf-repo-test-{Guid.NewGuid():N}");
    private readonly FileSystemRepositoryAnalyzer _analyzer = new(NullLogger<FileSystemRepositoryAnalyzer>.Instance);

    public RepositoryAnalyzerTests()
    {
        Directory.CreateDirectory(_tmpRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tmpRoot))
            Directory.Delete(_tmpRoot, recursive: true);
    }

    [Fact]
    public void Open_ValidDirectory_ReturnsRepository()
    {
        var result = _analyzer.Open(_tmpRoot);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RootPath.Should().Be(_tmpRoot);
        result.Value.Name.Should().NotBeEmpty();
    }

    [Fact]
    public void Open_NonExistentPath_ReturnsFailure()
    {
        var result = _analyzer.Open(Path.Combine(_tmpRoot, "does-not-exist"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task BuildDependencyGraphAsync_CSharpServiceFile_DetectsComponent()
    {
        File.WriteAllText(Path.Combine(_tmpRoot, "OrderService.cs"), """
            namespace App;
            public class OrderService
            {
                public void Process() {}
            }
            """);

        var repo = _analyzer.Open(_tmpRoot).Value!;
        var graphResult = await _analyzer.BuildDependencyGraphAsync(repo);

        graphResult.IsSuccess.Should().BeTrue();
        graphResult.Value!.Nodes.Should().Contain(n => n.Name == "OrderService");
    }

    [Fact]
    public async Task BuildDependencyGraphAsync_JavaAnnotatedService_DetectsComponent()
    {
        File.WriteAllText(Path.Combine(_tmpRoot, "PaymentService.java"), """
            @Service
            public class PaymentService {
                public void pay() {}
            }
            """);

        var repo = _analyzer.Open(_tmpRoot).Value!;
        var graphResult = await _analyzer.BuildDependencyGraphAsync(repo);

        graphResult.IsSuccess.Should().BeTrue();
        graphResult.Value!.Nodes.Should().Contain(n => n.Name == "PaymentService");
    }

    [Fact]
    public async Task AnalyzeComplexityAsync_SimpleFile_ReturnsSingleInsight()
    {
        File.WriteAllText(Path.Combine(_tmpRoot, "Helper.cs"), """
            namespace App;
            public class HelperService
            {
                public void Run()
                {
                    if (true) { } else { }
                }
            }
            """);

        var repo = _analyzer.Open(_tmpRoot).Value!;
        var result = await _analyzer.AnalyzeComplexityAsync(repo);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCountGreaterThan(0);
        var insight = result.Value![0];
        insight.CyclomaticComplexity.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public async Task FindImpactedFilesAsync_ReferenceToChangedSymbol_ReturnsImpactedFile()
    {
        // Create a service
        File.WriteAllText(Path.Combine(_tmpRoot, "CustomerService.cs"), """
            namespace App;
            public class CustomerService { }
            """);

        // Create a file that references the service
        File.WriteAllText(Path.Combine(_tmpRoot, "OrderController.cs"), """
            namespace App;
            public class OrderController
            {
                private readonly CustomerService _customerService;
            }
            """);

        var repo = _analyzer.Open(_tmpRoot).Value!;
        var changed = new[] { "CustomerService.cs" };
        var result = await _analyzer.FindImpactedFilesAsync(repo, changed);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().Contain(f => f.FilePath.Contains("OrderController.cs"));
    }

    [Fact]
    public async Task FindImpactedFilesAsync_NoSymbolMatch_ReturnsEmptyList()
    {
        File.WriteAllText(Path.Combine(_tmpRoot, "Alpha.cs"), """
            namespace App;
            public class AlphaService { }
            """);
        File.WriteAllText(Path.Combine(_tmpRoot, "Beta.cs"), """
            namespace App;
            public class BetaService { }
            """);

        var repo = _analyzer.Open(_tmpRoot).Value!;
        var result = await _analyzer.FindImpactedFilesAsync(repo, ["Alpha.cs"]);

        // Beta doesn't reference Alpha symbols
        result.Value!.Should().NotContain(f => f.FilePath.Contains("Beta.cs"));
    }

    [Fact]
    public async Task BuildDependencyGraphAsync_IgnoresNodeModulesAndBinDirs()
    {
        // Files in ignored dirs should not appear
        var nodeModules = Path.Combine(_tmpRoot, "node_modules");
        Directory.CreateDirectory(nodeModules);
        File.WriteAllText(Path.Combine(nodeModules, "FakeService.cs"), """
            public class NodeModuleService { }
            """);

        var repo = _analyzer.Open(_tmpRoot).Value!;
        var graph = await _analyzer.BuildDependencyGraphAsync(repo);

        graph.Value!.Nodes.Should().NotContain(n => n.Name == "NodeModuleService");
    }
}
