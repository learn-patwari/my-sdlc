# 1. Requirement Validation & Clarifications

Covers specification output sections **§1 Requirement Validation** and **§2 Clarifying Questions**.

## 1.1 Requirement inventory and validation

Each specification module was analyzed for completeness, consistency, and architectural impact.

| # | Module | Verdict | Notes |
|---|--------|---------|-------|
| M1 | Configuration Management | ✅ Valid | Multi-profile requirement drives a profile-scoped configuration store; all other modules consume configuration exclusively through it (no hardcoded values, per global rules). |
| M2 | SRS Generator | ✅ Valid | Jira description update is write-gated (approval + audit). Template adherence is a hard rule (Rule 5). |
| M3 | Software Architecture Document | ✅ Valid, one constraint | Draw.io XML is generated natively (mxGraph XML). PNG preview requires a rendering step — see decision D-7. |
| M4 | Sprint Planning | ✅ Valid | "Subtask estimates shall never exceed configured development days" is enforced as a blocking validation before items can be queued for approval. |
| M5 | Software Detailed Design | ✅ Valid | "Search before create" (Rule 4) implemented as a mandatory Jira cross-project search step with results surfaced to the user before any new SDD is drafted. |
| M6 | Repository Analyzer | ✅ Valid | Static/structural analysis (no code execution). Classification heuristics are per-technology plugins (Java/Spring, Python, TS/Angular/React). |
| M7 | Unit Test Generator | ✅ Valid | Coverage target is configurable and used as a generation goal + coverage-matrix report; actual measured coverage requires running the user's toolchain and is reported, not guaranteed. |
| M8 | Jira Automation | ✅ Valid | All writes flow through the audit/approval gate; searches are read-only but still audited (spec explicitly lists Jira Search as auditable). |
| M9 | Document Management | ✅ Valid | Every export is content-hashed and versioned. |
| M10 | Repository Integration | ✅ Valid | Local + GitHub/GitLab/Bitbucket behind one provider abstraction. Repository *modification* is future-phase and always approval-gated ("where safe", per M12 rollback scope). |
| M11 | AI Orchestration | ✅ Valid | Agents are roles over a shared provider abstraction, not separate processes. Central orchestrator owns sequencing, retries, and audit hooks. |
| M12 | Audit / Traceability / Approval | ✅ Valid — architectural keystone | Audit-before-execute is enforced structurally (write-gate decorator), not by convention. |
| M13 | Security | ✅ Valid | DPAPI per-user encryption; see threat model in doc 11. |
| M14 | Future Scope | ✅ Valid | Satisfied by abstraction seams (`ISdlcTool`, `IAiProvider`, `IRepositoryProvider`, plugin SDK surface) — no speculative code. |

### Cross-cutting rules validation

| Rule | How it is satisfied structurally |
|---|---|
| 1–2 Never write Jira/repos without approval | All write services are wrapped by `AuditedOperationDecorator`; there is no DI registration of an unwrapped write service. |
| 3 Never overwrite documentation silently | Document engine versions every artifact; overwrite requires an approval item showing the diff. |
| 4 Search existing SDDs first | SDD workflow's first pipeline stage is a Jira search; skipping it is not a reachable code path. |
| 9–10 Every action audited; block if audit fails | Audit record is written and flushed *before* execution; any audit-write failure throws `AuditUnavailableException`, aborting the operation. |
| 12 Reproducible AI artifacts | Every AI call persists prompt, rendered template + inputs, model, and full sampling parameters; a "replay" runs the identical request. |
| 18 Never fabricate missing data | Generators emit `NEEDS-CLARIFICATION` markers and the UI blocks submission until resolved by the user. |

## 1.2 Clarifying questions — resolved with the user

These were asked and answered before design was finalized:

| Question | Decision |
|---|---|
| Desktop framework? | **.NET 8 + WPF** (MVVM, CommunityToolkit.Mvvm). |
| Scope of this iteration? | **Complete design package + compilable solution scaffold**; module implementations follow the phased roadmap (doc 15). |
| Shell layout? | **Left navigation rail** + top profile bar + bottom status bar (wireframes confirmed by user, doc 05). |
| Approval presentation? | **Approvals Center only — no modal dialogs.** All writes queue into a dedicated screen; nothing executes until decided there. |
| Development approach? | **Phase-wise**, with explicit milestones and exit criteria per phase (doc 15). |

## 1.3 Recorded assumptions (non-critical, reversible, all surfaced here per Rule 18)

| ID | Assumption | Rationale | Reversal cost |
|---|---|---|---|
| A-1 | Single-user desktop deployment; the audit trail is per-machine, per-user | Spec describes a desktop app with local Working Directory | Low — audit records carry username + machine name, so later server aggregation is a pure addition |
| A-2 | SQLite is the local audit index; JSONL files remain the source of truth | Zero-install, searchable, transactional | Low — index is rebuildable from JSONL at any time |
| A-3 | Jira Cloud REST v3 is the initial target; Server/DC v2 differences isolated in the client layer | v3 is current; DC support is a config-selectable API dialect | Low |
| A-4 | PNG preview of draw.io diagrams uses a locally bundled drawio CLI export when available, else the XML is still produced and PNG generation is reported as unavailable | No cloud dependency permitted for a preview | Low — documented in D-7 |
| A-5 | Digital signatures on audit records are optional and off by default (spec: "if enabled"), using a user-supplied X.509 certificate | Enterprise PKI varies | None — config flag |
| A-6 | English is the initial document language; templates own all wording | Templates are user-supplied | None |

## 1.4 Key design decisions

| ID | Decision | Alternatives considered | Justification |
|---|---|---|---|
| D-1 | Clean Architecture + MVVM, one project per layer | Monolithic WPF app; plugin-only architecture | Testability, replaceable modules (spec: independently replaceable), clear dependency rule |
| D-2 | Audit write-gate as a decorator over every write service | Aspect weaving; manual calls in each service | Structural enforcement beats convention; impossible to forget |
| D-3 | Hash-chained JSONL audit log + SQLite index | DB-only; plain log files | Tamper-evidence + human-readable + queryable |
| D-4 | Raw Jira REST v3 typed client | Atlassian.NET SDK | SDK is unmaintained and Cloud-lagging; typed client keeps full control of custom fields |
| D-5 | Own `IAiProvider` abstraction with per-vendor adapters | LangChain-style framework dependency | Full auditability of every request/response; no hidden prompt mutation (spec: no hidden logic) |
| D-6 | DPAPI (per-user) for secrets; ciphertext-only config files | Bundled key file; OS keyring via third-party lib | Native, zero-dependency, per-user Windows protection |
| D-7 | draw.io: emit mxGraph XML natively; PNG via optional bundled drawio CLI | Embed a browser to render; cloud export API | Cloud is unacceptable (data egress); XML is the durable artifact, PNG is a convenience |
| D-8 | Estimates/holidays computed against a configurable working calendar | Naive day math | Sprint constraint (dev days, buffer, holidays, hours) is first-class configuration |
