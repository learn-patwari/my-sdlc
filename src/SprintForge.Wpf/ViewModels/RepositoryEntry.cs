namespace SprintForge.Wpf.ViewModels;

public sealed class RepositoryEntry
{
    public string Name      { get; set; } = "";
    public string Url       { get; set; } = "";
    public string Branch    { get; set; } = "main";
    public string TechStack { get; set; } = "";
    public string Type      { get; set; } = "GitHub";
}
