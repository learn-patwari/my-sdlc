using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class AuditCenterPage : ContentPage
{
    public AuditCenterPage(AuditCenterViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
