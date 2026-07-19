using System.Text.Json;
using SprintForge.Application.Audit;
using SprintForge.Application.Sdlc;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;
using SprintForge.Domain.Planning;

namespace SprintForge.Infrastructure.Planning;

/// <summary>
///   Audited write-gate operation that bulk-creates work items as Jira issues.
///   Parent issues (epics/stories) are created first; children use the returned Jira key as parentKey.
///   Must be executed via AuditedOperationRunner — never called directly.
/// </summary>
public sealed class BulkCreateIssuesOperation : IAuditedOperation<IReadOnlyList<string>>
{
    private readonly ISdlcTool _sdlc;
    private readonly IReadOnlyList<WorkItem> _items;
    private readonly string _projectKey;

    public BulkCreateIssuesOperation(ISdlcTool sdlc, IReadOnlyList<WorkItem> items, string projectKey)
    {
        _sdlc = sdlc;
        _items = items;
        _projectKey = projectKey;
    }

    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        Module = AuditModule.Sprint,
        Action = AuditAction.SprintPush,
        StartedAt = DateTimeOffset.UtcNow,
        UserName = userName,
        MachineName = machineName,
        InputsJson = JsonSerializer.Serialize(new
        {
            ProjectKey = _projectKey,
            ItemCount = _items.Count,
            Items = _items.Select(i => new { i.LocalId, i.Kind, i.Summary, i.EstimateDays })
        })
    };

    public async Task<Result<IReadOnlyList<string>>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
    {
        // localId → jiraKey mapping, populated as items are created
        var keyMap = new Dictionary<string, string>();
        var createdKeys = new List<string>();

        // Process in hierarchy order: epics first, then stories, then tasks, then subtasks
        var ordered = _items
            .OrderBy(i => i.Kind switch
            {
                WorkItemKind.Epic    => 0,
                WorkItemKind.Story   => 1,
                WorkItemKind.Task    => 2,
                WorkItemKind.Subtask => 3,
                WorkItemKind.Bug     => 2,
                WorkItemKind.Spike   => 2,
                _                   => 4
            })
            .ToList();

        foreach (var item in ordered)
        {
            ct.ThrowIfCancellationRequested();

            var parentJiraKey = item.ParentLocalId is not null
                ? keyMap.GetValueOrDefault(item.ParentLocalId)
                : null;

            var request = new SdlcIssueCreate
            {
                ProjectKey = _projectKey,
                IssueType = MapIssueType(item.Kind),
                Summary = item.Summary,
                Description = item.Description,
                Priority = item.Kind == WorkItemKind.Bug ? "High" : "Medium",
                Assignee = item.Assignee,
                StoryPoints = item.StoryPoints,
                EpicKey = item.EpicKey ?? (item.Kind != WorkItemKind.Epic ? keyMap.GetValueOrDefault(FindEpicId(item, ordered)) : null),
                ParentKey = parentJiraKey,
                Labels = item.Labels,
                Components = item.Components
            };

            var result = await _sdlc.CreateIssueAsync(request, ct).ConfigureAwait(false);
            if (result.IsFailure)
                return Result.Failure<IReadOnlyList<string>>($"Failed to create '{item.Summary}': {result.Error}");

            keyMap[item.LocalId] = result.Value!;
            createdKeys.Add(result.Value!);
        }

        return Result.Success<IReadOnlyList<string>>(createdKeys);
    }

    private static string MapIssueType(WorkItemKind kind) => kind switch
    {
        WorkItemKind.Epic    => "Epic",
        WorkItemKind.Story   => "Story",
        WorkItemKind.Task    => "Task",
        WorkItemKind.Subtask => "Sub-task",
        WorkItemKind.Bug     => "Bug",
        WorkItemKind.Spike   => "Story",
        _                    => "Task"
    };

    private static string FindEpicId(WorkItem item, List<WorkItem> all)
    {
        var current = item;
        while (current.ParentLocalId is not null)
        {
            var parent = all.FirstOrDefault(i => i.LocalId == current.ParentLocalId);
            if (parent is null) break;
            if (parent.Kind == WorkItemKind.Epic) return parent.LocalId;
            current = parent;
        }
        return string.Empty;
    }
}
