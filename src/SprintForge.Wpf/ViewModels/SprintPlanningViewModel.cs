using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Planning;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class WorkItemRow : ObservableObject
{
    [ObservableProperty] private string _name     = "";
    [ObservableProperty] private string _type     = "Task";   // Epic|Story|Task|SubTask
    [ObservableProperty] private string _assignee = "";
    [ObservableProperty] private int    _estimatePts;
    [ObservableProperty] private string _status   = "To Do";  // Done|In Progress|To Do
    [ObservableProperty] private string _jiraKey  = "";
    [ObservableProperty] private int    _indent;               // 0=Epic 1=Story 2=Task 3=SubTask

    public double IndentWidth => Indent * 20.0;
    partial void OnIndentChanged(int value) => OnPropertyChanged(nameof(IndentWidth));
}

public sealed record SrsItem(string Id, string Title, string[] Services, int SuggestedPts)
{
    public string ServicesDisplay => string.Join(", ", Services);
}

public sealed record GanttBar(
    string Label,
    string JiraKey,
    string Status,
    int StartDay,
    int DurationDays)
{
    public double BarLeft  => StartDay * 50.0;
    public double BarWidth => DurationDays * 50.0;
}

public sealed partial class SprintPlanningViewModel : ObservableObject
{
    private readonly ISprintPlanningService _planningService;

    [ObservableProperty] private string _sprintName     = "SPR-34 (Jul 14 – Jul 27)";
    [ObservableProperty] private int    _capacityPts    = 160;
    [ObservableProperty] private int    _committedPts   = 142;
    [ObservableProperty] private int    _completedPts   = 38;
    [ObservableProperty] private int    _remainingPts   = 104;
    [ObservableProperty] private int    _utilizationPct = 89;
    [ObservableProperty] private string _statusMessage  = string.Empty;
    [ObservableProperty] private bool   _isValidating;
    [ObservableProperty] private bool   _showSrsPicker;

    public ObservableCollection<WorkItemRow> WorkItems    { get; } = [];
    public ObservableCollection<GanttBar>    GanttBars    { get; } = [];
    public ObservableCollection<SrsItem>     AvailableSrs { get; } = [];

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
    private void ToggleSrsPicker() => ShowSrsPicker = !ShowSrsPicker;

    [RelayCommand]
    private void AddSrsToSprint(SrsItem srs)
    {
        int half = Math.Max(1, srs.SuggestedPts / 2);
        WorkItems.Add(new WorkItemRow { Name = srs.Title,       Type = "Story",   Indent = 1, EstimatePts = srs.SuggestedPts, Status = "To Do", JiraKey = srs.Id });
        WorkItems.Add(new WorkItemRow { Name = "Development",   Type = "Task",    Indent = 2, EstimatePts = half, Status = "To Do" });
        WorkItems.Add(new WorkItemRow { Name = "Code Review",   Type = "Task",    Indent = 2, EstimatePts = 1,    Status = "To Do" });
        WorkItems.Add(new WorkItemRow { Name = "Unit Tests",    Type = "Task",    Indent = 2, EstimatePts = 2,    Status = "To Do" });
        WorkItems.Add(new WorkItemRow { Name = "Documentation", Type = "SubTask", Indent = 3, EstimatePts = 1,    Status = "To Do" });
        AvailableSrs.Remove(srs);
        RefreshCapacityMetrics();
        StatusMessage = $"'{srs.Title}' added with 4 default tasks.";
    }

    [RelayCommand]
    private void AddTask(WorkItemRow parent)
    {
        string childType = parent.Type switch
        {
            "Epic"  => "Story",
            "Story" => "Task",
            _       => "SubTask"
        };
        int insertAt = FindInsertPoint(parent);
        WorkItems.Insert(insertAt, new WorkItemRow
        {
            Name = $"New {childType}", Type = childType,
            Indent = parent.Indent + 1, EstimatePts = 2, Status = "To Do"
        });
        RefreshCapacityMetrics();
    }

    [RelayCommand]
    private void RemoveItem(WorkItemRow item)
    {
        int idx = WorkItems.IndexOf(item);
        if (idx < 0) return;
        int count = 1;
        while (idx + count < WorkItems.Count && WorkItems[idx + count].Indent > item.Indent)
            count++;
        for (int i = 0; i < count; i++)
            WorkItems.RemoveAt(idx);
        RefreshCapacityMetrics();
    }

    private int FindInsertPoint(WorkItemRow parent)
    {
        int idx = WorkItems.IndexOf(parent);
        if (idx < 0) return WorkItems.Count;
        int insertAt = idx + 1;
        while (insertAt < WorkItems.Count && WorkItems[insertAt].Indent > parent.Indent)
            insertAt++;
        return insertAt;
    }

    private void RefreshCapacityMetrics()
    {
        CommittedPts   = WorkItems.Where(w => w.Type == "Story").Sum(w => w.EstimatePts);
        CompletedPts   = WorkItems.Where(w => w.Type == "Task" && w.Status == "Done").Sum(w => w.EstimatePts);
        RemainingPts   = CommittedPts - CompletedPts;
        UtilizationPct = CapacityPts > 0 ? (int)Math.Round(CommittedPts * 100.0 / CapacityPts) : 0;
    }

    [RelayCommand]
    private async Task PushToJiraAsync()
    {
        IsValidating  = true;
        StatusMessage = "Validating sprint plan…";
        await Task.Delay(400);
        StatusMessage = "Validation passed — queued in Approvals Center. No Jira writes until approved.";
        IsValidating  = false;
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
        WorkItems.Add(new WorkItemRow { Name = "Fund Transfer Epic",      Type = "Epic",  Indent = 0, Status = "In Progress", JiraKey = "RB-100" });
        WorkItems.Add(new WorkItemRow { Name = "Implement fund transfer", Type = "Story", Indent = 1, EstimatePts = 15, Status = "In Progress", JiraKey = "RB-101" });
        WorkItems.Add(new WorkItemRow { Name = "Develop API",             Type = "Task",  Indent = 2, EstimatePts = 5,  Status = "Done",        JiraKey = "RB-102", Assignee = "Rahul Sharma" });
        WorkItems.Add(new WorkItemRow { Name = "Develop API (FE)",        Type = "Task",  Indent = 2, EstimatePts = 5,  Status = "In Progress", JiraKey = "RB-103", Assignee = "Priya Singh" });
        WorkItems.Add(new WorkItemRow { Name = "Unit Tests",              Type = "Task",  Indent = 2, EstimatePts = 3,  Status = "In Progress", JiraKey = "RB-104", Assignee = "Neha Verma" });
        WorkItems.Add(new WorkItemRow { Name = "Code Review",             Type = "SubTask", Indent = 3, EstimatePts = 2, Status = "To Do",      JiraKey = "RB-105", Assignee = "Arjun Patel" });
        WorkItems.Add(new WorkItemRow { Name = "Account Management Epic", Type = "Epic",  Indent = 0, Status = "In Progress", JiraKey = "RB-200" });
        WorkItems.Add(new WorkItemRow { Name = "Balance Enquiry API",     Type = "Story", Indent = 1, EstimatePts = 8,  Status = "To Do",       JiraKey = "RB-201" });
        WorkItems.Add(new WorkItemRow { Name = "REST endpoint",           Type = "Task",  Indent = 2, EstimatePts = 3,  Status = "To Do",       JiraKey = "RB-202", Assignee = "Rahul Sharma" });
        WorkItems.Add(new WorkItemRow { Name = "DB query optimization",   Type = "SubTask", Indent = 3, EstimatePts = 2, Status = "To Do",      JiraKey = "RB-203", Assignee = "Neha Verma" });

        GanttBars.Clear();
        GanttBars.Add(new GanttBar("Develop API",         "RB-102", "Done",        0, 3));
        GanttBars.Add(new GanttBar("Develop API (FE)",    "RB-103", "In Progress", 2, 4));
        GanttBars.Add(new GanttBar("Unit Tests",          "RB-104", "In Progress", 4, 3));
        GanttBars.Add(new GanttBar("Code Review",         "RB-105", "To Do",       6, 2));
        GanttBars.Add(new GanttBar("Balance Enquiry API", "RB-201", "To Do",       7, 4));
        GanttBars.Add(new GanttBar("DB Optimization",     "RB-203", "To Do",       9, 2));

        AvailableSrs.Clear();
        AvailableSrs.Add(new SrsItem("SRS-001", "User Authentication & Authorization", ["Account Service", "Security Service"], 13));
        AvailableSrs.Add(new SrsItem("SRS-002", "Transaction History & Reporting",     ["Payment Service", "Reporting Service"], 8));
        AvailableSrs.Add(new SrsItem("SRS-003", "Notification & Alerts Engine",        ["Notification Service", "Kafka"], 5));
        AvailableSrs.Add(new SrsItem("SRS-004", "Audit Log & Compliance Reporting",    ["Audit Service"], 8));
        AvailableSrs.Add(new SrsItem("SRS-005", "Multi-currency Support",              ["Payment Service", "FX Service"], 13));
    }
}
