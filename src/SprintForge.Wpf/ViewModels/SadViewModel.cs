using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Documents;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed record ComponentNode(
    string Name,
    string NodeType,     // "gateway" | "service" | "layer" | "db" | "cache" | "messaging"
    int Depth,
    IReadOnlyList<ComponentNode> Children);

public sealed partial class SadViewModel : ObservableObject
{
    private readonly ISadService _sadService;

    [ObservableProperty] private string _activeTab = "Architecture";
    [ObservableProperty] private string _projectName = "Retail Banking Platform";
    [ObservableProperty] private string _serviceName = "Payment Service";
    [ObservableProperty] private string _documentPreview = string.Empty;
    [ObservableProperty] private string _statusMessage = "Select a component to view details.";
    [ObservableProperty] private ComponentNode? _selectedComponent;
    [ObservableProperty] private bool _isGenerating;

    public ObservableCollection<FlatNode> FlatTree { get; } = [];

    public SadViewModel(ISadService sadService)
    {
        _sadService = sadService;
        BuildDemoTree();
        BuildDemoDocument();
    }

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private async Task GenerateAsync()
    {
        IsGenerating = true;
        StatusMessage = "Generating SAD via AI…";
        try
        {
            await Task.Delay(500);
            StatusMessage = "AI provider not configured — showing demo architecture.";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void SelectNode(FlatNode node)
    {
        foreach (var n in FlatTree) n.IsSelected = false;
        node.IsSelected = true;
        StatusMessage = $"Selected: {node.Name} ({node.NodeType})";
    }

    private void BuildDemoTree()
    {
        FlatTree.Add(new FlatNode("retail-banking-platform", "root", 0));
        FlatTree.Add(new FlatNode("API Gateway", "gateway", 1, isExpanded: true));
        FlatTree.Add(new FlatNode("Payment Service", "service", 1, isExpanded: true, isHighlighted: true));
        FlatTree.Add(new FlatNode("Controller", "layer", 2));
        FlatTree.Add(new FlatNode("Service", "layer", 2));
        FlatTree.Add(new FlatNode("Repository", "layer", 2));
        FlatTree.Add(new FlatNode("Payment Processor", "service", 2));
        FlatTree.Add(new FlatNode("Adapter", "layer", 3));
        FlatTree.Add(new FlatNode("Account Service", "service", 1));
        FlatTree.Add(new FlatNode("Notification Service", "service", 1));
        FlatTree.Add(new FlatNode("Database", "db", 1));
        FlatTree.Add(new FlatNode("PostgreSQL", "db", 2));
        FlatTree.Add(new FlatNode("Redis Cache", "cache", 1));
        FlatTree.Add(new FlatNode("Kafka", "messaging", 1));
    }

    private void BuildDemoDocument()
    {
        DocumentPreview =
            "# SAD — Retail Banking Platform\n" +
            "**Service:** Payment Service  |  **Version:** 1.0\n\n" +
            "---\n\n" +
            "## 1. Overview\n" +
            "The Payment Service is responsible for processing fund transfers, " +
            "validating account balances, and publishing payment events to downstream consumers.\n\n" +
            "## 2. Architecture Diagram\n" +
            "See center panel for interactive diagram.\n\n" +
            "## 3. Components\n" +
            "- **PaymentController** — REST API entry point\n" +
            "- **PaymentService** — Core business logic\n" +
            "- **PaymentRepository** — Data access layer\n" +
            "- **PaymentProcessor** — External payment gateway adapter\n\n" +
            "## 4. Dependencies\n" +
            "- PostgreSQL (primary datastore)\n" +
            "- Redis (idempotency cache)\n" +
            "- Kafka (event streaming)";
    }
}

public sealed partial class FlatNode : ObservableObject
{
    public string Name { get; }
    public string NodeType { get; }
    public int Depth { get; }
    public bool IsExpanded { get; }
    public bool IsHighlighted { get; }
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

    public FlatNode(string name, string nodeType, int depth,
                    bool isExpanded = false, bool isHighlighted = false)
    {
        Name = name; NodeType = nodeType; Depth = depth;
        IsExpanded = isExpanded; IsHighlighted = isHighlighted;
    }
}
