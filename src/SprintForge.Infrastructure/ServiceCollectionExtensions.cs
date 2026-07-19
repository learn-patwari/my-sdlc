using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SprintForge.Application.Approval;
using SprintForge.Application.Audit;
using SprintForge.Application.Security;
using SprintForge.Application.Sdlc;
using SprintForge.Infrastructure.Approval;
using SprintForge.Infrastructure.Audit;
using SprintForge.Infrastructure.Data;
using SprintForge.Infrastructure.Security;
using SprintForge.Infrastructure.Sdlc;

namespace SprintForge.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///   Registers all Infrastructure services.
    ///   <paramref name="auditJsonlPath"/> is the JSONL source-of-truth file.
    ///   <paramref name="secretIndexPath"/> is the DPAPI index file.
    ///   <paramref name="auditDbPath"/> is the SQLite database path.
    ///
    ///   NOTE: ISdlcTool write methods are NOT directly registered — callers must use
    ///   <see cref="AuditedOperationRunner"/> to invoke them through the write gate.
    /// </summary>
    public static IServiceCollection AddSprintForgeInfrastructure(
        this IServiceCollection services,
        string auditJsonlPath,
        string secretIndexPath,
        string auditDbPath)
    {
        // Audit — the write-gate chain
        services.AddSingleton(sp =>
            new JsonlAuditWriter(auditJsonlPath, sp.GetRequiredService<ILogger<JsonlAuditWriter>>()));

        services.AddDbContext<AuditDbContext>(opt =>
            opt.UseSqlite($"Data Source={auditDbPath}"));

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<AuditedOperationRunner>();

        // Approval gate
        services.AddSingleton<IApprovalGate, InMemoryApprovalGate>();

        // Security
        services.AddSingleton<ISecretStore>(sp =>
            new DpapiSecretStore(secretIndexPath, sp.GetRequiredService<ILogger<DpapiSecretStore>>()));

        // Sdlc tool (read operations exposed; writes go through the gate externally)
        services.AddHttpClient<JiraClient>();
        services.AddScoped<ISdlcTool, JiraClient>();

        return services;
    }
}
