# 8. Integrations

Covers specification output sections **§12 Jira Integration Design** and **§13 Repository Integration Design**.

---

## 8.1 Jira Integration Design

### 8.1.1 API approach

**Jira Cloud REST v3** via raw `HttpClient` (not Atlassian.NET SDK — that SDK is unmaintained and cloud-lagging). All HTTP calls go through `HttpClientFactory` with named clients per integration, Polly retry/circuit-breaker, and per-call audit.

```csharp
interface IJiraClient {
  // Read operations (audited; do not require approval gate)
  Task<SearchResults>   Search(string jql, int maxResults = 50, string[] fields = null);
  Task<Issue>           GetIssue(string issueKey, string[] fields = null);
  Task<IList<Project>>  ListProjects();
  Task<IList<IssueType>> GetIssueTypes(string projectKey);
  Task<IList<Field>>    GetCustomFields();
  Task<IList<Transition>> GetTransitions(string issueKey);

  // Write operations (all wrapped by AuditedOperationDecorator — require approval)
  Task<Issue>   CreateIssue(IssueCreateRequest request);
  Task          UpdateIssue(string issueKey, IssueUpdateRequest request);
  Task          TransitionIssue(string issueKey, string transitionId);
  Task          AttachFile(string issueKey, byte[] content, string fileName, string mimeType);
  Task          LinkIssues(string fromKey, string toKey, string linkType);
  Task          BulkCreateIssues(IssueCreateRequest[] requests);  // Sprint Planning
}
```

### 8.1.2 Client registration

```csharp
services.AddHttpClient<IJiraClient, JiraClient>((sp, client) => {
  var settings = sp.GetRequiredService<IOptionsSnapshot<JiraSettings>>().Value;
  client.BaseAddress = new Uri($"{settings.ServerUrl}/rest/api/3/");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());
```

Auth header is injected per-request (not in `DefaultRequestHeaders`) so it reads from DPAPI at call time, never from configuration values in memory longer than needed:

```csharp
private async Task<HttpRequestMessage> BuildRequest(HttpMethod method, string path) {
  var token = await _secretStore.Get(_settings.Auth.SecretRef);
  var msg = new HttpRequestMessage(method, path);
  msg.Headers.Authorization = new AuthenticationHeaderValue(
    "Basic", Convert.ToBase64String(
      Encoding.UTF8.GetBytes($"{_settings.Auth.Username}:{token}")));
  return msg;
}
```

### 8.1.3 Field mapping

Jira's custom field IDs (e.g., `customfield_10016` for Story Points) are resolved from configuration:

```csharp
class JiraFieldMapper {
  private readonly JiraConfig _config;

  public object MapStoryPoints(int points) =>
    new Dictionary<string, object> { [_config.CustomFields["storyPoints"]] = points };

  public object MapSprint(string sprintId) =>
    new Dictionary<string, object> { [_config.CustomFields["sprint"]] = int.Parse(sprintId) };

  public object MapEpicLink(string epicKey) =>
    new Dictionary<string, object> { [_config.EpicLinkField] = epicKey };
}
```

No hardcoded field names anywhere. All field IDs come from the active profile.

### 8.1.4 JQL query builder

Both visual (filter chips) and raw JQL modes are supported. The visual builder outputs a JQL string:

```
project IN (PROJ, PLAT)
  AND status != Done
  AND issuetype = Story
  AND sprint in openSprints()
  ORDER BY created DESC
```

JQL is validated by calling the Jira search API with `maxResults=0` first (dry-run); errors are shown inline.

### 8.1.5 Bulk operations and Sprint Planning push

Sprint Planning can create 50+ issues in one "Push to Jira" action. Jira supports `/rest/api/3/issue/bulk` for up to 50 issues per request; the client batches larger payloads:

```csharp
public async Task BulkCreateIssues(IssueCreateRequest[] requests) {
  foreach (var batch in requests.Chunk(50)) {
    var body = new { issueUpdates = batch };
    var response = await PostAsync("issue/bulk", body);
    // record each created issue key in audit
  }
}
```

Each batch is a single approval item in the Approvals Center (with all issue summaries shown); the user clicks "Approve" once for the whole batch.

### 8.1.6 Confluence support (read)

Sprint plan input can be a Confluence page (Module 4). Confluence REST API v2:

```csharp
interface IConfluenceClient {
  Task<ConfluencePage> GetPage(string pageId);
  Task<string> GetPageContent(string pageId, string format = "storage");  // storage or wiki
}
```

Confluence writes (publishing generated docs) are Phase 7 scope.

### 8.1.7 Error handling

| HTTP Status | Handling |
|---|---|
| 400 Bad Request | Parse Jira error body; show field-level errors in UI |
| 401 Unauthorized | Show "Invalid credentials" banner; prompt to re-enter token in Config |
| 403 Forbidden | Show "Permission denied for [action] on [project]"; link to Jira admin |
| 404 Not Found | Issue/project no longer exists; update local cache |
| 429 Too Many Requests | Polly exponential backoff (headers `Retry-After` respected if present) |
| 503 Service Unavailable | Circuit breaker opens; show "Jira unavailable" status bar indicator |

All errors are recorded as FAILED audit events with the raw HTTP status and body.

### 8.1.8 API dialect support

`apiDialect` in configuration selects Cloud v3 vs Server v2 behavior:

```csharp
interface IJiraApiDialect {
  string SearchEndpoint { get; }  // "rest/api/3/search" vs "rest/api/2/search"
  object BuildIssueBody(IssueCreateRequest req);  // v3 uses ADF content; v2 uses plain text
  string ParseIssueKey(JsonElement response);
}
```

---

## 8.2 Repository Integration Design

### 8.2.1 Provider abstraction

```csharp
interface IRepositoryProvider {
  string ProviderId { get; }  // "local", "github", "gitlab", "bitbucket"
  Task<IRepository> Open(RepositoryConfig config);
  Task Clone(RepositoryConfig config, string localPath, IProgress<CloneProgress> progress);
}

interface IRepository {
  string LocalPath { get; }
  string Name { get; }
  Task<string>          GetCurrentBranch();
  Task<IList<Branch>>   ListBranches();
  Task<IList<Commit>>   GetCommitHistory(string branch, int limit = 50);
  Task<IList<FileChange>> GetChangedFiles(string fromRef, string toRef);
  Task<string>          ReadFile(string relativePath);
  Task<IList<string>>   ListFiles(string pattern = "**/*");
}
```

### 8.2.2 Local repository (LibGit2Sharp)

Zero `git.exe` dependency — LibGit2Sharp wraps `libgit2`:

```csharp
class LocalGitRepository : IRepository {
  private readonly LibGit2Sharp.Repository _repo;

  public LocalGitRepository(string localPath) {
    _repo = new LibGit2Sharp.Repository(localPath);
  }

  public Task<IList<Commit>> GetCommitHistory(string branch, int limit) {
    var commits = _repo.Branches[branch].Commits.Take(limit)
      .Select(c => new Commit { Sha = c.Sha, Message = c.MessageShort, Author = c.Author.Name, Timestamp = c.Author.When })
      .ToList();
    return Task.FromResult<IList<Commit>>(commits);
  }
}
```

### 8.2.3 Remote providers (GitHub, GitLab, Bitbucket)

Each remote provider uses its REST API for metadata (branches, commits, PR data) while LibGit2Sharp handles the actual clone/pull:

```csharp
class GitHubRepositoryProvider : IRepositoryProvider {
  public string ProviderId => "github";

  public async Task<IRepository> Open(RepositoryConfig config) {
    var token = await _secretStore.Get(config.SecretRef);
    // Ensure local clone is up to date
    await EnsureCloned(config, token);
    return new LocalGitRepository(config.LocalPath);
  }

  private async Task EnsureCloned(RepositoryConfig config, string token) {
    if (!Directory.Exists(config.LocalPath)) {
      LibGit2Sharp.Repository.Clone(
        config.Url,
        config.LocalPath,
        new CloneOptions {
          CredentialsProvider = (_, __, ___) =>
            new LibGit2Sharp.UsernamePasswordCredentials { Username = "x-token", Password = token }
        });
    } else {
      // Pull latest
      using var repo = new LibGit2Sharp.Repository(config.LocalPath);
      var remote = repo.Network.Remotes["origin"];
      repo.Network.Fetch(remote.Name,
        new[] { $"refs/heads/{config.DefaultBranch}:refs/heads/{config.DefaultBranch}" },
        new FetchOptions {
          CredentialsProvider = (_, __, ___) =>
            new LibGit2Sharp.UsernamePasswordCredentials { Username = "x-token", Password = token }
        });
    }
  }
}
```

GitLab and Bitbucket providers follow the same pattern with their respective auth mechanisms (GitLab PAT, Bitbucket App Password).

### 8.2.4 Repository analyzer

The analyzer classifies files by language + framework patterns:

```csharp
class RepositoryAnalyzer : IRepositoryAnalyzer {
  private readonly IList<ILanguageAnalyzerPlugin> _plugins;

  public async Task<AnalysisResult> Analyze(IRepository repo) {
    var files = await repo.ListFiles("**/*.{java,kt,py,ts,tsx,js,jsx,cs}");
    var units = new List<CodeUnit>();

    foreach (var file in files) {
      var content = await repo.ReadFile(file);
      foreach (var plugin in _plugins) {
        if (plugin.CanAnalyze(file)) {
          units.AddRange(plugin.ExtractUnits(file, content));
        }
      }
    }

    return new AnalysisResult {
      Repository = repo.Name,
      CodeUnits = units,
      DependencyGraph = BuildDependencyGraph(units),
      ExternalCalls = ExtractExternalCalls(units)
    };
  }
}
```

**Language plugins:**

| Plugin | Detects |
|---|---|
| `JavaAnalyzerPlugin` | `@Service`, `@Controller`, `@Repository`, `@RestController`, `@Component` classes; method signatures |
| `PythonAnalyzerPlugin` | Classes, functions; FastAPI route decorators (`@app.get`, `@router.post`); SQLAlchemy models |
| `TypeScriptAnalyzerPlugin` | Exported classes/functions; Angular `@Component`, `@Injectable`, `@Pipe`; React functional components |
| `CSharpAnalyzerPlugin` | ASP.NET controllers, services; EF Core DbContexts |

Each plugin produces `CodeUnit` records:

```csharp
record CodeUnit {
  public string FilePath { get; set; }
  public string Name { get; set; }
  public CodeUnitKind Kind { get; set; }  // Service, Controller, Repository, DTO, Utility, Config, Model
  public string Language { get; set; }
  public IList<string> Methods { get; set; }
  public IList<string> Dependencies { get; set; }
  public IList<ExternalCall> ExternalCalls { get; set; }
}
```

### 8.2.5 Impact analysis

For sprint planning and SDD generation, the analyzer recommends which files are likely impacted by a change:

```csharp
class ImpactAnalyzer {
  public ImpactReport RecommendImpacted(ChangeDescription change, AnalysisResult analysis) {
    // 1. Match change keywords to CodeUnit names and method names
    // 2. Traverse dependency graph: if Service A depends on Service B, and B is impacted, flag A too
    // 3. Return ranked list with explanation
    return new ImpactReport {
      ImpactedUnits = ranked,
      Explanation = "PaymentService and its 3 callers are likely impacted because..."
    };
  }
}
```

### 8.2.6 Security considerations for repos

- Repository tokens are stored in DPAPI, never on disk in plaintext.
- Cloning only proceeds if the target directory is within the configured Working Directory (path traversal check).
- Repository writes (committing test files) are approval-gated (Phase 6 scope).
- LibGit2Sharp's TLS verification is enabled by default; custom CA certs configurable for enterprise GitLab.
