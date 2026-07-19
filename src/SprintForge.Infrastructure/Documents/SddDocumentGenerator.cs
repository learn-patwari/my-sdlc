using System.Security.Cryptography;
using System.Text;
using SprintForge.Application.Ai;
using SprintForge.Application.Documents;
using SprintForge.Application.Sdlc;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Generates a Software Detailed Design document via AI.
///   Implements search-before-create: the service layer calls SearchExistingSddsAsync
///   before invoking this generator.
/// </summary>
public sealed class SddDocumentGenerator : IDocumentGenerator
{
    private readonly IAiOrchestrator _ai;
    private readonly ISdlcTool _sdlc;

    public DocumentKind Kind => DocumentKind.Sdd;

    public SddDocumentGenerator(IAiOrchestrator ai, ISdlcTool sdlc)
    {
        _ai = ai;
        _sdlc = sdlc;
    }

    public async Task<Result<DocumentDraft>> GenerateAsync(DocumentGenerationRequest request, CancellationToken ct = default)
    {
        var aiRequest = new AiRequest
        {
            SystemPrompt = SddSystemPrompt,
            UserPrompt = BuildUserPrompt(request),
            Temperature = 0.2,
            MaxTokens = 8192,
            CorrelationId = request.CorrelationId
        };

        var completion = await _ai.RunAsync(aiRequest, ct: ct).ConfigureAwait(false);
        if (completion.IsFailure)
            return Result.Failure<DocumentDraft>($"AI generation failed: {completion.Error}");

        var content = completion.Value!.Content;
        var hash = ComputeSha256(content);

        return Result.Success(new DocumentDraft
        {
            DocumentId = request.DocumentId,
            Kind = DocumentKind.Sdd,
            ContentMarkdown = content,
            ContentHash = hash,
            TemplateUsed = request.TemplateId ?? "default-sdd",
            JiraIssueKey = request.JiraIssueKey,
            CorrelationId = request.CorrelationId,
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    ///   Searches configured Jira projects for existing SDD issues matching the service name.
    ///   Returns an empty list (not a failure) when no matches are found.
    /// </summary>
    public async Task<Result<IReadOnlyList<ExistingSddMatch>>> SearchExistingAsync(
        string serviceName, IReadOnlyList<string> projectKeys, CancellationToken ct = default)
    {
        var query = new SdlcSearchQuery
        {
            ProjectKeys = projectKeys,
            Summary = $"SDD {serviceName}",
            MaxResults = 10
        };

        var searchResult = await _sdlc.SearchIssuesAsync(query, ct).ConfigureAwait(false);
        if (searchResult.IsFailure)
            return Result.Success<IReadOnlyList<ExistingSddMatch>>([]);

        var matches = searchResult.Value!
            .Where(i => i.Summary.Contains("SDD", StringComparison.OrdinalIgnoreCase) ||
                        i.Summary.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
            .Select(i => new ExistingSddMatch
            {
                JiraIssueKey = i.Key,
                Summary = i.Summary,
                Description = i.Description,
                Status = i.Status,
                ProjectKey = i.Key.Split('-')[0]
            })
            .ToList();

        return Result.Success<IReadOnlyList<ExistingSddMatch>>(matches);
    }

    private static string BuildUserPrompt(DocumentGenerationRequest r) =>
        $"""
        Project Key: {r.JiraProjectKey}
        Document ID: {r.DocumentId}
        Jira Issue: {r.JiraIssueKey ?? "N/A"}
        Correlation ID: {r.CorrelationId}

        Service/Component Description:
        {r.InputMarkdown}

        Generate the complete SDD document.
        """;

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private const string SddSystemPrompt = """
        You are a senior software engineer creating detailed Software Design Documents.

        Generate a comprehensive SDD for a single service or component. Rules:
        - Document every public method/API with signature, parameters, return value, and logic summary
        - Include Mermaid sequence diagrams for key flows
        - List all dependencies with version notes where known
        - Describe error handling strategies explicitly
        - Number test case ideas as TC-001, TC-002, …
        - Use Markdown with proper heading hierarchy

        Required structure:

        # 1. Overview
        ## 1.1 Purpose
        ## 1.2 Scope and Boundaries
        ## 1.3 Dependencies

        # 2. Design Details
        ## 2.1 Class/Module Structure
        ## 2.2 Public API
        *(table: Method | Parameters | Return Type | Description)*
        ## 2.3 Data Models
        ## 2.4 Sequence Diagrams
        *(Mermaid sequenceDiagram blocks for primary flows)*

        # 3. Error Handling and Edge Cases
        ## 3.1 Error Scenarios
        ## 3.2 Retry and Fallback Strategy
        ## 3.3 Known Limitations

        # 4. Test Case Design
        *(table: TC-ID | Test Type | Description | Expected Outcome)*
        ## 4.1 Positive Cases
        ## 4.2 Negative Cases
        ## 4.3 Boundary Cases
        ## 4.4 Exception Cases

        # 5. Performance Considerations

        Return ONLY the Markdown document. No preamble, no commentary.
        """;
}
