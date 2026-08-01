using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class UnitTestsPage : ContentPage
{
    public UnitTestsPage(PlaceholderViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
