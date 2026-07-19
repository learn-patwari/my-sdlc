using SprintForge.Application.Planning;
using SprintForge.Domain.Common;
using SprintForge.Domain.Planning;

namespace SprintForge.Infrastructure.Planning;

/// <summary>
///   Parses a Markdown sprint plan into a work-item tree.
///
///   Supported format:
///   # Epic: Title                     (H1 = Epic)
///   ## Story: Title [N days]          (H2 = Story)
///   ### Task: Title [N days]          (H3 = Task)
///   #### Subtask: Title [N days]      (H4 = Subtask)
///   - bullet items under a story are parsed as tasks
///   Estimate markers: [N days], [N d], [Np] (N story points)
/// </summary>
public sealed class MarkdownSprintPlanParser : ISprintPlanParser
{
    public string FormatId => "Markdown";

    public bool CanParse(string content) =>
        content.TrimStart().StartsWith('#') ||
        content.Contains("## ") ||
        content.Contains("Story:") ||
        content.Contains("Task:");

    public Task<Result<SprintPlan>> ParseAsync(string content, SprintCalendar calendar, CancellationToken ct = default)
    {
        var items = new List<WorkItem>();
        int itemIndex = 0;

        string? currentEpicId = null;
        string? currentStoryId = null;

        foreach (var rawLine in content.Split('\n'))
        {
            ct.ThrowIfCancellationRequested();
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("#### ", StringComparison.Ordinal))
            {
                var item = ParseLine(line[5..], WorkItemKind.Subtask, ++itemIndex, currentStoryId ?? currentEpicId);
                items.Add(item);
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                var item = ParseLine(line[4..], WorkItemKind.Task, ++itemIndex, currentStoryId ?? currentEpicId);
                items.Add(item);
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var item = ParseLine(line[3..], WorkItemKind.Story, ++itemIndex, currentEpicId);
                currentStoryId = item.LocalId;
                items.Add(item);
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var item = ParseLine(line[2..], WorkItemKind.Epic, ++itemIndex, null);
                currentEpicId = item.LocalId;
                currentStoryId = null;
                items.Add(item);
            }
            else if ((line.TrimStart().StartsWith("- ", StringComparison.Ordinal) ||
                      line.TrimStart().StartsWith("* ", StringComparison.Ordinal)) &&
                     currentStoryId is not null)
            {
                var bulletText = line.TrimStart().TrimStart('-', '*', ' ');
                if (!string.IsNullOrWhiteSpace(bulletText))
                {
                    var item = ParseLine(bulletText, WorkItemKind.Task, ++itemIndex, currentStoryId);
                    items.Add(item);
                }
            }
        }

        if (items.Count == 0)
            return Task.FromResult(Result.Failure<SprintPlan>("No work items found in the input. Use headings (# Epic, ## Story, ### Task) or bullet lists."));

        var plan = new SprintPlan
        {
            Calendar = calendar,
            RawItems = items,
            SourceFormat = FormatId,
            ParsedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(Result.Success(plan));
    }

    private static WorkItem ParseLine(string text, WorkItemKind kind, int index, string? parentId)
    {
        // Strip "Epic:", "Story:", "Task:", "Subtask:" prefixes
        var prefixes = new[] { "Epic:", "Story:", "Task:", "Subtask:", "Bug:", "Spike:" };
        foreach (var prefix in prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[prefix.Length..].TrimStart();
                if (prefix.Equals("Bug:", StringComparison.OrdinalIgnoreCase)) kind = WorkItemKind.Bug;
                if (prefix.Equals("Spike:", StringComparison.OrdinalIgnoreCase)) kind = WorkItemKind.Spike;
                break;
            }
        }

        var estimate = ExtractEstimate(ref text);
        var summary = text.Trim();

        return new WorkItem
        {
            LocalId = $"WI-{index:D3}",
            ParentLocalId = parentId,
            Kind = kind,
            Summary = summary.Length > 0 ? summary : $"{kind} {index}",
            EstimateDays = estimate,
            StoryPoints = estimate > 0 ? (int)Math.Ceiling(estimate * 2) : null
        };
    }

    private static decimal ExtractEstimate(ref string text)
    {
        // Matches: [2 days], [2d], [2 d], [5p], [5 pts], [5 points], [3sp]
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"\[(\d+(?:\.\d+)?)\s*(?:days?|d|pts?|p|points?|sp|story\s*points?)?\]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            text = text.Replace(match.Value, "").Trim();
            return decimal.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        return 0;
    }
}
