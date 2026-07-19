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

    [ObservableProperty] private string _repoName = "retail-banking-platform";
    [ObservableProperty] private string _branch = "main";
    [ObservableProperty] private string _lastScanned = "30 Jul 2026  10:30 AM";
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private RepoFileNode? _selectedNode;

    // Code insight metrics
    [ObservableProperty] private double _complexity = 12.4;
    [ObservableProperty] private double _duplicationPct = 8.2;
    [ObservableProperty] private string _coverageStatus = "Needs Improvement";
    [ObservableProperty] private double _techDebtDays = 2.6;

    public ObservableCollection<RepoFileNode> FileTree { get; } = [];
    public ObservableCollection<ImpactedFileItem> ImpactedFiles { get; } = [];

    public RepoAnalyzerViewModel(IRepositoryAnalyzer analyzer)
    {
        _analyzer = analyzer;
        LoadDemoData();
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

    private void LoadDemoData()
    {
        FileTree.Clear();
        FileTree.Add(new RepoFileNode("retail-banking-platform", "root", 0));
        FileTree.Add(new RepoFileNode("api-gateway",             "service", 1));
        FileTree.Add(new RepoFileNode("payment-service",         "service", 1, isHighlighted: true));
        FileTree.Add(new RepoFileNode("src/main/java",           "folder",  2));
        FileTree.Add(new RepoFileNode("PaymentController.java",  "folder",  3));
        FileTree.Add(new RepoFileNode("PaymentService.java",     "folder",  3));
        FileTree.Add(new RepoFileNode("account-service",         "service", 1));
        FileTree.Add(new RepoFileNode("notification-service",    "service", 1));
        FileTree.Add(new RepoFileNode("document-service",        "service", 1));

        ImpactedFiles.Clear();
        ImpactedFiles.Add(new ImpactedFileItem("PaymentService.java",         "Direct change",   1.0));
        ImpactedFiles.Add(new ImpactedFileItem("PaymentController.java",      "Direct change",   0.9));
        ImpactedFiles.Add(new ImpactedFileItem("PaymentRepository.java",      "Dependency",      0.8));
        ImpactedFiles.Add(new ImpactedFileItem("FundTransferService.java",    "Dependency",      0.7));
        ImpactedFiles.Add(new ImpactedFileItem("PaymentProcessor.java",       "Dependency",      0.7));
        ImpactedFiles.Add(new ImpactedFileItem("AccountService.java",         "Transitive dep",  0.5));
        ImpactedFiles.Add(new ImpactedFileItem("NotificationService.java",    "Transitive dep",  0.4));
        ImpactedFiles.Add(new ImpactedFileItem("PaymentValidationUtils.java", "Utility",         0.4));
        ImpactedFiles.Add(new ImpactedFileItem("TransactionMapper.java",      "Mapper",          0.3));
        ImpactedFiles.Add(new ImpactedFileItem("AuditEventPublisher.java",    "Event publisher", 0.3));
        ImpactedFiles.Add(new ImpactedFileItem("PaymentServiceTest.java",     "Test class",      0.2));
        ImpactedFiles.Add(new ImpactedFileItem("IntegrationTestBase.java",    "Test class",      0.2));
        ImpactedFiles.Add(new ImpactedFileItem("ApiGatewayConfig.java",       "Config",          0.1));
    }
}
