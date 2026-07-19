namespace SprintForge.Domain.Tests;

/// <summary>Supported unit testing frameworks and their target language.</summary>
public enum TestFramework
{
    XUnit,      // C# — default for this project
    NUnit,      // C#
    MsTest,     // C#
    JUnit5,     // Java
    Pytest,     // Python
    Jest,       // TypeScript / JavaScript
    Vitest,     // TypeScript (Vite-native)
    Mocha       // JavaScript
}

/// <summary>Language family of the service under test.</summary>
public enum TestingLanguage
{
    CSharp,
    Java,
    Python,
    TypeScript,
    JavaScript
}

/// <summary>A single generated test case ready to be written to a test file.</summary>
public sealed record GeneratedTestCase
{
    public required string TestId { get; init; }
    public required string TestName { get; init; }
    public required string Description { get; init; }

    /// <summary>BDD scenario this test validates (e.g. "Given a valid order, When Pay is called, Then a receipt is returned").</summary>
    public string? Scenario { get; init; }

    public required TestPriority Priority { get; init; }
    public required string SourceCode { get; init; }
    public required string ClassName { get; init; }

    /// <summary>Methods or APIs exercised by this test (for coverage reporting).</summary>
    public IReadOnlyList<string> CoveredMethods { get; init; } = [];
}

/// <summary>Priority band for generated test cases.</summary>
public enum TestPriority { High, Medium, Low }

/// <summary>A complete generated test suite for one service.</summary>
public sealed record TestSuite
{
    public required string SuiteId { get; init; }
    public required string ServiceName { get; init; }
    public required TestFramework Framework { get; init; }
    public required TestingLanguage Language { get; init; }
    public required IReadOnlyList<GeneratedTestCase> TestCases { get; init; }

    /// <summary>Estimated line coverage achievable with the generated tests (0–100).</summary>
    public double EstimatedCoveragePercent { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
    public required string CorrelationId { get; init; }
}
