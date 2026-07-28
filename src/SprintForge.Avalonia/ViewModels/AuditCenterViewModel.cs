using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Audit;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;
using System.Collections.ObjectModel;

namespace SprintForge.Avalonia.ViewModels;

public sealed partial class AuditCenterViewModel : ObservableObject, IDisposable
{
    private readonly IAuditDashboardService _dashboard;
    private readonly CancellationTokenSource _cts = new();
    private List<AuditRecord> _allRecords = [];

    [ObservableProperty] private ObservableCollection<AuditRecord> _records = [];
    [ObservableProperty] private AuditRecord? _selectedRecord;
    [ObservableProperty] private string _searchText    = string.Empty;
    [ObservableProperty] private string _activeTab     = "Timeline";
    [ObservableProperty] private string _statusMessage = "Loading audit records…";
    [ObservableProperty] private bool   _isLoading;

    // Computed tab active states (replace WPF DataTriggers)
    public bool IsTimelineTabActive  => ActiveTab == "Timeline";
    public bool IsJiraTabActive      => ActiveTab == "Jira";
    public bool IsAiTabActive        => ActiveTab == "AI";
    public bool IsFilesTabActive     => ActiveTab == "Files";
    public bool IsApprovalsTabActive => ActiveTab == "Approvals";
    public bool IsSessionsTabActive  => ActiveTab == "Sessions";

    public AuditCenterViewModel(IAuditDashboardService dashboard)
    {
        _dashboard = dashboard;
        _ = LoadTabAsync("Timeline");
    }

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsTimelineTabActive));
        OnPropertyChanged(nameof(IsJiraTabActive));
        OnPropertyChanged(nameof(IsAiTabActive));
        OnPropertyChanged(nameof(IsFilesTabActive));
        OnPropertyChanged(nameof(IsApprovalsTabActive));
        OnPropertyChanged(nameof(IsSessionsTabActive));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter(value);

    [RelayCommand]
    private async Task SelectTabAsync(string tab)
    {
        ActiveTab = tab;
        await LoadTabAsync(tab);
    }

    [RelayCommand]
    private void SelectRecord(AuditRecord record) => SelectedRecord = record;

    [RelayCommand]
    private async Task RefreshAsync() => await LoadTabAsync(ActiveTab);

    private async Task LoadTabAsync(string tab)
    {
        IsLoading     = true;
        StatusMessage = $"Loading {tab}…";

        try
        {
            Result<IReadOnlyList<AuditRecord>> result = tab switch
            {
                "Jira"      => await _dashboard.GetJiraChangesAsync(pageSize: 100, ct: _cts.Token),
                "AI"        => await _dashboard.GetAiPromptsAsync(pageSize: 100, ct: _cts.Token),
                "Approvals" => await _dashboard.GetApprovalHistoryAsync(pageSize: 100, ct: _cts.Token),
                _           => await _dashboard.GetTimelineAsync(pageSize: 200, ct: _cts.Token)
            };

            if (result.IsSuccess && result.Value is not null)
            {
                _allRecords = [.. result.Value];
                ApplyFilter(SearchText);
                SelectedRecord = Records.FirstOrDefault();
                StatusMessage  = $"{_allRecords.Count} records";
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
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter(string filter)
    {
        Records.Clear();
        var src = string.IsNullOrWhiteSpace(filter)
            ? _allRecords
            : _allRecords.Where(r =>
                r.Module.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.Action.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.UserName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (r.JiraIssueKey?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                r.AuditId.Contains(filter, StringComparison.OrdinalIgnoreCase));
        foreach (var rec in src) Records.Add(rec);
    }

    public void Dispose() => _cts.Cancel();
}
