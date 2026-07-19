using SprintForge.Domain.Common;

namespace SprintForge.Application.Ai;

/// <summary>
///   Encapsulates a specific AI-driven generation task (SRS, SAD, SDD, tests, etc.).
///   Each agent owns its prompt templates and output parsing; the orchestrator handles routing.
/// </summary>
public interface IAiAgent<TInput, TOutput>
{
    string AgentId { get; }
    Task<Result<TOutput>> GenerateAsync(TInput input, string correlationId, CancellationToken ct = default);
}

/// <summary>A named, versioned prompt template stored under the working-directory templates folder.</summary>
public sealed record PromptTemplate
{
    public required string TemplateId { get; init; }
    public required string AgentId { get; init; }
    public required string Version { get; init; }

    /// <summary>Handlebars-style {{variable}} placeholders.</summary>
    public required string SystemPromptTemplate { get; init; }
    public required string UserPromptTemplate { get; init; }
}
