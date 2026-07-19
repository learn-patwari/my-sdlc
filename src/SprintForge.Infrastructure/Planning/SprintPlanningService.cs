using System.Text.Json;
using SprintForge.Application.Ai;
using SprintForge.Application.Approval;
using SprintForge.Application.Planning;
using SprintForge.Application.Sdlc;
using SprintForge.Domain.Approvals;
using SprintForge.Domain.Common;
using SprintForge.Domain.Planning;
using SprintForge.Infrastructure.Audit;

namespace SprintForge.Infrastructure.Planning;

/// <summary>
///   Full sprint planning workflow: parse → AI-enrich → validate → approve → bulk Jira create.
///   Writes to Jira ONLY after approval and through the write gate.
/// </summary>
public sealed class SprintPlanningService : ISprintPlanningService
{
    private readonly IReadOnlyList<ISprintPlanParser> _parsers;
    private readonly SprintValidator _validator;
    private readonly IAiOrchestrator _ai;
    private readonly ISdlcTool _sdlc;
    private readonly IApprovalGate _approvalGate;
    private readonly AuditedOperationRunner _runner;

    public SprintPlanningService(
        IReadOnlyList<ISprintPlanParser> parsers,
        SprintValidator validator,
        IAiOrchestrator ai,
        ISdlcTool sdlc,
        IApprovalGate approvalGate,
        AuditedOperationRunner runner)
    {
        _parsers = parsers;
        _validator = validator;
        _ai = ai;
        _sdlc = sdlc;
        _approvalGate = approvalGate;
        _runner = runner;
    }

    public async Task<Result<SprintPlan>> ParseAsync(string markdownContent, SprintCalendar calendar, CancellationToken ct = default)
    {
        var parser = _parsers.FirstOrDefault(p => p.CanParse(markdownContent))
                     ?? _parsers.First(); // fallback to first registered parser

        return await parser.ParseAsync(markdownContent, calendar, ct).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<WorkItem>>> EnrichWithAiAsync(SprintPlan plan, string jiraProjectKey, CancellationToken ct = default)
    {
        var request = new AiRequest
        {
            SystemPrompt = EnrichmentSystemPrompt,
            UserPrompt = BuildEnrichmentPrompt(plan, jiraProjectKey),
            Temperature = 0.3,
            MaxTokens = 8192,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        var completion = await _ai.RunAsync(request, ct: ct).ConfigureAwait(false);
        if (completion.IsFailure)
            return Result.Failure<IReadOnlyList<WorkItem>>($"AI enrichment failed: {completion.Error}");

        // Parse the AI's JSON response
        var enriched = ParseEnrichedJson(completion.Value!.Content, plan.RawItems);
        return Result.Success(enriched);
    }

    public SprintValidationSummary Validate(IReadOnlyList<WorkItem> items, SprintCalendar calendar) =>
        _validator.Validate(items, calendar);

    public async Task<Result<IReadOnlyList<ApprovalRequest>>> SubmitBatchForApprovalAsync(
        IReadOnlyList<WorkItem> items,
        string jiraProjectKey,
        string correlationId,
        string userName,
        CancellationToken ct = default)
    {
        var stories = items.Where(i => i.Kind is WorkItemKind.Story or WorkItemKind.Epic).ToList();
        var batchId = Guid.NewGuid().ToString("N");
        var queued = new List<ApprovalRequest>();

        foreach (var story in stories)
        {
            var children = items.Where(i => i.ParentLocalId == story.LocalId).ToList();
            var request = new ApprovalRequest
            {
                ApprovalId = Guid.NewGuid().ToString("N"),
                AuditId = Guid.NewGuid().ToString("N"),
                CorrelationId = correlationId,
                BatchId = batchId,
                ModuleTag = "Sprint",
                OperationDescription = $"Create Jira {story.Kind} '{story.Summary}' with {children.Count} child item(s)",
                ProposedValueJson = JsonSerializer.Serialize(new
                {
                    story.LocalId,
                    story.Kind,
                    story.Summary,
                    story.EstimateDays,
                    story.StoryPoints,
                    Children = children.Select(c => new { c.LocalId, c.Kind, c.Summary, c.EstimateDays })
                }),
                AiExplanation = "AI-enriched sprint work item ready for Jira creation. Review description and estimate.",
                AffectedFiles = [],
                QueuedAt = DateTimeOffset.UtcNow
            };

            var q = await _approvalGate.QueueAsync(request, ct).ConfigureAwait(false);
            queued.Add(q);
        }

        return Result.Success<IReadOnlyList<ApprovalRequest>>(queued);
    }

    public async Task<Result<IReadOnlyList<string>>> PersistApprovedBatchAsync(
        IReadOnlyList<ApprovalDecision> decisions,
        IReadOnlyList<WorkItem> items,
        string jiraProjectKey,
        string correlationId,
        string userName,
        CancellationToken ct = default)
    {
        var approvedIds = decisions
            .Where(d => d.Decision == ApprovalStatus.Approved)
            .Select(d => d.ApprovalId)
            .ToHashSet();

        if (approvedIds.Count == 0)
            return Result.Failure<IReadOnlyList<string>>("No items were approved.");

        var operation = new BulkCreateIssuesOperation(_sdlc, items, jiraProjectKey);
        return await _runner.RunAsync(operation, correlationId, userName, Environment.MachineName, ct).ConfigureAwait(false);
    }

    private static string BuildEnrichmentPrompt(SprintPlan plan, string projectKey)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Project: {projectKey}");
        sb.AppendLine($"Sprint: {plan.Calendar.SprintName} ({plan.Calendar.StartDate} – {plan.Calendar.EndDate})");
        sb.AppendLine($"Capacity: {plan.Calendar.DevelopmentDays} development days");
        sb.AppendLine();
        sb.AppendLine("Work items to enrich:");
        foreach (var item in plan.RawItems)
            sb.AppendLine($"  [{item.LocalId}] {item.Kind}: {item.Summary} (estimate: {item.EstimateDays}d)");
        sb.AppendLine();
        sb.AppendLine("Return JSON array of enriched items. Each item: {localId, description, acceptanceCriteria, storyPoints}");
        return sb.ToString();
    }

    private static IReadOnlyList<WorkItem> ParseEnrichedJson(string json, IReadOnlyList<WorkItem> original)
    {
        // Attempt to extract and apply AI enrichment; fall back to originals if parsing fails
        try
        {
            var start = json.IndexOf('[');
            var end = json.LastIndexOf(']');
            if (start < 0 || end < 0) return original;

            var arrayJson = json[start..(end + 1)];
            using var doc = System.Text.Json.JsonDocument.Parse(arrayJson);
            var enrichments = doc.RootElement.EnumerateArray()
                .Select(e => new
                {
                    LocalId = e.TryGetProperty("localId", out var id) ? id.GetString() : null,
                    Description = e.TryGetProperty("description", out var d) ? d.GetString() : null,
                    AcceptanceCriteria = e.TryGetProperty("acceptanceCriteria", out var ac) ? ac.GetString() : null,
                    StoryPoints = e.TryGetProperty("storyPoints", out var sp) && sp.TryGetInt32(out var spv) ? (int?)spv : null
                })
                .Where(e => e.LocalId is not null)
                .ToDictionary(e => e.LocalId!, e => e);

            return original.Select(item =>
            {
                if (!enrichments.TryGetValue(item.LocalId, out var enrichment)) return item;
                return item with
                {
                    Description = enrichment.Description ?? item.Description,
                    AcceptanceCriteria = enrichment.AcceptanceCriteria ?? item.AcceptanceCriteria,
                    StoryPoints = enrichment.StoryPoints ?? item.StoryPoints
                };
            }).ToList();
        }
        catch
        {
            return original;
        }
    }

    private const string EnrichmentSystemPrompt = """
        You are a senior agile coach enriching sprint work items with detailed descriptions and acceptance criteria.

        For each work item provided, return a JSON array:
        [
          {
            "localId": "WI-001",
            "description": "Detailed technical description of what needs to be done (2-4 sentences).",
            "acceptanceCriteria": "Given [...], When [...], Then [...]. At least 2 criteria.",
            "storyPoints": 3
          }
        ]

        Story point scale: 1=trivial, 2=simple, 3=moderate, 5=complex, 8=very complex, 13=huge (suggest splitting).
        Return ONLY valid JSON. No preamble, no commentary.
        """;
}
