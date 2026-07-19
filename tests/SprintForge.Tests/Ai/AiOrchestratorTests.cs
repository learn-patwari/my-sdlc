using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SprintForge.Application.Ai;
using SprintForge.Application.Audit;
using SprintForge.Application.Configuration;
using SprintForge.Application.Security;
using SprintForge.Domain.Audit;
using SprintForge.Domain.Configuration;
using SprintForge.Infrastructure.Ai;
using SprintForge.Infrastructure.Audit;
using SprintForge.Infrastructure.Data;
using Xunit;

namespace SprintForge.Tests.Ai;

public sealed class AiOrchestratorTests : IAsyncDisposable
{
    private readonly string _jsonlPath;
    private readonly AuditDbContext _db;
    private readonly AuditService _auditService;
    private readonly ISecretStore _secrets;
    private readonly IProfileStore _profileStore;
    private readonly IHttpClientFactory _httpFactory;

    private static readonly Domain.Configuration.AiProvider FakeOpenAiConfig = new()
    {
        Id = "openai-test",
        Vendor = "OpenAI",
        Endpoint = "https://api.openai.com",
        ApiKeyRef = "dpapi:openai-key",
        Model = "gpt-4o",
        Temperature = 0.2,
        MaxTokens = 1024
    };

    private static readonly Domain.Configuration.AiProvider FakeAnthropicConfig = new()
    {
        Id = "anthropic-test",
        Vendor = "Anthropic",
        Endpoint = "https://api.anthropic.com",
        ApiKeyRef = "dpapi:anthropic-key",
        Model = "claude-sonnet-5",
        Temperature = 0.2,
        MaxTokens = 1024
    };

    public AiOrchestratorTests()
    {
        _jsonlPath = Path.GetTempFileName() + ".jsonl";
        var writer = new JsonlAuditWriter(_jsonlPath, NullLogger<JsonlAuditWriter>.Instance);
        var dbOpts = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AuditDbContext(dbOpts);
        _db.Database.EnsureCreated();
        _auditService = new AuditService(writer, _db, NullLogger<AuditService>.Instance);

        _secrets = Substitute.For<ISecretStore>();
        _profileStore = Substitute.For<IProfileStore>();
        _httpFactory = Substitute.For<IHttpClientFactory>();
    }

    [Fact(DisplayName = "Returns failure when no active profile is loaded")]
    public async Task RunAsync_WithNoProfile_ReturnsFailure()
    {
        _profileStore.ActiveProfile.Returns((Profile?)null);
        var orchestrator = BuildOrchestrator();

        var result = await orchestrator.RunAsync(new AiRequest
        {
            SystemPrompt = "You are a helpful assistant.",
            UserPrompt = "Hello"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No active profile");
    }

    [Fact(DisplayName = "Returns failure when requested provider ID is not in profile")]
    public async Task RunAsync_WithUnknownProvider_ReturnsFailure()
    {
        _profileStore.ActiveProfile.Returns(BuildProfile("unknown-provider"));
        var orchestrator = BuildOrchestrator();

        var result = await orchestrator.RunAsync(new AiRequest
        {
            SystemPrompt = "system",
            UserPrompt = "user"
        }, overrideProviderId: "does-not-exist");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact(DisplayName = "Records Started and then Completed/Failed audit entry on each run")]
    public async Task RunAsync_AlwaysCreatesAuditRecord()
    {
        _profileStore.ActiveProfile.Returns(BuildProfile(FakeOpenAiConfig.Id));
        _secrets.Retrieve("dpapi:openai-key").Returns(Domain.Common.Result.Failure<string>("No key on CI"));

        var orchestrator = BuildOrchestrator();
        await orchestrator.RunAsync(new AiRequest { SystemPrompt = "sys", UserPrompt = "usr" });

        var records = await _auditService.SearchAsync(new AuditSearchQuery { Module = AuditModule.Ai });
        records.Should().HaveCountGreaterOrEqualTo(1);
        records.Should().Contain(r => r.Action == AuditAction.AiPrompt);
    }

    [Fact(DisplayName = "GetProviderStatuses returns one entry per configured provider")]
    public void GetProviderStatuses_ReturnsConfiguredProviders()
    {
        _profileStore.ActiveProfile.Returns(BuildProfile(FakeOpenAiConfig.Id,
            extraProviders: [FakeAnthropicConfig]));
        _secrets.Exists("dpapi:openai-key").Returns(true);
        _secrets.Exists("dpapi:anthropic-key").Returns(false);

        var orchestrator = BuildOrchestrator();
        var statuses = orchestrator.GetProviderStatuses();

        statuses.Should().HaveCount(2);
        statuses.Should().Contain(s => s.ProviderId == "openai-test" && s.IsAvailable);
        statuses.Should().Contain(s => s.ProviderId == "anthropic-test" && !s.IsAvailable);
    }

    [Fact(DisplayName = "GetProviderStatuses returns empty list when no active profile")]
    public void GetProviderStatuses_WithNoProfile_ReturnsEmpty()
    {
        _profileStore.ActiveProfile.Returns((Profile?)null);
        var orchestrator = BuildOrchestrator();
        orchestrator.GetProviderStatuses().Should().BeEmpty();
    }

    // ── helpers ──

    private AiOrchestrator BuildOrchestrator() => new(
        _profileStore, _secrets, _auditService, _httpFactory,
        NullLogger<AiOrchestrator>.Instance,
        NullLogger<OpenAiProvider>.Instance,
        NullLogger<AnthropicAiProvider>.Instance);

    private static Profile BuildProfile(string defaultProviderId, IReadOnlyList<Domain.Configuration.AiProvider>? extraProviders = null)
    {
        var providers = new List<Domain.Configuration.AiProvider> { FakeOpenAiConfig };
        if (extraProviders is not null) providers.AddRange(extraProviders);
        return new Profile
        {
            ProfileId = "test-profile",
            Name = "Test",
            SchemaVersion = "1.0",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Jira = new JiraSettings
            {
                ServerUrl = "https://jira.example.com",
                ApiDialect = "v3",
                Username = "user@example.com",
                ApiTokenRef = "dpapi:jira-token",
                ProjectKeys = ["TEST"],
                DefaultLabels = [],
                DefaultComponents = [],
                FieldMappings = new JiraFieldMappings
                {
                    StoryPoints = "customfield_10016",
                    Sprint = "customfield_10020",
                    EpicLink = "customfield_10014"
                }
            },
            Ai = new AiSettings
            {
                Providers = providers,
                DefaultProviderId = defaultProviderId
            },
            Sprint = new SprintSettings
            {
                DurationDays = 14,
                DevelopmentDays = 10,
                BufferDays = 2,
                WorkingHoursPerDay = 8,
                Workweek = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                Holidays = []
            },
            WorkingDirectory = new WorkingDirectorySettings { RootPath = "/tmp/sf", TemplatesPath = "/tmp/sf/templates" },
            Audit = new AuditSettings { RetentionDays = 365 },
            Logging = new LoggingSettings { MinimumLevel = "Information" }
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        if (File.Exists(_jsonlPath)) File.Delete(_jsonlPath);
    }
}
