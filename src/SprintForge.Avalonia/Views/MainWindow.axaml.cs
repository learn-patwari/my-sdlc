using Avalonia.Controls;
using SprintForge.Avalonia.ViewModels;

namespace SprintForge.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
