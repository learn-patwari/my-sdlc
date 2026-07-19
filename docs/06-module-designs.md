# 6. Module-by-Module Design

Covers specification output section **§10 Module-by-Module Design**. All 14 modules are described with responsibilities, core interfaces, workflows, and edge cases.

## Module 1: Configuration Management

**Responsibility:** manage user profiles (Jira, AI, sprint, repo, working directory, logging, audit settings); validate against schema; provide strongly-typed configuration to the app at runtime; support hot-reload for non-credential settings.

**Core interfaces:**
```csharp
interface IProfileStore {
  Task<Profile> GetActiveProfile();
  Task<Profile> LoadProfile(string profileId);
  Task SaveProfile(Profile profile);  // audited write
  Task DeleteProfile(string profileId);  // audited write
  Task<IList<Profile>> ListProfiles();
  Task<bool> ValidateProfile(Profile, out ValidationError[]);
}

interface ISecretStore {  // Windows DPAPI
  Task<string> Get(string handle);  // dpapi:ai-key-primary → decrypts
  Task Store(string handle, string secret);  // audited
  Task Delete(string handle);  // audited
}
```

**Workflow:**
1. On app startup, load active profile from `%APPDATA%\SdlcCopilot\profiles\active.json`.
2. Validate against schema; if validation fails, show error banner and revert to last-known-good profile.
3. For non-credential settings (template dir, sprint config), support `IOptionsMonitor<T>` hot-reload: file changes trigger a refresh without restart.
4. Every profile save → audit record (PENDING) → approval (Approvals Center) → write (EXECUTING) → complete (COMPLETED with hash of new profile).

**Edge cases:**
- Profile references a secret handle that no longer exists in DPAPI store → error on load, suggest re-enter credentials.
- User deletes the active profile → automatically fall back to first available profile or prompt to create a new one.
- Jira project list in config no longer exists in Jira → warning banner in Jira integration screen, offer to refresh list.

## Module 2: SRS Generator

**Responsibility:** generate Software Requirement Specification documents from user's brief description, enforcing a template-driven format; version every SRS; automatically update the Jira issue description after approval.

**Core interfaces:**
```csharp
interface ISrsGenerator {
  Task<SrsDocument> Generate(SrsGenerationRequest request);  // (brief, template, srsId, config, aiProvider)
  Task<SrsGenerationRequest> LoadTemplate(string templateName);
}

interface IDocumentVersionStore {  // shared by SRS, SAD, SDD, tests
  Task<DocumentVersion> Save(string documentId, DocumentKind kind, byte[] content, string format);
  Task<IList<DocumentVersion>> GetVersions(string documentId);
  Task<DocumentVersion> GetVersion(string documentId, int versionNum);
}
```

**Workflow:**
1. User provides brief description + SRS ID + selects template.
2. Generator renders the template with AI filling in each section (functional requirements, non-functional, risks, acceptance criteria, …).
3. Each section is tagged with `[AI-GENERATED]` and an explanation of why that section was chosen.
4. User can regenerate (iterating on brief/template) or submit for approval.
5. On approval, SRS document is versioned + Jira issue description is queued as a separate write operation (not atomic; each is separately audited).

**Edge cases:**
- Template references custom fields not in config → validation error before generation, suggest fixing template or config.
- User submits an SRS that is semantically identical to a prior version → show warning "This SRS is identical to v003; are you sure?" but allow submission.
- AI prompt times out → show error with guidance to retry or adjust model/tokens; operation remains queued for later retry.

## Module 3: Software Architecture Document

**Responsibility:** generate architecture diagrams (Draw.io XML + PNG preview), SAD document, deployment diagram, component diagram; search Jira for existing ADRs; attach diagrams to Jira.

**Core interfaces:**
```csharp
interface IArchitectureGenerator {
  Task<ArchitectureDocument> Generate(ArchitectureGenerationRequest);
  Task<byte[]> RenderDrawioAsPng(string drawioXml);  // via bundled CLI or Playwright print
}

interface IDrawioWriter {  // native Draw.io XML generation (no external tool)
  string CreateDiagram(DiagramSpec spec);  // returns mxGraph XML
  void AddComponent(string diagramXml, Component comp, Position pos);
  void AddRelationship(string diagramXml, Component from, Component to, string label);
}
```

**Workflow:**
1. User selects Jira ticket → architecture style (microservices/monolith/…) → technology stack (pre-filled from config).
2. Repository analyzer scans to identify services; user can select/deselect services to include in diagram.
3. Generator creates Draw.io XML natively (no cloud export).
4. PNG preview is rendered locally (via bundled drawio CLI if available; otherwise XML is shown as tree).
5. SAD document (markdown) is generated with architecture rationale, trade-offs, technology choices.
6. On approval: XML + PNG + SAD markdown are versioned and attached to the Jira ticket.

**Edge cases:**
- Bundled drawio CLI unavailable → PNG preview shows "unavailable" message; XML still produced and can be opened in draw.io.com later.
- Repository analyzer found no services (e.g., static website) → show placeholder diagram with note "No backend components detected."
- User modifies the diagram in the preview → changes are not persisted; regenerating overwrites edits (warn before regenerate).

## Module 4: Sprint Planning

**Responsibility:** import sprint plan from Confluence, Markdown, Excel, Word, or plain text; parse stories and tasks; validate estimates against configured sprint constraints; generate work items with detailed descriptions and acceptance criteria; queue for Jira batch creation.

**Core interfaces:**
```csharp
interface ISprintPlanParser {
  Task<SprintPlan> Parse(ISprintPlanSource source);  // Confluence/MD/Excel/Word/Text
}

interface ISprintValidator {
  ValidationResult Validate(WorkItem item, SprintCalendar calendar);
  // checks: subtask estimate ≤ configured dev days, total estimate ≤ sprint capacity, etc.
}

interface IWorkItemGenerator {
  Task<WorkItem[]> GenerateWorkItems(SprintPlan plan, JiraConfig jiraConfig, IAiProvider ai);
  // fills in detailed descriptions, acceptance criteria, story points, dependencies
}
```

**Workflow:**
1. User selects import source (picker UI) + provides file/URL.
2. Parser converts input format to an intermediate SprintPlan model (stories, tasks, subtasks).
3. Validator checks every item's estimate against `SprintCalendar.CalculateAvailableHours(...)` for the configured sprint.
4. If any estimate exceeds dev-day budget → validation banner warns user; list shows only oversized items; user can adjust or override.
5. On "Submit for Approval" → all items are queued as a single batch to Approvals Center (one approval item per story, with subtasks listed).

**Edge cases:**
- Import file has malformed structure (e.g., Excel missing header row) → parser error with line numbers; suggest user fix file.
- Estimate is in days but user's sprint config is in hours → auto-convert with UI confirmation ("Converting 2 days = 16 hours; is this correct?").
- Story already exists in Jira (user imported an old plan) → search Jira, show existing issue, ask user to skip or link.
- Sprint calendar has no available dev days (all buffer or holidays) → block submission with error "No development days in sprint; check calendar."

## Module 5: Software Detailed Design

**Responsibility:** search existing SDDs across all configured Jira projects before creating new; generate SDD documents for service classes with design details, methods, dependencies, test cases; one SDD per service.

**Core interfaces:**
```csharp
interface ISddSearch {
  Task<SddSearchResult[]> SearchExisting(string serviceName, JiraProjectRef[] projects);
}

interface ISddGenerator {
  Task<SddDocument> Generate(SddGenerationRequest);  // (serviceName, repo, repoAnalysis, aiProvider)
  Task<byte[]> RenderCoveragMatrix(SddDocument sdd);  // table: methods × test cases
}
```

**Workflow:**
1. User selects service/class name (autocomplete from repo analyzer results).
2. **First step: Jira search** across all configured projects for existing SDD matching the service. User can:
   - View and reuse an existing SDD (no new generation needed).
   - Proceed to generate a new SDD.
3. Generator analyzes the service's repository code, identifies all public methods, dependencies, external calls, exceptions.
4. AI generates a detailed SDD: purpose, methods (with signatures and logic summaries), dependencies, error handling, edge cases, sequence diagrams (mermaid), test-case ideas.
5. On approval: SDD is versioned and linked to the Jira ticket (or epic, if configured).

**Edge cases:**
- Service name is ambiguous (multiple classes with same name in different repos) → show disambiguation list; user selects.
- Jira search times out → show "Search unavailable; proceeding to generate new SDD" and continue.
- Repository code unavailable (repo not cloned locally) → skip code analysis; AI uses service name alone to generate a high-level SDD.
- Generated SDD conflicts with existing one (major changes detected) → show side-by-side comparison and ask user to confirm overwrite.

## Module 6: Repository Analyzer

**Responsibility:** scan local repositories to identify services, controllers, DTOs, utilities; recommend impacted files for a change; extract dependencies and external calls; build a dependency graph.

**Core interfaces:**
```csharp
interface IRepositoryAnalyzer {
  Task<AnalysisResult> Analyze(IRepository repository);
  // returns: services, controllers, DTOs, dependencies, external calls, tech stack
  
  Task<CodeUnit[]> RecommendImpactedFiles(ChangeDescription change, AnalysisResult analysis);
}
```

**Workflow:**
1. User selects one or more repositories from config (or all).
2. Analyzer walks the repo file tree, identifies classes/functions by language (Java/Python/TS/…):
   - Java: `@Service`, `@Controller`, `@Repository`, `@RestController` annotations; also plain public classes.
   - Python: classes with `Service`, `Controller` in name; functions in files.
   - TypeScript: exported classes/functions.
3. Builds a dependency graph: which services call which; which call external APIs.
4. User can request "Recommend files for feature X" → analyzer suggests which services/controllers/DTOs will change.
5. Results are exported as a report (markdown or HTML) or piped to the SDD generator.

**Edge cases:**
- Repository has no recognizable patterns (e.g., shell scripts, config files only) → report "No analyzable code found; please check repo path."
- Private/internal methods are numerous → flag in report "Large internal complexity detected; suggest breaking down service."
- External API calls are hardcoded URLs → suggest externalizing to config and link to Configuration screen.

## Module 7: Unit Test Generator

**Responsibility:** generate unit tests (JUnit, pytest, Jest, Angular, React) targeting a configured coverage percentage; support positive, negative, boundary, and exception cases; generate a coverage-prediction matrix.

**Core interfaces:**
```csharp
interface IUnitTestGenerator {
  Task<TestSuite> Generate(TestGenerationRequest);  // (service, framework, coverageTarget, aiProvider)
  Task<CoverageMatrix> PredictCoverage(TestSuite tests);
}
```

**Workflow:**
1. User selects repository → service/class → coverage target (slider, e.g., 90%) → test framework (from config defaults or override).
2. Generator analyzes the service (via repo analysis) and AI drafts test cases:
   - Positive: "happy path" for each method.
   - Negative: invalid inputs, null checks, error states.
   - Boundary: edge values (empty collections, zero, max int, …).
   - Exception: try/catch scenarios, retry logic.
3. Tests are written in the selected framework with mocks for dependencies (via Mockito, unittest.mock, Jest mocks, etc.).
4. Coverage matrix shows predicted coverage: which lines/methods are tested by which test cases.
5. On approval, tests are saved to the repository's test directory (not committed; user reviews and commits manually).

**Edge cases:**
- Service has no public methods (all private/internal) → warning "Service appears to be internal; testing may not be applicable."
- Framework not supported for this language → show error with list of supported frameworks.
- AI can't generate meaningful tests (e.g., service is a pure data holder) → show placeholder tests with comments "Add tests for [method]; consider [approach]."
- User adjusts coverage target to 95%, then regenerates → old tests are discarded; new tests aim for 95% (not merged with old ones).

## Module 8: Jira Automation

**Responsibility:** search Jira via JQL; create, update, and transition issues; attach files and diagrams; link issues; bulk operations; populate custom fields.

**Core interfaces:**
```csharp
interface IJiraClient {
  Task<SearchResults> Search(string jql);
  Task<Issue> CreateIssue(IssueCreate request);  // audited write-gate
  Task UpdateIssue(string issueKey, IssueUpdate update);  // audited
  Task TransitionIssue(string issueKey, TransitionRequest request);  // audited
  Task AttachFile(string issueKey, byte[] fileContent, string fileName);  // audited
  Task LinkIssues(string issueKey1, string issueKey2, string linkType);  // audited
}
```

**Workflow:**
1. User opens Jira Integration screen → builds a JQL query (visual builder or raw JQL) → clicks "Run search."
2. Results display as a list (virtualized); user can click an issue to see details + audit trail (all app-initiated updates).
3. For bulk operations:
   - Select issues (multi-select with Ctrl+Click).
   - Choose bulk action: "Transition to [status]," "Update custom field [name] to [value]," "Link to Epic," "Attach SDD," etc.
   - Each action queues a separate approval item per issue (or one batch item if all are identical).
4. On approval, bulk operations execute in sequence; any failure is recorded and reported.

**Edge cases:**
- Jira search returns 0 results → show "No issues match query" with suggestion to broaden query.
- User tries to transition an issue to an invalid state → Jira API returns error; app shows error and suggests valid transitions.
- Attaching a file exceeds Jira's attachment size limit → show error with limit; suggest splitting file or compressing.
- Custom field does not exist or user lacks permission → Jira API error is caught and shown to user; suggest checking config.

## Module 9: Document Management

**Responsibility:** version every generated document (SRS, SAD, SDD, tests, …); support DOCX, PDF, Markdown, HTML, Draw.io XML, and PNG formats; export documents; maintain a version manifest with hashes; support rollback via audit trail.

**Core interfaces:**
```csharp
interface IDocumentVersionStore {
  Task<DocumentVersion> Save(string documentId, DocumentKind kind, byte[] content, string format);
  Task<DocumentVersion> GetVersion(string documentId, int versionNum, string format = null);
  Task<VersionManifest> GetManifest(string documentId);  // lists all versions, formats, hashes, audit IDs
}

interface IDocumentExporter {
  Task<byte[]> Export(DocumentVersion doc, string targetFormat);  // DOCX → PDF, SDD MD → HTML, etc.
}
```

**Workflow:**
1. Every generator (SRS, SAD, SDD, tests) calls `IDocumentVersionStore.Save(...)` on approval, which:
   - Increments version number (v001 → v002).
   - Computes SHA-256 hash of content.
   - Records in the manifest: `{ documentId, version, format, hash, auditId, timestamp }`.
   - Stores file under `<WorkingDirectory>/Documents/<type>/<id>/v<num>/<filename>`.
2. User can "Compare v1 vs v2" → diff viewer shows changes (section-level for docs, line-level for code).
3. User can "Restore to v1" → creates a new version from an old one (v003 ← clone of v001), not a destructive rollback.
4. "Export as PDF" takes the markdown/HTML content and converts to PDF (via QuestPDF or wkhtmltopdf).

**Edge cases:**
- User tries to export SDD as Excel → unsupported format error; suggest DOCX or PDF.
- File hash in manifest doesn't match the actual file on disk → integrity error; suggest re-generate or rollback.
- Version manifest is corrupted → audit trail is still intact; recover manifest from JSONL audit records.

## Module 10: Repository Integration

**Responsibility:** support local git repositories and remote providers (GitHub, GitLab, Bitbucket); enable branch detection, commit history, file impact analysis.

**Core interfaces:**
```csharp
interface IRepositoryProvider {
  Task<IRepository> OpenRepository(RepositoryConfig config);  // local or remote
  Task<BranchInfo[]> ListBranches();
  Task<CommitInfo[]> GetCommitHistory(string branchName, int count = 50);
  Task<ImpactAnalysis> AnalyzeImpact(string commitSha, string baseBranch = "main");
}

interface IRepository {
  string LocalPath { get; }
  Task<string> GetCurrentBranch();
  Task<FileChange[]> GetChangedFiles(string fromCommit, string toCommit);
}
```

**Workflow:**
1. User configures repositories in Configuration (URL + local path + auth).
2. On demand (from Sprint Planning, SDD generation, or Repository Analysis screen), app clones/pulls the repo to the local path.
3. User can browse branches, view commit history, see what files changed between commits.
4. For impact analysis (e.g., "if we commit feature X, what tests should we run?"), analyzer suggests changed files + recommends reanalyzing dependent services.

**Edge cases:**
- Remote repo is private and auth token is expired → clone fails with permission error; prompt user to refresh token in Configuration.
- Repository is huge (> 1 GB) → show progress spinner and suggest shallow clone (–depth 1) to speed up.
- User deletes the local repo directory while the app is running → app detects missing repo on next access and offers to re-clone.
- Branch no longer exists on remote → show "Branch has been deleted; use [Refresh] to update list."

## Module 11: AI Orchestration

**Responsibility:** abstract multiple AI providers; manage prompt templates; ensure every call is audited and reproducible; coordinate agents (SRS, SAD, SDD, Tests, Repository, Architecture agents).

**Core interfaces:**
```csharp
interface IAiProvider {
  Task<AiResponse> Complete(AiRequest request);
  // includes: prompt, model, temperature, max_tokens, top_p, seed (if supported)
  
  Task<string> RenderTemplate(PromptTemplate template, Dictionary<string, object> variables);
}

interface IAiOrchestrator {
  Task<AiResponse> Execute(IAiAgent agent, AiRequest request);
  // logs prompt/response, manages retries, records token usage
}

interface IAiAgent {
  string Name { get; }
  Task<AiResponse> Execute(AiRequest request, IAiOrchestrator orchestrator);
}
```

**Workflow:**
1. Configuration defines AI providers (OpenAI, Azure, Anthropic, Gemini, Ollama); user selects a default.
2. Each module (SRS, SAD, etc.) uses a specialized agent (SrsAgent, SadAgent, …) that prepares a prompt request.
3. Orchestrator:
   - Renders the prompt template with module-specific variables.
   - Calls the AI provider.
   - Records the PENDING audit event before the call.
   - Makes the HTTP call (with Polly retry/circuit-breaker).
   - Records COMPLETED or FAILED audit event.
   - Returns the response to the agent.
4. Every prompt + response is stored in the audit directory so it can be replayed (same model + params + seed = identical output).

**Edge cases:**
- User switches AI providers mid-session (e.g., from OpenAI to Anthropic) → new prompt goes to new provider; old audit trail shows which provider was used.
- AI response is empty or malformed → error is recorded; agent returns a "parse error" result; user can retry.
- Rate limit hit (429) → Polly retries with exponential backoff; if all retries exhausted, return error to user.
- Model specified in config no longer exists (e.g., `gpt-4` deprecated) → suggest alternative in error message; allow user to override model in the generator UI.

## Module 12: Audit, Traceability & Approval Framework

**Responsibility:** record every action (read or write) in an immutable, hash-chained JSONL log; enforce that writes cannot happen without audit + approval; maintain a searchable SQLite index; support rollback; provide audit dashboard.

**Core interfaces:**
```csharp
interface IAuditService {
  Task<AuditId> Record(AuditContext context);  // records PENDING event, flushes to disk, returns ID
  Task UpdateAudit(AuditId id, AuditUpdate update);  // records COMPLETED/FAILED/outcome
}

interface IApprovalGate {  // THE write-gate
  Task<ApprovalRequest> Enqueue(IAuditedOperation op);  // queues to Approvals Center
  Task<OperationResult> Execute(ApprovalId id);  // admin: called by Approvals Center when user clicks Approve
}

interface IAuditedOperation<T> {
  AuditContext AuditContext { get; }
  Task<T> Execute();  // the actual write
}

// Decorator: all write services are registered wrapped
public class AuditedOperationDecorator : ISomeWriteService {
  public AuditedOperationDecorator(ISomeWriteService inner, IApprovalGate gate) { }
  
  public async Task WriteStuff(...) {
    var op = new AuditedOperation<Result>(
      context: ...,
      execute: () => inner.WriteStuff(...)
    );
    await gate.Enqueue(op);  // never executes inner directly
  }
}
```

**Workflow:**
1. Module wants to write (create Jira issue, save SDD, update config) → calls a write service method.
2. Write service is wrapped by `AuditedOperationDecorator` → encapsulates the operation and calls the approval gate.
3. Gate records a PENDING audit entry (with inputs, previous state, audit ID) and flushes to JSONL.
4. If flush succeeds → gate enqueues to Approvals Center and returns to UI.
5. If flush fails → `AuditUnavailableException` is thrown; operation does not proceed.
6. User opens Approvals Center → sees queued operation with diff → clicks Approve.
7. Gate receives approval → records EXECUTING audit event → calls the inner service's method → records COMPLETED/FAILED.
8. UI updates with result.

**Rollback:** when user requests rollback of an approval, system:
- Reads the COMPLETED audit entry + its previous state.
- Crafts a new operation (e.g., "Jira: revert issue PROJ-1234 to state X").
- Queues it to Approvals Center (must be approved again).

**Edge cases:**
- Audit JSONL is on a read-only drive or disk full → record operation cannot proceed; block all writes.
- Hash chain is broken (one record's hash doesn't link to prior) → audit integrity check fails; show error banner; block new operations.
- User tries to rollback an operation that can't be rolled back (e.g., deleted a repo file) → show error with guidance.

## Module 13: Security

**Responsibility:** encrypt secrets (tokens, API keys) using Windows DPAPI; enforce no plaintext secrets in files; manage certificate-based signing for audit records (if enabled); implement threat mitigations.

**Core interfaces:**
```csharp
interface ISecretStore {
  Task<string> Get(string handle);  // dpapi:ai-token → decrypts
  Task Store(string handle, string secret);  // encrypts + stores (audited)
  Task Delete(string handle);  // audited
}

interface IDigitalSigner {  // optional, if audit.signRecords = true
  string Sign(AuditRecord record, X509Certificate2 cert);
  bool Verify(AuditRecord record, string signature, X509Certificate2 cert);
}
```

**Workflow:**
1. User enters a credential (Jira API token, AI API key, GitHub token) in Configuration.
2. Secret store encrypts it with Windows DPAPI (per-user, this-machine scope).
3. Config file stores only the ciphertext handle (e.g., `dpapi:jira-token-acme`); the plaintext is never logged or exported.
4. At runtime, when a service needs the secret, it fetches via the store (decryption happens in-memory).
5. If audit.signRecords is enabled, every audit record is signed with a user-supplied X.509 certificate (requires PKI setup; optional for most deployments).

**Threat model (doc 11) covers:**
- Plaintext secret disclosure in logs, error messages, audit exports.
- DPAPI key compromise (OS user elevation).
- Man-in-the-middle on Jira/AI API calls (mitigated by TLS + certificate pinning if configured).

## Module 14: Future Scope

**Extensibility:** all core abstractions (`ISdlcTool`, `IAiProvider`, `IRepositoryProvider`, `IDocumentGenerator`) are designed to accept new implementations without core changes. Examples:

- **New SDLC tool:** implement `ISdlcTool` (Azure DevOps, GitLab, ServiceNow adapters). Module 8 Jira-specific logic is isolated in a Jira adapter class; new tools plug in the same way.
- **New AI provider:** implement `IAiProvider` (any OpenAI-compatible endpoint, Llama, Hugging Face, etc.).
- **New document format:** implement `IDocumentGenerator` (Confluence, Notion, Google Docs exporters).
- **Plugin SDK:** future versions support loading assemblies at runtime (versioned plugins with capability declarations).

The phased roadmap (doc 15) outlines Customer Requirement Specification (CRS), Worklog Assistant, RTM, Release Notes Generator, Code Review Assistant, and plugin SDK as Phase 7+ work — each leverages existing abstractions.
