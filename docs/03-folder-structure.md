# 3. Folder Structure

Covers specification output section **§6 Folder Structure** — both the source solution and the runtime Working Directory.

## 3.1 Solution structure

```
my-sdlc/
├── SprintForge.sln
├── Directory.Build.props            # nullable enabled, warnings-as-errors, shared version
├── README.md
├── docs/                            # design package (see README doc map)
├── config/
│   └── profile.schema.json          # published JSON Schema for configuration profiles
├── src/
│   ├── SprintForge.Domain/
│   │   ├── Audit/                   # AuditRecord, AuditAction, AuditStatus, ApprovalStatus
│   │   ├── Approvals/               # ApprovalRequest, ApprovalDecision, ProposedChange
│   │   ├── Documents/               # DocumentVersion, DocumentKind, ArtifactHash
│   │   ├── Planning/                # WorkItem, WorkItemKind, Estimate, SprintCalendar
│   │   ├── Configuration/           # Profile, JiraSettings, AiSettings, SprintSettings, …
│   │   └── Common/                  # Ids, Result<T>, DomainException hierarchy
│   ├── SprintForge.Application/
│   │   ├── Audit/                   # IAuditService, IAuditQueryService, AuditContext,
│   │   │                            # IAuditedOperation, AuditUnavailableException
│   │   ├── Approvals/               # IApprovalQueue, IApprovalGate (the write-gate)
│   │   ├── Ai/                      # IAiProvider, IAiOrchestrator, IAiAgent, AiRequest/Response,
│   │   │                            # PromptTemplate, IPromptTemplateStore
│   │   ├── Sdlc/                    # ISdlcTool, IssueRef, IssueChange, JqlQuery, TransitionRequest
│   │   ├── Repositories/            # IRepositoryProvider, IRepositoryAnalyzer, CodeUnit, ImpactReport
│   │   ├── Documents/               # IDocumentGenerator, IDocumentVersionStore, ExportFormat
│   │   ├── Configuration/           # IProfileStore, ISecretStore, ProfileValidator
│   │   └── Modules/                 # one folder per module use case (Srs/, Sad/, Sdd/, Sprint/, Tests/)
│   ├── SprintForge.Infrastructure/
│   │   ├── Audit/                   # JsonlAuditWriter (hash chain), AuditDbContext, AuditIndexer
│   │   ├── Security/                # DpapiSecretStore
│   │   ├── Jira/                    # JiraClient (REST v3), JiraFieldMapper
│   │   ├── Ai/                      # OpenAiProvider, AzureOpenAiProvider, AnthropicProvider,
│   │   │                            # GeminiProvider, OllamaProvider (OpenAI-compatible reuse)
│   │   ├── Git/                     # LocalGitRepository (LibGit2Sharp), GitHub/GitLab/Bitbucket adapters
│   │   ├── Documents/               # DocxGenerator, PdfGenerator, MarkdownGenerator, DrawioWriter
│   │   └── Configuration/           # JsonProfileStore (schema-validated), options wiring
│   └── SprintForge.Wpf/             # net8.0-windows (Windows-only build)
│       ├── App.xaml(.cs)            # generic-host bootstrap, DI composition root
│       ├── Shell/                   # MainWindow, left nav rail, status bar, approvals badge
│       ├── Modules/                 # one folder per screen: Home/, Srs/, Sad/, Sprint/, Sdd/,
│       │                            # Repos/, Tests/, Jira/, Docs/, Audit/, Config/, Approvals/
│       └── Common/                  # converters, behaviors, diff viewer control
└── tests/
    └── SprintForge.Tests/
        ├── Audit/                   # write-gate blocking, hash-chain integrity
        ├── Configuration/           # schema validation round-trips
        └── Security/                # DPAPI round-trip (Windows-only, skipped elsewhere)
```

Dependency rule (enforced by project references): `Wpf → Application → Domain`; `Infrastructure → Application → Domain`; nothing references `Wpf` or `Infrastructure`.

## 3.2 Runtime Working Directory (user-configured root)

All generated artifacts live under the user's configured Working Directory — exactly as mandated by M12:

```
<WorkingDirectory>/
├── Audit/
│   ├── index.db                     # SQLite search index (rebuildable from JSONL)
│   └── YYYY/MM/DD/
│       ├── audit.jsonl              # immutable, hash-chained source of truth
│       ├── audit.html               # generated daily view
│       ├── audit.csv                # generated daily export
│       ├── prompts/                 # <auditId>.prompt.json (full rendered prompt + params)
│       ├── responses/               # <auditId>.response.json
│       ├── jira/                    # request/response snapshots per Jira call
│       ├── repository/              # scan reports, diffs
│       ├── generated_documents/     # per-day copies of exported artifacts
│       ├── approvals/               # approval decision records
│       ├── rollback/                # previous-state captures + rollback receipts
│       ├── errors/                  # exception details
│       └── screenshots/            # optional UI captures at approval time
├── Documents/
│   ├── SRS/<SRS-ID>/v001/…          # every version retained; latest symlinked via manifest
│   ├── SAD/<ticket>/v001/…
│   ├── SDD/<service>/v001/…
│   └── Tests/<repo>/<change-id>/…
├── Templates/                       # user-supplied SRS/SAD/SDD/test templates
├── Workspaces/                      # cloned/linked repositories for analysis
└── Profiles/                        # exported profile backups (secrets stripped)
```

Notes:

- Live profiles (with DPAPI-encrypted secrets) reside in `%APPDATA%\SprintForge\profiles\` — outside the Working Directory so audit exports never carry credentials.
- `index.db` lives beside the JSONL tree but is disposable: an `AuditIndexer` rebuild scans the JSONL files and reconstructs it, preserving the "files are the source of truth" property.
- Every file written below `Documents/` is content-hashed; the hash is recorded in the corresponding audit record and in the version manifest (doc 09).
