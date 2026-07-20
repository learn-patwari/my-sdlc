# SprintForge Beta

> AI-Powered SDLC Copilot — Windows desktop application for end-to-end software delivery automation.

[![Build & Package](https://github.com/learn-patwari/my-sdlc/actions/workflows/build-release.yml/badge.svg)](https://github.com/learn-patwari/my-sdlc/actions/workflows/build-release.yml)

SprintForge automates the complete Software Development Life Cycle: AI-assisted document generation (SRS, SAD, SDD), Jira automation, sprint planning, repository analysis, and unit-test generation — built around a mandatory audit and approval framework where **no write operation ever executes without a pre-recorded audit entry and explicit user approval**.

---

## Features

| Module | What it does |
|---|---|
| **Requirements (SRS)** | Generate Software Requirements Specifications from free-text input; diff view; submit for approval |
| **Architecture (SAD)** | Produce System Architecture Documents with interactive component tree and draw.io export |
| **Detailed Design (SDD)** | Generate per-service design documents and unit test suites with coverage stats |
| **Sprint Planning** | Pick SRS items, auto-schedule tasks on a draggable Gantt timeline, push to Jira |
| **Repository Analyzer** | Scan repos for complexity, duplication, tech debt, and impacted files |
| **Jira Integration** | Search, create, and update issues — every write goes through the Approvals Center |
| **Approvals Center** | Single queue for all pending writes; approve, modify, or reject before anything executes |
| **Audit Center** | Immutable hash-chained audit log with timeline, search, and AI-prompt replay |
| **Settings** | Jira, AI provider, Git repositories, sprint settings — all profiles encrypted via DPAPI |

---

## Technology

| Concern | Choice |
|---|---|
| Platform | Windows 10/11 desktop, .NET 8 |
| UI | WPF + MVVM (CommunityToolkit.Mvvm) |
| Architecture | Clean Architecture — Domain / Application / Infrastructure / WPF |
| DI / hosting | `Microsoft.Extensions.Hosting` generic host |
| Audit store | Hash-chained JSONL (source of truth) + SQLite index via EF Core |
| Secrets | Windows DPAPI, per-user scope — never plaintext at rest |
| Jira | REST v3 typed client via `HttpClientFactory` + Polly retry |
| AI providers | OpenAI, Azure OpenAI, Anthropic Claude, Gemini, Ollama — all behind `IAiProvider` |
| Git | LibGit2Sharp + adapters for GitHub / GitLab / Bitbucket |
| Documents | Open XML SDK (DOCX), QuestPDF (PDF), Markdig (MD/HTML), native draw.io XML |
| Logging | Serilog with correlation IDs end-to-end |

---

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) *(installer only)*
- Windows 10 or 11 (WPF requires Windows)

### All commands

```powershell
# ── Restore ────────────────────────────────────────────────────────────────
dotnet restore SprintForge.sln

# ── Build ──────────────────────────────────────────────────────────────────
dotnet build SprintForge.sln

# ── Test ───────────────────────────────────────────────────────────────────
dotnet test tests/SprintForge.Tests/SprintForge.Tests.csproj

# ── Run (debug) ────────────────────────────────────────────────────────────
dotnet run --project src/SprintForge.Wpf/SprintForge.Wpf.csproj

# ── Publish portable EXE ───────────────────────────────────────────────────
dotnet publish src/SprintForge.Wpf/SprintForge.Wpf.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish\SprintForge-win-x64
# Output: publish\SprintForge-win-x64\SprintForge.exe

# ── Build installer (Setup.exe) ────────────────────────────────────────────
.\installer\build-installer.ps1
# Output: installer\output\Setup.exe

# ── Tag a GitHub Release (CI builds + attaches both files automatically) ───
git tag v1.0.0
git push origin v1.0.0
```

> The WPF project targets `net8.0-windows` and compiles on Windows only.  
> Domain, Application, Infrastructure, and Tests target `net8.0` and are cross-platform.

---

## CI / Artifacts

Every push to `main` or the development branch runs the [build-release workflow](.github/workflows/build-release.yml):

1. Restore → Build (all projects)
2. Run xUnit tests
3. Publish self-contained `SprintForge.exe`
4. Upload as a GitHub Actions artifact named **SprintForge-win-x64**

Download the latest artifact from the [Actions tab](https://github.com/learn-patwari/my-sdlc/actions).

---

## Repository layout

```
src/
├── SprintForge.Domain/           Entities, enums, value objects (no dependencies)
├── SprintForge.Application/      Core interfaces and orchestration contracts
├── SprintForge.Infrastructure/   EF Core, DPAPI secrets, Jira/AI/Git adapters
└── SprintForge.Wpf/              WPF shell, ViewModels, Views

tests/
└── SprintForge.Tests/            xUnit — audit write-gate, config validation, DPAPI

config/
└── profile.schema.json           JSON Schema for configuration profiles

docs/
└── ...                           Full design package (see below)
```

---

## Design documents

| Document | Contents |
|---|---|
| [01-requirements-validation.md](docs/01-requirements-validation.md) | Requirement validation, clarifications, recorded assumptions |
| [02-solution-architecture.md](docs/02-solution-architecture.md) | High-level architecture, technology stack, WPF justification |
| [03-folder-structure.md](docs/03-folder-structure.md) | Solution layout and working-directory conventions |
| [04-data-and-config.md](docs/04-data-and-config.md) | SQLite audit schema, configuration JSON Schema |
| [05-ui-ux.md](docs/05-ui-ux.md) | Screen inventory, wireframes, navigation flow, Approvals Center spec |
| [06-module-designs.md](docs/06-module-designs.md) | Per-module design for all 14 modules |
| [07-ai-orchestration.md](docs/07-ai-orchestration.md) | Agent registry, prompt templates, reproducibility |
| [08-integrations.md](docs/08-integrations.md) | Jira and repository integration designs |
| [09-document-engine.md](docs/09-document-engine.md) | Document generation pipeline, templating, versioning |
| [10-audit-approval-framework.md](docs/10-audit-approval-framework.md) | Audit-first pipeline, hash chain, approval workflow, rollback |
| [11-security.md](docs/11-security.md) | Security architecture, threat model, DPAPI encryption |
| [12-operations.md](docs/12-operations.md) | Error handling, logging, deployment and MSIX packaging |
| [13-testing-strategy.md](docs/13-testing-strategy.md) | Unit, integration, UI, and E2E testing strategy |
| [14-performance-scalability.md](docs/14-performance-scalability.md) | Performance and scalability considerations |
| [15-roadmap.md](docs/15-roadmap.md) | Phased delivery plan, future scope, risks and mitigations |

---

## Phased delivery plan

| Phase | Scope | Milestone |
|---|---|---|
| 1 — Foundation | Configuration, Security, Audit core | Every action audited; secrets encrypted; profiles CRUD |
| 2 — Integrations | Jira, AI orchestration | Approval-gated Jira round-trip; reproducible AI calls |
| 3 — First generation | SRS Generator, Document engine | Brief → SRS → approval → Jira update, fully audited |
| 4 — Architecture docs | SAD, SDD | Diagrams and docs generated, linked in Jira |
| 5 — Planning & repos | Sprint Planning, Repository Analyzer | Bulk work-item creation via Approvals Center |
| 6 — Test generation | Unit test generator, rollback, packaging | Coverage-targeted tests; MSIX installer |
| 7 — Future scope | RTM, release notes, additional SDLC tools, plugin SDK | Extensions behind existing abstractions |

---

## Security note

Secrets (API tokens, passwords) are stored using Windows DPAPI and are never written in plaintext to disk, logs, or audit records. Configuration profiles store only encrypted ciphertext references (`dpapi:handle`).
