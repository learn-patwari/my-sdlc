namespace SprintForge.Domain.Configuration;

/// <summary>Top-level configuration profile. Profiles are JSON files; secrets are stored as dpapi: handles.</summary>
public sealed record Profile
{
    public required string ProfileId { get; init; }
    public required string Name { get; init; }
    public required string SchemaVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastModifiedAt { get; init; }

    public required JiraSettings Jira { get; init; }
    public required AiSettings Ai { get; init; }
    public required SprintSettings Sprint { get; init; }
    public required WorkingDirectorySettings WorkingDirectory { get; init; }
    public required AuditSettings Audit { get; init; }
    public required LoggingSettings Logging { get; init; }
}

public sealed record JiraSettings
{
    public required string ServerUrl { get; init; }
    public required string ApiDialect { get; init; }
    public required string Username { get; init; }

    /// <summary>DPAPI handle (e.g., "dpapi:jira-token-acme"). Never contains plaintext.</summary>
    public required string ApiTokenRef { get; init; }

    public required IReadOnlyList<string> ProjectKeys { get; init; }
    public required IReadOnlyList<string> DefaultLabels { get; init; }
    public required IReadOnlyList<string> DefaultComponents { get; init; }
    public required JiraFieldMappings FieldMappings { get; init; }
    public string? CustomCaCertPath { get; init; }
}

public sealed record JiraFieldMappings
{
    public required string StoryPoints { get; init; }
    public required string Sprint { get; init; }
    public required string EpicLink { get; init; }
    public string? Team { get; init; }
}

public sealed record AiSettings
{
    public required IReadOnlyList<AiProvider> Providers { get; init; }
    public required string DefaultProviderId { get; init; }
    public string? FallbackProviderId { get; init; }
    public decimal? CostAlertThreshold { get; init; }
}

public sealed record AiProvider
{
    public required string Id { get; init; }
    public required string Vendor { get; init; }
    public required string Endpoint { get; init; }

    /// <summary>DPAPI handle for the API key.</summary>
    public required string ApiKeyRef { get; init; }

    public required string Model { get; init; }
    public double Temperature { get; init; } = 0.2;
    public int MaxTokens { get; init; } = 8192;
    public int ContextWindow { get; init; } = 200000;
    public int? Seed { get; init; }
    public string? CustomCaCertPath { get; init; }
}

public sealed record SprintSettings
{
    public required int DurationDays { get; init; }
    public required int DevelopmentDays { get; init; }
    public required int BufferDays { get; init; }
    public required int WorkingHoursPerDay { get; init; }
    public required IReadOnlyList<DayOfWeek> Workweek { get; init; }
    public required IReadOnlyList<DateOnly> Holidays { get; init; }
}

public sealed record WorkingDirectorySettings
{
    public required string RootPath { get; init; }
    public required string TemplatesPath { get; init; }
}

public sealed record AuditSettings
{
    public bool EnableDigitalSignature { get; init; }
    public string? SigningCertificatePath { get; init; }
    public int RetentionDays { get; init; } = 365;
}

public sealed record LoggingSettings
{
    public required string MinimumLevel { get; init; }
    public string? LogFilePath { get; init; }
}
