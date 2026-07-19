using SprintForge.Domain.Audit;

namespace SprintForge.Application.Audit;

/// <summary>Ambient context flowing through a single audited operation.</summary>
public sealed record AuditContext
{
    public required string AuditId { get; init; }
    public required string CorrelationId { get; init; }
    public string? ParentAuditId { get; init; }
    public required string UserName { get; init; }
    public required string MachineName { get; init; }
    public required AuditModule Module { get; init; }
    public required AuditAction Action { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public string? InputsJson { get; init; }
    public string? JiraIssueKey { get; init; }
}
