# 15. Roadmap, Future Enhancements & Risks

Covers specification output sections **§22 Future Enhancement Roadmap**, **§23 Phased Development Plan with Milestones**, and **§24 Risks, Assumptions, and Mitigations**.

---

## 15.1 Phased development plan (§23)

Each phase has explicit entry criteria, exit criteria, and a testable milestone. Phases are sequential; each phase's work is delivered to the `claude/sdlc-copilot-architecture-*` branch series and reviewed before the next begins.

---

### Phase 1 — Foundation
**Duration estimate:** 4–6 weeks  
**Modules:** M1 Configuration Management, M13 Security, M12 Audit core, Logging

**Scope:**
- WPF shell: left rail navigation, top bar (profile selector), status bar (connection, approvals badge).
- Profile CRUD (Configuration screen, all 9 tabs, Test Connection buttons).
- DPAPI secret store: store, retrieve, delete, rotate.
- JSONL audit writer with hash chain.
- SQLite audit index + `AuditIndexer` background service.
- Audit write-gate decorator (structurally enforced — the keystone).
- Approvals Center: queue display, Approve/Reject/Cancel per item, Approve All.
- Audit Dashboard: Timeline tab (read-only view of today's events).
- Hash chain integrity verifier (on startup, blocking alert on failure).
- Serilog structured logging with correlation IDs.
- `RetentionService` for log/audit file cleanup.
- Profile schema validation + migration framework.

**Exit criteria / milestone:**
- App starts, active profile loads, DPAPI encrypts/decrypts credentials.
- Every action (app start, profile save, profile switch) produces an audit record in JSONL.
- Hash chain is valid after 100+ events.
- An operation blocked when audit flush fails (unit test green; observable in the UI).
- Approvals Center shows and executes a queued profile save.
- Audit Dashboard Timeline shows all session events.
- Integrity check verifies a known-good log and detects a manually tampered record.

---

### Phase 2 — Integrations Core
**Duration estimate:** 4–5 weeks  
**Modules:** M8 Jira Automation (read + write), M11 AI Orchestration

**Scope:**
- Jira REST v3 client: search, get issue, list projects, list issue types, list custom fields.
- JQL query builder UI (visual + raw).
- Jira write operations (create, update, transition, attach, link) — all approval-gated.
- AI provider adapters: Anthropic, OpenAI, Azure OpenAI, Gemini, Ollama.
- AI orchestrator: prompt render, call, retry, fallback, audit.
- Prompt template engine (Liquid syntax, YAML template files).
- Agent registry + base agent class.
- AI Prompts tab in Audit Dashboard (view prompt, response, tokens, cost, [Replay]).
- Token accounting and cost threshold alerts.

**Exit criteria / milestone:**
- Connect to a real Jira Cloud instance; search returns issues; JQL validates.
- Create a Jira issue via the Approvals Center; audit trail shows PENDING → APPROVED → COMPLETED.
- Call Anthropic API; prompt and response are stored in audit; replay produces identical request hash.
- Jira + AI connection status shown in status bar.
- Test Connection in Configuration works for all supported AI vendors.

---

### Phase 3 — First Generation Slice (SRS)
**Duration estimate:** 4–5 weeks  
**Modules:** M2 SRS Generator, M9 Document Engine (core), full Approvals Center UX

**Scope:**
- SRS Generator screen (three-zone layout: input, preview, action bar).
- DOCX template engine (Open XML SDK + `.dotx` templates).
- PDF export (QuestPDF primary, Playwright fallback).
- Markdown + HTML export (Markdig).
- Document version store: save, manifest, hash, `v001/v002/…` directories.
- Version Compare (DiffPlex side-by-side diff viewer in WPF).
- Restore-to-version (creates new version from old).
- SRS Agent (prompt template, variables, AI call).
- Jira: update issue description after SRS approval.
- `NEEDS-CLARIFICATION` marker detection: block submission until all markers resolved.
- Approvals Center: batch display, per-item diff, Approve All, Modify in-place.
- Documents screen: version history list, export, compare, download.

**Exit criteria / milestone:**
- Brief description → AI-generated SRS → versioned DOCX/PDF/MD saved to Working Directory.
- Version Compare shows changes between two SRS versions.
- "Submit for Approval" queues Jira description update in Approvals Center.
- Approve → Jira description updated → audit trail complete (PENDING → APPROVED → COMPLETED).
- Complete audit trail viewable in Dashboard from brief input to Jira update.

---

### Phase 4 — Architecture & Design Documents
**Duration estimate:** 5–6 weeks  
**Modules:** M3 SAD Generator, M5 SDD Generator

**Scope:**
- SAD Generator screen + SAD Agent.
- Draw.io XML writer (native mxGraph XML; component shapes, edges, labels).
- PNG preview via drawio CLI (graceful degradation if CLI absent).
- Deployment + component + sequence diagram generation.
- SAD document (markdown + DOCX export).
- SDD search workflow: cross-project Jira search before generate.
- SDD Generator screen + SDD Agent.
- Coverage matrix generation (methods × test cases table).
- SDD DOCX/PDF export.
- Jira: attach diagrams, attach SDD, link to epic.
- Confluence read for sprint plan input (Confluence REST v2).
- Audit: all Jira attachment operations gated + audited.

**Exit criteria / milestone:**
- SAD: Draw.io XML file produced + attached to Jira ticket after approval.
- SDD: cross-project search runs first; existing SDD shown; new SDD created if none found.
- Coverage matrix shows method/test coverage in the SDD preview.
- Jira attachments visible in the issue after approval.
- All archive files hashed and in manifest.

---

### Phase 5 — Sprint Planning & Repository Intelligence
**Duration estimate:** 6–7 weeks  
**Modules:** M4 Sprint Planning, M10 Repository Integration, M6 Repository Analyzer

**Scope:**
- Sprint Planning screen: import source picker (Confluence / MD / Excel / Word / text).
- Parsers: Confluence page → SprintPlan, Markdown → SprintPlan, Excel (ClosedXML) → SprintPlan, Word (Open XML SDK) → SprintPlan.
- Work item generator (AI): Stories, Tasks, Subtasks, Dev/Test/Doc/Review/Deploy tasks.
- Sprint validator: estimate vs dev-day budget enforcement.
- Validation banner UI with filter to oversized items.
- Bulk Jira create via Jira Bulk API (batches of 50).
- Repository Integration screen: configure, clone, pull.
- LibGit2Sharp local repo; GitHub/GitLab/Bitbucket adapters.
- Repository Analyzer screen: scan results, dependency graph (mermaid diagram), external calls.
- Language analyzer plugins: Java, Python, TypeScript, C#.
- Impact analysis: recommend impacted files for a change description.

**Exit criteria / milestone:**
- Sprint plan from an Excel file → 30 work items generated → bulk-created in Jira via Approvals Center (single batch approval).
- Estimate validation catches a subtask with estimate > configured dev days.
- Repository scan on a Java Spring Boot repo shows services, controllers, repositories, external calls.
- Impact analysis recommends files for a described change.

---

### Phase 6 — Test Generation, Rollback & Packaging
**Duration estimate:** 6–8 weeks  
**Modules:** M7 Unit Test Generator, M12 rollback flows, Audit Dashboard (full), MSIX packaging

**Scope:**
- Unit Test Generator screen + Tests Agent.
- Framework adapters: JUnit 5 + Mockito, pytest, Jest + RTL, Angular Testing, Spring Boot Test.
- Positive / negative / boundary / exception case generation.
- Coverage matrix (predicted).
- Tests saved to repository test directory (approval-gated).
- Rollback: Jira issue (transition to Cancelled), document version, config profile.
- Rollback UI in Audit Dashboard event detail pane.
- Audit Dashboard: all 8 tabs fully implemented (Timeline, Tree, Sessions, Jira, AI Prompts, Files, Approvals, Rollbacks).
- MSIX package (signed).
- MSI/EXE fallback installer (WiX).
- Auto-update mechanism.
- Performance profiling pass (meet all targets from doc 14).
- End-to-end tests (FlaUI) for critical workflows.

**Exit criteria / milestone:**
- Generate JUnit 5 + Mockito tests for a Java service; save to repo (approval-gated).
- Generate pytest tests for a Python FastAPI route.
- Rollback a Jira issue creation; audit trail shows original → rollback request → rollback completed.
- Audit Dashboard Tree tab shows the full event hierarchy for a complete SRS → Jira workflow.
- MSIX installer installs and launches on a clean Windows 11 machine.
- All performance targets from doc 14 met and validated.

---

### Phase 7 — Future Scope & Extensions
**Duration estimate:** Ongoing / as prioritized  
**Modules:** M14 Future Scope

Each item below maps to an existing abstraction and requires no core rework:

| Feature | Abstraction used | Notes |
|---|---|---|
| Azure DevOps | `ISdlcTool` adapter | Board + work-item API; replaces Jira client |
| GitLab Issues | `ISdlcTool` adapter | GitLab Boards as SDLC tool |
| ServiceNow | `ISdlcTool` adapter | Change management integration |
| Confluence Publishing | `IDocumentGenerator` adapter | Write generated docs to Confluence pages |
| Customer Requirement Spec (CRS) | New module (M15) + agent | Pre-SRS customer input capture |
| Requirement Traceability Matrix (RTM) | New module (M16) | CRS → SRS → SDD → Tests cross-reference |
| Release Notes Generator | New module (M17) + agent | Jira release → changelog document |
| Code Review Assistant | New module (M18) + agent | PR diff analysis + suggestions |
| Pull Request Assistant | New module (M19) + agent | Auto-draft PR descriptions from commit history |
| Worklog Assistant | New module (M20) | Time logging against Jira issues |
| Regression Test Generator | New module (M21) + agent | Impact-aware test regeneration |
| AI Agent Activity Import | `IAiAgent` plugin | Import external agent logs into audit trail |
| REST Plugin SDK | Plugin API surface | Third-party modules load at runtime |
| Additional AI providers | `IAiProvider` adapter | Any OpenAI-compatible, Llama, HuggingFace, etc. |

---

## 15.2 Future enhancement roadmap (§22)

Priority order for Phase 7+ enhancements (informed by spec M14):

1. **Confluence Publishing** — highest demand; closes the doc distribution loop.
2. **Azure DevOps adapter** — broadens enterprise reach.
3. **RTM** — bridges CRS → SRS → SDD → Tests; compliance-critical.
4. **Release Notes Generator** — high frequency use case; low complexity.
5. **Code Review / PR Assistants** — AI-in-the-loop for code quality.
6. **REST Plugin SDK** — enables community/enterprise customization.
7. **Worklog Assistant** — time-tracking automation.
8. **Regression Test Generator** — closes the testing loop.

---

## 15.3 Risks, assumptions & mitigations (§24)

### Risks

| ID | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R-1 | **Jira API breaking change** (Atlassian Cloud moves from v3 or changes custom field behavior) | Medium | High | Raw typed client isolates all field names to config + `JiraApiDialect`; updating the adapter is a contained change |
| R-2 | **AI model deprecation** (specified model removed by provider) | High (models change often) | Medium | Model is a config value; user updates it; error message includes suggestion; no hardcoded model names anywhere |
| R-3 | **DPAPI key loss** (user re-installs Windows or moves machine) | Medium | High | Document clearly in UI: "Credentials are machine-bound; re-enter after OS reinstall." Provide an export-without-secrets profile backup. |
| R-4 | **Audit disk full** (JSONL grows, Working Directory fills up) | Low | Critical | `RetentionService` enforces configurable retention; low-disk warning at 20% remaining; writes blocked with a clear error if disk is full |
| R-5 | **AI response quality** (generated SRS/SDD doesn't match expectations) | Medium | Medium | `NEEDS-CLARIFICATION` markers; preview before approval; full edit-and-regenerate loop; user always in control before any write |
| R-6 | **Scope creep** (14 modules is already large) | High | High | Phased delivery; each phase is a shippable increment; Phase 7 features gated by existing abstractions only |
| R-7 | **QuestPDF license compliance** (Community license has revenue threshold) | Low | Medium | Evaluate license before Phase 3 ship; fallback to Playwright print (already designed); or purchase commercial license |
| R-8 | **drawio CLI distribution** (licensing for bundling draw.io CLI) | Medium | Low | PNG preview degrades gracefully to "unavailable"; XML artifact is always produced; user can open locally in draw.io |
| R-9 | **LibGit2Sharp native lib** (libgit2.dll compatibility on future Windows versions) | Low | Medium | Periodically update LibGit2Sharp NuGet; abstraction behind `IRepository` means git.exe fallback is a contained swap |
| R-10 | **Prompt injection via user input** (adversarial brief description manipulates AI) | Low | Medium | Prompts are constructed with strict template + variable delimiters; user input is never inserted into instruction slots |

### Assumptions

| ID | Assumption | If wrong: mitigation |
|---|---|---|
| A-1 | Single-user, single-machine deployment | Multi-user: replace `IAuditService`/`IApprovalQueue` with server-backed implementations; no core rework |
| A-2 | Jira Cloud as the primary SDLC tool | Jira Server/DC: `apiDialect = "server-v2"` config; Azure DevOps/GitLab: Phase 7 `ISdlcTool` adapters |
| A-3 | English-language documents only | Translate: swap prompt templates (externalized as YAML); Open XML SDK supports Unicode; no code changes |
| A-4 | Working Directory on local disk (fast) | Network share: async I/O already in use; JSONL flush latency may increase; consider configuring a local cache path |
| A-5 | .NET 8 LTS available on target machines | .NET 9+ released: self-contained publish bundles the runtime; no dependency on machine-installed runtime version |
| A-6 | Windows 10 1903+ (MSIX support, DPAPI per-user) | Windows 7/8: not supported (MSIX requires Win10; DPAPI per-user available from XP but not tested) |

### Recorded decision log (for future reference)

| Decision | Rationale | Reversal trigger |
|---|---|---|
| No Atlassian.NET SDK | Unmaintained; lagging Cloud API support | If official SDK becomes actively maintained and covers v3 fully |
| QuestPDF over iTextSharp | Permissive Community license; modern API; no GPL-infection concerns | Revenue threshold exceeded → commercial license or swap to Playwright print |
| SQLite over SQL Server LocalDB | Zero-install; sufficient for single-user audit volume | Multi-user server deployment → swap `AuditDbContext` connection string to SQL Server; schema is compatible |
| Liquid (Fluid) over Handlebars/Razor | Lightweight, safe (no code execution in templates), designer-friendly | If complex conditional logic needed in templates → evaluate Razor in a sandboxed `RazorLightEngine` |
| FlaUI over WinAppDriver | FlaUI is open-source and maintained; WinAppDriver is officially deprecated by Microsoft | If FlaUI lags WPF updates → Playwright Windows automation (preview) |
