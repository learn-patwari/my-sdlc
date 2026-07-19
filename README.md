# SprintForge

An enterprise-grade Windows desktop application that automates and manages the complete Software Development Life Cycle (SDLC) — AI-assisted document generation (SRS / SAD / SDD), Jira automation, sprint planning, repository analysis, and unit-test generation — built around a mandatory, immutable audit and approval framework.

**Core invariant:** no write operation (Jira, repository, file system, configuration) ever executes without a pre-recorded audit entry **and** explicit user approval through the Approvals Center. If an audit record cannot be created, the operation is blocked.

## Technology

| Concern | Choice |
|---|---|
| Platform | Windows desktop, .NET 8 |
| UI | WPF + MVVM (CommunityToolkit.Mvvm), Clean Architecture |
| Hosting / DI | `Microsoft.Extensions.Hosting` generic host |
| Audit store | Hash-chained JSONL (source of truth) + SQLite index (EF Core) |
| Secrets | Windows DPAPI, per-user scope — never plaintext at rest |
| Jira | REST v3 typed client via `HttpClientFactory` + Polly |
| AI providers | OpenAI, Azure OpenAI, Anthropic (Claude), Gemini, Ollama behind `IAiProvider` |
| Git | LibGit2Sharp + provider adapters (GitHub / GitLab / Bitbucket) |
| Documents | Open XML SDK (DOCX), QuestPDF (PDF), Markdig (MD/HTML), native draw.io XML |
| Logging | Serilog with correlation IDs end-to-end |

## Repository layout

```
docs/        Complete design package (see map below)
src/         .NET solution source (Clean Architecture layers)
tests/       xUnit test projects
config/      Published JSON Schemas (configuration profiles)
```

## Design package map

The specification's 24 required output sections map to the documents below.

| Doc | Contents | Spec sections |
|---|---|---|
| [docs/01-requirements-validation.md](docs/01-requirements-validation.md) | Requirement validation, resolved clarifications, recorded assumptions | §1, §2 |
| [docs/02-solution-architecture.md](docs/02-solution-architecture.md) | High-level architecture, technology stack, WPF framework justification | §3, §4, §5 |
| [docs/03-folder-structure.md](docs/03-folder-structure.md) | Solution + working-directory layout | §6 |
| [docs/04-data-and-config.md](docs/04-data-and-config.md) | SQLite schema, configuration JSON Schema | §7, §8 |
| [docs/05-ui-ux.md](docs/05-ui-ux.md) | Screen inventory, wireframes, navigation flow, Approvals Center spec | §9 |
| [docs/06-module-designs.md](docs/06-module-designs.md) | Module-by-module design (all 14 modules) | §10 |
| [docs/07-ai-orchestration.md](docs/07-ai-orchestration.md) | Agents, orchestrator, prompt templates, reproducibility | §11 |
| [docs/08-integrations.md](docs/08-integrations.md) | Jira integration design, repository integration design | §12, §13 |
| [docs/09-document-engine.md](docs/09-document-engine.md) | Document generation engine, templating, versioning | §14 |
| [docs/10-audit-approval-framework.md](docs/10-audit-approval-framework.md) | Audit-first pipeline, hash chain, approval workflow, rollback, dashboard | §15 |
| [docs/11-security.md](docs/11-security.md) | Security architecture, threat model, encryption | §16 |
| [docs/12-operations.md](docs/12-operations.md) | Error handling & recovery, logging & monitoring, deployment & packaging | §17, §18, §19 |
| [docs/13-testing-strategy.md](docs/13-testing-strategy.md) | Unit / integration / UI / E2E testing strategy | §20 |
| [docs/14-performance-scalability.md](docs/14-performance-scalability.md) | Performance and scalability considerations | §21 |
| [docs/15-roadmap.md](docs/15-roadmap.md) | Future enhancements, phased development plan, risks & mitigations | §22, §23, §24 |

## Building

Requires the .NET 8 SDK.

```bash
dotnet build SprintForge.sln
dotnet test
```

The WPF project (`SprintForge.Wpf`) targets `net8.0-windows` and builds on Windows only; all other projects (Domain, Application, Infrastructure, Tests) target `net8.0` and build cross-platform.

## Development phases

| Phase | Modules | Milestone |
|---|---|---|
| 1 — Foundation | Configuration, Security, Audit core, Logging | Every action audited; secrets encrypted; profiles CRUD |
| 2 — Integrations core | Jira automation, AI orchestration | Approval-gated Jira round-trip; reproducible AI calls |
| 3 — First generation slice | SRS Generator, Document engine, Approvals Center | Brief → SRS → approval → Jira update, fully audited |
| 4 — Architecture & design docs | SAD, SDD | Diagrams + documents generated, linked in Jira |
| 5 — Planning & repo intelligence | Sprint Planning, Repository integration/analyzer | Bulk work-item creation via Approvals Center |
| 6 — Test generation & hardening | Unit test generator, rollback, packaging | Coverage-targeted tests; MSIX package |
| 7 — Future scope | CRS, RTM, release notes, more SDLC tools, plugin SDK | Extensions behind existing abstractions |

See [docs/15-roadmap.md](docs/15-roadmap.md) for milestones, exit criteria, and risks.
