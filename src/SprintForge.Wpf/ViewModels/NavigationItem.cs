using CommunityToolkit.Mvvm.ComponentModel;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class NavigationItem : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public required string Label { get; init; }
    public required string Icon { get; init; }
    public required Type ViewModelType { get; init; }
}
