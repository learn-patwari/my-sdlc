using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Ai;
using SprintForge.Application.Approval;
using SprintForge.Application.Audit;
using SprintForge.Application.Configuration;
using SprintForge.Application.Documents;
using SprintForge.Application.Planning;
using SprintForge.Application.Repositories;
using SprintForge.Application.Security;
using SprintForge.Application.Sdlc;
using SprintForge.Infrastructure.Ai;
using SprintForge.Infrastructure.Approval;
using SprintForge.Infrastructure.Audit;
using SprintForge.Infrastructure.Configuration;
using SprintForge.Infrastructure.Data;
using SprintForge.Infrastructure.Documents;
using SprintForge.Infrastructure.Planning;
using SprintForge.Infrastructure.Repositories;
using SprintForge.Infrastructure.Security;
using SprintForge.Infrastructure.Sdlc;

namespace SprintForge.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///   Registers all Infrastructure services.
    ///
    ///   Write operations are NEVER exposed as raw services — callers must use
    ///   <see cref="AuditedOperationRunner"/> to execute them through the write gate.
    /// </summary>
    public static IServiceCollection AddSprintForgeInfrastructure(
        this IServiceCollection services,
        string auditJsonlPath,
        string secretIndexPath,
        string auditDbPath,
        string profilesRootPath,
        string workingDirectory = ".")
    {
        // ── Audit — the write-gate chain ──────────────────────────────────────
        services.AddSingleton(sp =>
            new JsonlAuditWriter(auditJsonlPath, sp.GetRequiredService<ILogger<JsonlAuditWriter>>()));

        services.AddDbContext<AuditDbContext>(opt =>
            opt.UseSqlite($"Data Source={auditDbPath}"));

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<AuditedOperationRunner>();

        // ── Approval gate ─────────────────────────────────────────────────────
        services.AddSingleton<IApprovalGate, InMemoryApprovalGate>();

        // ── Security ──────────────────────────────────────────────────────────
        services.AddSingleton<ISecretStore>(sp =>
            new DpapiSecretStore(secretIndexPath, sp.GetRequiredService<ILogger<DpapiSecretStore>>()));

        // ── Configuration / profiles ──────────────────────────────────────────
        services.AddSingleton<IProfileStore>(sp =>
            new JsonFileProfileStore(profilesRootPath, sp.GetRequiredService<ILogger<JsonFileProfileStore>>()));

        // ── Jira (read operations directly accessible; writes go via AuditedOperationRunner) ──
        services.AddHttpClient<JiraClient>()
            .AddStandardResilienceHandler(opt =>
            {
                opt.Retry.MaxRetryAttempts = 3;
                opt.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            });
        services.AddScoped<ISdlcTool, JiraClient>();

        // ── AI orchestrator (providers resolved dynamically from active profile) ──
        services.AddHttpClient(); // registers IHttpClientFactory
        services.AddScoped<IAiOrchestrator, AiOrchestrator>();

        // ── Document engine ────────────────────────────────────────────────────
        services.AddSingleton<IDocumentVersionStore>(sp =>
            new FileSystemDocumentVersionStore(workingDirectory, sp.GetRequiredService<ILogger<FileSystemDocumentVersionStore>>()));

        // draw.io writer (stateless, singleton)
        services.AddSingleton<IDrawioWriter, DrawioWriter>();

        // SRS
        services.AddScoped<SrsDocumentGenerator>();
        services.AddScoped<ISrsService>(sp => new SrsService(
            sp.GetRequiredService<SrsDocumentGenerator>(),
            sp.GetRequiredService<IDocumentVersionStore>(),
            sp.GetRequiredService<IApprovalGate>(),
            sp.GetRequiredService<AuditedOperationRunner>()));

        // SAD
        services.AddScoped<SadDocumentGenerator>();
        services.AddScoped<ISadService>(sp => new SadService(
            sp.GetRequiredService<SadDocumentGenerator>(),
            sp.GetRequiredService<IDocumentVersionStore>(),
            sp.GetRequiredService<IApprovalGate>(),
            sp.GetRequiredService<AuditedOperationRunner>()));

        // SDD
        services.AddScoped<SddDocumentGenerator>();
        services.AddScoped<ISddService>(sp => new SddService(
            sp.GetRequiredService<SddDocumentGenerator>(),
            sp.GetRequiredService<IDocumentVersionStore>(),
            sp.GetRequiredService<IApprovalGate>(),
            sp.GetRequiredService<AuditedOperationRunner>()));

        // ── Sprint Planning ────────────────────────────────────────────────────
        // Multiple parsers: register all, ISprintPlanningService resolves the right one
        services.AddScoped<ISprintPlanParser, MarkdownSprintPlanParser>();
        services.AddScoped<SprintValidator>();
        services.AddScoped<ISprintPlanningService>(sp => new SprintPlanningService(
            sp.GetServices<ISprintPlanParser>().ToList(),
            sp.GetRequiredService<SprintValidator>(),
            sp.GetRequiredService<IAiOrchestrator>(),
            sp.GetRequiredService<ISdlcTool>(),
            sp.GetRequiredService<IApprovalGate>(),
            sp.GetRequiredService<AuditedOperationRunner>()));

        // ── Repository integration ─────────────────────────────────────────────
        services.AddScoped<IRepositoryAnalyzer, FileSystemRepositoryAnalyzer>();
        // LocalRepositoryProvider registered as concrete type; callers resolve by concrete type
        // or by name/key if multiple providers are needed in future.
        services.AddScoped<LocalRepositoryProvider>(sp =>
            new LocalRepositoryProvider(workingDirectory, sp.GetRequiredService<ILogger<LocalRepositoryProvider>>()));

        return services;
    }
}
