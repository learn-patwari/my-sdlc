using System.Text.Json;
using SprintForge.Application.Audit;
using SprintForge.Application.Sdlc;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;

namespace SprintForge.Infrastructure.Sdlc;

/// <summary>
///   Audited write operations for Jira. Every one of these implements
///   <see cref="IAuditedOperation{T}"/> so callers MUST run them via
///   <see cref="SprintForge.Infrastructure.Audit.AuditedOperationRunner"/>.
/// </summary>

public sealed class CreateIssueOperation(ISdlcTool jira, SdlcIssueCreate request)
    : IAuditedOperation<string>
{
    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        UserName = userName,
        MachineName = machineName,
        Module = AuditModule.Jira,
        Action = AuditAction.JiraCreate,
        StartedAt = DateTimeOffset.UtcNow,
        InputsJson = JsonSerializer.Serialize(request)
    };

    public Task<Result<string>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
        => jira.CreateIssueAsync(request, ct);
}

public sealed class UpdateIssueOperation(ISdlcTool jira, string issueKey, SdlcIssueUpdate update)
    : IAuditedOperation<string>
{
    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        UserName = userName,
        MachineName = machineName,
        Module = AuditModule.Jira,
        Action = AuditAction.JiraUpdate,
        JiraIssueKey = issueKey,
        StartedAt = DateTimeOffset.UtcNow,
        InputsJson = JsonSerializer.Serialize(new { issueKey, update })
    };

    public Task<Result<string>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
        => jira.UpdateIssueAsync(issueKey, update, ct);
}

public sealed class TransitionIssueOperation(ISdlcTool jira, string issueKey, string transitionId)
    : IAuditedOperation<bool>
{
    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        UserName = userName,
        MachineName = machineName,
        Module = AuditModule.Jira,
        Action = AuditAction.JiraTransition,
        JiraIssueKey = issueKey,
        StartedAt = DateTimeOffset.UtcNow,
        InputsJson = JsonSerializer.Serialize(new { issueKey, transitionId })
    };

    public Task<Result<bool>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
        => jira.TransitionIssueAsync(issueKey, transitionId, ct);
}

public sealed class BulkCreateIssuesOperation(ISdlcTool jira, IReadOnlyList<SdlcIssueCreate> requests)
    : IAuditedOperation<IReadOnlyList<string>>
{
    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        UserName = userName,
        MachineName = machineName,
        Module = AuditModule.Jira,
        Action = AuditAction.JiraBulkCreate,
        StartedAt = DateTimeOffset.UtcNow,
        InputsJson = JsonSerializer.Serialize(new { count = requests.Count, summaries = requests.Select(r => r.Summary) })
    };

    public Task<Result<IReadOnlyList<string>>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
        => jira.BulkCreateIssuesAsync(requests, ct);
}
