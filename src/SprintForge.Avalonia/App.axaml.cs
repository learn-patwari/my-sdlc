using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SprintForge.Application.Configuration;
using SprintForge.ViewModels;
using SprintForge.Avalonia.Views;
using SprintForge.Domain.Configuration;
using SprintForge.Infrastructure;
using SprintForge.Infrastructure.Data;

namespace SprintForge.Avalonia;

public partial class App : global::Avalonia.Application
{
    private IHost?         _host;
    private IServiceScope? _appScope;

    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SprintForgeBeta", "crash.log");

    private static void WriteCrashLog(string context, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath,
                $"[{DateTimeOffset.Now:o}] [{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
        }
        catch { /* last-resort log; swallow if disk is full */ }
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                WriteCrashLog("AppDomainUnhandled", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog("UnobservedTask", args.Exception);
            args.SetObserved();
        };

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
            await _host.StartAsync();

            _appScope = _host.Services.CreateScope();

            var db = _appScope.ServiceProvider.GetRequiredService<AuditDbContext>();
            await db.Database.EnsureCreatedAsync();

            await EnsureDefaultProfileAsync(_appScope.ServiceProvider);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = _appScope.ServiceProvider.GetRequiredService<MainWindow>();
                desktop.MainWindow.Show();

                desktop.ShutdownRequested += async (_, _) =>
                {
                    _appScope?.Dispose();
                    if (_host is not null)
                    {
                        await _host.StopAsync(TimeSpan.FromSeconds(5));
                        _host.Dispose();
                    }
                };
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnFrameworkInitializationCompleted", ex);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task EnsureDefaultProfileAsync(IServiceProvider sp)
    {
        var store = sp.GetRequiredService<IProfileStore>();
        var list  = await store.ListAsync();
        if (list.IsSuccess && list.Value!.Count > 0)
        {
            var first  = list.Value[0];
            var loaded = await store.LoadAsync(first.ProfileId);
            if (loaded.IsSuccess) store.SetActiveProfile(loaded.Value!);
            return;
        }

        var defaultProfile = new Profile
        {
            ProfileId      = "00000000-0000-0000-0000-000000000001",
            Name           = "Default (unconfigured)",
            SchemaVersion  = "1.0",
            CreatedAt      = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Jira = new JiraSettings
            {
                ServerUrl         = "https://your-org.atlassian.net",
                ApiDialect        = "v3",
                Username          = "user@example.com",
                ApiTokenRef       = "dpapi:not-configured",
                ProjectKeys       = [],
                DefaultLabels     = [],
                DefaultComponents = [],
                FieldMappings     = new JiraFieldMappings
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
                DurationDays       = 14,
                DevelopmentDays    = 10,
                BufferDays         = 2,
                WorkingHoursPerDay = 8,
                Workweek           = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                Holidays           = []
            },
            WorkingDirectory = new WorkingDirectorySettings
            {
                RootPath      = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SprintForgeBeta"),
                TemplatesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SprintForgeBeta", "templates")
            },
            Audit   = new AuditSettings  { RetentionDays = 365 },
            Logging = new LoggingSettings { MinimumLevel = "Information" }
        };

        await store.SaveAsync(defaultProfile);
        store.SetActiveProfile(defaultProfile);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SprintForgeBeta");
        Directory.CreateDirectory(appData);

        services.AddSprintForgeInfrastructure(
            auditJsonlPath:   Path.Combine(appData, "audit.jsonl"),
            secretIndexPath:  Path.Combine(appData, "secrets.json"),
            auditDbPath:      Path.Combine(appData, "audit.db"),
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
