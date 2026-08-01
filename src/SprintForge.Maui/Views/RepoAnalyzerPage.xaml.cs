using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class RepoAnalyzerPage : ContentPage
{
    public RepoAnalyzerPage(RepoAnalyzerViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
