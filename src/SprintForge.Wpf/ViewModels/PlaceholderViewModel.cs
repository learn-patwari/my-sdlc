using CommunityToolkit.Mvvm.ComponentModel;

namespace SprintForge.Wpf.ViewModels;

public sealed partial class PlaceholderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _moduleName = "Module";
}
