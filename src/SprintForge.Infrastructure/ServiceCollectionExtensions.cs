using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Ai;
using SprintForge.Application.Approval;
using SprintForge.Application.Audit;
using SprintForge.Application.Configuration;
using SprintForge.Application.Security;
using SprintForge.Application.Sdlc;
using SprintForge.Infrastructure.Ai;
using SprintForge.Infrastructure.Approval;
using SprintForge.Infrastructure.Audit;
using SprintForge.Infrastructure.Configuration;
using SprintForge.Infrastructure.Data;
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
        string profilesRootPath)
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

        return services;
    }
}
