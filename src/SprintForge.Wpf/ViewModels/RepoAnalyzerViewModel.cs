using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Repositories;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed record ImpactedFileItem(string FileName, string Reason, double ImpactScore);

public sealed partial class RepoFileNode : ObservableObject
{
    public string Name { get; }
    public string Kind { get; }  // "root" | "folder" | "service"
    public int Depth { get; }
    public bool IsHighlighted { get; }
    [ObservableProperty] private bool _isSelected;

    public double IndentWidth => Depth * 16.0;
    public string Icon => Kind switch
    {
        "root"    => "▸",
        "service" => "□",
        "folder"  => "▾",
        _         => "·"
    };

    public RepoFileNode(string name, string kind, int depth, bool isHighlighted = false)
    {
        Name = name; Kind = kind; Depth = depth; IsHighlighted = isHighlighted;
    }
}

public sealed partial class RepoAnalyzerViewModel : ObservableObject
{
    private readonly IRepositoryAnalyzer _analyzer;

    [ObservableProperty] private string _repoName = string.Empty;
    [ObservableProperty] private string _branch = string.Empty;
    [ObservableProperty] private string _lastScanned = "Never";
    [ObservableProperty] private string _statusMessage = "Add a repository in Settings → Repositories, then click Scan.";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private RepoFileNode? _selectedNode;

    // Code insight metrics
    [ObservableProperty] private double _complexity;
    [ObservableProperty] private double _duplicationPct;
    [ObservableProperty] private string _coverageStatus = "—";
    [ObservableProperty] private double _techDebtDays;

    public ObservableCollection<RepoFileNode> FileTree { get; } = [];
    public ObservableCollection<ImpactedFileItem> ImpactedFiles { get; } = [];

    public RepoAnalyzerViewModel(IRepositoryAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    [RelayCommand]
    private void SelectNode(RepoFileNode node)
    {
        foreach (var n in FileTree) n.IsSelected = false;
        node.IsSelected = true;
        SelectedNode = node;
        StatusMessage = $"Selected: {node.Name}";
    }

    [RelayCommand]
    private async Task RescanAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning repository…";
        try
        {
            await Task.Delay(500);
            StatusMessage = "Repository provider not configured — showing demo data.";
        }
        finally { IsScanning = false; }
    }

    [RelayCommand]
    private void Pause() => StatusMessage = "Scan paused.";

}
