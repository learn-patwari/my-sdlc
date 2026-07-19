# 11. Security Architecture

Covers specification output section **§16 Security Architecture**.

---

## 11.1 Threat model

| Asset | Threat | Mitigation |
|---|---|---|
| Jira API tokens | Plaintext disclosure in config, logs, exports | DPAPI encryption; `secretRef` handles only; audit exports strip secrets |
| AI API keys | Same as above | Same |
| Git provider tokens | Same | Same |
| Audit log | Tampering / deletion of records | Hash-chained JSONL; integrity verified on startup; write-gated (only append) |
| Generated documents | Unauthorized access | Stored under the user's configured Working Directory; OS file ACLs apply |
| Profile files | Secret extraction | Profiles contain only ciphertext handles; live in `%APPDATA%` (per-user); never committed to source control |
| AI prompt/response | Data exfiltration via prompt injection | Prompts are constructed server-side from templates + variables, not from raw user HTML; AI responses are sandboxed and never executed |
| Jira write operations | Unauthorized Jira changes | Approval gate; no write without a prior audit record and explicit user decision |

---

## 11.2 Secret storage: Windows DPAPI

**Design choice:** Windows DPAPI (`System.Security.Cryptography.ProtectedData`) with `DataProtectionScope.CurrentUser`. Ciphertext is machine- and user-bound — only decryptable by the same Windows user on the same machine.

### Storage layout

Ciphertext blobs are stored in `%APPDATA%\SdlcCopilot\secrets\<handle-id>.bin`. The `handle-id` matches the `secretRef` value in the profile (`dpapi:jira-token-acme` → `jira-token-acme.bin`).

### Implementation

```csharp
class DpapiSecretStore : ISecretStore {
  private readonly string _secretsDir;

  public DpapiSecretStore(IOptions<AppPaths> paths) {
    _secretsDir = paths.Value.SecretsDir;  // %APPDATA%\SdlcCopilot\secrets\
    Directory.CreateDirectory(_secretsDir);
  }

  public Task<string> Get(string handle) {
    var handleId = ParseHandleId(handle);  // "dpapi:jira-token-acme" → "jira-token-acme"
    var path = Path.Combine(_secretsDir, $"{handleId}.bin");

    if (!File.Exists(path))
      throw new SecretNotFoundException($"Secret '{handle}' not found. Re-enter credentials.");

    var ciphertext = File.ReadAllBytes(path);
    var plaintext = ProtectedData.Unprotect(
      ciphertext,
      null,  // optional additional entropy (can be set per-secret for defense-in-depth)
      DataProtectionScope.CurrentUser);

    return Task.FromResult(Encoding.UTF8.GetString(plaintext));
  }

  public async Task Store(string handle, string secret) {
    // Audited write (caller must hold an audit record before calling this)
    var handleId = ParseHandleId(handle);
    var plaintext = Encoding.UTF8.GetBytes(secret);
    var ciphertext = ProtectedData.Protect(
      plaintext,
      null,
      DataProtectionScope.CurrentUser);

    // Zero out plaintext immediately after encryption
    CryptographicOperations.ZeroMemory(plaintext);

    var path = Path.Combine(_secretsDir, $"{handleId}.bin");
    await File.WriteAllBytesAsync(path, ciphertext);
  }

  public Task Delete(string handle) {
    var path = Path.Combine(_secretsDir, $"{ParseHandleId(handle)}.bin");
    if (File.Exists(path)) File.Delete(path);
    return Task.CompletedTask;
  }

  private static string ParseHandleId(string handle) {
    const string prefix = "dpapi:";
    if (!handle.StartsWith(prefix)) throw new ArgumentException("Invalid secret handle format");
    return handle[prefix.Length..];
  }
}
```

### Operational rules

- Secrets are **never** logged (Serilog has a destructuring policy that masks any property named `*token*`, `*key*`, `*password*`, `*secret*`).
- Audit records **never** contain plaintext secrets — only the `dpapi:handle` reference.
- Profile exports (for backup) strip all `secretRef` values and note "Secret removed — re-enter on import."
- The secrets directory is excluded from Working Directory backups.

---

## 11.3 Credential handling in memory

- Secrets are decrypted on-demand and held in memory only for the duration of the HTTP request.
- The variable holding a decrypted secret is a `string` (GC-managed); upon completion, no explicit zeroing is possible on managed strings. For higher-security deployments, a `SecureString` or `ReadOnlySpan<byte>` pipeline can be introduced in a future phase.
- HTTP request objects holding the Authorization header are disposed immediately after the request completes.

---

## 11.4 Transport security (TLS)

All outbound HTTPS calls:
- Use `HttpClientFactory`-managed `HttpClient` instances with TLS 1.2+ enforced.
- Certificate validation is **not** disabled — never.
- For enterprise GitLab / self-hosted Jira with custom CA: custom CA certificate path configurable per repository/SDLC-tool integration.

```csharp
services.AddHttpClient<IJiraClient, JiraClient>()
  .ConfigurePrimaryHttpMessageHandler(sp => {
    var settings = sp.GetRequiredService<JiraSettings>();
    var handler = new HttpClientHandler();
    if (settings.CustomCaCertPath != null) {
      handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => {
        // Append custom CA to chain validation
        var customCa = new X509Certificate2(settings.CustomCaCertPath);
        chain.ChainPolicy.ExtraStore.Add(customCa);
        return chain.Build(cert);
      };
    }
    return handler;
  });
```

---

## 11.5 Input validation and injection prevention

| Input source | Risk | Mitigation |
|---|---|---|
| User-supplied brief / SRS ID | Prompt injection (adversarial input to AI) | Templates are rendered server-side; user input goes in clearly delimited variable slots, not inline in prompt instructions |
| Jira JQL from UI | JQL injection | JQL is sent to the Jira search endpoint, not executed locally; Jira validates it |
| File paths (template dir, working dir) | Path traversal | All configured paths are canonicalized and compared against allowed root directories |
| Import files (Excel, Word) | Malicious macros | Open XML SDK is used for structured parsing only; macros are not executed; files are opened read-only |
| draw.io XML | XML injection | Output XML is constructed via `XDocument` API, not string concatenation |

---

## 11.6 Data-at-rest protection summary

| Data | Location | Protection |
|---|---|---|
| Plaintext secrets | Memory only (transient) | DPAPI encryption at rest; zeroed after use |
| Ciphertext secrets | `%APPDATA%\SdlcCopilot\secrets\` | OS file ACLs (per-user); DPAPI content |
| Profile JSON | `%APPDATA%\SdlcCopilot\profiles\` | OS file ACLs; no secrets in profiles |
| Audit JSONL | Working Directory / Audit / | OS file ACLs; hash-chained integrity |
| SQLite index | Working Directory / Audit / index.db | OS file ACLs; rebuildable from JSONL |
| Generated docs | Working Directory / Documents / | OS file ACLs; content-hashed |
| AI prompts / responses | Working Directory / Audit / …/prompts/ | OS file ACLs; may contain PII from user input — document this in privacy policy |

---

## 11.7 Audit log export security

When the user exports audit data (CSV, HTML) for sharing:
- Option to **redact** AI prompts and responses (may contain business-sensitive content).
- Option to **exclude** input/output fields (keeps only metadata: action, status, timestamp, audit ID).
- Exports are themselves audited (an export event is appended to the log).

---

## 11.8 Future security enhancements (Phase 6+)

- **Secrets rotation**: detect expiring tokens and prompt user to rotate without disrupting ongoing sessions.
- **Audit log encryption at rest**: encrypt JSONL files using DPAPI (adds protection if Working Directory is on a shared drive).
- **Role-based access** (multi-user scenario): config controls which modules each user can access (out of scope for single-user Phase 1).
- **Signed MSIX packaging**: code-signed installer for enterprise distribution via Windows Package Manager or SCCM.
