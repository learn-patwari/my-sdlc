# 4. Data & Configuration Schemas

Covers specification output sections **§7 Database Schema** and **§8 Configuration Schema**.

## 4.1 SQLite audit index schema

The JSONL files are the immutable source of truth; SQLite is the searchable index (rebuildable at any time). EF Core migrations manage the schema.

```sql
-- Every audit event (one row per JSONL record)
CREATE TABLE AuditEvents (
    AuditId          TEXT PRIMARY KEY,          -- ULID (sortable, unique)
    ParentAuditId    TEXT NULL REFERENCES AuditEvents(AuditId),
    SessionId        TEXT NOT NULL,
    CorrelationId    TEXT NOT NULL,             -- one user action → many events
    UtcTimestamp     TEXT NOT NULL,             -- ISO-8601
    LocalTimestamp   TEXT NOT NULL,
    Username         TEXT NOT NULL,
    MachineName      TEXT NOT NULL,
    AppVersion       TEXT NOT NULL,
    Module           TEXT NOT NULL,             -- M1..M14 identifier
    Action           TEXT NOT NULL,             -- enum: AiPrompt, JiraUpdate, FileCreate, Approval, …
    Category         TEXT NOT NULL,             -- Read | Write | Approval | System | Error
    Status           TEXT NOT NULL,             -- Pending | Executing | Completed | Failed | Blocked
    ApprovalStatus   TEXT NULL,                 -- Queued | Approved | Modified | Rejected | Cancelled
    RollbackStatus   TEXT NULL,                 -- NotApplicable | Available | RolledBack
    RetryCount       INTEGER NOT NULL DEFAULT 0,
    ExecutionMs      INTEGER NULL,
    ErrorSummary     TEXT NULL,
    InputRef         TEXT NULL,                 -- relative path to input snapshot file
    OutputRef        TEXT NULL,
    PreviousStateRef TEXT NULL,                 -- rollback/ capture path
    NewStateRef      TEXT NULL,
    JiraProject      TEXT NULL,
    JiraIssue        TEXT NULL,
    Repository       TEXT NULL,
    Branch           TEXT NULL,
    CommitSha        TEXT NULL,
    AiPromptId       TEXT NULL,
    AiModel          TEXT NULL,
    AiConfigJson     TEXT NULL,                 -- temperature, max tokens, etc.
    RecordHash       TEXT NOT NULL,             -- SHA-256 of canonical JSONL record
    PreviousHash     TEXT NOT NULL,             -- hash chain link
    Signature        TEXT NULL                  -- optional X.509 signature
);
CREATE INDEX IX_Audit_Correlation ON AuditEvents(CorrelationId);
CREATE INDEX IX_Audit_Session     ON AuditEvents(SessionId);
CREATE INDEX IX_Audit_Time        ON AuditEvents(UtcTimestamp);
CREATE INDEX IX_Audit_Jira        ON AuditEvents(JiraIssue);
CREATE INDEX IX_Audit_Module_Action ON AuditEvents(Module, Action);

-- Files touched by an event (N per event)
CREATE TABLE AuditArtifacts (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    AuditId   TEXT NOT NULL REFERENCES AuditEvents(AuditId),
    Kind      TEXT NOT NULL,                    -- Generated | Modified | Deleted | Attached
    Path      TEXT NOT NULL,                    -- relative to Working Directory
    Sha256    TEXT NOT NULL
);
CREATE INDEX IX_Artifacts_Audit ON AuditArtifacts(AuditId);

-- Document/version registry (M9, M12 version control)
CREATE TABLE DocumentVersions (
    DocumentId   TEXT NOT NULL,                 -- e.g. SRS:PROJ-104
    Version      INTEGER NOT NULL,
    CreatedUtc   TEXT NOT NULL,
    AuditId      TEXT NOT NULL REFERENCES AuditEvents(AuditId),
    Path         TEXT NOT NULL,
    Sha256       TEXT NOT NULL,
    Format       TEXT NOT NULL,                 -- DOCX | PDF | MD | HTML | DRAWIO | PNG
    Supersedes   INTEGER NULL,
    PRIMARY KEY (DocumentId, Version, Format)
);

-- Approvals queue (live) + history
CREATE TABLE Approvals (
    ApprovalId    TEXT PRIMARY KEY,
    AuditId       TEXT NOT NULL REFERENCES AuditEvents(AuditId),
    QueuedUtc     TEXT NOT NULL,
    DecidedUtc    TEXT NULL,
    Decision      TEXT NULL,                    -- Approved | Modified | Rejected | Cancelled
    DecidedBy     TEXT NULL,
    BatchId       TEXT NULL,                    -- groups bulk operations (sprint push)
    DiffRef       TEXT NOT NULL,                -- path to stored current/proposed diff payload
    ExplanationRef TEXT NULL                    -- AI explanation snapshot
);

-- Profile metadata (values live in %APPDATA% JSON; index for audit joins)
CREATE TABLE Profiles (
    ProfileId    TEXT PRIMARY KEY,
    Name         TEXT NOT NULL UNIQUE,
    CreatedUtc   TEXT NOT NULL,
    UpdatedUtc   TEXT NOT NULL,
    SchemaVersion TEXT NOT NULL
);
```

Integrity: on startup and on demand, `AuditChainVerifier` re-walks each day's JSONL, recomputes hashes, and compares against `RecordHash`/`PreviousHash`; discrepancies raise a blocking integrity alert (doc 10).

## 4.2 Configuration profile schema

Profiles are JSON documents validated against `config/profile.schema.json` (published in this repo). Shape overview — authoritative schema is the JSON Schema file:

```jsonc
{
  "schemaVersion": "1.0",
  "profileName": "Acme-Prod",
  "sdlcTool": {
    "provider": "jira",                       // jira | azuredevops | gitlab | servicenow (future)
    "serverUrl": "https://acme.atlassian.net",
    "auth": {
      "mode": "apiToken",                     // apiToken | oauth | sso
      "username": "user@acme.com",
      "secretRef": "dpapi:jira-token-acme"    // ciphertext handle, never the secret
    },
    "apiDialect": "cloud-v3"                  // cloud-v3 | server-v2
  },
  "ai": {
    "providers": [{
      "id": "primary",
      "vendor": "anthropic",                  // openai | azureopenai | anthropic | gemini | ollama
      "endpoint": "https://api.anthropic.com",
      "secretRef": "dpapi:ai-key-primary",
      "model": "claude-sonnet-5",
      "temperature": 0.2,
      "maxTokens": 8192,
      "contextWindow": 200000
    }],
    "defaultProviderId": "primary",
    "promptTemplateDir": "Templates/prompts"
  },
  "jira": {
    "projects": ["PROJ", "PLAT"],
    "components": ["payments", "auth"],
    "labels": ["ai-generated"],
    "issueTypes": { "story": "Story", "task": "Task", "subtask": "Sub-task" },
    "customFields": { "storyPoints": "customfield_10016", "sprint": "customfield_10020" },
    "epicLinkField": "customfield_10014",
    "defaultRelease": null
  },
  "sprint": {
    "durationDays": 10,
    "developmentDays": 7,
    "bufferDays": 2,
    "workingHoursPerDay": 8,
    "holidays": ["2026-08-15"],
    "workweek": ["Mon", "Tue", "Wed", "Thu", "Fri"]
  },
  "repositories": [{
    "name": "payments-api",
    "kind": "java",                           // ui | java | python
    "provider": "github",                     // local | github | gitlab | bitbucket
    "url": "https://github.com/acme/payments-api",
    "localPath": "Workspaces/payments-api",
    "defaultBranch": "main",
    "secretRef": "dpapi:gh-token"
  }],
  "technologyStack": ["spring-boot", "angular", "postgresql", "kafka"],
  "testing": {
    "coverageTargetPercent": 90,
    "frameworks": { "java": "junit5-mockito", "python": "pytest", "ui": "jest-rtl" }
  },
  "workingDirectory": "D:/SdlcCopilot/Acme",
  "documents": { "defaultFormat": "DOCX", "templateDir": "Templates" },
  "logging": { "level": "Information", "retentionDays": 90 },
  "audit": {
    "signRecords": false,
    "certificateThumbprint": null,
    "screenshotOnApproval": false,
    "dailyHtmlExport": true,
    "dailyCsvExport": true
  },
  "preferences": { "theme": "system", "language": "en" }
}
```

Rules:

- **No hardcoded values** — every knob the spec lists is present here; code reads configuration exclusively through typed options bound from the active profile.
- **Secrets never appear in profiles** — only `dpapi:` handles; the ciphertext lives in the DPAPI-protected secret store (doc 11).
- **Versioned** — `schemaVersion` gates migrations; profile saves are audited writes and keep prior versions for rollback (M12).
- **Validated** — a profile that fails schema validation cannot be activated; validation errors are surfaced field-by-field in the Configuration screen.
