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

    public SettingsViewModel(IProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

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
    private async Task SaveChanges()
    {
        await Task.Delay(500);
    }
}
