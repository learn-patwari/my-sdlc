using SprintForge.Application.Approval;
using SprintForge.Application.Documents;
using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;
using SprintForge.Infrastructure.Audit;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Orchestrates the SAD workflow: generate (no write) → approve → persist via write gate.
/// </summary>
public sealed class SadService : ISadService
{
    private readonly IDocumentGenerator _generator;
    private readonly IDocumentVersionStore _store;
    private readonly IApprovalGate _approvalGate;
    private readonly AuditedOperationRunner _runner;

    public SadService(
        IDocumentGenerator generator,
        IDocumentVersionStore store,
        IApprovalGate approvalGate,
        AuditedOperationRunner runner)
    {
        _generator = generator;
        _store = store;
        _approvalGate = approvalGate;
        _runner = runner;
    }

    public Task<Result<DocumentDraft>> GenerateDraftAsync(SadGenerationRequest request, CancellationToken ct = default) =>
        _generator.GenerateAsync(new DocumentGenerationRequest
        {
            Kind = DocumentKind.Sad,
            DocumentId = request.DocumentId,
            JiraProjectKey = request.JiraProjectKey,
            InputMarkdown = BuildInputMarkdown(request),
            TemplateId = request.TemplateId,
            JiraIssueKey = request.JiraIssueKey,
            CorrelationId = request.CorrelationId
        }, ct);

    public async Task<Result<ApprovalRequest>> SubmitForApprovalAsync(
        DocumentDraft draft, string correlationId, string userName, CancellationToken ct = default)
    {
        var request = new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid().ToString("N"),
            AuditId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            ModuleTag = "SAD",
            OperationDescription = $"Persist SAD document '{draft.DocumentId}' with architecture diagram (hash: {draft.ContentHash[..8]}…)",
            ProposedValueJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                draft.DocumentId,
                draft.Kind,
                draft.ContentHash,
                draft.JiraIssueKey,
                AdditionalFileNames = draft.AdditionalFiles?.Keys.ToArray() ?? []
            }),
            AiExplanation = "AI-generated Software Architecture Document with native draw.io diagram. Review architecture before approval.",
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

    private static string BuildInputMarkdown(SadGenerationRequest r)
    {
        if (r.IdentifiedServices.Count == 0) return r.InputMarkdown;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(r.InputMarkdown);
        sb.AppendLine();
        sb.AppendLine("## Identified Services");
        foreach (var svc in r.IdentifiedServices) sb.AppendLine($"- {svc}");
        if (r.ArchitectureStyle is not null) { sb.AppendLine(); sb.AppendLine($"Architecture Style: {r.ArchitectureStyle}"); }
        if (r.TechStack is not null) { sb.AppendLine($"Technology Stack: {r.TechStack}"); }
        return sb.ToString();
    }
}
