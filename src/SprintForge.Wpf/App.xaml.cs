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
    private IServiceScope? _appScope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
            await _host.StartAsync();

            // Single scope for the app lifetime — correct for a single-user desktop app.
            // Scoped services (EF Core DbContext, audit dashboard, SRS service) are all
            // resolvable from this scope without "resolve scoped from root" exceptions.
            _appScope = _host.Services.CreateScope();

            var mainWindow = _appScope.ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Startup failed:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "SprintForge — Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _appScope?.Dispose();
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

        // Shell
        services.AddTransient<MainWindow>();
        services.AddTransient<MainWindowViewModel>();

        // Screen ViewModels
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SrsGeneratorViewModel>();
        services.AddTransient<ApprovalsViewModel>();
        services.AddTransient<AuditCenterViewModel>();
        services.AddTransient<SadViewModel>();
        services.AddTransient<SddViewModel>();
        services.AddTransient<SprintPlanningViewModel>();
        services.AddTransient<RepoAnalyzerViewModel>();
        services.AddTransient<JiraViewModel>();
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<PlaceholderViewModel>();
    }
}
