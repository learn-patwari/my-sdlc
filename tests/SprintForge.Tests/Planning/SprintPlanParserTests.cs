using FluentAssertions;
using SprintForge.Domain.Planning;
using SprintForge.Infrastructure.Planning;

namespace SprintForge.Tests.Planning;

public sealed class SprintPlanParserTests
{
    private static SprintCalendar MakeCalendar(int devDays = 10) => new()
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

    private readonly MarkdownSprintPlanParser _parser = new();

    [Fact]
    public async Task ParseAsync_WellFormedMarkdown_ReturnsHierarchy()
    {
        var md = """
            # Epic: User Auth
            ## Story: Login flow [3 days]
            ### Task: Build login form [1d]
            ### Task: API endpoint [2d]
            ## Story: Registration [2 days]
            """;

        var result = await _parser.ParseAsync(md, MakeCalendar());

        result.IsSuccess.Should().BeTrue();
        var plan = result.Value!;
        plan.RawItems.Should().HaveCount(5);

        var epic = plan.RawItems.Single(i => i.Kind == WorkItemKind.Epic);
        epic.Summary.Should().Be("User Auth");
        epic.ParentLocalId.Should().BeNull();

        var stories = plan.RawItems.Where(i => i.Kind == WorkItemKind.Story).ToList();
        stories.Should().HaveCount(2);
        stories[0].EstimateDays.Should().Be(3);
        stories[1].EstimateDays.Should().Be(2);

        var tasks = plan.RawItems.Where(i => i.Kind == WorkItemKind.Task).ToList();
        tasks.Should().HaveCount(2);
        tasks[0].EstimateDays.Should().Be(1);
        tasks[1].EstimateDays.Should().Be(2);
        tasks.All(t => t.ParentLocalId == stories[0].LocalId).Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_BulletItemsUnderStory_ParsedAsTasks()
    {
        var md = """
            ## Story: Checkout [5d]
            - Payment integration [2d]
            - Cart summary view [1d]
            """;

        var result = await _parser.ParseAsync(md, MakeCalendar());

        result.IsSuccess.Should().BeTrue();
        var tasks = result.Value!.RawItems.Where(i => i.Kind == WorkItemKind.Task).ToList();
        tasks.Should().HaveCount(2);
        tasks[0].Summary.Should().Be("Payment integration");
        tasks[0].EstimateDays.Should().Be(2);
        tasks[1].EstimateDays.Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_BugPrefix_SetsKindToBug()
    {
        var md = "## Bug: Null ref on checkout [1d]";

        var result = await _parser.ParseAsync(md, MakeCalendar());

        var bug = result.Value!.RawItems.Single();
        bug.Kind.Should().Be(WorkItemKind.Bug);
        bug.Summary.Should().Be("Null ref on checkout");
    }

    [Fact]
    public async Task ParseAsync_SpikePrefix_SetsKindToSpike()
    {
        var md = "## Spike: Investigate caching options [2d]";

        var result = await _parser.ParseAsync(md, MakeCalendar());

        var spike = result.Value!.RawItems.Single();
        spike.Kind.Should().Be(WorkItemKind.Spike);
    }

    [Fact]
    public async Task ParseAsync_EstimateInStoryPoints_ParsedAsDecimal()
    {
        var md = "## Story: Auth [5p]";

        var result = await _parser.ParseAsync(md, MakeCalendar());

        result.Value!.RawItems.Single().EstimateDays.Should().Be(5);
    }

    [Fact]
    public async Task ParseAsync_EmptyContent_ReturnsFailure()
    {
        var result = await _parser.ParseAsync("no headings here", MakeCalendar());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No work items found");
    }

    [Fact]
    public void CanParse_StartsWithHash_ReturnsTrue()
    {
        _parser.CanParse("# Epic: Test").Should().BeTrue();
    }

    [Fact]
    public void CanParse_EmptyString_ReturnsFalse()
    {
        _parser.CanParse("   plain text with no structure   ").Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_LocalIdsAreUnique()
    {
        var md = """
            # Epic: E1
            ## Story: S1 [2d]
            ## Story: S2 [3d]
            ### Task: T1 [1d]
            """;

        var result = await _parser.ParseAsync(md, MakeCalendar());
        var ids = result.Value!.RawItems.Select(i => i.LocalId).ToList();
        ids.Distinct().Should().HaveCount(ids.Count);
    }
}
