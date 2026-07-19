using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Sdlc;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed record JiraIssueRow(
    string Key,
    string Summary,
    string IssueType,   // "Epic" | "Story" | "Task" | "Bug"
    string Priority,    // "High" | "Medium" | "Low"
    string Status,      // "Done" | "In Progress" | "To Do" | "Blocked"
    string Assignee,
    int StoryPoints);

public sealed partial class JiraViewModel : ObservableObject
{
    private readonly ISdlcTool _sdlcTool;
    private readonly List<JiraIssueRow> _allIssues = [];

    [ObservableProperty] private string _projectKey = "RBP";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _activeSprint = "SPR-34";
    [ObservableProperty] private JiraIssueRow? _selectedIssue;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "Loaded demo data.";

    public ObservableCollection<JiraIssueRow> Issues { get; } = [];

    public JiraViewModel(ISdlcTool sdlcTool)
    {
        _sdlcTool = sdlcTool;
        LoadDemoData();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter(value);

    [RelayCommand]
    private void SelectIssue(JiraIssueRow? issue) => SelectedIssue = issue;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading from Jira…";
        try
        {
            var result = await _sdlcTool.SearchIssuesAsync(new SdlcSearchQuery
            {
                ProjectKeys = [ProjectKey],
                Status = null,
                MaxResults = 50
            });

            if (result.IsSuccess && result.Value is not null)
            {
                _allIssues.Clear();
                foreach (var issue in result.Value)
                    _allIssues.Add(new JiraIssueRow(
                        issue.Key, issue.Summary,
                        issue.IssueType ?? "Task",
                        issue.Priority ?? "Medium",
                        issue.Status,
                        issue.Assignee ?? "-",
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

    private void LoadDemoData()
    {
        _allIssues.Clear();
        _allIssues.Add(new JiraIssueRow("RB-100", "Fund Transfer Epic",          "Epic",  "High",   "In Progress", "-",           0));
        _allIssues.Add(new JiraIssueRow("RB-101", "Implement fund transfer",      "Story", "High",   "In Progress", "Rahul Sharma", 15));
        _allIssues.Add(new JiraIssueRow("RB-102", "Develop API",                  "Task",  "High",   "Done",        "Rahul Sharma", 5));
        _allIssues.Add(new JiraIssueRow("RB-103", "Develop API (FE)",             "Task",  "Medium", "In Progress", "Priya Singh",  5));
        _allIssues.Add(new JiraIssueRow("RB-104", "Unit Tests",                   "Task",  "Medium", "In Progress", "Neha Verma",   3));
        _allIssues.Add(new JiraIssueRow("RB-105", "Code Review",                  "Task",  "Low",    "To Do",       "Arjun Patel",  2));
        _allIssues.Add(new JiraIssueRow("RB-200", "Account Management Epic",      "Epic",  "High",   "In Progress", "-",            0));
        _allIssues.Add(new JiraIssueRow("RB-201", "Balance Enquiry API",          "Story", "High",   "To Do",       "-",            8));
        _allIssues.Add(new JiraIssueRow("RB-202", "REST endpoint",                "Task",  "Medium", "To Do",       "Rahul Sharma", 3));
        _allIssues.Add(new JiraIssueRow("RB-203", "DB query optimization",        "Task",  "Low",    "To Do",       "Neha Verma",   2));
        ApplyFilter(string.Empty);
    }
}
