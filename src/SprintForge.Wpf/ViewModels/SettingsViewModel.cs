using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Configuration;
using SprintForge.Domain.Common;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IProfileStore _profileStore;

    [ObservableProperty]
    private string _jiraUrl = "https://jira.sdc.com";

    [ObservableProperty]
    private string _jiraUsername = "akshay.patwari@sdc.com";

    [ObservableProperty]
    private string _jiraApiToken = "";

    [ObservableProperty]
    private string _connectionStatus = "Not tested";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _activeTab = "Integrations";

    // General tab
    [ObservableProperty]
    private string _profileName = "Default";

    [ObservableProperty]
    private string _workingDirectory = @"C:\Users\akshay.patwari\AppData\Roaming\SprintForge";

    // Preferences tab
    [ObservableProperty]
    private string _minimumLogLevel = "Information";

    [ObservableProperty]
    private bool _autoSave = true;

    [ObservableProperty]
    private int _retentionDays = 365;

    public IReadOnlyList<string> LogLevels { get; } =
        ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    public SettingsViewModel(IProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    [RelayCommand]
    private void SetTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private async Task TestJiraConnection()
    {
        ConnectionStatus = "Testing...";
        IsConnected = false;
        await Task.Delay(2000);
        ConnectionStatus = "✓ Connected";
        IsConnected = true;
    }

    [RelayCommand]
    private void BrowseWorkingDirectory()
    {
        // Opens folder dialog on real implementation via platform service
    }

    [RelayCommand]
    private async Task SaveChanges()
    {
        await Task.Delay(500);
    }
}
