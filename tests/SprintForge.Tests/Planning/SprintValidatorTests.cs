using FluentAssertions;
using SprintForge.Application.Planning;
using SprintForge.Domain.Planning;
using SprintForge.Infrastructure.Planning;

namespace SprintForge.Tests.Planning;

public sealed class SprintValidatorTests
{
    private static SprintCalendar MakeCalendar(int devDays) => new()
    {
        SprintName = "Sprint 1",
        StartDate = DateOnly.FromDateTime(DateTime.Today),
        EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
        DurationDays = 14,
        DevelopmentDays = devDays,
        BufferDays = 0,
        WorkingHoursPerDay = 8,
        CapacityPoints = devDays * 2
    };

    private static WorkItem MakeItem(string id, WorkItemKind kind, decimal estimate) => new()
    {
        LocalId = id,
        Kind = kind,
        Summary = $"Item {id}",
        EstimateDays = estimate
    };

    private readonly SprintValidator _validator = new(maxDeveloperDaysPerItem: 5m);

    [Fact]
    public void Validate_ItemsWithinCapacity_IsValidNoErrors()
    {
        var items = new[]
        {
            MakeItem("WI-001", WorkItemKind.Story, 3m),
            MakeItem("WI-002", WorkItemKind.Task, 2m)
        };
        var summary = _validator.Validate(items, MakeCalendar(10));

        summary.IsValid.Should().BeTrue();
        summary.TotalEstimateDays.Should().Be(5m);
        summary.Warnings.Should().NotContain(w => w.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_TotalExceedsCapacity_AddsCapacityError()
    {
        var items = new[]
        {
            MakeItem("WI-001", WorkItemKind.Story, 4m),
            MakeItem("WI-002", WorkItemKind.Story, 4m),
            MakeItem("WI-003", WorkItemKind.Story, 4m)
        };

        var summary = _validator.Validate(items, MakeCalendar(10));

        summary.IsValid.Should().BeFalse();
        summary.Warnings.Should().Contain(w =>
            w.Severity == ValidationSeverity.Error && w.LocalId == "CAPACITY");
    }

    [Fact]
    public void Validate_SingleItemExceedsMaxPerItem_AddsWarning()
    {
        var items = new[]
        {
            MakeItem("WI-001", WorkItemKind.Story, 8m) // exceeds 5d max
        };

        var summary = _validator.Validate(items, MakeCalendar(20));

        summary.Warnings.Should().Contain(w => w.LocalId == "WI-001" && w.Severity == ValidationSeverity.Warning);
        summary.IsValid.Should().BeTrue(); // no Error-level warnings, just a Warning
    }

    [Fact]
    public void Validate_ZeroDevelopmentDays_AddsCalendarError()
    {
        var items = new[] { MakeItem("WI-001", WorkItemKind.Task, 1m) };
        var summary = _validator.Validate(items, MakeCalendar(0));

        summary.IsValid.Should().BeFalse();
        summary.Warnings.Should().Contain(w => w.LocalId == "CALENDAR");
    }

    [Fact]
    public void Validate_AtNinetyPercentCapacity_AddsInfoWarning()
    {
        // 9/10 = 90% capacity → info warning but still valid
        var items = new[] { MakeItem("WI-001", WorkItemKind.Story, 9.5m) };
        var summary = _validator.Validate(items, MakeCalendar(10));

        summary.IsValid.Should().BeTrue();
        summary.Warnings.Should().Contain(w => w.Severity == ValidationSeverity.Info && w.LocalId == "CAPACITY");
    }

    [Fact]
    public void Validate_LargeSubtask_AddsInfoWarning()
    {
        // > 2.5d subtask (half of 5d max) → info warning
        var items = new[] { MakeItem("WI-001", WorkItemKind.Subtask, 3m) };
        var summary = _validator.Validate(items, MakeCalendar(20));

        summary.Warnings.Should().Contain(w =>
            w.LocalId == "WI-001" && w.Severity == ValidationSeverity.Info);
    }

    [Fact]
    public void Validate_ZeroEstimateItems_NotCountedInTotal()
    {
        var items = new[]
        {
            MakeItem("WI-001", WorkItemKind.Story, 0m), // no estimate
            MakeItem("WI-002", WorkItemKind.Task, 3m)
        };

        var summary = _validator.Validate(items, MakeCalendar(10));
        summary.TotalEstimateDays.Should().Be(3m);
    }
}
