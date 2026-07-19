# 12. Operations

Covers specification output sections **§17 Error Handling & Recovery**, **§18 Logging & Monitoring**, and **§19 Deployment & Packaging**.

---

## 12.1 Error handling & recovery (§17)

### Error taxonomy

| Category | Examples | Response |
|---|---|---|
| Configuration | Invalid profile, missing secret handle, schema validation failure | Block activation; show field-level errors; revert to last-known-good profile |
| Connectivity | Jira unreachable, AI API timeout, Git clone failure | Retry with backoff; show connectivity indicator in status bar; queue operations for retry when reconnected |
| Audit | JSONL write failed, disk full, hash chain broken | Block ALL write operations; show blocking alert; log to OS event log as fallback |
| Write-gate | Approval gate failure, decorator bypass attempt (impossible by design) | AuditUnavailableException propagates; UI shows blocking error |
| AI / Provider | Rate limit (429), context too long, invalid model | Polly retry/backoff for 429; truncate context with warning for length errors; surface model error with resolution hint |
| Validation | Sprint estimate overflow, SRS field missing, unsupported format | Blocking validation banner; list of specific violations with links to fix them |
| Jira API | 400/403/404 responses | Per-status handling (doc 08 §8.1.7); never silent; each error is a FAILED audit event |
| File system | Missing template, corrupt manifest, document hash mismatch | Show specific error with file path; offer to regenerate |
| Import parsing | Malformed Excel, invalid Confluence page ID | Show parse error with line/element reference; do not proceed to generation |

### Error record in audit

Every exception caught by the application writes a FAILED audit event with:
- `errorDetails.exceptionType`
- `errorDetails.message` (sanitized — no secrets, no stack traces containing secrets)
- `errorDetails.stackTrace` (inner exception chain, truncated to 4KB)
- `retryCount` (if Polly retried before the final failure)

### Recovery guidance (shown in error UI)

Each error shown to the user includes:
- **What happened**: plain English description.
- **Why it happened**: probable cause.
- **What to do next**: actionable steps (e.g., "Check your Jira token in Configuration → Jira → Test Connection").
- **Audit ID**: for tracing to the exact failure record.

### Retry and resilience

**AI calls (Polly):**
```
Retry: 3 attempts, exponential backoff (200ms, 400ms, 800ms)
Circuit breaker: opens after 5 consecutive failures, stays open for 30s
```

**Jira calls (Polly):**
```
Retry: 3 attempts for 5xx; no retry for 4xx (user action needed)
Circuit breaker: opens after 5 consecutive 5xx, stays open for 60s
Rate-limit (429): respect Retry-After header; max wait 120s
```

**Git operations:**
```
Retry: 3 attempts for network errors; no retry for auth failures
Shallow clone available as fallback for large repos
```

### Graceful shutdown

On application close:
1. Flush any in-progress audit writes.
2. Record "ApplicationShutdown" audit event.
3. Save active profile state.
4. Release LibGit2Sharp resources.
5. `IHost.StopAsync()` triggers all `IHostedService.StopAsync()` implementations.

---

## 12.2 Logging & monitoring (§18)

### Logging stack: Serilog

```csharp
Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Is(settings.Level)
  .Enrich.WithProperty("AppVersion", appVersion)
  .Enrich.WithProperty("MachineName", Environment.MachineName)
  .Enrich.WithProperty("Username", Environment.UserName)
  .Enrich.FromLogContext()  // correlation ID, session ID picked up here
  .Destructure.ByTransforming<JiraClient>(c => new { c.BaseUrl, Masked = "****" })
  .Filter.ByExcluding(Matching.WithProperty<string>("Token", _ => true))  // never log tokens
  .WriteTo.File(
    path: Path.Combine(workingDir, "Logs", "sdlc-copilot-.log"),
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: settings.RetentionDays,
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
  .WriteTo.SQLite(  // queryable log for the Audit Dashboard "system" tab
    Path.Combine(workingDir, "Audit", "log.db"),
    tableName: "Logs",
    storeTimestampInUtc: true)
  .CreateLogger();
```

### Correlation IDs

Every user action (button click) generates a `CorrelationId` (ULID). It flows through:

```
UI action (button click)
    → CorrelationId = NewUlid()
    → ILogger LogContext.PushProperty("CorrelationId", correlationId)
    → Audit record correlationId field
    → HTTP request header X-Correlation-ID (Jira, AI)
    → Child audit events inherit correlationId
```

This allows filtering all log entries and audit events for one user action with a single query.

### Structured log categories

| Category | Examples |
|---|---|
| `SdlcCopilot.Audit` | Audit write success/failure, chain verification |
| `SdlcCopilot.Jira` | HTTP requests, responses, errors |
| `SdlcCopilot.Ai` | Prompt sent, response received, tokens used |
| `SdlcCopilot.Repository` | Clone start/end, analysis progress |
| `SdlcCopilot.Modules.Srs` | SRS generation steps |
| `SdlcCopilot.Modules.Sprint` | Parse, validate, bulk create |
| `SdlcCopilot.Security` | Secret access (handle only, never value), DPAPI operations |
| `SdlcCopilot.Startup` | Initialization, profile load, host start |

### Log retention

Configurable via `logging.retentionDays` in profile (default: 90 days). `RetentionService` (a `BackgroundService`) deletes log files older than the retention window on startup and nightly.

### Performance monitoring

Key operations record their execution time in the audit record (`executionMs`). The Audit Dashboard "Sessions" tab shows:
- Average AI response time per session.
- Average Jira API latency.
- Total tokens consumed.
- Estimated cost per session.

---

## 12.3 Deployment & packaging (§19)

### Target runtime

- Windows 10 (1903+) / Windows 11.
- .NET 8 Desktop Runtime (bundled or machine-wide).

### Build pipeline

```
GitHub Actions (or local MSBuild) pipeline:
1. dotnet restore
2. dotnet build -c Release
3. dotnet test (non-WPF projects)
4. dotnet publish -c Release -r win-x64 --self-contained false
5. Sign assemblies (signtool.exe, certificate from Azure Key Vault or local store)
6. Package → MSIX or EXE installer
7. Publish to release channel
```

### MSIX package (primary)

MSIX provides:
- Clean install / uninstall (no leftover registry keys).
- Automatic updates via Microsoft Store or private update endpoint.
- AppContainer sandbox (optional; lighter privilege model).
- Code-signed for enterprise distribution.

`Package.appxmanifest` declares:
- `Capabilities`: `privateNetworkClientServer` (Jira, AI), `documentsLibrary` (Working Directory).
- Entry point: `SdlcCopilot.Wpf.exe`.
- Start menu tile + taskbar pinning support.

### Installer fallback (EXE/MSI)

For enterprises that cannot deploy MSIX: WiX-based MSI (via `WiX.Toolset` NuGet). MSI installs:
- Application files to `%ProgramFiles%\SdlcCopilot\`.
- Writes `%APPDATA%\SdlcCopilot\` on first run (profile + secrets directories).
- Creates Start Menu shortcut.
- Registers in Windows Add/Remove Programs.

### Self-contained vs framework-dependent

**Default: framework-dependent** (`--self-contained false`) — requires .NET 8 Desktop Runtime installed separately. Package is smaller (~50 MB vs ~150 MB self-contained).

**Alternative: self-contained** (`--self-contained true --runtime win-x64`) — larger but zero-dependency. Recommended for environments without .NET deployment tooling.

### Updates

- **MSIX via private endpoint**: manifest URL in `Package.appxmanifest`; Windows checks periodically.
- **Manual update check**: Settings → "Check for Updates" makes an HTTP call to a configured update feed URL; if a newer version is found, downloads and relaunches via the installer.

### Configuration migration

`ProfileMigrator` checks `schemaVersion` on profile load. If version is older than current, it applies migration steps (declarative migrations similar to EF Core migrations):

```csharp
class ProfileMigrator {
  private readonly IList<IProfileMigration> _migrations;

  public Profile Migrate(Profile profile) {
    var current = profile;
    foreach (var migration in _migrations.Where(m => m.TargetVersion > profile.SchemaVersion)) {
      current = migration.Apply(current);
    }
    return current;
  }
}
```

Old profile backups are saved in `rollback/profiles/` before each migration.

### Bundled components

The installer optionally bundles:
- **drawio CLI** (for PNG diagram rendering) — user must accept draw.io license separately.
- **Chromium** is already available in the execution environment for PDF rendering; on end-user machines, the installer can use a bundled headless Chromium via Playwright.
