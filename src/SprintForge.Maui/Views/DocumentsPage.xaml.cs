using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class DocumentsPage : ContentPage
{
    public DocumentsPage(DocumentsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
