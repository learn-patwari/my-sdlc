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
    [ObservableProperty] private int _coveragePercent = 88;
    [ObservableProperty] private int _linesCovered = 441;
    [ObservableProperty] private int _linesMissed = 59;
    [ObservableProperty] private int _linesTotal = 500;
    [ObservableProperty] private string _sddPreview = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isGenerating;

    public ObservableCollection<ServiceItem> Services { get; } =
    [
        new ServiceItem("Account Service"),
        new ServiceItem("Payment Service", IsHighlighted: true),
        new ServiceItem("Notification Service"),
        new ServiceItem("Audit Service"),
    ];

    public ObservableCollection<string> GeneratedFiles { get; } =
    [
        "payment-service-sdd.md",
        "payment-service-test-data.json",
        "payment-service-test-report.html",
    ];

    public SddViewModel(ISddService sddService, ITestGenerationService testService)
    {
        _sddService = sddService;
        _testService = testService;
        SelectedService = Services[1];
        LoadDemoData();
    }

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private void SelectService(ServiceItem? service)
    {
        SelectedService = service;
        LoadDemoData();
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

    private void LoadDemoData()
    {
        var svc = SelectedService?.Name ?? "Service";
        TestCases.Clear();
        TestCases.Add(new TestCaseRow("testHandlePayment_ValidRequest", "High", "Passed", "●"));
        TestCases.Add(new TestCaseRow("testHandleFundTransfer_InsufficientFunds", "High", "Passed", "●"));
        TestCases.Add(new TestCaseRow("testHandlePayment_NullRequest", "Medium", "Passed", "●"));
        TestCases.Add(new TestCaseRow("testValidateAmount_NegativeValue", "High", "Passed", "●"));
        TestCases.Add(new TestCaseRow("testProcessRefund_Unauthorized", "Medium", "Failed", "○"));
        TestCases.Add(new TestCaseRow("testGetTransaction_NotFound", "Low", "Passed", "●"));
        TestCases.Add(new TestCaseRow("testKafkaPublish_Retry", "Medium", "Skipped", "○"));

        SddPreview =
            $"# SDD — {svc}\n\n" +
            "## 1. Overview\n" +
            $"The {svc} implements the core business logic for its domain. " +
            "It follows a layered architecture (Controller → Service → Repository) " +
            "with strict separation of concerns.\n\n" +
            "## 2. Design Details\n" +
            "- **API Layer:** REST endpoints exposed via Spring Boot controllers\n" +
            "- **Service Layer:** Business rules and transaction management\n" +
            "- **Data Layer:** JPA repositories with PostgreSQL\n\n" +
            "## 3. Sequence Diagrams\n" +
            "See draw.io export in generated files.\n\n" +
            "## 4. Error Handling\n" +
            "All exceptions are mapped to RFC 7807 ProblemDetails responses.";

        StatusMessage = $"Loaded demo data for {svc}";
    }
}
