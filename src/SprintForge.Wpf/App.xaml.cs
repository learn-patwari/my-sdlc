using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SprintForge.Infrastructure;
using SprintForge.Wpf.ViewModels;
using System.IO;
using System.Windows;

namespace SprintForge.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SprintForge");
        Directory.CreateDirectory(appData);

        services.AddSprintForgeInfrastructure(
            auditJsonlPath: Path.Combine(appData, "audit.jsonl"),
            secretIndexPath: Path.Combine(appData, "secrets.json"),
            auditDbPath: Path.Combine(appData, "audit.db"),
            profilesRootPath: Path.Combine(appData, "profiles"),
            workingDirectory: appData);

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PlaceholderViewModel>();

        // Shell
        services.AddTransient<MainWindow>();
    }
}
