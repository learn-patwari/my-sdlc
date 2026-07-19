namespace SprintForge.Application.Documents;

/// <summary>
///   Generates mxGraph XML (Draw.io format) natively — no cloud export, no external tools.
///   All geometry is computed from the DiagramSpec; the result is valid importable Draw.io XML.
/// </summary>
public interface IDrawioWriter
{
    /// <summary>Creates a fresh diagram from a spec and returns the complete mxGraph XML string.</summary>
    string CreateDiagram(DiagramSpec spec);
}

public sealed record DiagramSpec
{
    public required string Title { get; init; }
    public IReadOnlyList<DiagramComponent> Components { get; init; } = [];
    public IReadOnlyList<DiagramRelationship> Relationships { get; init; } = [];
}

public sealed record DiagramComponent
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public ComponentKind Kind { get; init; } = ComponentKind.Service;

    /// <summary>Optional technology hint shown as a sub-label (e.g., "PostgreSQL", "Redis").</summary>
    public string? Technology { get; init; }
}

public sealed record DiagramRelationship
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public string? Label { get; init; }
    public RelationshipStyle Style { get; init; } = RelationshipStyle.Sync;
}

public enum ComponentKind
{
    ApiGateway,
    Service,
    Database,
    Cache,
    MessageBroker,
    ExternalSystem,
    Client
}

public enum RelationshipStyle
{
    Sync,
    Async,
    Cache,
    Database
}
