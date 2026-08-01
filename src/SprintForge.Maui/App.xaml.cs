using SprintForge.Application.Configuration;
using SprintForge.Domain.Configuration;
using SprintForge.Infrastructure.Data;

namespace SprintForge.Maui;

public partial class App : Application
{
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SprintForgeBeta", "crash.log");

    public App(IServiceProvider services)
    {
        InitializeComponent();

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

        MainPage = services.GetRequiredService<AppShell>();

        _ = InitializeAsync(services);
    }

    private static async Task InitializeAsync(IServiceProvider sp)
    {
        try
        {
            var db = sp.GetRequiredService<AuditDbContext>();
            await db.Database.EnsureCreatedAsync();
            await EnsureDefaultProfileAsync(sp);
        }
        catch (Exception ex)
        {
            WriteCrashLog("InitializeAsync", ex);
        }
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

    private static void WriteCrashLog(string context, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath,
                $"[{DateTimeOffset.Now:o}] [{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
        }
        catch { }
    }
}
