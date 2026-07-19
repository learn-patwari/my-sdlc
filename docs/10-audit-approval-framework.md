# 10. Audit, Traceability & Approval Framework

Covers specification output section **§15 Audit & Approval Framework Design**. This is the most critical module — the load-bearing architectural pattern that enforces every rule in the specification.

---

## 10.1 Core invariants

1. **Audit-before-execute**: every PENDING audit record is flushed to disk before any write operation begins. If the flush fails, the operation is blocked.
2. **No unapproved writes**: operations reach the adapter (Jira, file system, Git) only through the approval gate. There is no DI-registered write service that bypasses it.
3. **Immutable log**: the JSONL file is append-only and hash-chained. Nothing is ever deleted or edited in the log.
4. **Rollback is additive**: rolling back an operation creates a new audit event (it doesn't delete the original).
5. **Audit integrity is mandatory**: a broken hash chain blocks all write operations and shows a blocking alert.

---

## 10.2 Write-gate pipeline

```
Module use case
    │
    ▼
Write service method
    │  (wrapped by AuditedOperationDecorator at DI registration)
    ▼
AuditedOperationDecorator.WriteXxx(...)
    │
    ├─ Capture: inputs, previous state, correlation ID
    ├─ Build AuditContext { action, module, category, input, previousState }
    ├─ IAuditService.Record(context)  ← write PENDING record to JSONL + flush
    │       │
    │       └─ If flush fails → throw AuditUnavailableException → operation blocked
    │
    ├─ IApprovalQueue.Enqueue(operationId, approvalItem)
    │       │
    │       └─ Approval item visible in Approvals Center
    │               Status bar badge updates
    │
    └─ Returns to caller (UI shows "Queued for approval")

[User opens Approvals Center → reviews diff → clicks Approve]

    │
    ▼
IApprovalGate.Execute(approvalId)
    │
    ├─ IAuditService.Update(pendingAuditId, status=Approved)
    ├─ IAuditService.Record(new context { status=Executing })
    ├─ Execute inner write (Jira create, file save, etc.)
    │       ├─ Success → IAuditService.Update(status=Completed, newState, executionMs)
    │       └─ Failure → IAuditService.Update(status=Failed, errorDetails)
    │                    RetryPolicy → retry count logged in audit
    └─ Return result to Approvals Center UI
```

### AuditedOperationDecorator pattern

All write services are registered wrapped:

```csharp
// Registration in DI
services.AddScoped<JiraWriteService>();
services.AddScoped<IJiraWriteService>(sp =>
  new AuditedJiraWriteServiceDecorator(
    sp.GetRequiredService<JiraWriteService>(),  // inner
    sp.GetRequiredService<IAuditService>(),
    sp.GetRequiredService<IApprovalQueue>()));
```

The decorator cannot be bypassed because `JiraWriteService` itself is not exported as `IJiraWriteService` — only the decorated version is resolvable.

---

## 10.3 Audit storage

### JSONL (source of truth)

Each line is one JSON record, appended atomically using `FileStream` with `FileShare.None` during writes (write lock held minimally).

**Record structure:**
```json
{
  "auditId": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "parentAuditId": null,
  "sessionId": "ses_2024121501",
  "correlationId": "corr_abc123",
  "utcTimestamp": "2024-12-15T09:00:00.000Z",
  "localTimestamp": "2024-12-15T14:30:00.000+05:30",
  "username": "akshay.patwari",
  "machineName": "ACME-DEV-01",
  "appVersion": "1.0.0",
  "module": "SRS",
  "action": "SrsGeneration",
  "category": "Write",
  "status": "Pending",
  "approvalStatus": "Queued",
  "rollbackStatus": "NotApplicable",
  "retryCount": 0,
  "executionMs": null,
  "input": { "briefDescription": "...", "srsId": "PROJ-1234", "template": "Default SRS" },
  "output": null,
  "previousState": null,
  "newState": null,
  "aiPromptId": "01ARZ3NDEKTSV4RRFFQ69G5FAU",
  "aiModel": "claude-sonnet-5",
  "aiConfig": { "temperature": 0.2, "maxTokens": 8192 },
  "jiraProject": "PROJ",
  "jiraIssue": "PROJ-1234",
  "repository": null,
  "branch": null,
  "commitSha": null,
  "generatedFiles": [],
  "errorDetails": null,
  "fileHash": null,
  "previousHash": "0000000000000000000000000000000000000000000000000000000000000000",
  "signature": null
}
```

**Hash chain**: `previousHash` = SHA-256 of the *previous* JSONL record's canonical JSON (deterministically serialized). The first record in a day's file uses `0000...0000`.

### Directory layout

```
<WorkingDirectory>/Audit/
├── index.db                         ← SQLite index (rebuildable)
└── 2024/
    └── 12/
        └── 15/
            ├── audit.jsonl          ← append-only source of truth
            ├── audit.html           ← daily view (generated at midnight or on demand)
            ├── audit.csv            ← daily export
            ├── prompts/
            │   ├── 01ARZ3...FAU.prompt.json
            │   └── ...
            ├── responses/
            │   └── 01ARZ3...FAV.response.json
            ├── jira/
            │   └── 01ARZ3...FAW.jira-request.json
            ├── repository/
            ├── generated_documents/
            ├── approvals/
            │   └── 01ARZ3...FAX.approval.json
            ├── rollback/
            ├── errors/
            └── screenshots/
```

### SQLite index

Populated by `AuditIndexer` (a `BackgroundService`) which tails the JSONL file and inserts new records. The index is used exclusively for search and dashboard queries. Schema is in doc 04.

**Rebuild procedure** (in case index is corrupted):
1. Drop and re-create SQLite database.
2. Walk all JSONL files in date order.
3. Parse each record and insert into `AuditEvents` + `AuditArtifacts`.
4. Verify hash chain on each file while rebuilding.
5. Show progress in the Audit Dashboard ("Rebuilding index: 2,450 / 12,300 events").

---

## 10.4 Audit integrity check

On application start and on demand (from Audit Dashboard → "Verify Integrity" button):

```csharp
class AuditChainVerifier {
  public async Task<IntegrityReport> Verify(string auditDir) {
    var files = Directory.GetFiles(auditDir, "audit.jsonl", SearchOption.AllDirectories)
      .OrderBy(f => f);

    var violations = new List<ChainViolation>();
    string previousHash = new string('0', 64);

    foreach (var file in files) {
      foreach (var line in await File.ReadAllLinesAsync(file)) {
        var record = JsonSerializer.Deserialize<AuditRecord>(line);
        
        // Check: previousHash field matches hash of prior record
        if (record.PreviousHash != previousHash) {
          violations.Add(new ChainViolation {
            AuditId = record.AuditId,
            File = file,
            Expected = previousHash,
            Actual = record.PreviousHash
          });
        }
        
        // Check: recordHash matches canonical JSON hash
        var canonicalJson = SerializeCanonical(record with { RecordHash = null });
        var expectedHash = Sha256(canonicalJson);
        if (record.RecordHash != expectedHash) {
          violations.Add(new ChainViolation { AuditId = record.AuditId, Kind = "RecordTamper" });
        }
        
        previousHash = record.RecordHash;
      }
    }

    return new IntegrityReport {
      TotalRecords = total,
      Violations = violations,
      IsIntact = violations.Count == 0
    };
  }
}
```

If violations are found:
- **Blocking alert** shown in the top bar (red banner).
- All write operations are blocked until the user acknowledges.
- Violation details are shown in the Audit Dashboard.
- An integrity-failure event is appended to the JSONL (paradoxically; a new valid record with `action = "IntegrityFailure"`).

---

## 10.5 User Approval Workflow (Approvals Center)

### Approval item model

```csharp
record ApprovalItem {
  public string ApprovalId { get; set; }
  public string AuditId { get; set; }         // links back to PENDING audit record
  public string CorrelationId { get; set; }
  public string Module { get; set; }
  public string Action { get; set; }
  public string BatchId { get; set; }         // groups bulk operations
  public object CurrentState { get; set; }    // what exists now
  public object ProposedChange { get; set; }  // what will be written
  public string AiExplanation { get; set; }   // why AI recommended this
  public IList<string> AffectedJiraFields { get; set; }
  public IList<string> AffectedFiles { get; set; }
  public IList<string> AffectedRepositories { get; set; }
  public DateTime QueuedUtc { get; set; }
  public ApprovalStatus Status { get; set; }  // Queued, Approved, Executing, Completed, Failed, Rejected, Cancelled, Modified
}
```

### Decision flow

```
User in Approvals Center:

Approve         → Record APPROVED audit event
                → Execute write (EXECUTING → COMPLETED/FAILED)
                
Approve All     → Approve every item with status=Queued in current view
                → Execute all in sequence (with per-item audit events)
                
Modify          → Open in-place editor for the ProposedChange
                → Re-queue modified version (new audit record; original marked Cancelled)
                
Reject          → Record REJECTED audit event
                → Operation does not execute; item removed from queue
                
Cancel          → Record CANCELLED audit event  
                → Operation does not execute; item removed from queue
```

### Batch operations

Sprint Planning creates a batch of 30+ issues. Each issue is a separate `ApprovalItem` grouped by `BatchId`. The Approvals Center UI:
- Shows a batch header: "Batch #2 — Sprint Planning — 32 items"
- Individual items are collapsible under the header
- "Approve All in Batch" approves all items in that `BatchId`
- Individual items can be rejected/cancelled within a batch

---

## 10.6 Rollback

### Supported rollback targets

| Target | Rollback mechanism | Limitations |
|---|---|---|
| Jira issue create | Create reverse operation: delete issue or transition to "Cancelled" status | Jira may not support delete via API (depends on permissions); transition is the safe default |
| Jira issue update | Restore `previousState` captured before write | Custom field history may not fully restore |
| Document save | "Restore to vN" creates a new version from the old file | Does not delete v(N+1); version history preserved |
| Config profile save | Restore previous profile JSON from `rollback/` directory | New approval required to apply rolled-back version |
| Repository file | N/A in Phase 1–6; Phase 6 scope | Only "where safe" per spec |

### Rollback procedure

```csharp
class RollbackService {
  public async Task<RollbackResult> Rollback(string auditId) {
    // 1. Read COMPLETED audit record
    var originalRecord = await _auditQuery.GetRecord(auditId);
    
    if (originalRecord.RollbackStatus != "Available")
      return RollbackResult.NotSupported("Operation cannot be rolled back");
    
    // 2. Read previous state from rollback/ directory
    var previousState = await File.ReadAllTextAsync(originalRecord.PreviousStateRef);
    
    // 3. Build reverse operation
    var reverseOp = BuildReverseOperation(originalRecord, previousState);
    
    // 4. Queue through approval gate (rollback also requires approval)
    await _approvalQueue.Enqueue(reverseOp);
    
    // 5. Return: "Rollback queued for approval"
    return RollbackResult.Queued(reverseOp.ApprovalId);
  }
}
```

Every rollback creates a chain of audit events:
- Parent audit event: the original operation (status=Completed).
- Child audit event: the rollback request (status=Pending, parentAuditId = original).
- Child audit event: the rollback execution (status=Completed or Failed).

---

## 10.7 Audit Dashboard

### Tab structure (Approvals Center view is separate — this is for viewing history)

**Timeline tab:**
```
2024-12-15 09:00:00  [SRS]       SrsGeneration     COMPLETED  ✓
2024-12-15 09:01:30  [AI]        Prompt            COMPLETED  ✓
2024-12-15 09:02:10  [Approval]  Queued            APPROVED   ✓
2024-12-15 09:02:15  [Jira]      UpdateDescription COMPLETED  ✓
2024-12-15 09:05:00  [Sprint]    BulkCreate(32)    COMPLETED  ✓
2024-12-15 09:06:30  [Sprint]    CreateIssue       FAILED     ✗ → [Retry]
```

**Tree tab** — parent/child hierarchy:
```
▼ SRS Generation [corr_abc123] — COMPLETED
  ▼ AI Prompt (SRS Agent) — COMPLETED
  ▼ Approval: Update Jira PROJ-1234 — APPROVED
    ▼ Jira: UpdateDescription — COMPLETED
```

**Sessions tab:** one row per session (app launch → shutdown); expand to see all events.

**AI Prompts tab:**
- Prompt, model, temperature, seed, tokens used, estimated cost.
- [Replay] button: re-runs the identical request via orchestrator.
- [View response]: full AI response text.

**Files tab:** all generated/modified files; columns: document ID, version, format, hash, date, audit ID.

**Approvals tab:** all approval decisions (past + current queue snapshot). Filter by module, decision, date.

**Rollbacks tab:** all rollback operations; shows original event, rollback request, outcome.

### Event detail pane

Clicking any event opens the detail pane:

```
Audit Event: 01ARZ3NDEKTSV4RRFFQ69G5FAV
─────────────────────────────────────────────────────
Module:     SRS
Action:     SrsGeneration
Status:     Completed ✓
User:       akshay.patwari
Machine:    ACME-DEV-01
Timestamp:  2024-12-15 09:00:00 UTC (14:30:00 IST)
Duration:   12,450 ms
Retries:    0

Inputs:
  Brief:    "System to manage payment processing..."
  SRS ID:   PROJ-1234
  Template: Default SRS

Output Files:
  📄 SRS-PROJ-1234.docx  SHA-256: abc123...  [Download]
  📄 SRS-PROJ-1234.pdf   SHA-256: def456...  [Download]

Linked Events:
  ↑ Parent: (none)
  ↓ Children: [AI Prompt 01ARZ...], [Approval 01ARZ...], [Jira Update 01ARZ...]

[View AI Prompt]  [View Jira Request]  [View Approval Decision]  [Rollback]
```

---

## 10.8 Version control for configuration and templates

In addition to document versions, the audit framework tracks versions of:
- **Profiles**: every save creates a backup in `rollback/profiles/<profileId>/<timestamp>.json`.
- **Prompt templates**: every edited template is versioned (same manifest pattern as docs).
- **Generated code/tests**: each generation creates a new version (doc 09).

Compare and Restore are available for all version-tracked items from the Audit Dashboard "Files" tab.

---

## 10.9 Digital signatures (optional)

When `audit.signRecords = true` and a certificate thumbprint is configured:

```csharp
class DigitalSigner : IDigitalSigner {
  public string Sign(AuditRecord record, X509Certificate2 cert) {
    var canonicalJson = SerializeCanonical(record);
    using var rsa = cert.GetRSAPrivateKey()!;
    var signature = rsa.SignData(
      Encoding.UTF8.GetBytes(canonicalJson),
      HashAlgorithmName.SHA256,
      RSASignaturePadding.Pkcs1);
    return Convert.ToBase64String(signature);
  }

  public bool Verify(AuditRecord record, string signature, X509Certificate2 cert) {
    var canonicalJson = SerializeCanonical(record);
    using var rsa = cert.GetRSAPublicKey()!;
    return rsa.VerifyData(
      Encoding.UTF8.GetBytes(canonicalJson),
      Convert.FromBase64String(signature),
      HashAlgorithmName.SHA256,
      RSASignaturePadding.Pkcs1);
  }
}
```

Signatures are stored in the `signature` field of each JSONL record and verified during integrity checks. The certificate thumbprint is stored in configuration (not in the audit log); certificate retrieval uses the Windows Certificate Store.
