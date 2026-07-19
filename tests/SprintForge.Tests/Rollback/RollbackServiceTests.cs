using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SprintForge.Application.Audit;
using SprintForge.Application.Sdlc;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;
using SprintForge.Infrastructure.Audit;
using SprintForge.Infrastructure.Rollback;

namespace SprintForge.Tests.Rollback;

public sealed class RollbackServiceTests
{
    private readonly IAuditService _auditSvc = Substitute.For<IAuditService>();
    private readonly ISdlcTool _sdlc = Substitute.For<ISdlcTool>();

    private RollbackService CreateSut()
    {
        var runner = new AuditedOperationRunner(_auditSvc, NullLogger<AuditedOperationRunner>.Instance);
        return new RollbackService(_auditSvc, _sdlc, runner, NullLogger<RollbackService>.Instance);
    }

    private static AuditRecord MakeRecord(
        string auditId,
        string correlationId,
        AuditAction action = AuditAction.JiraCreate,
        AuditStatus status = AuditStatus.Completed,
        string? outputsJson = null) => new()
    {
        AuditId = auditId,
        CorrelationId = correlationId,
        Module = AuditModule.Jira,
        Action = action,
        Status = status,
        UserName = "user1",
        MachineName = "dev",
        UtcTimestamp = DateTimeOffset.UtcNow,
        PreviousHash = "0000000000000000",
        InputsJson = "{}",
        OutputsJson = outputsJson
    };

    [Fact]
    public async Task GetCandidatesAsync_CompletedJiraCreate_IncludesRollbackable()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        _auditSvc.SearchAsync(Arg.Any<AuditSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditRecord>
            {
                MakeRecord("au-1", correlationId, AuditAction.JiraCreate, AuditStatus.Completed)
            });

        var result = await CreateSut().GetCandidatesAsync(correlationId);

        result.IsSuccess.Should().BeTrue();
        var candidates = result.Value!;
        candidates.Should().HaveCount(1);
        candidates[0].IsRollbackable.Should().BeTrue();
        candidates[0].AuditId.Should().Be("au-1");
    }

    [Fact]
    public async Task GetCandidatesAsync_FailedRecord_ExcludedFromCandidates()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        _auditSvc.SearchAsync(Arg.Any<AuditSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditRecord>
            {
                MakeRecord("au-1", correlationId, AuditAction.JiraCreate, AuditStatus.Failed)
            });

        var result = await CreateSut().GetCandidatesAsync(correlationId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCandidatesAsync_NonJiraAction_MarkedNonRollbackable()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        _auditSvc.SearchAsync(Arg.Any<AuditSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditRecord>
            {
                MakeRecord("au-1", correlationId, AuditAction.SrsGeneration, AuditStatus.Completed)
            });

        var result = await CreateSut().GetCandidatesAsync(correlationId);

        result.Value![0].IsRollbackable.Should().BeFalse();
        result.Value![0].NonRollbackableReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RollbackAsync_AuditIdNotFound_ReturnsFailure()
    {
        _auditSvc.SearchAsync(Arg.Any<AuditSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditRecord>());

        var result = await CreateSut().RollbackAsync("au-unknown", "test", "user1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task RollbackAsync_JiraCreateWithKey_CallsTransition()
    {
        var auditId = "au-jira-1";
        var correlationId = Guid.NewGuid().ToString("N");
        var record = MakeRecord(auditId, correlationId, AuditAction.JiraCreate, AuditStatus.Completed,
            outputsJson: """["RBP-101"]""");

        _auditSvc.SearchAsync(Arg.Any<AuditSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditRecord> { record });
        _auditSvc.RecordStartAsync(Arg.Any<AuditContext>(), Arg.Any<CancellationToken>())
            .Returns("au-rollback-1");
        _auditSvc.RecordCompletedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _sdlc.TransitionIssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(true));

        var result = await CreateSut().RollbackAsync(auditId, "test rollback", "user1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("RBP-101");
        await _sdlc.Received().TransitionIssueAsync(
            "RBP-101",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RollbackAsync_NoJiraKeys_ReturnsFailure()
    {
        var auditId = "au-1";
        var correlationId = Guid.NewGuid().ToString("N");
        var record = MakeRecord(auditId, correlationId, AuditAction.JiraCreate, AuditStatus.Completed,
            outputsJson: "{}");

        _auditSvc.SearchAsync(Arg.Any<AuditSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditRecord> { record });

        var result = await CreateSut().RollbackAsync(auditId, "reason", "user1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No Jira keys found");
    }

    [Fact]
    public async Task RollbackAsync_NonRollbackableAction_ReturnsFailure()
    {
        var auditId = "au-1";
        var correlationId = Guid.NewGuid().ToString("N");
        var record = MakeRecord(auditId, correlationId, AuditAction.SrsGeneration, AuditStatus.Completed);

        _auditSvc.SearchAsync(Arg.Any<AuditSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditRecord> { record });

        var result = await CreateSut().RollbackAsync(auditId, "reason", "user1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not supported");
    }
}
