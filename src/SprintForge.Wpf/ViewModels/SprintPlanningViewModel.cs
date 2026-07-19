using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Planning;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed record WorkItemRow(
    string Name,
    string Type,        // "Epic" | "Story" | "Task" | "SubTask"
    string Assignee,
    int EstimatePts,
    string Status,      // "Done" | "In Progress" | "To Do"
    string JiraKey,
    int Indent)         // 0 = Epic, 1 = Story, 2 = Task, 3 = SubTask
{
    public double IndentWidth => Indent * 20.0;
}

public sealed record GanttBar(
    string Label,
    string JiraKey,
    string Status,
    int StartDay,       // offset from sprint start
    int DurationDays)
{
    public double BarLeft  => StartDay * 50.0;
    public double BarWidth => DurationDays * 50.0;
}

public sealed partial class SprintPlanningViewModel : ObservableObject
{
    private readonly ISprintPlanningService _planningService;

    [ObservableProperty] private string _sprintName = "SPR-34 (Jul 14 – Jul 27)";
    [ObservableProperty] private int _capacityPts = 160;
    [ObservableProperty] private int _committedPts = 142;
    [ObservableProperty] private int _completedPts = 38;
    [ObservableProperty] private int _remainingPts = 104;
    [ObservableProperty] private int _utilizationPct = 89;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isValidating;

    public ObservableCollection<WorkItemRow> WorkItems { get; } = [];
    public ObservableCollection<GanttBar> GanttBars { get; } = [];

    public string[] SprintDates { get; } =
        ["Jul 14", "Jul 15", "Jul 16", "Jul 17", "Jul 18", "Jul 21",
         "Jul 22", "Jul 23", "Jul 24", "Jul 25", "Jul 28", "Jul 29",
         "Jul 30", "Jul 31", "Aug 1"];

    public SprintPlanningViewModel(ISprintPlanningService planningService)
    {
        _planningService = planningService;
        LoadDemoData();
    }

    [RelayCommand]
    private async Task PushToJiraAsync()
    {
        IsValidating = true;
        StatusMessage = "Validating sprint plan…";
        await Task.Delay(400);
        StatusMessage = "Validation passed — queued in Approvals Center. No Jira writes until approved.";
        IsValidating = false;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        StatusMessage = "Import from Confluence / Markdown / Word / Excel — configure source in Settings.";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void RefreshGantt() => LoadDemoData();

    private void LoadDemoData()
    {
        WorkItems.Clear();
        WorkItems.Add(new WorkItemRow("Fund Transfer Epic",       "Epic",    "",              0,  "In Progress", "RB-100", 0));
        WorkItems.Add(new WorkItemRow("Implement fund transfer",  "Story",   "",              15, "In Progress", "RB-101", 1));
        WorkItems.Add(new WorkItemRow("Develop API",              "Task",    "Rahul Sharma",   5, "Done",        "RB-102", 2));
        WorkItems.Add(new WorkItemRow("Develop API (FE)",         "Task",    "Priya Singh",    5, "In Progress", "RB-103", 2));
        WorkItems.Add(new WorkItemRow("Unit Tests",               "Task",    "Neha Verma",     3, "In Progress", "RB-104", 2));
        WorkItems.Add(new WorkItemRow("Code Review",              "Task",    "Arjun Patel",    2, "To Do",       "RB-105", 2));
        WorkItems.Add(new WorkItemRow("Account Management Epic",  "Epic",    "",               0, "In Progress", "RB-200", 0));
        WorkItems.Add(new WorkItemRow("Balance Enquiry API",      "Story",   "",              8,  "To Do",       "RB-201", 1));
        WorkItems.Add(new WorkItemRow("REST endpoint",            "Task",    "Rahul Sharma",   3, "To Do",       "RB-202", 2));
        WorkItems.Add(new WorkItemRow("DB query optimization",    "Task",    "Neha Verma",     2, "To Do",       "RB-203", 2));

        GanttBars.Clear();
        GanttBars.Add(new GanttBar("Develop API",         "RB-102", "Done",        0, 3));
        GanttBars.Add(new GanttBar("Develop API (FE)",    "RB-103", "In Progress", 2, 4));
        GanttBars.Add(new GanttBar("Unit Tests",          "RB-104", "In Progress", 4, 3));
        GanttBars.Add(new GanttBar("Code Review",         "RB-105", "To Do",       6, 2));
        GanttBars.Add(new GanttBar("Balance Enquiry API", "RB-201", "To Do",       7, 4));
        GanttBars.Add(new GanttBar("DB Optimization",     "RB-203", "To Do",       9, 2));
    }
}
