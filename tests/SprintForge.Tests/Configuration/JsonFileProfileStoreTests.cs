using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SprintForge.Domain.Configuration;
using SprintForge.Infrastructure.Configuration;
using Xunit;

namespace SprintForge.Tests.Configuration;

public sealed class JsonFileProfileStoreTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"sf-profiles-{Guid.NewGuid():N}");
    private readonly JsonFileProfileStore _store;

    public JsonFileProfileStoreTests()
        => _store = new JsonFileProfileStore(_rootPath, NullLogger<JsonFileProfileStore>.Instance);

    [Fact(DisplayName = "SaveAsync rejects a profile with plaintext Jira token")]
    public async Task Save_WithPlaintextJiraToken_Throws()
    {
        var badProfile = BuildProfile(jiraTokenRef: "plaintext-secret-not-a-handle");
        var act = async () => await _store.SaveAsync(badProfile);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*plaintext Jira token*");
    }

    [Fact(DisplayName = "SaveAsync rejects a profile with plaintext AI provider key")]
    public async Task Save_WithPlaintextAiKey_Throws()
    {
        var badProfile = BuildProfile(aiKeyRef: "not-a-dpapi-handle");
        var act = async () => await _store.SaveAsync(badProfile);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*plaintext ApiKeyRef*");
    }

    [Fact(DisplayName = "Round-trip SaveAsync / LoadAsync returns identical profile")]
    public async Task RoundTrip_SaveAndLoad_ReturnsEqualProfile()
    {
        var profile = BuildProfile();
        await _store.SaveAsync(profile);

        var loaded = await _store.LoadAsync(profile.ProfileId);
        loaded.IsSuccess.Should().BeTrue();
        loaded.Value!.ProfileId.Should().Be(profile.ProfileId);
        loaded.Value.Jira.ApiTokenRef.Should().Be("dpapi:jira-test-token");
    }

    [Fact(DisplayName = "LoadAsync returns failure for unknown profile ID")]
    public async Task Load_UnknownId_ReturnsFailure()
    {
        var result = await _store.LoadAsync("does-not-exist");
        result.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "ListAsync returns saved profile in listing")]
    public async Task ListAsync_ReturnsSavedProfile()
    {
        var profile = BuildProfile();
        await _store.SaveAsync(profile);

        var list = await _store.ListAsync();
        list.IsSuccess.Should().BeTrue();
        list.Value.Should().Contain(s => s.ProfileId == profile.ProfileId);
    }

    [Fact(DisplayName = "ValidateAsync flags missing dpapi: prefix on tokens")]
    public async Task Validate_MissingDpapiPrefix_ReturnsErrors()
    {
        var profile = BuildProfile(jiraTokenRef: "plain");
        var errors = await _store.ValidateAsync(profile);
        errors.IsSuccess.Should().BeTrue();
        errors.Value.Should().Contain(e => e.Contains("DPAPI handle"));
    }

    [Fact(DisplayName = "SetActiveProfile makes profile available via ActiveProfile")]
    public void SetActiveProfile_MakesProfileAvailable()
    {
        var profile = BuildProfile();
        _store.SetActiveProfile(profile);
        _store.ActiveProfile.Should().NotBeNull();
        _store.ActiveProfile!.ProfileId.Should().Be(profile.ProfileId);
    }

    // ── helpers ──

    private static Profile BuildProfile(
        string jiraTokenRef = "dpapi:jira-test-token",
        string aiKeyRef = "dpapi:ai-test-key") => new()
    {
        ProfileId = "profile-test-001",
        Name = "CI Test Profile",
        SchemaVersion = "1.0",
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
        Jira = new JiraSettings
        {
            ServerUrl = "https://jira.example.com",
            ApiDialect = "v3",
            Username = "ci@example.com",
            ApiTokenRef = jiraTokenRef,
            ProjectKeys = ["CI"],
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
            Providers = [new Domain.Configuration.AiProvider
            {
                Id = "openai-ci",
                Vendor = "OpenAI",
                Endpoint = "https://api.openai.com",
                ApiKeyRef = aiKeyRef,
                Model = "gpt-4o"
            }],
            DefaultProviderId = "openai-ci"
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
        WorkingDirectory = new WorkingDirectorySettings { RootPath = "/tmp", TemplatesPath = "/tmp/templates" },
        Audit = new AuditSettings { RetentionDays = 365 },
        Logging = new LoggingSettings { MinimumLevel = "Information" }
    };

    public void Dispose()
    {
        if (Directory.Exists(_rootPath)) Directory.Delete(_rootPath, recursive: true);
    }
}
