using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SprintForge.Application.Configuration;
using SprintForge.Domain.Configuration;
using SprintForge.Infrastructure;
using SprintForge.Infrastructure.Data;
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

            // Ensure the SQLite audit schema exists on first run.
            var db = _appScope.ServiceProvider.GetRequiredService<AuditDbContext>();
            await db.Database.EnsureCreatedAsync();

            // Seed a default profile if none exists so the app can start without errors.
            await EnsureDefaultProfileAsync(_appScope.ServiceProvider);

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

    private static async Task EnsureDefaultProfileAsync(IServiceProvider sp)
    {
        var store = sp.GetRequiredService<IProfileStore>();
        var list = await store.ListAsync();
        if (list.IsSuccess && list.Value!.Count > 0)
        {
            // Load the first existing profile so ActiveProfile is never null.
            var first = list.Value[0];
            var loaded = await store.LoadAsync(first.ProfileId);
            if (loaded.IsSuccess) store.SetActiveProfile(loaded.Value!);
            return;
        }

        // First run: create a placeholder profile with unconfigured dpapi: handles.
        // This prevents "No active profile" errors; the user configures real credentials in Settings.
        var defaultProfile = new Profile
        {
            ProfileId  = "00000000-0000-0000-0000-000000000001",
            Name       = "Default (unconfigured)",
            SchemaVersion = "1.0",
            CreatedAt  = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Jira = new JiraSettings
            {
                ServerUrl       = "https://your-org.atlassian.net",
                ApiDialect      = "v3",
                Username        = "user@example.com",
                ApiTokenRef     = "dpapi:not-configured",
                ProjectKeys     = [],
                DefaultLabels   = [],
                DefaultComponents = [],
                FieldMappings   = new JiraFieldMappings
                {
                    StoryPoints = "customfield_10016",
                    Sprint      = "customfield_10020",
                    EpicLink    = "customfield_10014"
                }
            },
            Ai = new AiSettings
            {
                DefaultProviderId = "default",
                Providers =
                [
                    new AiProvider
                    {
                        Id        = "default",
                        Vendor    = "Anthropic",
                        Endpoint  = "https://api.anthropic.com",
                        ApiKeyRef = "dpapi:not-configured",
                        Model     = "claude-sonnet-5"
                    }
                ]
            },
            Sprint = new SprintSettings
            {
                DurationDays      = 14,
                DevelopmentDays   = 10,
                BufferDays        = 2,
                WorkingHoursPerDay = 8,
                Workweek          = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                Holidays          = []
            },
            WorkingDirectory = new WorkingDirectorySettings
            {
                RootPath      = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SprintForge"),
                TemplatesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SprintForge", "templates")
            },
            Audit   = new AuditSettings { RetentionDays = 365 },
            Logging = new LoggingSettings { MinimumLevel = "Information" }
        };

        await store.SaveAsync(defaultProfile);
        store.SetActiveProfile(defaultProfile);
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
