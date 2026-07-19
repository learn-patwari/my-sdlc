using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private object? _currentPage;

    public IReadOnlyList<NavigationItem> NavItems { get; }

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;

        NavItems =
        [
            new NavigationItem { Label = "Dashboard",       Icon = "⊞",  ViewModelType = typeof(DashboardViewModel) },
            new NavigationItem { Label = "SRS Generator",   Icon = "📄",  ViewModelType = typeof(SrsGeneratorViewModel) },
            new NavigationItem { Label = "SAD",             Icon = "🏗",  ViewModelType = typeof(SadViewModel) },
            new NavigationItem { Label = "SDD",             Icon = "📐",  ViewModelType = typeof(SddViewModel) },
            new NavigationItem { Label = "Sprint Planning", Icon = "📅",  ViewModelType = typeof(SprintPlanningViewModel) },
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

    [RelayCommand]
    private void Navigate(NavigationItem item)
    {
        foreach (var nav in NavItems)
            nav.IsActive = false;

        item.IsActive = true;

        var vm = _services.GetRequiredService(item.ViewModelType);

        if (vm is PlaceholderViewModel placeholder)
            placeholder.ModuleName = item.Label;

        CurrentPage = vm;
    }
}
