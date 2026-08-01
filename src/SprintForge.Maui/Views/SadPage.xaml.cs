using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class SadPage : ContentPage
{
    public SadPage(SadViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
