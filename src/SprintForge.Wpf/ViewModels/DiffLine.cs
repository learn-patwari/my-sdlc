namespace SprintForge.Wpf.ViewModels;

/// <summary>Display model for a single row in the SRS diff viewer.</summary>
public sealed record DiffLine(
    string ReqId,
    string Requirement,
    string Description,
    string ChangeType);   // "Added" | "Modified" | "Removed" | "Unchanged"
