namespace SprintForge.Avalonia.ViewModels;

public sealed class KpiCard
{
    public required string Title      { get; init; }
    public required string Value      { get; init; }
    public string?         Badge      { get; init; }
    public required string BadgeColor { get; init; }
}

public sealed class ActivityItem
{
    public required string Description { get; init; }
    public required string Module      { get; init; }
    public required string TimeAgo     { get; init; }
}

public sealed class DashboardViewModel
{
    public IReadOnlyList<KpiCard> KpiCards { get; } =
    [
        new KpiCard { Title = "Requirements",    Value = "—", Badge = "No data yet",      BadgeColor = "#64748B" },
        new KpiCard { Title = "Designs",         Value = "—", Badge = "No data yet",      BadgeColor = "#64748B" },
        new KpiCard { Title = "Code Services",   Value = "—", Badge = "No data yet",      BadgeColor = "#64748B" },
        new KpiCard { Title = "Unit Tests",      Value = "—", Badge = "No data yet",      BadgeColor = "#64748B" },
        new KpiCard { Title = "Open Jira Issues",Value = "—", Badge = "Configure Jira",   BadgeColor = "#64748B" },
        new KpiCard { Title = "Sprint Progress", Value = "—", Badge = "No active sprint", BadgeColor = "#64748B" },
    ];

    public IReadOnlyList<string> Recommendations { get; } =
    [
        "Configure Jira in Settings → Integrations to see live issues",
        "Connect an AI provider in Settings → AI to enable generation",
        "Add repositories in Settings → Repositories to analyse code",
    ];

    public IReadOnlyList<ActivityItem> RecentActivity { get; } = [];
}
