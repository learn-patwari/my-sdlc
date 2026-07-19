using System.Security.Cryptography;
using System.Text;
using SprintForge.Application.Ai;
using SprintForge.Application.Documents;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Generates a Software Requirements Specification via the AI orchestrator.
///   Returns a <see cref="DocumentDraft"/> — nothing is written to disk here.
///   The caller routes the draft through the Approvals Center before persisting.
/// </summary>
public sealed class SrsDocumentGenerator : IDocumentGenerator
{
    private readonly IAiOrchestrator _ai;

    public DocumentKind Kind => DocumentKind.Srs;

    public SrsDocumentGenerator(IAiOrchestrator ai) => _ai = ai;

    public async Task<Result<DocumentDraft>> GenerateAsync(DocumentGenerationRequest request, CancellationToken ct = default)
    {
        var aiRequest = new AiRequest
        {
            SystemPrompt = SrsSystemPrompt,
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
            Kind = DocumentKind.Srs,
            ContentMarkdown = content,
            ContentHash = hash,
            TemplateUsed = request.TemplateId ?? "default-srs",
            JiraIssueKey = request.JiraIssueKey,
            CorrelationId = request.CorrelationId,
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }

    private static string BuildUserPrompt(DocumentGenerationRequest r) =>
        $"""
        Project Key: {r.JiraProjectKey}
        Jira Issue: {r.JiraIssueKey ?? "N/A"}
        Correlation ID: {r.CorrelationId}

        Input Requirements:
        {r.InputMarkdown}

        Generate the complete SRS document.
        """;

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private const string SrsSystemPrompt = """
        You are a senior systems analyst creating enterprise-grade Software Requirements Specifications.

        Generate a comprehensive SRS following IEEE 830 format. Rules:
        - Every functional requirement must be verifiable with measurable acceptance criteria
        - Number functional requirements FR-001, FR-002, … and non-functional NFR-001, NFR-002, …
        - Use Markdown with proper heading hierarchy
        - Use tables for requirement listings (ID | Priority | Description | Acceptance Criteria)
        - Be specific, formal, and unambiguous — no vague language

        Required structure:

        # 1. Introduction
        ## 1.1 Purpose
        ## 1.2 Scope
        ## 1.3 Definitions and Abbreviations
        ## 1.4 References
        ## 1.5 Document Overview

        # 2. Overall Description
        ## 2.1 Product Perspective
        ## 2.2 Product Functions
        ## 2.3 User Classes and Characteristics
        ## 2.4 Operating Environment
        ## 2.5 Constraints and Assumptions

        # 3. Functional Requirements
        (table: FR-ID | Priority | Requirement Description | Acceptance Criteria)

        # 4. Non-Functional Requirements
        ## 4.1 Performance
        ## 4.2 Security
        ## 4.3 Reliability and Availability
        ## 4.4 Maintainability
        ## 4.5 Scalability

        # 5. External Interface Requirements
        ## 5.1 User Interfaces
        ## 5.2 Software Interfaces
        ## 5.3 Communication Interfaces

        # 6. System Constraints and Assumptions

        Return ONLY the Markdown document. No preamble, no commentary.
        """;
}
