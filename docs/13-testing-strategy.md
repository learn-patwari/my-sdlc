# 13. Testing Strategy

Covers specification output section **§20 Testing Strategy** — unit, integration, UI, and end-to-end.

---

## 13.1 Overview

| Level | Tooling | Scope | Run environment |
|---|---|---|---|
| Unit | xUnit + NSubstitute + FluentAssertions | Domain + Application layer (pure logic) | Linux / Windows / CI |
| Integration | xUnit + real adapters (test doubles for external APIs) | Infrastructure layer: DPAPI, SQLite, JSONL write, document generators | Windows (DPAPI); others cross-platform |
| UI | Coded UI via WPF TestFriendly patterns (ViewModel tests, no pixel-level) | ViewModel logic, navigation, binding | Windows |
| End-to-end | FlaUI or Playwright for .NET (Windows UI automation) | Full user workflow (config → generate → approve → verify Jira) | Windows with test Jira instance |

---

## 13.2 Unit tests

### Target projects: `SdlcCopilot.Domain`, `SdlcCopilot.Application`

Everything in Domain is pure C# with zero external dependencies — testable without mocks.

### Critical unit tests to implement

**Audit write-gate (the architectural keystone — Rule 10):**

```csharp
public class AuditedOperationDecoratorTests {
  [Fact]
  public async Task WriteIsBlocked_WhenAuditFlushFails() {
    // Arrange
    var failingAudit = Substitute.For<IAuditService>();
    failingAudit.Record(Arg.Any<AuditContext>())
      .Returns<AuditId>(_ => throw new IOException("Disk full"));

    var innerService = Substitute.For<IJiraWriteService>();
    var gate = new ApprovalGate(failingAudit, Substitute.For<IApprovalQueue>());
    var decorator = new AuditedJiraWriteServiceDecorator(innerService, gate);

    // Act + Assert
    await Assert.ThrowsAsync<AuditUnavailableException>(
      () => decorator.CreateIssue(new IssueCreateRequest { Summary = "Test" }));

    // Verify inner service was NEVER called
    await innerService.DidNotReceive().CreateIssue(Arg.Any<IssueCreateRequest>());
  }

  [Fact]
  public async Task WriteQueuesForApproval_WhenAuditFlushSucceeds() {
    // Arrange
    var audit = Substitute.For<IAuditService>();
    audit.Record(Arg.Any<AuditContext>()).Returns(new AuditId("01ARZ..."));
    var queue = Substitute.For<IApprovalQueue>();
    var innerService = Substitute.For<IJiraWriteService>();
    var gate = new ApprovalGate(audit, queue);
    var decorator = new AuditedJiraWriteServiceDecorator(innerService, gate);

    // Act
    await decorator.CreateIssue(new IssueCreateRequest { Summary = "Test" });

    // Assert: queued but NOT executed
    await queue.Received(1).Enqueue(Arg.Any<ApprovalItem>());
    await innerService.DidNotReceive().CreateIssue(Arg.Any<IssueCreateRequest>());
  }
}
```

**Sprint calendar validation:**

```csharp
public class SprintCalendarTests {
  [Theory]
  [InlineData(10, 7, 2, 8, 56)]  // 10 days, 7 dev days, 2 buffer, 8 hrs/day → 56 hrs capacity
  [InlineData(10, 7, 2, 8, 56, new[] {"2024-12-25"})]  // with holiday → 48 hrs
  public void CalculateCapacity_ReturnsCorrectHours(
    int durationDays, int devDays, int bufferDays, int hoursPerDay,
    int expectedHours, string[]? holidays = null) {
    
    var calendar = new SprintCalendar {
      DurationDays = durationDays, DevelopmentDays = devDays,
      BufferDays = bufferDays, WorkingHoursPerDay = hoursPerDay,
      Holidays = holidays?.Select(DateTime.Parse).ToArray() ?? Array.Empty<DateTime>()
    };
    
    var start = new DateTime(2024, 12, 23);
    Assert.Equal(expectedHours, calendar.CalculateAvailableHours(start));
  }

  [Fact]
  public void Validate_BlocksSubtask_WhenEstimateExceedsDevDays() {
    var item = new WorkItem { Kind = WorkItemKind.Subtask, EstimateHours = 100 };
    var calendar = new SprintCalendar { DevelopmentDays = 7, WorkingHoursPerDay = 8 };
    
    var result = new SprintValidator().Validate(item, calendar);
    
    Assert.False(result.IsValid);
    Assert.Contains(result.Violations, v => v.Code == "ESTIMATE_EXCEEDS_DEV_DAYS");
  }
}
```

**Configuration schema validation:**

```csharp
public class ProfileValidatorTests {
  [Fact]
  public async Task ValidProfile_PassesValidation() {
    var profile = ProfileFixtures.ValidAcmeProd();
    var validator = new ProfileValidator(schema: LoadSchema("config/profile.schema.json"));
    
    var result = await validator.Validate(profile);
    
    Assert.True(result.IsValid);
  }

  [Fact]
  public async Task MissingServerUrl_FailsWithFieldError() {
    var profile = ProfileFixtures.ValidAcmeProd() with { SdlcTool = new() { ServerUrl = "" } };
    var validator = new ProfileValidator(schema: LoadSchema("config/profile.schema.json"));
    
    var result = await validator.Validate(profile);
    
    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.Field == "sdlcTool.serverUrl");
  }
}
```

**AI reproducibility (identical params → same request hash):**

```csharp
public class AiReplayTests {
  [Fact]
  public void SameParamsProduceSameRequestHash() {
    var req1 = new AiRequest { Model = "claude-sonnet-5", Temperature = 0.2f, Seed = 42, UserPrompt = "X" };
    var req2 = new AiRequest { Model = "claude-sonnet-5", Temperature = 0.2f, Seed = 42, UserPrompt = "X" };
    
    Assert.Equal(AiRequestHasher.Hash(req1), AiRequestHasher.Hash(req2));
  }
}
```

### Naming convention

`{Class}Tests` for unit tests. Method naming: `{Method}_{Condition}_{ExpectedOutcome}`.

---

## 13.3 Integration tests

### Target: `SdlcCopilot.Infrastructure`

Integration tests hit real implementations with controlled external dependencies.

**DPAPI round-trip (Windows-only):**

```csharp
[WindowsOnlyFact]  // custom attribute skips on non-Windows
public async Task DpapiSecretStore_StoreAndRetrieve_Roundtrip() {
  var store = new DpapiSecretStore(TempSecretsDir());
  var secret = "my-super-secret-token-abc123";
  
  await store.Store("dpapi:test-key", secret);
  var retrieved = await store.Get("dpapi:test-key");
  
  Assert.Equal(secret, retrieved);
  
  await store.Delete("dpapi:test-key");
  await Assert.ThrowsAsync<SecretNotFoundException>(() => store.Get("dpapi:test-key"));
}
```

**JSONL audit writer + hash chain:**

```csharp
[Fact]
public async Task JsonlAuditWriter_HashChain_IsValid() {
  var writer = new JsonlAuditWriter(TempDir());
  
  var ids = new List<AuditId>();
  for (int i = 0; i < 5; i++) {
    ids.Add(await writer.Record(new AuditContext { Action = $"Action{i}", Module = "Test" }));
  }
  
  var verifier = new AuditChainVerifier();
  var report = await verifier.Verify(TempDir());
  
  Assert.True(report.IsIntact);
  Assert.Empty(report.Violations);
}

[Fact]
public async Task JsonlAuditWriter_HashChain_DetectsTampering() {
  var dir = TempDir();
  var writer = new JsonlAuditWriter(dir);
  
  await writer.Record(new AuditContext { Action = "Action1", Module = "Test" });
  await writer.Record(new AuditContext { Action = "Action2", Module = "Test" });
  
  // Tamper with first record
  var file = Directory.GetFiles(dir, "audit.jsonl", SearchOption.AllDirectories).First();
  var lines = File.ReadAllLines(file);
  var tampered = lines[0].Replace("Action1", "Tampered");
  File.WriteAllLines(file, new[] { tampered }.Concat(lines.Skip(1)));
  
  var report = await new AuditChainVerifier().Verify(dir);
  
  Assert.False(report.IsIntact);
  Assert.NotEmpty(report.Violations);
}
```

**SQLite audit index:**

```csharp
[Fact]
public async Task AuditDbContext_CanSaveAndQueryAuditEvent() {
  var options = new DbContextOptionsBuilder<AuditDbContext>()
    .UseSqlite("Data Source=:memory:")
    .Options;
  
  using var context = new AuditDbContext(options);
  await context.Database.EnsureCreatedAsync();
  
  var evt = new AuditEventEntity { AuditId = "01ARZ...", Module = "SRS", Action = "SrsGeneration" };
  context.AuditEvents.Add(evt);
  await context.SaveChangesAsync();
  
  var found = await context.AuditEvents.FindAsync("01ARZ...");
  Assert.NotNull(found);
  Assert.Equal("SRS", found.Module);
}
```

**Document generator (DOCX output validation):**

```csharp
[Fact]
public async Task DocxGenerator_ProducesValidOpenXmlDocument() {
  var generator = new DocxDocumentGenerator(TestTemplatesDir());
  var result = await generator.Generate(new DocumentGenerationRequest {
    DocumentId = "SRS:TEST-1",
    Content = "# Test SRS\n## Functional Requirements\n- Requirement 1",
    TemplateName = "default-srs"
  });
  
  // Validate that the output is a valid OOXML document
  using var stream = new MemoryStream(result.Outputs[ExportFormat.DOCX]);
  using var doc = WordprocessingDocument.Open(stream, false);
  Assert.NotNull(doc.MainDocumentPart);
  Assert.Contains("Requirement 1", doc.MainDocumentPart.Document.InnerText);
}
```

---

## 13.4 ViewModel tests (UI unit tests)

WPF ViewModels are tested without the WPF runtime:

```csharp
public class ApprovalsViewModelTests {
  [Fact]
  public async Task ApproveAll_ExecutesAllQueuedItems() {
    var gate = Substitute.For<IApprovalGate>();
    var queue = new InMemoryApprovalQueue();
    queue.Enqueue(new ApprovalItem { ApprovalId = "1" });
    queue.Enqueue(new ApprovalItem { ApprovalId = "2" });
    
    var vm = new ApprovalsViewModel(queue, gate);
    await vm.LoadAsync();
    
    await vm.ApproveAllCommand.ExecuteAsync(null);
    
    await gate.Received(2).Execute(Arg.Any<string>());
  }
}
```

---

## 13.5 End-to-end tests

**Tooling:** FlaUI (Windows UI Automation framework) for WPF, or Playwright for .NET (for any Chromium-rendered UI components).

**Key E2E scenario: SRS to Jira (happy path):**

```csharp
[WindowsOnlyFact]
public async Task SrsToJira_HappyPath_E2E() {
  // Launch the app
  var app = Application.Launch("SdlcCopilot.Wpf.exe");
  var mainWindow = app.GetMainWindow(Automation);

  // Navigate to SRS module
  mainWindow.FindFirstDescendant(cf => cf.ByName("SRS")).AsButton().Click();

  // Fill in brief description and SRS ID
  mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BriefDescriptionInput"))
    .AsTextBox().Enter("Payment processing system");
  mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SrsIdInput"))
    .AsTextBox().Enter("TEST-100");

  // Wait for generation (stubbed AI provider for test)
  await WaitForCondition(() =>
    mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("PreviewContent"))
      .AsTextBlock().Text.Contains("Functional Requirements"), timeout: 30.Seconds());

  // Submit for approval
  mainWindow.FindFirstDescendant(cf => cf.ByName("Submit for Approval")).AsButton().Click();

  // Navigate to Approvals Center
  mainWindow.FindFirstDescendant(cf => cf.ByName("Approvals")).AsButton().Click();

  // Approve the queued item
  mainWindow.FindFirstDescendant(cf => cf.ByName("Approve")).AsButton().Click();

  // Verify audit trail shows Completed
  mainWindow.FindFirstDescendant(cf => cf.ByName("Audit")).AsButton().Click();
  var firstEvent = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("AuditList"))
    .AsListBox().Items.First();
  Assert.Contains("COMPLETED", firstEvent.Text);
}
```

For CI: E2E tests run on a Windows agent with a test Jira Cloud instance (credentials from GitHub Secrets). AI calls use a `StubAiProvider` that returns pre-canned responses (fast, deterministic, no API cost).

---

## 13.6 Test coverage targets

| Project | Target | Enforcement |
|---|---|---|
| `SdlcCopilot.Domain` | 95% | Required gate in CI |
| `SdlcCopilot.Application` | 90% | Required gate in CI |
| `SdlcCopilot.Infrastructure` | 80% | Required gate in CI |
| `SdlcCopilot.Wpf` (ViewModels only) | 70% | Advisory (UI automation covers the gap) |

Coverage is measured via `coverlet` (Coverlet.Collector) and reported as `lcov` for GitHub Actions coverage summary.

---

## 13.7 Test data and fixtures

- `ProfileFixtures` static class: creates valid and invalid `Profile` instances for reuse.
- `AuditEventFixtures`: creates valid `AuditContext` instances.
- `StubAiProvider`: implements `IAiProvider`; returns pre-canned markdown responses for each agent name.
- `InMemoryApprovalQueue`: implements `IApprovalQueue` in memory for ViewModel tests.
- `TempDirectoryFixture` (`IDisposable`): creates a temp directory per test; cleans up after.
- `WindowsOnlyFactAttribute`: skips tests on non-Windows platforms (DPAPI, WPF automation).
