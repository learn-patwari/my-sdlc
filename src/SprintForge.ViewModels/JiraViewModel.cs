using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Sdlc;
using System.Collections.ObjectModel;

namespace SprintForge.ViewModels;

public sealed record JiraIssueRow(
    string Key,
    string Summary,
    string IssueType,   // "Epic" | "Story" | "Task" | "Bug"
    string Priority,    // "High" | "Medium" | "Low"
    string Status,      // "Done" | "In Progress" | "To Do" | "Blocked"
    string Assignee,
    int    StoryPoints)
{
    public string TypeBgHex => IssueType switch
    {
        "Epic"  => "#4C1D95",
        "Story" => "#2D1B69",
        "Bug"   => "#2D0707",
        _       => "#1A2235"
    };
    public string TypeFgHex => IssueType switch
    {
        "Epic"  => "#C4B5FD",
        "Story" => "#A78BFA",
        "Bug"   => "#F87171",
        _       => "#94A3B8"
    };
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
        "Done"        => "#052E16",
        "In Progress" => "#1e3a8a",
        "Blocked"     => "#2D0707",
        _             => "#1A2235"
    };
    public string StatusFgHex => Status switch
    {
        "Done"        => "#22C55E",
        "In Progress" => "#3B82F6",
        "Blocked"     => "#F87171",
        _             => "#64748B"
    };
    public string StoryPointsText => StoryPoints == 0 ? "-" : StoryPoints.ToString();
}

public sealed partial class JiraViewModel : ObservableObject
{
    private readonly ISdlcTool _sdlcTool;
    private readonly List<JiraIssueRow> _allIssues = [];

    [ObservableProperty] private string _projectKey   = string.Empty;
    [ObservableProperty] private string _searchText   = string.Empty;
    [ObservableProperty] private string _activeSprint = string.Empty;
    [ObservableProperty] private JiraIssueRow? _selectedIssue;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusMessage = "Configure Jira in Settings → Integrations, then click Refresh.";

    public ObservableCollection<JiraIssueRow> Issues { get; } = [];

    public JiraViewModel(ISdlcTool sdlcTool)
    {
        _sdlcTool = sdlcTool;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter(value);

    [RelayCommand]
    private void SelectIssue(JiraIssueRow? issue) => SelectedIssue = issue;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading     = true;
        StatusMessage = "Loading from Jira…";
        try
        {
            var result = await _sdlcTool.SearchIssuesAsync(new SdlcSearchQuery
            {
                ProjectKeys = [ProjectKey],
                Status      = null,
                MaxResults  = 50
            });

            if (result.IsSuccess && result.Value is not null)
            {
                _allIssues.Clear();
                foreach (var issue in result.Value)
                    _allIssues.Add(new JiraIssueRow(
                        issue.Key, issue.Summary,
                        issue.IssueType ?? "Task",
                        issue.Priority  ?? "Medium",
                        issue.Status,
                        issue.Assignee  ?? "-",
                        issue.StoryPoints ?? 0));
                ApplyFilter(SearchText);
                StatusMessage = $"Loaded {_allIssues.Count} issues.";
            }
            else
            {
                StatusMessage = $"Error: {result.Error}";
            }
        }
        catch (NotImplementedException)
        {
            StatusMessage = "Jira not configured — showing demo data.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void CreateIssue() =>
        StatusMessage = "Create Issue — queued to Approvals Center (configure Jira first).";

    private void ApplyFilter(string filter)
    {
        Issues.Clear();
        var src = string.IsNullOrWhiteSpace(filter)
            ? _allIssues
            : _allIssues.Where(i =>
                i.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                i.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                i.Assignee.Contains(filter, StringComparison.OrdinalIgnoreCase));
        foreach (var r in src) Issues.Add(r);
    }
}
