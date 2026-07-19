using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SprintForge.Application.Audit;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Common;
using SprintForge.Infrastructure.Audit;
using SprintForge.Infrastructure.Data;
using Xunit;

namespace SprintForge.Tests.Audit;

public sealed class WriteGateTests : IAsyncDisposable
{
    private readonly string _jsonlPath;
    private readonly AuditDbContext _db;
    private readonly JsonlAuditWriter _writer;
    private readonly AuditService _auditService;
    private readonly AuditedOperationRunner _runner;

    public WriteGateTests()
    {
        _jsonlPath = Path.GetTempFileName() + ".jsonl";
        _writer = new JsonlAuditWriter(_jsonlPath, NullLogger<JsonlAuditWriter>.Instance);

        var dbOpts = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AuditDbContext(dbOpts);
        _db.Database.EnsureCreated();

        _auditService = new AuditService(_writer, _db, NullLogger<AuditService>.Instance);
        _runner = new AuditedOperationRunner(_auditService, NullLogger<AuditedOperationRunner>.Instance);
    }

    [Fact(DisplayName = "Write is blocked when audit sink throws AuditUnavailableException")]
    public async Task WriteIsBlocked_WhenAuditFlushFails()
    {
        // Arrange — stub IAuditService that throws on RecordStart
        var brokenAudit = Substitute.For<IAuditService>();
        brokenAudit
            .RecordStartAsync(Arg.Any<AuditContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditUnavailableException("Simulated sink failure."));

        var blockedRunner = new AuditedOperationRunner(brokenAudit, NullLogger<AuditedOperationRunner>.Instance);
        var operation = new NoopWriteOperation();

        // Act
        var act = async () => await blockedRunner.RunAsync(operation, "corr-1", "testuser", "testmachine");

        // Assert — must throw, not swallow
        await act.Should().ThrowAsync<AuditUnavailableException>()
            .WithMessage("*Simulated sink failure*");
    }

    [Fact(DisplayName = "Successful operation produces Started + Completed audit records")]
    public async Task SuccessfulOperation_ProducesStartedAndCompletedRecords()
    {
        var operation = new NoopWriteOperation();

        var result = await _runner.RunAsync(operation, "corr-2", "testuser", "testmachine");

        result.IsSuccess.Should().BeTrue();

        var records = await _writer.ReadAllAsync();
        records.Should().HaveCount(2);
        records[0].Status.Should().Be(AuditStatus.Started);
        records[1].Status.Should().Be(AuditStatus.Completed);
    }

    [Fact(DisplayName = "Each record's PreviousHash equals prior record's SelfHash")]
    public async Task HashChain_IsValid_AfterMultipleOperations()
    {
        for (var i = 0; i < 3; i++)
            await _runner.RunAsync(new NoopWriteOperation(), $"corr-{i}", "testuser", "testmachine");

        var report = await _writer.VerifyChainAsync();

        report.IsValid.Should().BeTrue();
        report.RecordsChecked.Should().Be(6); // 3 ops × 2 records each
    }

    [Fact(DisplayName = "Failed operation produces a Failed audit record")]
    public async Task FailingOperation_ProducesFailedAuditRecord()
    {
        var operation = new FailingWriteOperation();

        var result = await _runner.RunAsync(operation, "corr-fail", "testuser", "testmachine");

        result.IsFailure.Should().BeTrue();
        var records = await _writer.ReadAllAsync();
        records.Should().Contain(r => r.Status == AuditStatus.Failed);
    }

    public async ValueTask DisposeAsync()
    {
        _writer.Dispose();
        await _db.DisposeAsync();
        if (File.Exists(_jsonlPath)) File.Delete(_jsonlPath);
    }
}

/// <summary>A no-op write operation for testing the write gate.</summary>
file sealed class NoopWriteOperation : IAuditedOperation<bool>
{
    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        UserName = userName,
        MachineName = machineName,
        Module = AuditModule.System,
        Action = AuditAction.AppStart,
        StartedAt = DateTimeOffset.UtcNow
    };

    public Task<Result<bool>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result.Success(true));
}

file sealed class FailingWriteOperation : IAuditedOperation<bool>
{
    public AuditContext BuildAuditContext(string correlationId, string userName, string machineName) => new()
    {
        AuditId = Guid.NewGuid().ToString("N"),
        CorrelationId = correlationId,
        UserName = userName,
        MachineName = machineName,
        Module = AuditModule.System,
        Action = AuditAction.AppStart,
        StartedAt = DateTimeOffset.UtcNow
    };

    public Task<Result<bool>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result.Failure<bool>("Deliberate operation failure."));
}
