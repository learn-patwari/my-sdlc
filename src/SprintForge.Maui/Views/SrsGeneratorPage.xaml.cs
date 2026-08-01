using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class SrsGeneratorPage : ContentPage
{
    public SrsGeneratorPage(SrsGeneratorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
