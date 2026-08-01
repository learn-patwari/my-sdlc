namespace SprintForge.ViewModels;

public sealed record DiffLine(string ReqId, string Requirement, string Description, string ChangeType)
{
    // "Added" | "Modified" | "Removed" | "Unchanged"
    public string RowBgHex => ChangeType switch
    {
        "Added"    => "#052E16",
        "Removed"  => "#2D0707",
        "Modified" => "#2D2007",
        _          => "Transparent"
    };

    public string BadgeBgHex => RowBgHex;

    public string BadgeFgHex => ChangeType switch
    {
        "Added"    => "#4ADE80",
        "Removed"  => "#F87171",
        "Modified" => "#FCD34D",
        _          => "#64748B"
    };
}
