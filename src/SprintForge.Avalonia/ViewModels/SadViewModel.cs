using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Documents;
using System.Collections.ObjectModel;

namespace SprintForge.Avalonia.ViewModels;

public sealed record ComponentNode(
    string Name,
    string NodeType,
    int    Depth,
    IReadOnlyList<ComponentNode> Children);

public sealed partial class FlatNode : ObservableObject
{
    public string Name         { get; }
    public string NodeType     { get; }
    public int    Depth        { get; }
    public bool   IsExpanded   { get; }
    public bool   IsHighlighted { get; }
    [ObservableProperty] private bool _isSelected;

    public double IndentWidth => Depth * 16.0;

    public string Icon => NodeType switch
    {
        "gateway"   => "⬡",
        "service"   => "□",
        "db"        => "◫",
        "cache"     => "⊡",
        "messaging" => "⊳",
        "root"      => "▸",
        _           => "·"
    };

    // Computed color properties (replace WPF DataTriggers)
    public string LabelFgHex     => IsHighlighted ? "#7C3AED" : IsSelected ? "#F1F5F9" : "#94A3B8";
    public string LabelFontWeight => IsHighlighted ? "SemiBold" : "Normal";

    public FlatNode(string name, string nodeType, int depth,
                    bool isExpanded = false, bool isHighlighted = false)
    {
        Name = name; NodeType = nodeType; Depth = depth;
        IsExpanded = isExpanded; IsHighlighted = isHighlighted;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(LabelFgHex));
    }
}

public sealed partial class SadViewModel : ObservableObject
{
    private readonly ISadService _sadService;

    [ObservableProperty] private string _activeTab       = "Architecture";
    [ObservableProperty] private string _projectName     = string.Empty;
    [ObservableProperty] private string _serviceName     = string.Empty;
    [ObservableProperty] private string _documentPreview = string.Empty;
    [ObservableProperty] private string _statusMessage   = "Click Generate SAD to analyse this service.";
    [ObservableProperty] private ComponentNode? _selectedComponent;
    [ObservableProperty] private bool   _isGenerating;

    public bool IsArchitectureTabActive => ActiveTab == "Architecture";
    public bool IsDocumentTabActive     => ActiveTab == "Document";

    public ObservableCollection<FlatNode> FlatTree { get; } = [];

    public SadViewModel(ISadService sadService)
    {
        _sadService = sadService;
    }

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsArchitectureTabActive));
        OnPropertyChanged(nameof(IsDocumentTabActive));
    }

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private async Task GenerateAsync()
    {
        IsGenerating  = true;
        StatusMessage = "Generating SAD via AI…";
        try
        {
            await Task.Delay(500);
            StatusMessage = "AI provider not configured — showing demo architecture.";
        }
        finally { IsGenerating = false; }
    }

    [RelayCommand]
    private void SelectNode(FlatNode node)
    {
        foreach (var n in FlatTree) n.IsSelected = false;
        node.IsSelected = true;
        StatusMessage   = $"Selected: {node.Name} ({node.NodeType})";
    }
}
