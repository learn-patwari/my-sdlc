using SprintForge.Application.Approval;
using SprintForge.Application.Audit;
using SprintForge.Application.Documents;
using SprintForge.Infrastructure.Audit;
using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Orchestrates the full SRS workflow:
///   generate (AI, no write) → submit for approval (Approvals Center) → persist (write gate).
///
///   INVARIANT: No file is written until the user explicitly approves in the Approvals Center
///   and the write gate pre-records the audit entry successfully.
/// </summary>
public sealed class SrsService : ISrsService
{
    private readonly IDocumentGenerator _generator;
    private readonly IDocumentVersionStore _store;
    private readonly IApprovalGate _approvalGate;
    private readonly AuditedOperationRunner _runner;

    public SrsService(
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

    public Task<Result<DocumentDraft>> GenerateDraftAsync(SrsGenerationRequest request, CancellationToken ct = default)
    {
        var generationRequest = new DocumentGenerationRequest
        {
            Kind = DocumentKind.Srs,
            DocumentId = request.DocumentId,
            JiraProjectKey = request.JiraProjectKey,
            InputMarkdown = request.InputMarkdown,
            TemplateId = request.TemplateId,
            JiraIssueKey = request.JiraIssueKey,
            CorrelationId = request.CorrelationId
        };
        return _generator.GenerateAsync(generationRequest, ct);
    }

    public async Task<Result<ApprovalRequest>> SubmitForApprovalAsync(
        DocumentDraft draft,
        string correlationId,
        string userName,
        CancellationToken ct = default)
    {
        var auditId = Guid.NewGuid().ToString("N");
        var request = new ApprovalRequest
        {
            ApprovalId = Guid.NewGuid().ToString("N"),
            AuditId = auditId,
            CorrelationId = correlationId,
            ModuleTag = "SRS",
            OperationDescription = $"Persist SRS document '{draft.DocumentId}' (hash: {draft.ContentHash[..8]}…)",
            ProposedValueJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                draft.DocumentId,
                draft.Kind,
                draft.ContentHash,
                draft.JiraIssueKey,
                draft.TemplateUsed,
                draft.GeneratedAt
            }),
            AiExplanation = "AI-generated SRS document based on input requirements. Review content before approval.",
            QueuedAt = DateTimeOffset.UtcNow
        };

        var queued = await _approvalGate.QueueAsync(request, ct).ConfigureAwait(false);
        return Result.Success(queued);
    }

    public async Task<Result<DocumentVersion>> PersistApprovedDraftAsync(
        ApprovalDecision decision,
        DocumentDraft draft,
        CancellationToken ct = default)
    {
        if (decision.Decision != ApprovalStatus.Approved)
            return Result.Failure<DocumentVersion>($"Cannot persist: approval status is {decision.Decision}.");

        var operation = new SaveDocumentVersionOperation(_store, draft, decision.DecidedByUser);

        return await _runner.RunAsync(
            operation,
            draft.CorrelationId,
            decision.DecidedByUser,
            Environment.MachineName,
            ct).ConfigureAwait(false);
    }
}
