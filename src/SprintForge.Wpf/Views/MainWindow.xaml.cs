using SprintForge.Wpf.ViewModels;
using System.Windows;

namespace SprintForge.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
