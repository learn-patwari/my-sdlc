# SprintForge — UI Requirements

## 1. Application identity

**Name:** SprintForge  
**Type:** Windows desktop application (.NET 8 + WPF)  
**Purpose:** Enterprise SDLC automation tool — AI-assisted document generation (SRS, SAD, SDD), Jira sprint planning, repository analysis, and unit test generation with a mandatory audit and approval framework.  
**Primary users:** Software engineers, tech leads, architects, scrum masters in enterprise environments.  
**Core UX principle:** Nothing writes to Jira, repositories, or the file system without explicit user approval through the Approvals Center. The tool generates and previews; the user decides.

---

## 2. Design system

### 2.1 Color palette

| Token | Light mode | Dark mode | Usage |
|---|---|---|---|
| `--primary` | `#2563EB` (blue-600) | `#3B82F6` (blue-500) | Primary actions, active nav item, links |
| `--primary-hover` | `#1D4ED8` | `#2563EB` | Button hover |
| `--success` | `#16A34A` (green-600) | `#22C55E` | Completed status, approve |
| `--warning` | `#D97706` (amber-600) | `#F59E0B` | Pending, queued, validation warnings |
| `--danger` | `#DC2626` (red-600) | `#EF4444` | Failed, rejected, errors, integrity alerts |
| `--surface` | `#FFFFFF` | `#1E1E2E` | Main workspace background |
| `--surface-raised` | `#F8FAFC` | `#252535` | Cards, panels, input areas |
| `--surface-overlay` | `#F1F5F9` | `#2D2D3F` | Hover states, alternate rows |
| `--border` | `#E2E8F0` | `#3A3A50` | Dividers, input borders |
| `--text-primary` | `#0F172A` | `#F1F5F9` | Main body text |
| `--text-secondary` | `#64748B` | `#94A3B8` | Labels, hints, metadata |
| `--text-disabled` | `#CBD5E1` | `#4A5568` | Disabled controls |
| `--nav-bg` | `#1E293B` (slate-800) | `#111827` | Left navigation rail background |
| `--nav-text` | `#94A3B8` | `#9CA3AF` | Nav item text (inactive) |
| `--nav-active` | `#FFFFFF` | `#F9FAFB` | Nav item text (active) |
| `--nav-active-bg` | `#2563EB` | `#2563EB` | Nav item background (active) |
| `--audit-badge` | `#EF4444` | `#EF4444` | Approvals badge, integrity alert |
| `--diff-add` | `#DCFCE7` | `#14532D` | Added lines in diff viewer |
| `--diff-remove` | `#FEE2E2` | `#7F1D1D` | Removed lines in diff viewer |
| `--code-bg` | `#F8FAFC` | `#1A1A2E` | Code/prompt preview areas |

### 2.2 Typography

| Role | Font | Size | Weight |
|---|---|---|---|
| App title | Segoe UI | 16px | SemiBold 600 |
| Screen heading | Segoe UI | 22px | SemiBold 600 |
| Section heading | Segoe UI | 15px | SemiBold 600 |
| Body / labels | Segoe UI | 13px | Regular 400 |
| Secondary / metadata | Segoe UI | 12px | Regular 400 |
| Code / prompts / JSONL | Cascadia Code, Consolas | 12px | Regular 400 |
| Badge / chip text | Segoe UI | 11px | Medium 500 |
| Status bar | Segoe UI | 11px | Regular 400 |

### 2.3 Spacing

Base unit: `4px`. Use multiples: `4, 8, 12, 16, 20, 24, 32, 40, 48`.  
Nav rail width: `200px` (expanded), `48px` (collapsed).  
Top bar height: `48px`.  
Status bar height: `28px`.  
Workspace padding: `24px`.

### 2.4 Border radius

| Element | Radius |
|---|---|
| Buttons (primary/secondary) | `6px` |
| Input fields | `6px` |
| Cards / panels | `8px` |
| Badges / chips | `12px` (pill) |
| Modals / drawers | `10px` |
| Status indicators (dot) | `50%` |

### 2.5 Elevation / shadows

| Level | Usage | Shadow |
|---|---|---|
| 0 | Nav rail, status bar | None |
| 1 | Cards, list rows (hover) | `0 1px 3px rgba(0,0,0,0.08)` |
| 2 | Panels, dropdowns | `0 4px 12px rgba(0,0,0,0.12)` |
| 3 | Toasts | `0 8px 24px rgba(0,0,0,0.16)` |

### 2.6 Icons

Use **Fluent UI System Icons** (Microsoft's open icon set, consistent with Windows 11 design language). Sizes: `16px` (inline), `20px` (nav, toolbar), `24px` (section headers).

Key icons:
- Home: `home`
- SRS: `document_text`
- SAD: `diagram`
- Sprint: `calendar_agenda`
- SDD: `code`
- Repos: `branch`
- Tests: `beaker`
- Jira: `ticket_diagonal`
- Docs: `folder`
- Audit: `history`
- Config: `settings`
- Approvals: `checkmark_circle`
- Approve: `checkmark` (green)
- Reject: `dismiss` (red)
- Cancel: `arrow_undo`
- Pending: `clock`
- Completed: `checkmark_circle_filled` (green)
- Failed: `error_circle` (red)
- Queued: `arrow_right_circle`
- Integrity OK: `shield_checkmark` (green)
- Integrity Error: `shield_error` (red)
- AI: `sparkle`
- Jira link: `link`
- Download: `arrow_download`
- Refresh: `arrow_clockwise`
- Expand/Collapse: `chevron_right` / `chevron_down`

---

## 3. Shell layout

The shell is the persistent container. It never changes structure across screens.

```
┌──────────────────────────────────────────────────────────────────────────┐
│  [≡]  SprintForge                Profile: [Acme-Prod ▼]  [🛡 ✓]  [⚙]   │  48px top bar
├────────────┬─────────────────────────────────────────────────────────────┤
│            │                                                              │
│  🏠 Home   │                                                              │
│            │                                                              │
│  📄 SRS    │                                                              │
│  🔷 SAD    │                                                              │
│  📅 Sprint │              WORKSPACE  (module screen renders here)        │
│  💻 SDD    │                                                              │
│  🌿 Repos  │                                                              │
│  🧪 Tests  │                                                              │
│  🎫 Jira   │                                                              │
│  📁 Docs   │                                                              │
│  📋 Audit  │                                                              │
│  ⚙ Config  │                                                              │
│            │                                                              │
│  ─────     │                                                              │
│  [◀ Hide]  │                                                              │
├────────────┴─────────────────────────────────────────────────────────────┤
│  ⊙ Jira: Connected  |  ✦ AI: Ready  |  [⏱ Approvals: 3]  |  v1.0.0    │  28px status bar
└──────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Top bar
- **Left:** hamburger icon `[≡]` to collapse/expand the nav rail; app name "SprintForge" in SemiBold.
- **Center:** `Profile: [Acme-Prod ▼]` — dropdown selector. Options: switch profile, New Profile, Clone, Delete. Switching profile is an audited write queued to the Approvals Center.
- **Right (left to right):**
  - Audit health shield icon — green `🛡✓` when chain intact, red `🛡⚠` when integrity check failed (clicking opens Audit Dashboard → integrity report).
  - Settings gear `⚙` — quick link to Configuration screen.

### 3.2 Left navigation rail
- Dark background (`--nav-bg`).
- Each item: `20px` icon + label.
- Active item: `--nav-active-bg` background, `--nav-active` text, left border accent `3px solid --primary`.
- Hover: `--surface-overlay` tint (lighter than active).
- Collapsed state: icons only, `48px` wide; tooltips on hover.
- Bottom: `[◀ Hide]` / `[▶ Show]` toggle.
- Group separator (thin line) between core modules and Config.

### 3.3 Status bar
- Fixed at bottom, `28px` height, small text `11px`.
- Segments (left to right, separated by `|`):
  - `⊙ Jira: Connected` (green dot) / `⊙ Jira: Disconnected` (red dot) / `↻ Jira: Syncing` (spinning).
  - `✦ AI: Ready` / `✦ AI: Unavailable`.
  - **Approvals badge:** `[⏱ Approvals: 3]` — amber background pill; clicking navigates to Approvals Center. Updates in real time. Shows `0` when queue is empty (neutral color).
  - App version `v1.0.0` right-aligned.

### 3.4 Toast notifications
- Non-blocking; slide in from bottom-right.
- Auto-dismiss after `5 seconds`; hover pauses timer.
- Types: Info (blue), Success (green), Warning (amber), Error (red).
- Example: "✓ SRS queued for approval" (Info); "✗ Jira connection failed" (Error).
- Max 3 visible at once; older ones push up.

---

## 4. Screens

---

### Screen 1: Home

**Purpose:** Dashboard overview; orientation point after app launch.

**Layout:** `2-column grid` with stat cards on top, recent activity below.

**Components:**

**Stats row (4 cards, equal width):**
| Card | Content |
|---|---|
| Active Profile | Profile name + last modified date |
| Workspace | Working directory path + size used |
| Jira Projects | Count of configured projects |
| Repos | Count of configured repositories |

**Recent Audit Events (last 10):**
- Table with columns: `Time`, `Module`, `Action`, `Status` (colored chip), `Jira Issue`.
- Each row is clickable → opens Audit Dashboard filtered to that event.
- Status chips: `Completed` (green), `Failed` (red), `Pending` (amber), `Queued` (blue).

**Quick Actions row (icon buttons):**
- `+ New SRS`, `+ New Sprint Plan`, `Scan Repos`, `Open Audit`, `Configure`.

**Pending Approvals banner (conditional):**
- Shown only when there are items in the queue.
- Amber background, full-width: `"⏱ 3 items waiting for approval"` + `[Open Approvals Center]` button.

---

### Screen 2: SRS Generator

**Purpose:** Generate a Software Requirements Specification from a brief description.

**Layout:** Three-zone vertical split.

```
┌─ ZONE 1: INPUT (collapsible) ──────────────────────────────────────────┐
│  SRS ID:          [PROJ-1234_________________________]                 │
│  Brief Desc:      [__________________________________________]          │
│                   [__________________________________________]          │
│  Template:        [Default SRS ▼]  [Preview Template]                  │
│  Impacted Svcs:   [payments, auth, gateway] [+ Add]                   │
│  Out of Scope:    [__________________________________________]          │
│  Limitations:     [__________________________________________]          │
│                                                          [Generate ✦]  │
├─ ZONE 2: PREVIEW ──────────────────────────────────────────────────────┤
│  Versions: [v001 ▼]  [Compare v001 vs v002]  [Restore to v001]        │
│  ─────────────────────────────────────────────────────────────────────  │
│  # SRS: PROJ-1234 — Payment Processing System                         │
│                                                                        │
│  ## 1. Functional Requirements                                         │
│  - FR-001: The system shall...                                         │
│  ...                                                                   │
│                                                                        │
│  [⚠ NEEDS-CLARIFICATION: Specify authentication method]               │
│                                                                        │
├─ ZONE 3: ACTIONS ──────────────────────────────────────────────────────┤
│  [↺ Regenerate]  [↓ DOCX]  [↓ PDF]  [↓ MD]                           │
│                                    [Submit for Approval →]            │
└────────────────────────────────────────────────────────────────────────┘
```

**Component details:**

- **SRS ID field:** text input with prefix label; auto-suggests from configured Jira projects.
- **Brief Description:** multiline text area, `4 rows` minimum, expandable.
- **Template dropdown:** lists files from configured template directory; "Preview Template" opens a read-only modal.
- **Impacted Services:** tag input (type + Enter to add); values from repo analysis autocomplete.
- **Generate button:** primary blue; shows spinner + "Generating…" while AI runs; cancellable via `✕`.
- **Version selector:** dropdown; shows version number + timestamp + note. Latest is default.
- **Preview area:** scrollable markdown renderer; `NEEDS-CLARIFICATION` markers shown as amber inline alert boxes with `[Resolve]` link — blocks "Submit for Approval" until all resolved.
- **Compare button:** opens a side-by-side diff modal.
- **Submit for Approval:** primary green; disabled when `NEEDS-CLARIFICATION` markers exist; clicking queues to Approvals Center and shows a success toast.

---

### Screen 3: SAD Generator (Software Architecture Document)

**Purpose:** Generate architecture diagrams and the SAD document.

**Layout:** Three-zone (same pattern as SRS).

**Zone 1 — Input:**
- Jira ticket key (text input with lookup icon).
- Architecture style (dropdown: Microservices, Monolith, Serverless, Event-Driven, Hybrid).
- Technology stack (tag input; pre-filled from config, editable).
- Services to include (multi-select checkbox list from repo analyzer results).
- Diagram type checkboxes: `☑ Architecture`, `☑ Deployment`, `☑ Component`, `☐ Sequence`.

**Zone 2 — Preview (tabbed):**
- **Diagram tab:** renders Draw.io XML as an interactive diagram preview (or "XML preview" if rendering unavailable, with info banner "PNG export requires draw.io CLI").
- **SAD Document tab:** markdown document preview.

**Zone 3 — Actions:**
- `[↓ Draw.io XML]` `[↓ PNG Preview]` `[↓ SAD DOCX]` `[Submit for Approval →]`

---

### Screen 4: Sprint Planning

**Purpose:** Import a sprint plan, generate work items, validate estimates, push to Jira.

**Layout:** Three-zone with a validation step between zones 1 and 2.

**Zone 1 — Import:**
```
Import source:
  ○ Confluence Page  [Page URL: ___________________] [Fetch]
  ● Markdown         [paste or drag .md file]
  ○ Excel File       [Browse…]  filename.xlsx
  ○ Word File        [Browse…]
  ○ Plain Text       [paste text area]

                                          [Parse & Generate ✦]
```

**Zone 2 — Validation banner (conditional, amber):**
```
⚠ 3 subtasks exceed the configured dev-day budget (7 days / 56 hours).
  [Show only oversized items]  [Dismiss and continue anyway]
```

**Zone 2 — Work item tree:**
```
▼ EPIC: Payment Processing Feature
  ▼ STORY: User Authentication [8 SP] [Est: 5d]
      ☑ DEV: Implement login API          [2d]  ✓
      ☑ DEV: Implement token refresh       [1d]  ✓
      ⚠ TST: Write unit tests             [10d] ✗ EXCEEDS BUDGET
      ☑ DOC: Update API docs              [0.5d] ✓
  ▼ STORY: Payment Gateway Integration [13 SP] [Est: 7d]
      ...
```

- Tree with expand/collapse.
- Each item has: type badge (`STORY`, `TASK`, `DEV`, `TST`, `DOC`, `REVIEW`, `DEPLOY`), summary, estimate, status icon.
- Click a row to open detail side-panel (right side): full description, acceptance criteria, labels, components, story points, epic link.
- Inline edit of estimate directly in the tree row.

**Zone 3 — Actions:**
- `[Export as Excel]` `[Push to Jira →]` (queues entire plan as a batch to Approvals Center).

---

### Screen 5: SDD Generator (Software Detailed Design)

**Purpose:** Search for existing SDDs first, then generate new if needed.

**Layout:** Three-zone; with a mandatory search step before generation.

**Zone 1 — Search & Input:**
```
Service / Class name:  [PaymentService________________] [🔍 Search Jira]

─── Search Results ────────────────────────────────────────
  ✓ Found 1 existing SDD:
  │ PROJ-890  PaymentService SDD  (Updated: 2024-11-10)  [View] [Reuse]
  └──────────────────────────────────────────────────────

  [Proceed to generate new SDD anyway]

Repository:    [payments-api ▼]
Format:        [DOCX ▼]
```

- Search Jira button triggers live search across all configured projects.
- Search results list with issue key, title, last updated; `[View]` opens in Jira, `[Reuse]` loads existing SDD into the preview zone.
- "Proceed to generate new SDD" link appears after search completes.
- If no existing SDD found: automatically shows "No existing SDD found. Ready to generate."

**Zone 2 — Preview (tabbed):**
- **SDD Document tab:** markdown document with all sections (purpose, methods, dependencies, error handling, edge cases, sequence diagrams in mermaid).
- **Coverage Matrix tab:** table showing methods (rows) × test case types (columns) with coverage prediction.

**Zone 3 — Actions:**
- `[↓ DOCX]` `[↓ PDF]` `[Submit for Approval →]` `[Attach to Jira]`

---

### Screen 6: Repository Analysis

**Purpose:** Scan repositories and identify services, dependencies, and impacted files.

**Layout:** Two-pane — left selector + right results.

**Left pane (280px):**
```
Repositories
─────────────
☑ payments-api    (Java)
☑ frontend-app    (TypeScript)
☐ infra-config    (Python)

Branch: [main ▼]

[🔍 Scan Selected]
```

**Right pane — Results (after scan):**

Tabs:
1. **Overview** — summary cards: `12 Services`, `4 Controllers`, `8 DTOs`, `3 External Calls`.
2. **Services** — flat list or tree of all detected CodeUnits, each with kind badge (`SERVICE`, `CONTROLLER`, `REPOSITORY`, `DTO`, `UTIL`, `MODEL`, `CONFIG`) + file path.
3. **Dependencies** — mermaid diagram of service-to-service calls rendered inline.
4. **External Calls** — table of detected external HTTP/DB calls: caller, endpoint, type (REST, DB, Kafka, Redis).
5. **Impact Analysis** — text input "Describe change:" → `[Recommend impacted files]` → ranked list of affected services with explanation.

**Actions:** `[Export Impact Report]` `[Generate SDD for selected service]` (shortcut to SDD Generator pre-filled).

---

### Screen 7: Unit Test Generator

**Purpose:** Generate unit tests for a service/class targeting a configured coverage percentage.

**Layout:** Three-zone.

**Zone 1 — Input:**
```
Repository:       [payments-api ▼]
Service / Class:  [PaymentService_________] (autocomplete from repo analysis)
Test Framework:   [JUnit 5 + Mockito ▼]
Coverage Target:  [──────────●──────] 90%

Test types:
  ☑ Positive (happy path)
  ☑ Negative (invalid input)
  ☑ Boundary (edge values)
  ☑ Exception handling
  ☑ Mock dependencies

                                          [Generate Tests ✦]
```

**Zone 2 — Preview (tabbed):**
- **Test Code tab:** syntax-highlighted code in the selected framework. Scrollable.
- **Coverage Matrix tab:** table showing methods (rows) × test case types (columns); predicted coverage %.
- **Summary tab:** `Predicted coverage: 91%`, `Test count: 24`, `Mocked dependencies: 3`.

**Zone 3 — Actions:**
- `[↓ Save to repo]` (queues to Approvals Center) `[↓ Download]` `[Submit for Approval →]`

---

### Screen 8: Jira Integration

**Purpose:** Search Jira, view issues, perform bulk updates — all writes queued to Approvals Center.

**Layout:** Two-pane — query builder top, results list + detail bottom.

**Query bar (top):**
```
[project IN (PROJ, PLAT) AND status != Done AND issuetype = Story  ▼ JQL]  [Run ▶]
```
Toggle between visual chip-based builder and raw JQL text editor.

**Results list (left, virtualized):**
```
┌───────────────────────────────────────┐
│ PROJ-1234  Payment Auth  │ In Progress│
│ PROJ-1235  Gateway Setup │ To Do      │
│ ...                                   │
└───────────────────────────────────────┘
[Select all]  [Clear]  Bulk actions: [Transition ▼] [Apply]
```

**Detail pane (right, shown on row click):**
- Issue summary, description, status, assignee, story points, sprint, labels, components.
- **SprintForge history** section: list of all updates made by this tool (with audit ID links).
- Action buttons: `[Transition Status]` `[Update Story Points]` `[Attach SDD]` `[Link to Epic]` — all queue to Approvals Center.

---

### Screen 9: Documents

**Purpose:** Browse, compare, export, and manage all generated document versions.

**Layout:** Left tree + right version list + bottom preview strip.

**Left tree:**
```
▼ SRS
  ▼ PROJ-1234 (v3)
  ▼ PROJ-1100 (v1)
▼ SAD
  ▼ PROJ-1234 (v2)
▼ SDD
  ▼ PaymentService (v4)
  ▼ AuthService (v1)
▼ Tests
  ▼ PaymentService (v2)
```

**Right — Version list (for selected document):**
| Version | Date | Format | Hash | Audit ID | Actions |
|---|---|---|---|---|---|
| v003 (latest) | 2024-12-16 | DOCX, PDF, MD | abc123 | 01ARZ… | Download, Compare |
| v002 | 2024-12-15 | DOCX | def456 | 01ARZ… | Download, Compare, Restore |
| v001 | 2024-12-14 | DOCX | ghi789 | 01ARZ… | Download, Compare, Restore |

**Bottom preview strip (on version click):** first page preview of the document (thumbnail).

**Actions:**
- `[Compare v2 vs v3]` → opens Compare modal (side-by-side diff).
- `[Restore to v2]` → queues "create new version from v2" to Approvals Center.
- `[↓ Export as PDF]` / `[↓ Export as DOCX]` / `[↓ Export as MD]`.
- `[🔗 Attach to Jira issue]` → queues attachment to Approvals Center.

---

### Screen 10: Audit Dashboard

**Purpose:** Complete, searchable, filterable history of every action the application has taken.

**Layout:** Tabs across top, filter bar, master list (left), detail pane (right).

**Filter bar:**
```
[Module: All ▼]  [Action: All ▼]  [Status: All ▼]  [From: 2024-12-01] [To: 2024-12-16]
[CorrelationId: _______________]  [Jira Issue: _______]  [🔍 Search]  [Clear]
```

**Tabs:**

**1. Timeline** — newest-first flat list:
```
2024-12-15 14:30:05  SRS    SrsGeneration     ● Completed    PROJ-1234  [▶ Details]
2024-12-15 14:30:01  AI     Prompt            ● Completed    —          [▶ Details]
2024-12-15 14:29:50  Jira   Search            ● Completed    PROJ       [▶ Details]
2024-12-15 14:28:00  System AppStart          ● Completed    —          [▶ Details]
```

**2. Tree** — parent/child event hierarchy (correlation ID grouping):
```
▼ [corr_abc123] SRS Generation  14:28–14:30  ● Completed
  ├─ AI Prompt (SrsAgent)                     ● Completed  tokens: 1,240
  ├─ Approval: Update Jira PROJ-1234          ✓ Approved
  │   └─ Jira: UpdateDescription              ● Completed
  └─ Document: Save SRS-PROJ-1234.docx        ● Completed
```

**3. Sessions** — one row per app session (launch to close); expand to see all events.

**4. Jira** — all Jira-related events; columns: action, issue key, project, status, time.

**5. AI Prompts** — all AI calls; columns: agent, model, tokens, cost, time, status.  
Click row to see: full rendered prompt, AI response, model config.  
`[▶ Replay]` button re-runs the identical request.

**6. Files** — all generated/modified files; columns: document ID, version, format, hash, size, date, audit ID.

**7. Approvals** — full approval history (not the live queue — that's the Approvals Center screen).

**8. Rollbacks** — all rollback operations.

**Detail pane (right, opens on any row click):**
```
─── Audit Event ─────────────────────────────────────────
Audit ID:    01ARZ3NDEKTSV4RRFFQ69G5FAV
Module:      SRS
Action:      SrsGeneration
Status:      ● Completed
User:        akshay.patwari
Machine:     ACME-DEV-01
Time (UTC):  2024-12-15 09:00:05
Time (IST):  2024-12-15 14:30:05
Duration:    12,450 ms  |  Retries: 0

Inputs:
  Brief:     "Payment processing system..."
  SRS ID:    PROJ-1234
  Template:  Default SRS

Output Files:
  📄 SRS-PROJ-1234.docx   SHA: abc123...  [↓ Download]
  📄 SRS-PROJ-1234.pdf    SHA: def456...  [↓ Download]

Linked Events:
  ↓ AI Prompt  01ARZ...FAU  [View]
  ↓ Approval   01ARZ...FAX  [View]
  ↓ Jira Update 01ARZ...FAW [View]

[🔁 Rollback]  [📋 Copy Audit ID]  [↗ View in Jira]
─────────────────────────────────────────────────────────
```

---

### Screen 11: Approvals Center

**Purpose:** The only place writes happen. All write operations queue here; nothing executes until the user decides.

**Layout:** Header stats bar + scrollable approval item list.

**Header stats bar:**
```
Queue: 3 pending   |   Today: 12 approved, 2 rejected   |   [Approve All]  [Cancel All]
```

**Approval item card (one per queued operation):**

```
┌─────────────────────────────────────────────────────────────────────────┐
│  ⏱ PENDING   [SRS]  Jira: Update description — PROJ-1234               │
│  Queued: 2024-12-15 14:30:00   Audit ID: 01ARZ3NDEKTSV4RRFFQ69G5FAV   │
├─────────────────────────────────────────────────────────────────────────┤
│  CURRENT VALUE                    │  PROPOSED VALUE                     │
│  (empty)                          │  # SRS: PROJ-1234                   │
│                                   │  This SRS describes the payment...  │
│                                   │  ## Functional Requirements         │
│                                   │  - FR-001: The system shall...      │
├─────────────────────────────────────────────────────────────────────────┤
│  ✦ AI Explanation:                                                      │
│  Generated from brief description using Default SRS template.          │
│  Model: claude-sonnet-5  |  Tokens: 1,240  |  Temp: 0.2               │
├─────────────────────────────────────────────────────────────────────────┤
│  Affected:                                                              │
│  📋 Jira field: description (PROJ-1234)                                │
│  📄 File: Documents/SRS/PROJ-1234/v001/SRS-PROJ-1234.docx             │
├─────────────────────────────────────────────────────────────────────────┤
│  [✓ Approve]  [✎ Modify]  [✗ Reject]  [✕ Cancel]                      │
└─────────────────────────────────────────────────────────────────────────┘
```

**Batch item card (Sprint Planning — grouped):**

```
┌─────────────────────────────────────────────────────────────────────────┐
│  ⏱ BATCH   [Sprint]  Jira: Create 32 work items  — Sprint 14           │
│  Batch ID: batch_20241215_001   Queued: 14:25:00                       │
├─────────────────────────────────────────────────────────────────────────┤
│  ▼ PROJ-2345  Story: User Auth Integration    [8 SP]  [5d]             │
│    ▼ PROJ-2346  DEV: Implement login          [2d]                     │
│    ▼ PROJ-2347  DEV: Token refresh            [1d]                     │
│    ▼ PROJ-2348  TST: Unit tests               [1d]                     │
│  ▼ PROJ-2349  Story: Payment Gateway          [13 SP] [7d]             │
│    ... (28 more)                                      [Show all ▼]     │
├─────────────────────────────────────────────────────────────────────────┤
│  [✓ Approve All in Batch]  [✎ Review individually]  [✗ Reject Batch]  │
└─────────────────────────────────────────────────────────────────────────┘
```

**Post-decision states (shown inline, replacing action buttons):**

- `● Executing…` with spinner
- `✓ Completed` in green with timestamp
- `✗ Failed` in red with error message + `[Retry]` + `[View Error Details]`
- `✗ Rejected` in red with timestamp
- `✕ Cancelled` in grey with timestamp

**Modify flow:**
- Clicking `[✎ Modify]` expands an in-place edit area below the diff.
- User edits the proposed value inline.
- Saving the modification creates a new audit record (original is marked Cancelled; modified version is a new Pending item).

---

### Screen 12: Configuration

**Purpose:** Manage all application settings in named profiles.

**Layout:** Profile selector at top + 9-tab editor below.

**Profile bar:**
```
Active profile:  [Acme-Prod ▼]   [+ New]  [Clone]  [Delete]  [Import]  [Export]
```

**Tabs:** `Jira` | `AI` | `Templates` | `Working Dir` | `Sprint` | `Repos` | `Preferences` | `Logging` | `Audit`

---

**Jira tab:**
```
Server URL:    [https://acme.atlassian.net___________________________]
API Dialect:   [Jira Cloud v3 ▼]
Username:      [user@acme.com_____]
Auth mode:     [API Token ▼]
Token:         [•••••••••••••••••]  [👁 Show]  [Clear]

Projects:      [PROJ]  [PLAT]  [+ Add project]  [Sync from Jira]
Components:    [payments]  [auth]  [+ Add]
Labels:        [ai-generated]  [+ Add]

Issue Types:   Story → [Story___]  Task → [Task___]  Subtask → [Sub-task___]
Custom Fields:
  Story Points: [customfield_10016_]  [🔍 Lookup field ID]
  Sprint:       [customfield_10020_]
  Epic Link:    [customfield_10014_]

                   [🔌 Test Connection]  ●  Connection OK (verified 2 hours ago)

[Save]  [Cancel]
```

**AI tab:**
```
Providers:
┌──────────────────────────────────────────────────────┐
│  ID: primary   Vendor: [Anthropic ▼]                 │
│  Endpoint: [https://api.anthropic.com___________]    │
│  API Key:  [•••••••••••••••]  [Clear]                │
│  Model:    [claude-sonnet-5_____________________]    │
│  Temperature: [───●──────────] 0.2                   │
│  Max Tokens:  [8192____]                             │
│  Context Window: [200000__]                          │
│  [🔌 Test Connection]  ● OK              [Remove]    │
└──────────────────────────────────────────────────────┘
[+ Add Provider]

Default Provider: [primary ▼]
Fallback Provider: [fallback ▼]
Cost Alert Threshold: [$50.00__] per session
```

**Sprint tab:**
```
Duration:           [10___] days
Development Days:   [7____] days
Buffer Days:        [2____] days
Working Hours/Day:  [8____] hours
Workweek:           ☑ Mon  ☑ Tue  ☑ Wed  ☑ Thu  ☑ Fri  ☐ Sat  ☐ Sun

Holidays:
  2026-08-15  Independence Day   [Remove]
  2026-10-02  Gandhi Jayanti     [Remove]
  [+ Add holiday]

Capacity preview: 56 working hours available in a 10-day sprint (after 2 buffer days, 0 holidays)
```

**Repos tab:**
```
┌──────────────────────────────────────────────────────┐
│  Name:     payments-api                              │
│  Kind:     [Java ▼]                                  │
│  Provider: [GitHub ▼]                                │
│  URL:      [https://github.com/acme/payments_]       │
│  Local:    [D:/SprintForge/Acme/Workspaces/pay…]  [Browse] │
│  Branch:   [main___]                                 │
│  Token:    [•••••••••] [Clear]                       │
│  [🔌 Test]  ● OK                       [Remove]      │
└──────────────────────────────────────────────────────┘
[+ Add Repository]
```

---

## 5. Shared components

### 5.1 Status chip
Colored pill badge. Variants:
| Status | Color | Icon |
|---|---|---|
| Completed | Green | ✓ |
| Failed | Red | ✗ |
| Pending | Amber | ⏱ |
| Queued | Blue | → |
| Executing | Blue (pulsing) | ↻ |
| Rejected | Red | ✗ |
| Cancelled | Grey | ✕ |
| Approved | Green | ✓ |

### 5.2 Diff viewer
Side-by-side panels with line numbers. `--diff-add` background for additions, `--diff-remove` for deletions. Section-level collapse (unchanged sections collapsed by default). Toggle between side-by-side and unified view.

### 5.3 Version selector
Dropdown showing: `v003 (latest) — 2024-12-16`, `v002 — 2024-12-15`, `v001 — 2024-12-14`. Clicking a non-latest version shows an amber banner "You are viewing a previous version. [Restore to this version]".

### 5.4 AI explanation panel
Collapsible, grey-bordered panel below preview content. Shows: model used, temperature, tokens, brief explanation of AI reasoning. Default collapsed; user expands on demand.

### 5.5 Audit ID link
Small `#01ARZ…` chip in metadata rows. Clicking navigates to the Audit Dashboard filtered to that event.

### 5.6 Connection status indicator
Small dot + label. `● Connected` (green), `● Disconnected` (red), `↻ Connecting` (amber animated), `⊘ Not configured` (grey).

### 5.7 NEEDS-CLARIFICATION marker
Inline amber box inside document previews: `⚠ NEEDS-CLARIFICATION: [message]` + `[Resolve]` link that scrolls to the relevant input field or opens an inline text override. "Submit for Approval" is disabled while any unresolved markers exist.

### 5.8 Progress bar (long operations)
Full-width, below the action bar. Shows: label ("Scanning repository: 450 / 1,240 files…"), percentage, elapsed time, `[✕ Cancel]` button.

### 5.9 Empty state
Centered illustration + heading + subtext + primary action button. Example: "No approvals pending — You're all caught up" with a green checkmark illustration.

---

## 6. Interaction patterns

### 6.1 Generate → preview → approve
1. User fills input fields.
2. Clicks Generate / AI button.
3. Progress spinner shows; generation is cancellable.
4. Preview populates with versioned content.
5. User reviews; resolves any NEEDS-CLARIFICATION markers.
6. User clicks "Submit for Approval."
7. Success toast: "Queued for approval. Visit Approvals Center."
8. Status bar approvals badge increments.

### 6.2 Approvals Center workflow
1. User clicks approvals badge or navigates to Approvals Center.
2. Reviews each item: sees current vs proposed diff, AI explanation, affected artifacts, audit ID.
3. Clicks Approve / Reject / Cancel per item (or Approve All).
4. Approved items show Executing spinner, then Completed/Failed.
5. Failed items show error + Retry option.

### 6.3 Rollback
1. User finds a COMPLETED event in the Audit Dashboard.
2. Clicks `[Rollback]` in the event detail pane.
3. A new approval item is queued: "Rollback: [original action]" with the previous state as the proposed value.
4. User approves the rollback in Approvals Center.
5. Rollback executes; a new COMPLETED audit event records the outcome.

### 6.4 Search → select → act (Jira, SDD)
1. User enters search query and clicks Search/Run.
2. Results populate in a virtualized list.
3. User clicks a row to see detail pane.
4. User selects one or more rows (bulk select with Ctrl+Click / Shift+Click).
5. User chooses a bulk action from a dropdown.
6. Items queue to Approvals Center.

---

## 7. Accessibility

- WCAG 2.1 AA compliance.
- Full keyboard navigation: Tab order matches visual order; Enter activates; Escape closes panels.
- Screen reader labels on all interactive elements (AutomationId + Name in WPF).
- Focus rings visible on all focusable elements (never hidden with `outline: none`).
- Color is never the sole indicator of state (always paired with icon or text).
- Font size configurable (Preferences tab: Small / Medium / Large / Extra Large).
- High contrast mode support (system `SystemParameters.HighContrast` detection).

---

## 8. Responsive / window behavior

- **Minimum window size:** `1024 × 768`.
- **Default window size:** `1280 × 800`.
- Workspace area uses available width; panels are resizable via drag handles.
- Nav rail collapses to icon-only at window widths below `1100px` (automatic; user can override).
- Preview zones are scrollable independently of the shell.
- Audit list and Jira results list use WPF virtualization — no performance degradation at 10,000+ items.

---

## 9. Animation & transitions

- Nav rail collapse/expand: `150ms ease-out` width transition.
- Toast slide-in: `200ms ease-out` from bottom-right.
- Status chip change (e.g., Executing → Completed): brief `300ms` background flash.
- Progress bar: smooth fill, no frame drops.
- All other transitions: prefer instant or `< 150ms`; this is a productivity tool, not a marketing page.
