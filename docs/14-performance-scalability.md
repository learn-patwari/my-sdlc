# 14. Performance & Scalability

Covers specification output section **§21 Performance and Scalability Considerations**.

---

## 14.1 Performance targets

| Operation | Target | Notes |
|---|---|---|
| Application startup | < 3 seconds | Profile load + DI init + SQLite index open |
| Profile switch | < 1 second | Reload options; no restart |
| Jira search (50 results) | < 2 seconds (UI responsive) | HTTP call is async; UI is never blocked |
| AI generation (SRS/SDD/SAD) | < 60 seconds typical | Depends on provider; progress shown; cancellable |
| Audit log write (single record) | < 50 ms | Must flush before operation proceeds |
| Audit dashboard load (last 30 days) | < 2 seconds | SQLite indexed query |
| Repository scan (1000 files) | < 30 seconds | Background service; progress reported |
| Sprint plan parse (100 items) | < 5 seconds | In-process; no network |
| Document export (DOCX, 50 pages) | < 10 seconds | Open XML SDK; in-process |

---

## 14.2 Async-first design (Rule 14)

Every operation that can block — network calls, file I/O, database queries, AI generation — is `async/await` end-to-end. The WPF UI thread is never blocked.

**Key patterns:**

```csharp
// ViewModel: never block UI thread
public class SrsViewModel : ObservableObject {
  [ObservableProperty] private bool _isGenerating;
  [ObservableProperty] private string _previewContent = "";

  [RelayCommand(CanExecute = nameof(CanGenerate))]
  private async Task GenerateAsync(CancellationToken ct) {
    IsGenerating = true;
    try {
      var result = await _srsGenerator.Generate(_request, ct);
      PreviewContent = result.Content;
    } finally {
      IsGenerating = false;
    }
  }
}

// All generators accept CancellationToken
// Cancellation creates a CANCELLED audit event
```

**Background services** (`IHostedService`):
- `AuditIndexer`: tails JSONL and updates SQLite index asynchronously.
- `IntegrityChecker`: runs hash chain verification on startup + nightly.
- `RetentionService`: deletes old log files nightly.
- `ConnectionMonitor`: pings Jira/AI endpoints every 60s and updates status bar.

---

## 14.3 Audit log performance

### Write path

The JSONL writer must be fast (< 50 ms) because it blocks the operation queue.

Optimizations:
- One `FileStream` per day file, held open with `FileShare.Read` (allows concurrent readers like `tail`).
- `Utf8JsonWriter` directly to the stream (no intermediate string allocation).
- `FileStream.FlushAsync()` is called after each record — not `File.AppendAllTextAsync()`, which opens and closes the file on every call.

```csharp
class JsonlAuditWriter {
  private FileStream? _currentStream;
  private string? _currentFilePath;
  private readonly SemaphoreSlim _writeLock = new(1, 1);

  public async Task<AuditId> Record(AuditContext context) {
    await _writeLock.WaitAsync();
    try {
      var today = DateTime.UtcNow.Date;
      EnsureStreamForDate(today);

      var record = BuildRecord(context);  // sets previousHash before writing
      await using var writer = new Utf8JsonWriter(_currentStream!, new JsonWriterOptions { SkipValidation = true });
      JsonSerializer.Serialize(writer, record);
      await _currentStream!.WriteAsync(NewLine);
      await _currentStream.FlushAsync();  // flush before returning

      return record.AuditId;
    } finally {
      _writeLock.Release();
    }
  }
}
```

### Read path (SQLite index)

Complex queries (filter by module + date range + status) use SQLite with indexes on `Module`, `UtcTimestamp`, `CorrelationId`, `JiraIssue`. EF Core LINQ queries are compiled where beneficial:

```csharp
// Compiled query for the most common dashboard query
private static readonly Func<AuditDbContext, string, IAsyncEnumerable<AuditEventEntity>>
  GetByCorrelation = EF.CompileAsyncQuery(
    (AuditDbContext ctx, string correlationId) =>
      ctx.AuditEvents.Where(e => e.CorrelationId == correlationId)
                     .OrderBy(e => e.UtcTimestamp));
```

### Dashboard virtualization

The audit timeline and tree views use WPF's `VirtualizingStackPanel` (default in `ListView`/`DataGrid`) so that only visible rows are rendered. A 100,000-event log file loads in < 2 seconds because only ~20 rows are materialized at any time.

---

## 14.4 AI token and cost management

**Context window management:** prompt + context must fit within the model's context window. If the repo analyzer result is large, the context is truncated before sending:

```csharp
class ContextWindowManager {
  public string TruncateToFit(string content, int maxTokens, int budgetForResponse) {
    var available = maxTokens - budgetForResponse - SystemPromptTokens;
    // Use a fast approximation: 1 token ≈ 4 characters (English prose)
    var charLimit = available * 4;
    if (content.Length <= charLimit) return content;
    return content[..charLimit] + "\n\n[Content truncated to fit context window]";
  }
}
```

Token counting uses a fast approximation (character-based) for pre-flight checks; actual token counts come from the API response.

**Parallel AI calls:** independent AI agents for sub-tasks (e.g., generating test cases for multiple methods in parallel) are coordinated by the orchestrator using `Task.WhenAll`:

```csharp
var tasks = methods.Select(method => orchestrator.Execute(
  testsAgent,
  new AiRequest { UserPrompt = BuildMethodTestPrompt(method) }));
var results = await Task.WhenAll(tasks);
```

Each parallel call is a separate audit record linked by correlation ID.

---

## 14.5 Repository scan performance

Large repositories (thousands of files) are scanned efficiently:
- File discovery uses `Directory.EnumerateFiles` (lazy enumeration, no full list in memory).
- Language plugins only read files matching their patterns (e.g., `*.java`, `*.py`).
- Analysis results are cached in memory for the session. Re-analysis is triggered manually or on detected repo changes (file system watcher on the local path).

For very large repos (> 50k files), progressive scanning shows per-package/per-module progress:

```csharp
var progress = new Progress<ScanProgress>(p =>
  _messenger.Send(new ScanProgressMessage(p.FilesScanned, p.TotalFiles)));
await _analyzer.Analyze(repo, progress, ct);
```

---

## 14.6 Jira bulk operations

Sprint planning can create 50+ issues. Sequential creation at ~500 ms/issue = 25 seconds for 50 issues.

Optimizations:
- Use Jira Bulk Create API (`/rest/api/3/issue/bulk`, up to 50 per request).
- Batch issues into groups of 50; execute batches sequentially (not parallel — avoids rate limits).
- Show per-item progress in the Approvals Center: "Creating issue 12 of 50…"

Estimated time for 50 issues: 2–4 seconds (vs 25 seconds sequential).

---

## 14.7 Document generation performance

DOCX generation is CPU-bound (Open XML SDK). For large documents (> 100 pages), run on a background thread via `Task.Run`:

```csharp
var bytes = await Task.Run(() => _docxGenerator.Generate(request));
```

PDF conversion (QuestPDF) is also run on a background thread. Estimated times:

| Document size | DOCX | PDF |
|---|---|---|
| Small (10 pages) | < 1 second | < 2 seconds |
| Medium (50 pages) | < 3 seconds | < 8 seconds |
| Large (200 pages) | < 10 seconds | < 30 seconds |

---

## 14.8 Scalability notes

The application is single-user, single-machine. Scalability considerations:

- **Working Directory size**: JSONL grows with usage. At 1KB/record × 1000 actions/day = 1MB/day; 30 days = 30MB. After compression, < 5MB. No practical limit for 5+ years of usage.
- **SQLite index size**: at 1 row/record × ~500 bytes/row = 500 bytes/record; 30,000 records ≈ 15MB. Well within SQLite's comfortable range (tested to 100GB+).
- **Multiple repo analysis**: up to 10 configured repositories, all analyzed independently. Each analysis is < 30 seconds; they can run in parallel in the background.
- **Future multi-user**: the audit + approval framework is single-user by design. A future server-hosted version would replace the local DPAPI + SQLite with a shared audit service and multi-user approval queue. All interfaces (`IAuditService`, `IApprovalQueue`) are designed to allow this substitution without core changes.
