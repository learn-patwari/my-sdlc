using SprintForge.Wpf.ViewModels;
using System.Windows.Controls;

namespace SprintForge.Wpf.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView(PlaceholderViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
