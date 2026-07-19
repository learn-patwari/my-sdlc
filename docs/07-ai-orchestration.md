# 7. AI Orchestration

Covers specification output section **§11 AI Orchestration Design**. The AI orchestration layer is responsible for managing multiple provider adapters, coordinating agents, ensuring reproducibility, and maintaining a complete audit trail of every AI interaction.

## 7.1 Architecture overview

```
Module (e.g., SRS Generator)
    ↓
Agent (SrsAgent)
    ↓ Prepare prompt + variables
Orchestrator (IAiOrchestrator)
    ├→ Record PENDING audit event
    ├→ Render prompt template
    ├→ Call IAiProvider.Complete()
    ├→ Record COMPLETED/FAILED audit event
    └→ Return response
    ↑
Provider (OpenAI / Anthropic / Gemini / Ollama)
```

**Key principle:** every interaction is audited and reproducible. Given the same prompt, model, and sampling parameters (temperature, seed, top_p), the output must be identical.

## 7.2 AI Providers abstraction

**Design goal:** abstract provider differences behind a single interface so modules never reference vendor APIs directly.

### Interface

```csharp
interface IAiProvider {
  string ProviderId { get; }  // "openai", "anthropic", "gemini", "ollama"
  string DefaultModel { get; }
  
  Task<AiResponse> Complete(AiRequest request);
}

record AiRequest {
  string Model;
  string SystemPrompt;
  string UserPrompt;
  float Temperature = 0.7f;
  int MaxTokens = 8192;
  float TopP = 1.0f;
  int? Seed = null;  // optional; if provided, deterministic output
  Dictionary<string, object> CustomParameters;  // provider-specific (e.g., OpenAI's top_k)
}

record AiResponse {
  string Id;  // provider's unique request ID for audit trail
  string Content;
  int PromptTokens;
  int CompletionTokens;
  string FinishReason;  // "stop" | "length" | "error"
  string ErrorMessage;  // if FinishReason == "error"
  Dictionary<string, object> Metadata;  // provider-specific (e.g., log_probs, usage details)
}
```

### Adapter implementations

Each adapter translates the generic `AiRequest` to the provider's API and back:

**OpenAI / Azure OpenAI:**
- `AiRequest` → `ChatCompletionCreateRequest` (OpenAI SDK).
- Maps `Temperature`, `MaxTokens`, `TopP`, `Seed` directly.
- Custom parameters (e.g., `logit_bias`) flow through `CustomParameters`.
- Response includes `id` (request ID), `usage` (prompt/completion tokens).

**Anthropic (Claude):**
- `AiRequest` → `TextGenerationParameters` (Anthropic SDK or raw REST).
- Anthropic uses `temperature` but not `seed` (determinism via model snapshot); adapter logs "Seed parameter ignored for Anthropic" in audit metadata.
- Maps `MaxTokens` to `max_tokens`.
- `system` parameter is supported (mapped from `SystemPrompt`).

**Google Gemini:**
- `AiRequest` → `GenerateContentRequest`.
- Gemini uses `temperature` and `topP` but not `seed`.
- Adapter logs seed-unsupported in metadata.

**Ollama (OpenAI-compatible endpoint):**
- Ollama exposes an OpenAI-compatible `/v1/chat/completions` endpoint.
- `AiRequest` is marshalled as OpenAI format (same as OpenAI adapter but different endpoint URL).
- Run locally or remote; configuration specifies endpoint URL.

### Provider registration

Configuration file lists providers:

```jsonc
{
  "ai": {
    "providers": [
      {
        "id": "primary",
        "vendor": "anthropic",
        "endpoint": "https://api.anthropic.com",
        "secretRef": "dpapi:anthropic-key",
        "model": "claude-sonnet-5",
        "temperature": 0.2,
        "maxTokens": 8192,
        "seed": null
      },
      {
        "id": "fallback",
        "vendor": "openai",
        "endpoint": "https://api.openai.com",
        "secretRef": "dpapi:openai-key",
        "model": "gpt-4",
        "temperature": 0.3,
        "maxTokens": 4096
      }
    ],
    "defaultProviderId": "primary"
  }
}
```

On app startup, `AiProviderFactory` instantiates each adapter and registers it in a provider registry. Fallback is enabled: if primary fails, retry on fallback.

## 7.3 Agents

**Design goal:** agents encapsulate the logic for a specific use case (SRS, SAD, SDD, tests, etc.) and prepare the prompt, but do not call the provider directly. They delegate to the orchestrator.

### Agent interface

```csharp
interface IAiAgent {
  string Name { get; }  // "SrsAgent", "SadAgent", "TestsAgent", …
  string ProviderId { get; }  // which provider to use for this agent
  
  Task<AiResponse> Execute(AiRequest request, IAiOrchestrator orchestrator);
}

abstract class AiAgentBase : IAiAgent {
  protected IPromptTemplateStore TemplateStore { get; }
  protected IConfiguration Config { get; }
  
  protected async Task<AiResponse> ExecuteViaOrchestrator(
    PromptTemplate template,
    Dictionary<string, object> variables,
    IAiOrchestrator orchestrator) {
    
    var request = new AiRequest {
      SystemPrompt = await TemplateStore.GetSystemPrompt(template),
      UserPrompt = await TemplateStore.Render(template, variables),
      // other params from config
    };
    
    return await orchestrator.Execute(this, request);
  }
}
```

### Agent examples

**SrsAgent:**
- Template: `Templates/prompts/srs.prompt`
- Variables: brief description, SRS ID, technology stack, impacted services.
- Output: structured markdown with sections (functional requirements, acceptance criteria, risks, …).

**SadAgent:**
- Template: `Templates/prompts/sad.prompt`
- Variables: ticket reference, architecture style, technology stack, services from repo analysis.
- Output: architecture rationale + draw.io diagram XML + deployment recommendations.

**SddAgent:**
- Template: `Templates/prompts/sdd.prompt`
- Variables: service name, code snippet (first 100 lines), dependencies, external calls.
- Output: design document with methods, error handling, test ideas.

**TestsAgent:**
- Template: `Templates/prompts/tests.prompt`
- Variables: service name, methods to test, coverage target, framework.
- Output: test code in the specified framework.

**RepositoryAgent:**
- Template: `Templates/prompts/repo-analysis.prompt`
- Variables: file tree, language(s), recent commits.
- Output: structured analysis (services, patterns, tech stack, recommendations).

### Agent registry

On app startup, `AiAgentRegistry` auto-discovers and registers agents:

```csharp
class AiAgentRegistry {
  void RegisterAgent(IAiAgent agent);
  IAiAgent GetAgent(string name);
  IList<IAiAgent> ListAgents();
}
```

Agents are registered via DI:

```csharp
services.AddSingleton<SrsAgent>();
services.AddSingleton<SadAgent>();
services.AddSingleton<SddAgent>();
services.AddSingleton<TestsAgent>();
services.AddSingleton<RepositoryAgent>();

services.AddSingleton<AiAgentRegistry>(sp => {
  var registry = new AiAgentRegistry();
  registry.RegisterAgent(sp.GetRequiredService<SrsAgent>());
  registry.RegisterAgent(sp.GetRequiredService<SadAgent>());
  // …
  return registry;
});
```

## 7.4 Orchestrator

**Core responsibility:** coordinate provider calls, manage retries, enforce audit, ensure reproducibility.

### Interface

```csharp
interface IAiOrchestrator {
  Task<AiResponse> Execute(IAiAgent agent, AiRequest request);
}

class AiOrchestrator : IAiOrchestrator {
  private readonly IAuditService _audit;
  private readonly AiProviderRegistry _providerRegistry;
  private readonly Polly.IAsyncPolicy<AiResponse> _retryPolicy;
  
  public async Task<AiResponse> Execute(IAiAgent agent, AiRequest request) {
    // Step 1: Record PENDING audit event (prompt + params)
    var auditContext = new AuditContext {
      Module = "AiOrchestration",
      Action = "Prompt",
      Category = "Read",
      AiProviderId = agent.ProviderId,
      AiModel = request.Model,
      AiPrompt = request.UserPrompt,
      AiSystemPrompt = request.SystemPrompt,
      AiConfig = new {
        request.Temperature,
        request.MaxTokens,
        request.TopP,
        request.Seed
      }
    };
    
    var auditId = await _audit.Record(auditContext);
    
    try {
      // Step 2: Get provider + render template
      var provider = _providerRegistry.GetProvider(agent.ProviderId);
      var finalRequest = new AiRequest {
        Model = request.Model ?? provider.DefaultModel,
        SystemPrompt = request.SystemPrompt,
        UserPrompt = request.UserPrompt,
        Temperature = request.Temperature,
        MaxTokens = request.MaxTokens,
        TopP = request.TopP,
        Seed = request.Seed
      };
      
      // Step 3: Call provider with retry policy
      var response = await _retryPolicy.ExecuteAsync(async () =>
        await provider.Complete(finalRequest)
      );
      
      // Step 4: Record COMPLETED audit event (response + tokens)
      await _audit.UpdateAudit(auditId, new AuditUpdate {
        Status = "Completed",
        AiResponse = response.Content,
        AiTokensPrompt = response.PromptTokens,
        AiTokensCompletion = response.CompletionTokens,
        ExecutionMs = (int)sw.ElapsedMilliseconds
      });
      
      // Step 5: Return response
      return response;
      
    } catch (Exception ex) {
      // Step 4b: Record FAILED audit event
      await _audit.UpdateAudit(auditId, new AuditUpdate {
        Status = "Failed",
        ErrorSummary = ex.Message
      });
      throw;
    }
  }
}
```

### Retry policy

Polly circuit-breaker + exponential backoff:

```csharp
_retryPolicy = Policy
  .Handle<HttpRequestException>()
  .Or<TaskCanceledException>()
  .OrResult<AiResponse>(r => r.FinishReason == "error")
  .WaitAndRetryAsync(
    retryCount: 3,
    sleepDurationProvider: attempt => 
      TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),  // 200ms, 400ms, 800ms
    onRetry: (outcome, timespan, attempt, context) => {
      _logger.LogWarning($"AI request retry {attempt} after {timespan.TotalMilliseconds}ms");
    }
  )
  .WrapAsync(
    Policy.CircuitBreakerAsync<AiResponse>(
      handledEventsAllowedBeforeBreaking: 5,
      durationOfBreak: TimeSpan.FromSeconds(30)
    )
  );
```

## 7.5 Prompt templates

**Design goal:** externalize prompts to user-editable templates so users can customize wording without code changes; every prompt + rendering is audited.

### Template format

Templates are YAML files in `Templates/prompts/`:

```yaml
# Templates/prompts/srs.prompt
name: SRS Generator
description: Generate a complete SRS from a brief description
version: 1.0
systemPrompt: |
  You are an enterprise requirements analyst. Generate a comprehensive SRS
  that follows best practices: clear, testable, unambiguous.

userPrompt: |
  Generate an SRS for the following:
  
  **Brief description:** {{briefDescription}}
  **SRS ID:** {{srsId}}
  **Technology stack:** {{techStack | join: ", "}}
  **Impacted services:** {{services | join: ", "}}
  
  **Sections to include:**
  1. Functional Requirements
  2. Non-Functional Requirements
  3. Assumptions
  4. Dependencies
  5. Business Rules
  6. Limitations
  7. Out of Scope
  8. Risks
  9. Acceptance Criteria
  
  Format output as markdown with clear section headers.

outputFormat: markdown
```

**Template variables** use Liquid syntax (e.g., `{{variable}}`, `{{list | join: ", "}}`). Rendering happens in the orchestrator:

```csharp
public class PromptTemplateRenderer {
  public async Task<string> Render(
    PromptTemplate template,
    Dictionary<string, object> variables) {
    
    var engine = new Fluid.FluidParser();
    var context = new Fluid.TemplateContext { Model = variables };
    var rendered = await engine.ParseAsync(template.UserPrompt)
      .ExecuteAsync(context);
    return rendered;
  }
}
```

Every render is audited: audit record includes the template name, variables, and rendered output.

## 7.6 Reproducibility

**Design goal:** given the same prompt, variables, model, and sampling parameters, the AI call produces identical output every time. This allows "replay" of past AI decisions.

### Reproducibility record

Every AI call stores:

```csharp
record AiCallReplay {
  public string AuditId { get; set; }
  public string TemplateName { get; set; }
  public Dictionary<string, object> Variables { get; set; }
  public string RenderedSystemPrompt { get; set; }
  public string RenderedUserPrompt { get; set; }
  public string Model { get; set; }
  public float Temperature { get; set; }
  public int MaxTokens { get; set; }
  public float TopP { get; set; }
  public int? Seed { get; set; }
  public string ProviderId { get; set; }
  public DateTime Timestamp { get; set; }
}
```

Stored in: `Audit/YYYY/MM/DD/ai-calls/<auditId>.replay.json`

### Replay UI

In the Audit Dashboard, AI Prompts tab:

```
[Search and list AI calls]

Click call "SRS-2024-12-15-001" →
  Template: SRS Generator
  Model: claude-sonnet-5
  Temperature: 0.2
  Seed: 42
  
  Prompt (read-only):
  ---
  [rendered prompt shown]
  ---
  
  Response (read-only):
  ---
  [AI response shown]
  ---
  
  [Replay] button → creates a new AiCallReplay with identical params + calls orchestrator
```

If the provider's model snapshot has changed (e.g., model version pinned at replay time has been updated), the response may differ; the UI shows "⚠️ Model version has been updated since original call; replay may produce different output."

## 7.7 Token accounting

Every AI call records token usage (prompt tokens, completion tokens, total cost). Configuration defines estimated cost per 1K tokens (for billing alerts):

```jsonc
{
  "ai": {
    "costPerKTokens": {
      "anthropic": 0.003,  // $0.003 per 1K prompt tokens
      "openai.gpt4": 0.03
    }
  }
}
```

Audit event includes:
- Prompt tokens consumed.
- Completion tokens generated.
- Estimated cost.
- Cumulative spend for the session.

If cumulative spend exceeds a configurable threshold (e.g., $50/session), UI shows a warning "High AI token usage detected" and suggests reviewing calls.

## 7.8 Provider failure & fallback

If the primary provider fails (timeout, rate limit, API error), orchestrator:
1. Records the failure in audit (status = "failed").
2. Checks if a fallback provider is configured.
3. If yes, retries the request with the fallback provider (new audit record with the fallback provider ID).
4. If no fallback or fallback also fails, returns error to UI.

Configuration example:

```jsonc
{
  "ai": {
    "defaultProviderId": "primary",
    "fallbackProviderId": "fallback",
    "providers": [
      { "id": "primary", "vendor": "anthropic", … },
      { "id": "fallback", "vendor": "openai", … }
    ]
  }
}
```

Each fallback attempt is a separate audit event, traceable back to the original user action via correlation ID.
