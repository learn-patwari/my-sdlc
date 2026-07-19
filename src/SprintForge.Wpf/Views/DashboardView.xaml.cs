using SprintForge.Wpf.ViewModels;
using System.Windows.Controls;

namespace SprintForge.Wpf.Views;

public partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
