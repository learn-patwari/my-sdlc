# SprintForge Beta — Windows Build & Run Guide

## Prerequisites

- **Windows 10 21H2** or **Windows 11** (WPF requires Windows)
- **.NET 8 SDK** (download from https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Visual Studio 2022** (recommended) or VS Code with C# extension

## Clone & Build

```powershell
# Clone the branch
git clone --branch claude/sdlc-copilot-architecture-a65lfd <repo-url>
cd my-sdlc

# Restore dependencies
dotnet restore

# Build entire solution
dotnet build SprintForge.sln -c Debug
```

## Run the Application

### Option 1: From CLI

```powershell
dotnet run --project src\SprintForge.Wpf\SprintForge.Wpf.csproj
```

### Option 2: From Visual Studio

1. Open `SprintForge.sln` in Visual Studio 2022
2. Right-click `SprintForge.Wpf` project → Set as Startup Project
3. Press **F5** to run (Debug) or **Ctrl+F5** (Release)

## What's Built

### Core (Linux-safe, already tested)
- **Domain** — entities, enums, value objects
- **Application** — interfaces, contracts (ITestGenerationService, IRollbackService, etc.)
- **Infrastructure** — implementations, EF Core, Jira client, AI orchestration, audit write-gate
- **Tests** — 93 xUnit tests (91 passing, 2 Windows-only DPAPI skipped)

All 93 tests pass on Linux. Run them with:
```powershell
dotnet test tests\SprintForge.Tests\SprintForge.Tests.csproj
```

### UI (Windows-only)
- **SprintForge.Wpf** — WPF shell + MVVM
  - **App.xaml** — dark theme (navy #080C18 + purple #7C3AED), color tokens, typography, global styles
  - **MainWindow** — 56px top bar + 220px left nav rail with 11 nav items, content routing
  - **DashboardView** — KPI stat cards, AI recommendations, recent activity feed
  - **SettingsView** — Jira integration form (URL, username, API token), Test Connection button
  - **PlaceholderView** — "Coming soon" for SRS, SAD, SDD, Sprint, Repository, Tests, Jira modules

## Features Implemented

| Feature | Status | Module |
|---------|--------|--------|
| Audit write-gate (JSONL + SQLite index) | ✅ Complete | M12 |
| Hash-chained audit records | ✅ Complete | M12 |
| DPAPI secret store (Windows) | ✅ Complete | M13 |
| Jira REST v3 client | ✅ Complete | M8 |
| AI orchestration (5 providers) | ✅ Complete | M11 |
| Config profile management | ✅ Complete | M1 |
| Document versioning (DOCX/PDF/MD) | ✅ Complete | M9 |
| SRS generator | ✅ Complete | M2 |
| SAD generator (draw.io XML) | ✅ Complete | M3 |
| SDD generator | ✅ Complete | M5 |
| Sprint planning | ✅ Complete | M4 |
| Repository analyzer | ✅ Complete | M6 |
| Repository integration (local + GitHub stub) | ✅ Complete | M10 |
| Unit test generator | ✅ Complete | M7 |
| Rollback service | ✅ Complete | M7 |
| Audit dashboard (7 read-only views) | ✅ Complete | M7 |
| WPF shell + navigation | ✅ Complete | UI |
| Dashboard screen | ✅ Complete | UI |
| Settings screen | ✅ Complete | UI |
| MSIX packaging stub | ✅ Complete | Packaging |

## Application Initialization

On first run, the app creates:
```
%APPDATA%\SprintForge\
├── audit.jsonl         (immutable audit log)
├── audit.db            (SQLite index)
├── secrets.json        (encrypted with DPAPI)
└── profiles/           (user configuration profiles)
```

## Architecture Overview

**Clean Architecture** with MVVM:
- Domain layer — entities, enums (no dependencies)
- Application layer — interfaces, contracts, orchestration
- Infrastructure layer — EF Core, HTTP clients, file I/O, adapters
- WPF layer — MVVM views, view-models, generic host bootstrap

**Write-gate pattern**:
All write operations (Jira, docs, config, tests) go through `AuditedOperationRunner`:
1. Pre-record audit entry → 2. Get approval → 3. Execute → 4. Record outcome

**Testing**:
- 93 xUnit tests (no mocks for file I/O, DPAPI, JSONL)
- Real temp directories, real SQLite, real JSONL files
- Windows-only tests skipped on Linux (DPAPI)

## Troubleshooting

### "This project requires the WindowsDesktop SDK"
**You're on Linux or macOS.** The solution builds on those platforms (non-WPF projects), but the WPF project requires Windows. Use it on Windows 10/11.

### Build fails on Windows with "NuGet restore failed"
```powershell
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
dotnet restore
```

### "DPAPI decryption failed"
DPAPI credentials are per-user and per-machine. If you copy `secrets.json` between machines, delete it and re-enter credentials.

## Next Steps

**Phase 7** (future) — adapters for Azure DevOps, GitLab, ServiceNow; CRS module, worklog assistant, release notes, PR assistant, Confluence publishing, plugin SDK. All behind existing abstractions — no core rework needed.

---

Build date: `7b04cf2` on `claude/sdlc-copilot-architecture-a65lfd`  
Tests: **93 total** (91 pass + 2 Windows-only skipped on non-Windows)
