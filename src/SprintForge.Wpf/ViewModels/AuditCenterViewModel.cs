using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Audit;
using SprintForge.Domain.Audit;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class AuditCenterViewModel : ObservableObject
{
    private readonly IAuditDashboardService _dashboard;
    private IReadOnlyList<AuditRecord> _allRecords = [];

    [ObservableProperty]
    private ObservableCollection<AuditRecord> _records = [];

    [ObservableProperty]
    private AuditRecord? _selectedRecord;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _activeTab = "Timeline";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [RelayCommand]
    private void SelectRecord(AuditRecord? record)
    {
        SelectedRecord = record;
    }

    public AuditCenterViewModel(IAuditDashboardService dashboard)
    {
        _dashboard = dashboard;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public async Task LoadAsync()
    {
        try
        {
            StatusMessage = "Loading…";
            var result = await _dashboard.GetTimelineAsync(pageSize: 200);
            if (result.IsSuccess)
            {
                _allRecords = result.Value ?? [];
                ApplyFilter();
                SelectedRecord = Records.FirstOrDefault();
                StatusMessage = $"{_allRecords.Count} events";
            }
            else
            {
                StatusMessage = $"Error: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private void ShowTab(string tab)
    {
        ActiveTab = tab;
        _ = LoadTabAsync(tab);
    }

    private async Task LoadTabAsync(string tab)
    {
        try
        {
            StatusMessage = "Loading…";
            var result = tab switch
            {
                "Jira"      => await _dashboard.GetJiraChangesAsync(pageSize: 100),
                "AI Prompts"=> await _dashboard.GetAiPromptsAsync(pageSize: 100),
                "Approvals" => await _dashboard.GetApprovalHistoryAsync(100),
                "Rollbacks" => await _dashboard.GetRollbackHistoryAsync(100),
                _           => await _dashboard.GetTimelineAsync(pageSize: 200)
            };

            if (result.IsSuccess)
            {
                _allRecords = result.Value ?? [];
                ApplyFilter();
                SelectedRecord = Records.FirstOrDefault();
                StatusMessage = $"{_allRecords.Count} events";
            }
            else
            {
                StatusMessage = $"Error: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allRecords
            : _allRecords.Where(r =>
                r.Module.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.Action.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.UserName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (r.JiraIssueKey?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                r.AuditId.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        Records = new ObservableCollection<AuditRecord>(filtered);
    }
}
