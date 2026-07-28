using CommunityToolkit.Mvvm.ComponentModel;

namespace SprintForge.Avalonia.ViewModels;

public sealed partial class NavigationItem : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    public required string Label         { get; init; }
    public required string Icon          { get; init; }
    public required Type   ViewModelType { get; init; }

    public string NavBgHex => IsActive ? "#7C3AED" : "Transparent";
    public string NavFgHex => IsActive ? "#FFFFFF"  : "#94A3B8";

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(NavBgHex));
        OnPropertyChanged(nameof(NavFgHex));
    }
}
