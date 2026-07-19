using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace SprintForge.Tests.Configuration;

/// <summary>
/// Verifies that config/profile.schema.json accepts well-formed profiles
/// and rejects profiles with plaintext credentials or missing required fields.
/// </summary>
public sealed class ProfileSchemaValidationTests
{
    private static readonly string SchemaPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../..", "config", "profile.schema.json"));

    private static JsonSchema LoadSchema()
    {
        var json = File.ReadAllText(SchemaPath);
        return JsonSchema.FromText(json);
    }

    private static JsonNode ValidProfile() => JsonNode.Parse("""
        {
          "id": "550e8400-e29b-41d4-a716-446655440000",
          "name": "Default",
          "version": 1,
          "jira": {
            "baseUrl": "https://your-org.atlassian.net",
            "userName": "user@example.com",
            "apiTokenRef": "dpapi:AAABBBCCC="
          },
          "ai": {
            "defaultProviderId": "anthropic-main",
            "providers": [
              {
                "id": "anthropic-main",
                "vendor": "Anthropic",
                "model": "claude-sonnet-5",
                "apiKeyRef": "dpapi:DDDEEEFFF="
              }
            ]
          },
          "sprint": {
            "defaultSprintLengthDays": 14
          },
          "workingDirectory": {
            "path": "C:\\SprintForge"
          },
          "audit": {},
          "logging": {}
        }
        """)!;

    [Fact]
    public void ValidProfile_PassesSchemaValidation()
    {
        var schema = LoadSchema();
        var result = schema.Evaluate(ValidProfile(), new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        Assert.True(result.IsValid,
            $"Expected valid profile to pass. Errors: {string.Join("; ", result.Details.Where(d => !d.IsValid).Select(d => $"{d.InstanceLocation}: {d.Errors?.FirstOrDefault().Value}"))}");
    }

    [Fact]
    public void PlaintextJiraToken_FailsSchemaValidation()
    {
        var profile = ValidProfile();
        profile!["jira"]!["apiTokenRef"] = JsonValue.Create("sk-my-secret-token");

        var schema = LoadSchema();
        var result = schema.Evaluate(profile, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.False(result.IsValid, "Profile with plaintext Jira token must fail schema validation.");
    }

    [Fact]
    public void PlaintextAiApiKey_FailsSchemaValidation()
    {
        var profile = ValidProfile();
        var providers = profile!["ai"]!["providers"]!.AsArray();
        providers[0]!["apiKeyRef"] = JsonValue.Create("sk-openai-raw-key-here");

        var schema = LoadSchema();
        var result = schema.Evaluate(profile, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.False(result.IsValid, "Profile with plaintext AI API key must fail schema validation.");
    }

    [Fact]
    public void MissingRequiredField_FailsSchemaValidation()
    {
        // Remove the required "jira" object entirely
        var profile = ValidProfile()!.AsObject();
        profile.Remove("jira");

        var schema = LoadSchema();
        var result = schema.Evaluate(profile, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.False(result.IsValid, "Profile missing required 'jira' object must fail schema validation.");
    }

    [Fact]
    public void InvalidVendorEnum_FailsSchemaValidation()
    {
        var profile = ValidProfile();
        profile!["ai"]!["providers"]!.AsArray()[0]!["vendor"] = JsonValue.Create("FakeVendor");

        var schema = LoadSchema();
        var result = schema.Evaluate(profile, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.False(result.IsValid, "Profile with unknown AI vendor must fail schema validation.");
    }

    [Fact]
    public void DpapiRefWithCorrectPrefix_IsAccepted()
    {
        // Any base64 payload after "dpapi:" should be accepted
        var profile = ValidProfile();
        profile!["jira"]!["apiTokenRef"] = JsonValue.Create("dpapi:dGhpcyBpcyBhIHRlc3Q=");

        var schema = LoadSchema();
        var result = schema.Evaluate(profile, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(result.IsValid, "Valid dpapi: handle must pass schema validation.");
    }
}
