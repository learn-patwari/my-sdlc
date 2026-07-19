using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SprintForge.Application.Ai;
using SprintForge.Application.Documents;
using SprintForge.Domain.Common;
using SprintForge.Domain.Documents;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Generates a Software Architecture Document via AI + a native draw.io diagram.
///   Returns a <see cref="DocumentDraft"/> with the draw.io XML in AdditionalFiles["architecture.drawio"].
///   Nothing is written to disk here — the caller routes through the Approvals Center.
/// </summary>
public sealed class SadDocumentGenerator : IDocumentGenerator
{
    private readonly IAiOrchestrator _ai;
    private readonly IDrawioWriter _drawio;

    public DocumentKind Kind => DocumentKind.Sad;

    public SadDocumentGenerator(IAiOrchestrator ai, IDrawioWriter drawio)
    {
        _ai = ai;
        _drawio = drawio;
    }

    public async Task<Result<DocumentDraft>> GenerateAsync(DocumentGenerationRequest request, CancellationToken ct = default)
    {
        // Parse services from the input markdown (comma-separated or bullet list)
        var services = ParseServices(request.InputMarkdown);

        // Generate the draw.io diagram natively
        var spec = BuildDiagramSpec(request.DocumentId, services);
        var drawioXml = _drawio.CreateDiagram(spec);

        // Generate SAD markdown via AI
        var aiRequest = new AiRequest
        {
            SystemPrompt = SadSystemPrompt,
            UserPrompt = BuildUserPrompt(request, services, drawioXml),
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
            Kind = DocumentKind.Sad,
            ContentMarkdown = content,
            ContentHash = hash,
            TemplateUsed = request.TemplateId ?? "default-sad",
            JiraIssueKey = request.JiraIssueKey,
            CorrelationId = request.CorrelationId,
            GeneratedAt = DateTimeOffset.UtcNow,
            AdditionalFiles = new Dictionary<string, string>
            {
                ["architecture.drawio"] = drawioXml
            }
        });
    }

    private static IReadOnlyList<string> ParseServices(string inputMarkdown)
    {
        var services = new List<string>();
        foreach (var line in inputMarkdown.Split('\n'))
        {
            var trimmed = line.TrimStart('-', '*', '+', ' ', '\t').Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length < 80)
                services.Add(trimmed);
        }
        return services.Count > 0 ? services : ["API Gateway", "Application Service", "Database"];
    }

    private static DiagramSpec BuildDiagramSpec(string documentId, IReadOnlyList<string> services)
    {
        var components = new List<DiagramComponent>();
        var relationships = new List<DiagramRelationship>();

        // Always include an API gateway
        components.Add(new DiagramComponent { Id = "gateway", Label = "API Gateway", Kind = ComponentKind.ApiGateway });

        string? previousId = "gateway";
        for (int i = 0; i < services.Count && i < 8; i++)
        {
            var id = $"svc{i}";
            var name = services[i];
            var kind = DetermineKind(name);
            components.Add(new DiagramComponent { Id = id, Label = name, Kind = kind });

            if (kind is ComponentKind.Service or ComponentKind.ApiGateway)
                relationships.Add(new DiagramRelationship { SourceId = previousId!, TargetId = id, Label = "REST", Style = RelationshipStyle.Sync });
            else if (kind == ComponentKind.MessageBroker)
                relationships.Add(new DiagramRelationship { SourceId = previousId!, TargetId = id, Style = RelationshipStyle.Async });
            else if (kind == ComponentKind.Database)
                relationships.Add(new DiagramRelationship { SourceId = previousId!, TargetId = id, Style = RelationshipStyle.Database });
            else if (kind == ComponentKind.Cache)
                relationships.Add(new DiagramRelationship { SourceId = previousId!, TargetId = id, Style = RelationshipStyle.Cache });

            if (kind == ComponentKind.Service) previousId = id;
        }

        return new DiagramSpec { Title = documentId, Components = components, Relationships = relationships };
    }

    private static ComponentKind DetermineKind(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("gateway") || lower.Contains("proxy")) return ComponentKind.ApiGateway;
        if (lower.Contains("database") || lower.Contains("postgres") || lower.Contains("mysql") || lower.Contains("mongo")) return ComponentKind.Database;
        if (lower.Contains("redis") || lower.Contains("cache") || lower.Contains("memcache")) return ComponentKind.Cache;
        if (lower.Contains("kafka") || lower.Contains("rabbit") || lower.Contains("queue") || lower.Contains("bus") || lower.Contains("broker")) return ComponentKind.MessageBroker;
        if (lower.Contains("external") || lower.Contains("third") || lower.Contains("aws") || lower.Contains("azure")) return ComponentKind.ExternalSystem;
        return ComponentKind.Service;
    }

    private static string BuildUserPrompt(DocumentGenerationRequest r, IReadOnlyList<string> services, string drawioXml)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Project Key: {r.JiraProjectKey}");
        sb.AppendLine($"Document ID: {r.DocumentId}");
        sb.AppendLine($"Jira Issue: {r.JiraIssueKey ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("Identified Services/Components:");
        foreach (var svc in services) sb.AppendLine($"  - {svc}");
        sb.AppendLine();
        sb.AppendLine("Input Description:");
        sb.AppendLine(r.InputMarkdown);
        sb.AppendLine();
        sb.AppendLine("A Draw.io architecture diagram has already been generated (attached as architecture.drawio).");
        sb.AppendLine("Reference it when describing component relationships.");
        sb.AppendLine();
        sb.AppendLine("Generate the complete SAD document.");
        return sb.ToString();
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private const string SadSystemPrompt = """
        You are a senior software architect creating enterprise-grade Software Architecture Documents.

        Generate a comprehensive SAD. Rules:
        - Describe every component with its responsibility, technology, and interfaces
        - Reference the pre-generated architecture diagram in the Architecture Diagram section
        - Include explicit trade-off analysis for key architectural decisions
        - Use Markdown with proper heading hierarchy
        - Include sequence diagrams using Mermaid code blocks where applicable

        Required structure:

        # 1. Introduction
        ## 1.1 Purpose and Scope
        ## 1.2 Stakeholders
        ## 1.3 Definitions and Abbreviations

        # 2. Architectural Overview
        ## 2.1 Architecture Style and Rationale
        ## 2.2 Architecture Diagram
        *(Reference: architecture.drawio — open in Draw.io for full interactive diagram)*
        ## 2.3 Key Architectural Decisions

        # 3. Component Descriptions
        *(one sub-section per component: responsibility, technology, interfaces, dependencies)*

        # 4. Data Architecture
        ## 4.1 Data Flow
        ## 4.2 Data Storage
        ## 4.3 Data Consistency and Transactions

        # 5. Integration and Interface Design
        ## 5.1 API Design
        ## 5.2 Event/Message Contracts
        ## 5.3 External System Integrations

        # 6. Non-Functional Architecture
        ## 6.1 Scalability
        ## 6.2 Resilience and Fault Tolerance
        ## 6.3 Security Architecture
        ## 6.4 Observability

        # 7. Deployment Architecture
        ## 7.1 Environment Overview
        ## 7.2 Infrastructure Requirements

        # 8. Architecture Trade-offs and Risks

        Return ONLY the Markdown document. No preamble, no commentary.
        """;
}
