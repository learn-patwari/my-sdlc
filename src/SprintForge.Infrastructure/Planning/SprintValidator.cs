using SprintForge.Application.Planning;
using SprintForge.Domain.Planning;

namespace SprintForge.Infrastructure.Planning;

/// <summary>
///   Validates work items against sprint capacity.
///   Does not throw or block — callers surface warnings to the user via the validation banner.
/// </summary>
public sealed class SprintValidator
{
    private readonly decimal _maxDeveloperDaysPerItem;

    public SprintValidator(decimal maxDeveloperDaysPerItem = 5m)
    {
        _maxDeveloperDaysPerItem = maxDeveloperDaysPerItem;
    }

    public SprintValidationSummary Validate(IReadOnlyList<WorkItem> items, SprintCalendar calendar)
    {
        var warnings = new List<WorkItemValidationWarning>();
        var totalEstimate = 0m;

        if (calendar.DevelopmentDays <= 0)
        {
            warnings.Add(new WorkItemValidationWarning(
                "CALENDAR", "Sprint Calendar",
                "No development days available in this sprint. Check holiday and buffer configuration.",
                ValidationSeverity.Error));
        }

        foreach (var item in items)
        {
            if (item.EstimateDays <= 0) continue;

            totalEstimate += item.EstimateDays;

            if (item.EstimateDays > _maxDeveloperDaysPerItem)
            {
                warnings.Add(new WorkItemValidationWarning(
                    item.LocalId, item.Summary,
                    $"Estimate {item.EstimateDays:N1}d exceeds max developer days per item ({_maxDeveloperDaysPerItem:N1}d). Consider splitting.",
                    ValidationSeverity.Warning));
            }

            if (item.Kind == WorkItemKind.Subtask && item.EstimateDays > _maxDeveloperDaysPerItem / 2)
            {
                warnings.Add(new WorkItemValidationWarning(
                    item.LocalId, item.Summary,
                    $"Subtask estimate {item.EstimateDays:N1}d is large. Subtasks should ideally be ≤{_maxDeveloperDaysPerItem / 2:N1}d.",
                    ValidationSeverity.Info));
            }
        }

        var capacityDays = calendar.DevelopmentDays;
        if (totalEstimate > capacityDays)
        {
            warnings.Add(new WorkItemValidationWarning(
                "CAPACITY", "Sprint Capacity",
                $"Total estimate {totalEstimate:N1}d exceeds sprint capacity {capacityDays}d (overloaded by {totalEstimate - capacityDays:N1}d).",
                ValidationSeverity.Error));
        }
        else if (totalEstimate > capacityDays * 0.9m)
        {
            warnings.Add(new WorkItemValidationWarning(
                "CAPACITY", "Sprint Capacity",
                $"Sprint is at {totalEstimate / capacityDays:P0} capacity. Consider leaving buffer for unplanned work.",
                ValidationSeverity.Info));
        }

        return new SprintValidationSummary
        {
            IsValid = warnings.All(w => w.Severity != ValidationSeverity.Error),
            TotalItems = items.Count,
            TotalEstimateDays = totalEstimate,
            CapacityDays = capacityDays,
            Warnings = warnings
        };
    }
}
