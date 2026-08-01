using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class SddPage : ContentPage
{
    public SddPage(SddViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
