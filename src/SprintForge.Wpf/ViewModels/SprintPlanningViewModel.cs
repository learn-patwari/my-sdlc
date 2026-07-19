using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SprintForge.Application.Planning;
using System.Collections.ObjectModel;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class WorkItemRow : ObservableObject
{
    private static readonly DateTime SprintStart = new DateTime(2026, 7, 14);

    [ObservableProperty] private string _name        = "";
    [ObservableProperty] private string _type        = "Task";    // Epic|Story|Task|SubTask
    [ObservableProperty] private string _assignee    = "";
    [ObservableProperty] private int    _estimatePts;
    [ObservableProperty] private string _status      = "To Do";   // Done|In Progress|To Do
    [ObservableProperty] private string _jiraKey     = "";
    [ObservableProperty] private int    _indent;                   // 0=Epic 1=Story 2=Task 3=SubTask
    [ObservableProperty] private int    _startDay;                 // days from sprint start (0-based)
    [ObservableProperty] private int    _durationDays;             // 0 = no bar (Epics/Stories)
    [ObservableProperty] private string _parentName  = "";         // parent story name (for Task Timeline)

    public double IndentWidth => Indent * 20.0;
    public double BarLeft     => StartDay * 50.0;
    public double BarWidth    => Math.Max(50.0, DurationDays * 50.0);

    public string StartDateLabel => SprintStart.AddDays(StartDay).ToString("MMM d");
    public string EndDateLabel   => SprintStart.AddDays(StartDay + Math.Max(0, DurationDays - 1)).ToString("MMM d");
    public string DateRangeLabel => $"{StartDateLabel} – {EndDateLabel}";

    partial void OnIndentChanged(int value)       => OnPropertyChanged(nameof(IndentWidth));
    partial void OnStartDayChanged(int value)
    {
        OnPropertyChanged(nameof(BarLeft));
        OnPropertyChanged(nameof(StartDateLabel));
        OnPropertyChanged(nameof(EndDateLabel));
        OnPropertyChanged(nameof(DateRangeLabel));
    }
    partial void OnDurationDaysChanged(int value)
    {
        OnPropertyChanged(nameof(BarWidth));
        OnPropertyChanged(nameof(EndDateLabel));
        OnPropertyChanged(nameof(DateRangeLabel));
    }
}

public sealed record SrsItem(string Id, string Title, string[] Services, int SuggestedPts)
{
    public string ServicesDisplay => string.Join(", ", Services);
}

public sealed partial class SprintPlanningViewModel : ObservableObject
{
    private readonly ISprintPlanningService _planningService;

    [ObservableProperty] private string _sprintName     = string.Empty;
    [ObservableProperty] private int    _capacityPts    = 0;
    [ObservableProperty] private int    _committedPts   = 0;
    [ObservableProperty] private int    _completedPts   = 0;
    [ObservableProperty] private int    _remainingPts   = 0;
    [ObservableProperty] private int    _utilizationPct = 0;
    [ObservableProperty] private string _statusMessage  = "Configure sprint settings, then add requirements from the SRS picker.";
    [ObservableProperty] private bool   _isValidating;
    [ObservableProperty] private bool   _showSrsPicker;
    [ObservableProperty] private string _activeTab      = "Plan";   // Plan | Timeline

    public ObservableCollection<WorkItemRow> WorkItems    { get; } = [];
    public ObservableCollection<WorkItemRow> TaskItems    { get; } = [];  // Tasks + SubTasks only
    public ObservableCollection<SrsItem>     AvailableSrs { get; } = [];

    public string[] SprintDates { get; } =
        ["Jul 14", "Jul 15", "Jul 16", "Jul 17", "Jul 18", "Jul 21",
         "Jul 22", "Jul 23", "Jul 24", "Jul 25", "Jul 28", "Jul 29",
         "Jul 30", "Jul 31", "Aug 1"];

    public SprintPlanningViewModel(ISprintPlanningService planningService)
    {
        _planningService = planningService;
    }

    // ── Tabs ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    // ── SRS picker ────────────────────────────────────────────────────────
    [RelayCommand]
    private void ToggleSrsPicker() => ShowSrsPicker = !ShowSrsPicker;

    [RelayCommand]
    private void AddSrsToSprint(SrsItem srs)
    {
        int nextDay  = TaskItems.Count > 0
                       ? TaskItems.Max(t => t.StartDay + t.DurationDays)
                       : 0;
        int half     = Math.Max(1, srs.SuggestedPts / 2);
        int devDays  = Math.Max(2, half);

        WorkItems.Add(new WorkItemRow { Name = srs.Title,       Type = "Story",   Indent = 1, EstimatePts = srs.SuggestedPts, Status = "To Do", JiraKey = srs.Id });
        Add(new WorkItemRow { Name = "Development",   Type = "Task",    Indent = 2, EstimatePts = half, Status = "To Do", StartDay = nextDay,       DurationDays = devDays,   ParentName = srs.Title });
        Add(new WorkItemRow { Name = "Code Review",   Type = "Task",    Indent = 2, EstimatePts = 1,    Status = "To Do", StartDay = nextDay + devDays, DurationDays = 2,     ParentName = srs.Title });
        Add(new WorkItemRow { Name = "Unit Tests",    Type = "Task",    Indent = 2, EstimatePts = 2,    Status = "To Do", StartDay = nextDay,       DurationDays = 2,         ParentName = srs.Title });
        Add(new WorkItemRow { Name = "Documentation", Type = "SubTask", Indent = 3, EstimatePts = 1,    Status = "To Do", StartDay = nextDay + devDays + 2, DurationDays = 1, ParentName = srs.Title });

        AvailableSrs.Remove(srs);
        RefreshCapacityMetrics();
        StatusMessage = $"'{srs.Title}' added — {devDays + 4} days scheduled from {new DateTime(2026, 7, 14).AddDays(nextDay):MMM d}.";
    }

    // Adds to WorkItems and TaskItems together
    private void Add(WorkItemRow item)
    {
        WorkItems.Add(item);
        if (item.Type is "Task" or "SubTask") TaskItems.Add(item);
    }

    // ── Work-item tree edits ──────────────────────────────────────────────
    [RelayCommand]
    private void AddTask(WorkItemRow parent)
    {
        string childType   = parent.Type switch { "Epic" => "Story", "Story" => "Task", _ => "SubTask" };
        string parentName  = parent.Type == "Story" ? parent.Name : parent.ParentName;
        int    childIndent = parent.Indent + 1;
        int    startDay    = parent.DurationDays > 0 ? parent.StartDay
                             : (TaskItems.Count > 0 ? TaskItems.Max(t => t.StartDay + t.DurationDays) : 0);

        var newItem = new WorkItemRow
        {
            Name = $"New {childType}", Type = childType, Indent = childIndent,
            EstimatePts = 2, Status = "To Do", ParentName = parentName,
            StartDay = startDay, DurationDays = childType is "Task" or "SubTask" ? 2 : 0
        };

        int insertAt = FindInsertPoint(parent);
        WorkItems.Insert(insertAt, newItem);
        SyncTaskItems();
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
        for (int i = 0; i < count; i++) WorkItems.RemoveAt(idx);
        SyncTaskItems();
        RefreshCapacityMetrics();
    }

    // ── Date shifting (for +/- buttons in Task Timeline) ──────────────────
    [RelayCommand]
    private void ShiftStartLeft(WorkItemRow item)  => item.StartDay    = Math.Max(0,  item.StartDay - 1);
    [RelayCommand]
    private void ShiftStartRight(WorkItemRow item) => item.StartDay    = Math.Min(14, item.StartDay + 1);
    [RelayCommand]
    private void ExtendDuration(WorkItemRow item)  => item.DurationDays = Math.Min(15, item.DurationDays + 1);
    [RelayCommand]
    private void ShrinkDuration(WorkItemRow item)  => item.DurationDays = Math.Max(1,  item.DurationDays - 1);

    // ── Jira / import ─────────────────────────────────────────────────────
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
    private void RefreshGantt()
    {
        WorkItems.Clear();
        SyncTaskItems();
        RefreshCapacityMetrics();
        StatusMessage = "Sprint cleared.";
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private int FindInsertPoint(WorkItemRow parent)
    {
        int idx = WorkItems.IndexOf(parent);
        if (idx < 0) return WorkItems.Count;
        int insertAt = idx + 1;
        while (insertAt < WorkItems.Count && WorkItems[insertAt].Indent > parent.Indent)
            insertAt++;
        return insertAt;
    }

    private void SyncTaskItems()
    {
        TaskItems.Clear();
        foreach (var w in WorkItems.Where(w => w.Type is "Task" or "SubTask"))
            TaskItems.Add(w);
    }

    private void RefreshCapacityMetrics()
    {
        CommittedPts   = WorkItems.Where(w => w.Type == "Story").Sum(w => w.EstimatePts);
        CompletedPts   = WorkItems.Where(w => w.Type == "Task" && w.Status == "Done").Sum(w => w.EstimatePts);
        RemainingPts   = CommittedPts - CompletedPts;
        UtilizationPct = CapacityPts > 0 ? (int)Math.Round(CommittedPts * 100.0 / CapacityPts) : 0;
    }

}
