using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Documents;
using SprintForge.Application.Tests;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed record ServiceItem(string Name, bool IsHighlighted = false);

public sealed record TestCaseRow(
    string TestName,
    string Priority,     // "High" | "Medium" | "Low"
    string Status,       // "Passed" | "Failed" | "Skipped"
    string Coverage);    // "●" filled or "○" empty

public sealed partial class SddViewModel : ObservableObject
{
    private readonly ISddService _sddService;
    private readonly ITestGenerationService _testService;

    [ObservableProperty] private string _activeTab = "SDD";
    [ObservableProperty] private ServiceItem? _selectedService;
    [ObservableProperty] private ObservableCollection<TestCaseRow> _testCases = [];
    [ObservableProperty] private int _coveragePercent;
    [ObservableProperty] private int _linesCovered;
    [ObservableProperty] private int _linesMissed;
    [ObservableProperty] private int _linesTotal;
    [ObservableProperty] private string _sddPreview = string.Empty;
    [ObservableProperty] private string _statusMessage = "Select a service and click Generate.";
    [ObservableProperty] private bool _isGenerating;

    public ObservableCollection<ServiceItem> Services { get; } = [];
    public ObservableCollection<string> GeneratedFiles { get; } = [];

    public SddViewModel(ISddService sddService, ITestGenerationService testService)
    {
        _sddService = sddService;
        _testService = testService;
    }

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private void SelectService(ServiceItem? service)
    {
        SelectedService = service;
        StatusMessage = service is null ? "Select a service." : $"Selected: {service.Name}. Click Generate.";
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        IsGenerating = true;
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
