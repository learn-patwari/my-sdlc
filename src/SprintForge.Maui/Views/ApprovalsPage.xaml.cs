using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class ApprovalsPage : ContentPage
{
    public ApprovalsPage(ApprovalsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
