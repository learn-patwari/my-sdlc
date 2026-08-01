using CommunityToolkit.Mvvm.ComponentModel;

namespace SprintForge.ViewModels;

public sealed partial class PlaceholderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _moduleName = "Module";
}
