using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class JiraPage : ContentPage
{
    public JiraPage(JiraViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
