using SprintForge.Application.Approval;
using SprintForge.Application.Documents;
using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Infrastructure.Audit;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Orchestrates the SDD workflow.
///   INVARIANT: callers must surface SearchExistingSddsAsync results to the user before generating.
/// </summary>
public sealed class SddService : ISddService
{
    private readonly SddDocumentGenerator _generator;
    private readonly IDocumentVersionStore _store;
    private readonly IApprovalGate _approvalGate;
    private readonly AuditedOperationRunner _runner;

    public SddService(
        SddDocumentGenerator generator,
        IDocumentVersionStore store,
        IApprovalGate approvalGate,
        AuditedOperationRunner runner)
    {
        _generator = generator;
        _store = store;
        _approvalGate = approvalGate;
        _runner = runner;
    }

    public Task<Result<IReadOnlyList<ExistingSddMatch>>> SearchExistingSddsAsync(
        string serviceName, IReadOnlyList<string> projectKeys, CancellationToken ct = default) =>
        _generator.SearchExistingAsync(serviceName, projectKeys, ct);

    public Task<Result<DocumentDraft>> GenerateDraftAsync(SddGenerationRequest request, CancellationToken ct = default)
    {
        var input = BuildInputMarkdown(request);
        return _generator.GenerateAsync(new DocumentGenerationRequest
        {
            Kind = DocumentKind.Sdd,
            DocumentId = request.DocumentId,
            JiraProjectKey = request.JiraProjectKey,
            InputMarkdown = input,
            TemplateId = request.TemplateId,
            JiraIssueKey = request.JiraIssueKey,
            CorrelationId = request.CorrelationId
        }, ct);
    }

    public async Task<Result<ApprovalRequest>> SubmitForApprovalAsync(
        DocumentDraft draft, string correlationId, string userName, CancellationToken ct = default)
    {
        var request = new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid().ToString("N"),
            AuditId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            ModuleTag = "SDD",
            OperationDescription = $"Persist SDD document '{draft.DocumentId}' (hash: {draft.ContentHash[..8]}…)",
            ProposedValueJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                draft.DocumentId,
                draft.Kind,
                draft.ContentHash,
                draft.JiraIssueKey
            }),
            AiExplanation = "AI-generated Software Detailed Design document. Review content and test cases before approval.",
            QueuedAt = DateTimeOffset.UtcNow
        };

        var queued = await _approvalGate.QueueAsync(request, ct).ConfigureAwait(false);
        return Result.Success(queued);
    }

    public async Task<Result<DocumentVersion>> PersistApprovedDraftAsync(
        ApprovalDecision decision, DocumentDraft draft, CancellationToken ct = default)
    {
        if (decision.Decision != ApprovalStatus.Approved)
            return Result.Failure<DocumentVersion>($"Cannot persist: approval status is {decision.Decision}.");

        var operation = new SaveDocumentVersionOperation(_store, draft, decision.DecidedByUser);
        return await _runner.RunAsync(operation, draft.CorrelationId, decision.DecidedByUser, Environment.MachineName, ct).ConfigureAwait(false);
    }

    private static string BuildInputMarkdown(SddGenerationRequest r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Service Name: {r.ServiceName}");
        if (r.ServiceApiMarkdown is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Public API");
            sb.AppendLine(r.ServiceApiMarkdown);
        }
        if (r.DependencySummary is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Dependencies");
            sb.AppendLine(r.DependencySummary);
        }
        return sb.ToString();
    }
}
