using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Configuration;
using SprintForge.Domain.Configuration;
using SprintForge.Infrastructure;
using SprintForge.Infrastructure.Data;
using SprintForge.Maui.Views;
using SprintForge.ViewModels;

namespace SprintForge.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        RegisterServices(builder.Services);
        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
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
        services.AddSingleton<AppShell>();

        // Pages
        services.AddTransient<DashboardPage>();
        services.AddTransient<SrsGeneratorPage>();
        services.AddTransient<SadPage>();
        services.AddTransient<SddPage>();
        services.AddTransient<SprintPlanningPage>();
        services.AddTransient<RepoAnalyzerPage>();
        services.AddTransient<UnitTestsPage>();
        services.AddTransient<JiraPage>();
        services.AddTransient<DocumentsPage>();
        services.AddTransient<AuditCenterPage>();
        services.AddTransient<ApprovalsPage>();
        services.AddTransient<SettingsPage>();

        // ViewModels
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SrsGeneratorViewModel>();
        services.AddTransient<SadViewModel>();
        services.AddTransient<SddViewModel>();
        services.AddTransient<SprintPlanningViewModel>();
        services.AddTransient<RepoAnalyzerViewModel>();
        services.AddTransient<PlaceholderViewModel>();
        services.AddTransient<JiraViewModel>();
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<AuditCenterViewModel>();
        services.AddTransient<ApprovalsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
