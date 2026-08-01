using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Documents;
using SprintForge.Application.Tests;
using System.Collections.ObjectModel;

namespace SprintForge.ViewModels;

public sealed record ServiceItem(string Name, bool IsHighlighted = false)
{
    public string LabelFgHex    => IsHighlighted ? "#7C3AED" : "#94A3B8";
    public string LabelFontWeight => IsHighlighted ? "SemiBold" : "Normal";
}

public sealed record TestCaseRow(
    string TestName,
    string Priority,    // "High" | "Medium" | "Low"
    string Status,      // "Passed" | "Failed" | "Skipped"
    string Coverage)    // "●" filled or "○" empty
{
    public string PriorityBgHex => Priority switch
    {
        "High"   => "#2D0707",
        "Medium" => "#2D2007",
        _        => "#1A2235"
    };
    public string PriorityFgHex => Priority switch
    {
        "High"   => "#F87171",
        "Medium" => "#FCD34D",
        _        => "#94A3B8"
    };
    public string StatusBgHex => Status switch
    {
        "Passed"  => "#052E16",
        "Failed"  => "#2D0707",
        _         => "#1A2235"
    };
    public string StatusFgHex => Status switch
    {
        "Passed"  => "#22C55E",
        "Failed"  => "#F87171",
        _         => "#94A3B8"
    };
}

public sealed partial class SddViewModel : ObservableObject
{
    private readonly ISddService _sddService;
    private readonly ITestGenerationService _testService;

    [ObservableProperty] private string _activeTab = "SDD";
    [ObservableProperty] private ServiceItem? _selectedService;
    [ObservableProperty] private ObservableCollection<TestCaseRow> _testCases = [];
    [ObservableProperty] private int    _coveragePercent;
    [ObservableProperty] private int    _linesCovered;
    [ObservableProperty] private int    _linesMissed;
    [ObservableProperty] private int    _linesTotal;
    [ObservableProperty] private string _sddPreview = string.Empty;
    [ObservableProperty] private string _statusMessage = "Select a service and click Generate.";
    [ObservableProperty] private bool   _isGenerating;

    public bool IsSddTabActive      => ActiveTab == "SDD";
    public bool IsUnitTestsTabActive => ActiveTab == "UnitTests";

    public ObservableCollection<ServiceItem> Services      { get; } = [];
    public ObservableCollection<string>      GeneratedFiles { get; } = [];

    public SddViewModel(ISddService sddService, ITestGenerationService testService)
    {
        _sddService  = sddService;
        _testService = testService;
    }

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsSddTabActive));
        OnPropertyChanged(nameof(IsUnitTestsTabActive));
    }

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private void SelectService(ServiceItem? service)
    {
        SelectedService = service;
        StatusMessage   = service is null ? "Select a service." : $"Selected: {service.Name}. Click Generate.";
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        IsGenerating  = true;
        StatusMessage = "Generating…";
        try
        {
            await Task.Delay(400);
            StatusMessage = "AI provider not configured — showing demo data.";
        }
        finally { IsGenerating = false; }
    }

    [RelayCommand]
    private async Task SubmitForApprovalAsync()
    {
        StatusMessage = "Queued in Approvals Center. Navigate to Approvals to review.";
        await Task.CompletedTask;
    }
}
