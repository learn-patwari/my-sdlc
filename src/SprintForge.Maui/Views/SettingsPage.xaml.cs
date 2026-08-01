using SprintForge.ViewModels;

namespace SprintForge.Maui.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    // Token fields are NOT bound to the ViewModel — they are read in code-behind
    // to prevent secrets appearing in binding traces or observable properties.
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var jiraToken = JiraApiTokenEntry.Text;
        var aiKey     = AiApiKeyEntry.Text;
        await _vm.SaveWithSecretsAsync(jiraToken, aiKey);
    }
}
