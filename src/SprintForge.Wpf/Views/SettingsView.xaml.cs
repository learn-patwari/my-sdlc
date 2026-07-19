using SprintForge.Wpf.ViewModels;
using System.Windows.Controls;

namespace SprintForge.Wpf.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
