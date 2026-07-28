using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace SprintForge.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private bool    _isNavExpanded = true;

    public double NavWidth => IsNavExpanded ? 220.0 : 52.0;

    public IReadOnlyList<NavigationItem> NavItems { get; }

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;

        NavItems =
        [
            new NavigationItem { Label = "Dashboard",       Icon = "⊞",  ViewModelType = typeof(DashboardViewModel) },
            new NavigationItem { Label = "Requirements",    Icon = "📄",  ViewModelType = typeof(SrsGeneratorViewModel) },
            new NavigationItem { Label = "Architecture",    Icon = "🏗",  ViewModelType = typeof(SadViewModel) },
            new NavigationItem { Label = "Design Document", Icon = "📐",  ViewModelType = typeof(SddViewModel) },
            new NavigationItem { Label = "Sprint Builder",  Icon = "📅",  ViewModelType = typeof(SprintPlanningViewModel) },
            new NavigationItem { Label = "Repository",      Icon = "🗂",  ViewModelType = typeof(RepoAnalyzerViewModel) },
            new NavigationItem { Label = "Unit Tests",      Icon = "✓",   ViewModelType = typeof(SddViewModel) },
            new NavigationItem { Label = "Jira",            Icon = "🎯",  ViewModelType = typeof(JiraViewModel) },
            new NavigationItem { Label = "Documents",       Icon = "📁",  ViewModelType = typeof(DocumentsViewModel) },
            new NavigationItem { Label = "Approvals",       Icon = "✅",  ViewModelType = typeof(ApprovalsViewModel) },
            new NavigationItem { Label = "Audit Center",    Icon = "🔍",  ViewModelType = typeof(AuditCenterViewModel) },
            new NavigationItem { Label = "Settings",        Icon = "⚙",   ViewModelType = typeof(SettingsViewModel) },
        ];

        Navigate(NavItems[0]);
    }

    partial void OnIsNavExpandedChanged(bool value) => OnPropertyChanged(nameof(NavWidth));

    [RelayCommand]
    private void ToggleNav() => IsNavExpanded = !IsNavExpanded;

    [RelayCommand]
    private void NavigateToSettings() =>
        Navigate(NavItems.First(n => n.ViewModelType == typeof(SettingsViewModel)));

    [RelayCommand]
    private void Navigate(NavigationItem item)
    {
        foreach (var nav in NavItems)
            nav.IsActive = false;

        item.IsActive = true;

        if (CurrentPage is IDisposable outgoing)
            outgoing.Dispose();

        var vm = _services.GetRequiredService(item.ViewModelType);

        if (vm is PlaceholderViewModel placeholder)
            placeholder.ModuleName = item.Label;

        CurrentPage = vm;
    }
}
