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
            new NavigationItem { Label = "SRS Generator",   Icon = "📄",  ViewModelType = typeof(PlaceholderViewModel) },
            new NavigationItem { Label = "SAD",             Icon = "🏗",  ViewModelType = typeof(PlaceholderViewModel) },
            new NavigationItem { Label = "SDD",             Icon = "📐",  ViewModelType = typeof(PlaceholderViewModel) },
            new NavigationItem { Label = "Sprint Planning", Icon = "📅",  ViewModelType = typeof(PlaceholderViewModel) },
            new NavigationItem { Label = "Repository",      Icon = "🗂",  ViewModelType = typeof(PlaceholderViewModel) },
            new NavigationItem { Label = "Unit Tests",      Icon = "✓",   ViewModelType = typeof(PlaceholderViewModel) },
            new NavigationItem { Label = "Jira",            Icon = "🎯",  ViewModelType = typeof(PlaceholderViewModel) },
            new NavigationItem { Label = "Documents",       Icon = "📁",  ViewModelType = typeof(PlaceholderViewModel) },
            new NavigationItem { Label = "Audit Center",    Icon = "🔍",  ViewModelType = typeof(PlaceholderViewModel) },
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
