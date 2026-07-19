using System.Text.Json;
using SprintForge.Application.Audit;
using SprintForge.Application.Documents;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Audited write-gate operation for persisting an approved document draft.
///   Must be executed via AuditedOperationRunner — never called directly.
/// </summary>
public sealed class SaveDocumentVersionOperation : IAuditedOperation<DocumentVersion>
{
    private readonly IDocumentVersionStore _store;
    private readonly DocumentDraft _draft;
    private readonly string _createdByUser;

    public SaveDocumentVersionOperation(IDocumentVersionStore store, DocumentDraft draft, string createdByUser)
    {
        _store = store;
        _draft = draft;
        _createdByUser = createdByUser;
    }

    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        Module = AuditModule.Documents,
        Action = _draft.Kind switch
        {
            DocumentKind.Srs => AuditAction.SrsVersionCreate,
            DocumentKind.Sad => AuditAction.SadGeneration,
            DocumentKind.Sdd => AuditAction.SddGeneration,
            DocumentKind.Tests => AuditAction.TestGeneration,
            _ => AuditAction.DocumentExport
        },
        StartedAt = DateTimeOffset.UtcNow,
        UserName = userName,
        MachineName = machineName,
        InputsJson = JsonSerializer.Serialize(new
        {
            _draft.DocumentId,
            _draft.Kind,
            _draft.ContentHash,
            _draft.JiraIssueKey,
            _draft.TemplateUsed,
            _draft.GeneratedAt
        })
    };

    public Task<Result<DocumentVersion>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default) =>
        _store.SaveDraftAsync(_draft, ctx.AuditId, _createdByUser, ct);
}
