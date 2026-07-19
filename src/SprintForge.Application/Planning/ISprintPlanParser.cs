using SprintForge.Domain.Common;
using SprintForge.Domain.Planning;

namespace SprintForge.Application.Planning;

/// <summary>Converts raw text in a specific format into an intermediate SprintPlan model.</summary>
public interface ISprintPlanParser
{
    /// <summary>Format identifier returned by this parser (e.g., "Markdown", "PlainText").</summary>
    string FormatId { get; }

    bool CanParse(string content);

    Task<Result<SprintPlan>> ParseAsync(string content, SprintCalendar calendar, CancellationToken ct = default);
}
