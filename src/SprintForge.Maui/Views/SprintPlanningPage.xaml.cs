using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class SprintPlanningPage : ContentPage
{
    public SprintPlanningPage(SprintPlanningViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
