namespace SprintForge.Wpf.ViewModels;

public sealed class KpiCard
{
    public required string Title { get; init; }
    public required string Value { get; init; }
    public string? Badge { get; init; }
    public required string BadgeColor { get; init; }
}

public sealed class ActivityItem
{
    public required string Description { get; init; }
    public required string Module { get; init; }
    public required string TimeAgo { get; init; }
}

public sealed class DashboardViewModel
{
    public IReadOnlyList<KpiCard> KpiCards { get; } =
    [
        new KpiCard { Title = "Requirements",    Value = "128",   Badge = "+12 this sprint",      BadgeColor = "#22C55E" },
        new KpiCard { Title = "Designs",         Value = "24",    Badge = "+5 this sprint",       BadgeColor = "#22C55E" },
        new KpiCard { Title = "Code Services",   Value = "36",    Badge = "3 Impacted",           BadgeColor = "#F59E0B" },
        new KpiCard { Title = "Unit Tests",      Value = "1,248", Badge = "82% Coverage",         BadgeColor = "#7C3AED" },
        new KpiCard { Title = "Open Jira Issues",Value = "56",    Badge = "12 High Priority",     BadgeColor = "#EF4444" },
        new KpiCard { Title = "Sprint Progress", Value = "68%",   Badge = "On Track",             BadgeColor = "#22C55E" },
    ];

    public IReadOnlyList<string> Recommendations { get; } =
    [
        "12 requirements are incomplete",
        "3 services have high complexity",
        "Test coverage below 65% in 4 services",
        "3 Jira issues are blocked",
    ];

    public IReadOnlyList<ActivityItem> RecentActivity { get; } =
    [
        new ActivityItem { Description = "SRS generated for Account Management module",  Module = "SRS",    TimeAgo = "2 min ago" },
        new ActivityItem { Description = "RBP-101 created in Jira",                      Module = "Jira",   TimeAgo = "15 min ago" },
        new ActivityItem { Description = "SAD diagram updated for Payment Service",       Module = "SAD",    TimeAgo = "1 hr ago" },
        new ActivityItem { Description = "Test suite generated: 24 test cases",          Module = "Tests",  TimeAgo = "2 hrs ago" },
        new ActivityItem { Description = "Sprint SPR-34 planned: 142 points committed",  Module = "Sprint", TimeAgo = "Yesterday" },
    ];
}
