# SprintForge Beta — UI Requirements

## 1. Application identity

**Name:** SprintForge Beta  
**Type:** Windows desktop application (.NET 8 + WPF)  
**Purpose:** Enterprise SDLC automation tool — AI-assisted document generation (SRS, SAD, SDD), Jira sprint planning, repository analysis, and unit test generation with a mandatory audit and approval framework.  
**Tagline:** "AI-Powered SDLC Copilot — Accelerate delivery. Ensure quality."  
**Primary users:** Software engineers, tech leads, architects, scrum masters in enterprise environments.  
**Core UX principle:** Nothing writes to Jira, repositories, or the file system without explicit user approval through the Approvals Center. The tool generates and previews; the user decides.

---

## 2. Design system

### 2.1 Color palette — dark theme (primary)

Dark navy with purple brand accent. Dark theme is the default and primary mode.

| Token | Value | Usage |
|---|---|---|
| `--bg-base` | `#080C18` | Outermost window background |
| `--bg-surface` | `#0D1117` | Page / content area background |
| `--bg-card` | `#111827` | Card and panel backgrounds |
| `--bg-elevated` | `#1A2235` | Hover states, elevated cards, tooltips |
| `--bg-sidebar` | `#0B1120` | Left navigation rail background |
| `--bg-input` | `#0F1629` | Input field backgrounds |
| `--border` | `#1E2D45` | Card borders, dividers, input borders |
| `--border-subtle` | `#152033` | Subtle separators inside panels |
| `--accent-purple` | `#7C3AED` | Primary CTA, active nav item, brand |
| `--accent-purple-hover` | `#6D28D9` | Button hover, active hover |
| `--accent-blue` | `#3B82F6` | Links, info states, secondary actions |
| `--text-primary` | `#F1F5F9` | Headings, values, primary text |
| `--text-secondary` | `#94A3B8` | Labels, descriptions, nav inactive |
| `--text-muted` | `#64748B` | Timestamps, placeholders, captions |
| `--text-disabled` | `#374151` | Disabled controls |
| `--success` | `#22C55E` | Connected, Done, pass, Approve |
| `--success-bg` | `#052E16` | Success chip / row background |
| `--warning` | `#F59E0B` | In-progress, pending, validation |
| `--warning-bg` | `#2D1B00` | Warning chip / row background |
| `--error` | `#EF4444` | High priority, failed, rejected |
| `--error-bg` | `#2D0707` | Error chip / row background |
| `--diff-added-bg` | `#052E16` | Diff viewer: added row background |
| `--diff-added-text` | `#4ADE80` | Diff viewer: added row text / tag |
| `--diff-removed-bg` | `#2D0707` | Diff viewer: removed row background |
| `--diff-removed-text` | `#F87171` | Diff viewer: removed row text / tag |
| `--diff-modified-bg` | `#2D1F07` | Diff viewer: modified row background |
| `--diff-modified-text` | `#FCD34D` | Diff viewer: modified row text / tag |
| `--code-bg` | `#0A0F1C` | Code blocks, prompt preview areas |
| `--audit-badge` | `#EF4444` | Approvals badge, integrity alert |

### 2.2 Color palette — light theme (secondary)

Light theme uses the same semantic token names; swap to lighter values while keeping the purple brand accent.

| Token | Light value |
|---|---|
| `--bg-base` | `#F8FAFC` |
| `--bg-surface` | `#FFFFFF` |
| `--bg-card` | `#F1F5F9` |
| `--bg-elevated` | `#E2E8F0` |
| `--bg-sidebar` | `#1E293B` |
| `--bg-input` | `#FFFFFF` |
| `--border` | `#E2E8F0` |
| `--text-primary` | `#0F172A` |
| `--text-secondary` | `#64748B` |
| `--text-muted` | `#94A3B8` |
| `--accent-purple` | `#7C3AED` (same) |
| `--success` | `#16A34A` |
| `--warning` | `#D97706` |
| `--error` | `#DC2626` |

### 2.3 Brand identity

**Logo construction:**
- Icon: A rounded square (~28×28px) with a purple-to-violet gradient (`#7C3AED` → `#4F46E5`), containing a white "S" letterform in bold sans-serif.
- Wordmark: "sprintforge" in lowercase, clean sans-serif (Inter or Segoe UI), `--text-primary`, weight 600, tracked slightly wide.
- Beta badge: "beta" in small caps or uppercase, muted purple chip (`rgba(124,58,237,0.2)` bg, `#A78BFA` text), placed top-right of the wordmark.
- Tagline (login screen only): "AI-Powered SDLC Copilot" in `--text-secondary`, 12px, below the wordmark.

### 2.4 Typography

| Role | Font stack | Size | Weight |
|---|---|---|---|
| App title / wordmark | `Inter, 'Segoe UI', system-ui` | 18px | SemiBold 600 |
| Screen heading | Inter, Segoe UI | 22px | SemiBold 600 |
| Section heading | Inter, Segoe UI | 16px | SemiBold 600 |
| Card title / nav label | Inter, Segoe UI | 14px | Medium 500 |
| Body / labels | Inter, Segoe UI | 14px | Regular 400 |
| Secondary / metadata | Inter, Segoe UI | 12px | Regular 400 |
| Caption / timestamp | Inter, Segoe UI | 11px | Regular 400 |
| KPI number | Inter, Segoe UI | 28–32px | Bold 700 |
| Code / prompts / JSONL | `'Cascadia Code', Consolas, monospace` | 13px | Regular 400 |
| Badge / chip text | Inter, Segoe UI | 11px | Medium 500 |

### 2.5 Spacing

Base unit: `4px`. Use multiples: `4, 8, 12, 16, 20, 24, 32, 40, 48`.  
Nav rail width: `220px` (expanded), `56px` (collapsed to icons only).  
Top bar height: `56px`.  
Workspace padding: `24px`.  
Card padding: `16px`.  
Section gap: `16px`.

### 2.6 Border radius

| Element | Radius |
|---|---|
| Buttons (primary/secondary) | `8px` |
| Input fields | `8px` |
| Cards / panels | `10px` |
| Badges / chips | `999px` (pill) |
| Modals / drawers | `12px` |
| Status indicator dot | `50%` |
| Architecture diagram nodes | `8px` |
| Nav active state pill | `8px` |

### 2.7 Elevation / shadows

Dark theme shadows are more pronounced due to dark backgrounds.

| Level | Usage | Shadow |
|---|---|---|
| 0 | Nav rail, top bar | None |
| 1 | Cards (resting) | `0 1px 3px rgba(0,0,0,0.4)` |
| 2 | Elevated cards, dropdowns, panels | `0 4px 16px rgba(0,0,0,0.6)` |
| 3 | Modals, toasts | `0 8px 32px rgba(0,0,0,0.8)` |

### 2.8 Icons

Use **Fluent UI System Icons** (Microsoft open icon set, consistent with Windows 11). Sizes: `16px` inline, `20px` nav/toolbar, `24px` section headers.

Nav icons (filled variant when active, regular when inactive):
- Dashboard: `grid_dots`
- SRS: `document_text`
- SAD: `diagram`
- Sprint Planner: `calendar_agenda`
- SDD: `code_block`
- Repository Analyzer: `branch`
- Unit Tests: `beaker`
- Jira: `ticket_diagonal`
- Documents: `folder`
- Audit Center: `history`
- Settings: `settings`

Action icons: `sparkle` AI, `checkmark` approve, `dismiss` reject, `arrow_undo` rollback, `arrow_download` export, `arrow_clockwise` refresh, `play` replay, `shield_checkmark` integrity OK, `shield_error` integrity fail, `person` user, `bell` notifications, `search` search.

---

## 3. Shell layout

The shell is the persistent container. It renders identically across all screens.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ TOP BAR (56px, --bg-base border-bottom --border)                            │
│  [sprintforge β]  [Retail Banking Platform ▼]  [● AI Active ▼]   [🔍]  [🔔][👤] │
├──────────────────┬──────────────────────────────────────────────────────────┤
│ LEFT NAV (220px) │                                                           │
│ --bg-sidebar     │   CONTENT AREA  (--bg-surface)                           │
│                  │                                                           │
│  ● Dashboard     │                                                           │
│    SRS           │                                                           │
│    SAD           │                                                           │
│    SDD           │                                                           │
│    Sprint…       │                                                           │
│    Repo…         │                                                           │
│    Unit Tests    │                                                           │
│    Jira          │                                                           │
│    Documents     │                                                           │
│    Audit Center  │                                                           │
│    Settings      │                                                           │
│                  │                                                           │
│  ─── (divider) ──│                                                           │
│  ◀  Collapse     │                                                           │
└──────────────────┴──────────────────────────────────────────────────────────┘
```

### 3.1 Top bar

Height: `56px`. Background: `--bg-base`. Bottom border: `1px solid --border`.

**Left section:**
- Logo: purple gradient "S" icon (24×24px) + "sprintforge" wordmark + small "β" beta badge.

**Center-left section:**
- **Platform context selector:** dropdown pill `[Retail Banking Platform ▼]` in `--bg-card`, `--text-primary`, `8px` radius. Switches the active project context across all modules.
- **AI Copilot status chip:** `[● AI Active]` (green dot when connected, amber when connecting, red when unavailable). Click to open AI provider status details.

**Right section (left to right):**
- **Search:** `[🔍 Search  Ctrl+F]` — compact input or icon that expands on focus. Global search across documents, Jira issues, audit events.
- **Audit integrity shield:** `🛡✓` green when chain intact; `🛡⚠` red when integrity check failed. Clicking opens Audit Center → integrity report tab.
- **Notifications bell:** `🔔` with red badge count. Clicking shows notification dropdown: pending approvals, completed AI operations, connection alerts.
- **User profile:** avatar circle + name `[Akshay Patwari ▼]`. Click → dropdown: profile settings, switch profile, sign out.

### 3.2 Left navigation rail

Background: `--bg-sidebar`.  
Width: `220px` expanded, `56px` collapsed.

**Item anatomy (expanded):** `20px` icon (Fluent) + `14px` label text, `16px` horizontal padding, `40px` item height.

**Active item:** Rounded pill background in `--accent-purple`, white icon + `--text-primary` label. No left border accent.

**Inactive item:** `--text-secondary` icon + label. Hover: `--bg-elevated` background.

**Collapsed state:** Icons only (`20px`), centered. Tooltips on hover show label.

**Bottom of nav rail:**
- Thin `--border-subtle` divider.
- `◀ Collapse` / `▶ Expand` toggle button.
- **SprintForge Copilot** entry: sparkle icon + "Copilot" label. Click opens the AI Copilot side panel.

### 3.3 Toast notifications

Non-blocking; slide in from bottom-right corner.  
Auto-dismiss after `5 seconds`; hover pauses timer.  
Types: Info (blue), Success (green), Warning (amber), Error (red).  
Max 3 visible at once.  
Example: "✓ SRS queued for approval" / "✗ Jira connection failed".

---

## 4. Screens

---

### Screen 1: Login & Configuration Setup

**Purpose:** Authenticate and configure environment on first launch or profile switch.

**Layout:** Two-column card — login on left, environment setup on right — centered on `--bg-base`.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                           (--bg-base full window)                            │
│   ┌─────────────────────────┐   ┌────────────────────────────────────────┐  │
│   │  [S] sprintforge β      │   │  Configure Your Environment            │  │
│   │  AI-Powered SDLC…       │   │  Set up integrations and preferences   │  │
│   │                         │   │                                        │  │
│   │  [Sign In] [SSO Login]  │   │  ┌──────────┐ ┌──────────┐ ┌───────┐  │  │
│   │                         │   │  │ Jira ✓   │ │ AI Prov  │ │ Git ✓ │  │  │
│   │  Email                  │   │  │Connected │ │OpenAI-4o │ │GitHub │  │  │
│   │  [___________________]  │   │  └──────────┘ └──────────┘ └───────┘  │  │
│   │                         │   │  ┌──────────┐ ┌──────────┐ ┌───────┐  │  │
│   │  Password               │   │  │ Work Dir │ │ Sprint   │ │ Tmpl  │  │  │
│   │  [•••••••••••]  [👁]    │   │  │128 GB Fr │ │SPR-34    │ │12 Tmp │  │  │
│   │                         │   │  └──────────┘ └──────────┘ └───────┘  │  │
│   │  ☐ Remember me  Forgot? │   │  ┌──────────┐ ┌──────────────────────┐ │  │
│   │                         │   │  │ Security │ │ Audit & Compliance   │ │  │
│   │  [    Sign In   →    ]  │   │  │SSO Enable│ │Audit Logging Enabled │ │  │
│   │                         │   │  │Role:Arch │ │Immutable & Encrypted │ │  │
│   │  or continue with       │   │  └──────────┘ └──────────────────────┘ │  │
│   │  [⊞][G][○][✦]          │   │                                        │  │
│   │                         │   │         [Save & Continue  →]           │  │
│   │  v0.1.0-beta            │   │                                        │  │
│   └─────────────────────────┘   └────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────────┘
```

**Component details:**

- **Login card:** `--bg-card`, `10px` radius, `32px` padding, `360px` wide, `--border` border.
- **Tabs:** "Sign In" / "SSO Login" — pill toggle, `--accent-purple` active state.
- **Email / Password fields:** `--bg-input`, `--border`, `8px` radius. Password field has eye-toggle `[👁]` to reveal.
- **Sign In button:** full-width, `--accent-purple` background, white text, `8px` radius.
- **Social icons:** equal-width buttons with `--border` border — Microsoft `⊞`, Google `G`, GitHub `○`, Anthropic `✦`.
- **Configuration panel:** `--bg-card`, `10px` radius. 8 configuration tile cards in 4-column × 2-row grid. Each tile: icon + title + status line or value.
- **Tile status:** `● Connected` (green dot) or value text. Connected tiles show a green pill chip.
- **Save & Continue button:** full-width in config panel, `--accent-purple`.

---

### Screen 2: Main Dashboard

**Purpose:** Overview of SDLC health, sprint progress, AI recommendations, and recent activity.

**Layout:** Full-width KPI row → two-column below (chart left, recommendations right) → recent activity row → SprintForge Copilot panel.

```
┌─ 6 KPI STAT CARDS (equal width, --bg-card) ────────────────────────────────┐
│  Requirements  │ Designs  │ Code Services │ Unit Tests  │ Jira Issues │ Sprint│
│  128           │ 24       │ 36            │ 1,248  82%  │ 56   🔴12   │  68%  │
│  This Sprint   │ +5 ▲     │ 3 Impacted⚠  │ Coverage○   │ High Prio   │ ○On T │
│  +12 ▲         │          │               │             │             │       │
└────────────────────────────────────────────────────────────────────────────┘

┌─ Sprint Burndown (left 55%) ───────┐  ┌─ AI Recommendations (right 45%) ──┐
│  ╲ Ideal (dashed)                 │  │  ✦ 12 requirements are incomplete │
│     ╲ Actual (solid)              │  │  ✦ 3 services have high complexity│
│       ╲___                        │  │  ✦ Test coverage below 65%        │
│  Jul14   Jul21   Aug1             │  │  ✦ 3 Jira issues are blocked      │
└────────────────────────────────────┘  └────────────────────────────────────┘

┌─ Recent Activity (full width) ─────────────────────────────────────────────┐
│  Just now    SRS generated for 'Customer Service'                 [View]   │
│  25 min ago  SAD published for 'Payment Service'                  [View]   │
│  56 Jira issues synced successfully                               [View]   │
│  65 min ago  Unit tests generated for 'Auth Service'              [View]   │
└────────────────────────────────────────────────────────────────────────────┘

┌─ SprintForge Copilot ─────────────────────────────────────────────────────┐
│  ✦ Good morning, Akshay! How can I help you today?        [Add Copilot +] │
└────────────────────────────────────────────────────────────────────────────┘
```

**KPI card anatomy:**
- Background: `--bg-card`, `10px` radius, `--border` border.
- Large number: `28–32px`, bold, `--text-primary`.
- Sub-label: `12px`, `--text-secondary`.
- Trend badge: small pill — green `+N ▲` for positive, red for negative, orange for warnings.
- Unit Tests card: includes a circular progress ring (`82%`) rendered in green with `--accent-purple` track.
- Sprint Progress card: large circular gauge (`68%`) in `--accent-purple`, "On Track" label in green.

**Sprint Burndown chart:**
- Background: `--bg-card`. Line chart: Ideal (dashed, `--text-muted`), Actual (solid, `--accent-purple`).
- X-axis: dates (Jul 14 → Aug 1). Y-axis: story points. Grid lines in `--border-subtle`.

**AI Recommendations:**
- Background: `--bg-card`. Numbered list with `✦` sparkle prefix and `[View]` link on each item. Title bar with "AI Recommendations" label.

**Recent Activity:**
- Background: `--bg-card`. Rows: relative timestamp + description + `[View]` link. Alternating `--bg-elevated` row hover.

**SprintForge Copilot panel:**
- Background: `--bg-card` with `--accent-purple` left border accent (4px). Greeting text + `[Add Copilot +]` button. Opens a full side-panel chat interface.

---

### Screen 3: SRS Generator Workspace

**Purpose:** Generate a Software Requirements Specification from an input brief.

**Layout:** Top breadcrumb + toolbar → three-panel content → right side approval panel.

```
Breadcrumb: SRS ›  Retail Banking Platform ›  Account Management ›  Input Requirements

Toolbar: [Template ▼] [SRS v0.1 Standard ▼]  [Auto Save ◉]  [Stored] [Found] [Compare] [History]
─────────────────────────────────────────────────────────────────────────────────────
┌──────────────────────┬──────────────────────┬──────────────────┬──────────────────┐
│ 1. Input             │ 2. AI Generated SRS  │ 3. Diff Viewer   │ Approval Panel   │
│ Requirements         │ (Preview)            │                  │                  │
│                      │                      │  Status          │ [Avatar]         │
│ B I U ≡ ─ 🔗 {}     │ 1. Introduction      │  Pending Review  │ Neha Verma       │
│                      │                      │  ──────────────  │ Reviewer         │
│ 1. Introduction      │ The system enables   │  Added ▌FR-01   │                  │
│                      │ customers to securely│  Added ▌FR-02   │ [Comment here…]  │
│ The system shall…    │ view account balances│  Modified▌FR-03 │                  │
│                      │ in real time…        │  Modified▌FR-04 │ [  Approve   ]   │
│ 2. Functional        │                      │  ──────────────  │ [Request Changes]│
│ Requirements         │ 2. Functional        │  Req  │Req│Desc  │ [   Reject   ]   │
│                      │ Requirements         │  FR-01│…  │…     │                  │
│ FR-01  View Balance  │                      │  FR-02│…  │…     │ Impacted (3):    │
│ FR-02  Transfer…     │ FR-001 The system…   │                  │ Account Service  │
│ FR-03  Beneficiary…  │                      │                  │ Payment Service  │
│ FR-04  Statement…    │                      │                  │ Notification Svc │
│                      │                      │                  │                  │
│ 1024 words [Analyze] │                      │                  │ [Send for Review]│
└──────────────────────┴──────────────────────┴──────────────────┴──────────────────┘
```

**Panel 1 — Input Requirements:**
- Rich text editor with formatting toolbar: Bold, Italic, Underline, lists, horizontal rule, link, code block.
- Supports structured sections (numbered headings).
- Word count displayed at bottom left.
- `[Analyze]` button sends content to AI for brief validation before generation.

**Panel 2 — AI Generated SRS (Preview):**
- Scrollable markdown-rendered document.
- `NEEDS-CLARIFICATION` markers appear as amber inline boxes — block Submit until resolved.
- Version selector at top if multiple versions exist: `[SRS v0.1 Standard ▼]`.

**Panel 3 — Diff Viewer:**
- Three row types with distinct backgrounds:
  - **Added:** `--diff-added-bg` background, left green border accent, green "Added" pill tag.
  - **Removed:** `--diff-removed-bg` background, left red border accent, red "Removed" pill tag.
  - **Modified:** `--diff-modified-bg` background, left amber border accent, amber "Modified" pill tag.
- Table format: Req ID | Requirement | Description.

**Right — Approval Panel (280px):**
- Reviewer avatar + name + role label.
- Status chip: "Pending Review" (amber) / "Approved" (green) / "Changes Requested" (orange).
- Comments textarea.
- Action buttons: `[Approve]` (green filled), `[Request Changes]` (outline), `[Reject]` (red text).
- Impacted Services list.
- `[Send for Review]` primary button at bottom.

---

### Screen 4: SAD Generator (Software Architecture Document)

**Purpose:** Generate architecture diagrams and the SAD document from a Jira ticket.

**Layout:** Breadcrumb + tabs → three-panel (component tree | diagram canvas | document preview).

```
Breadcrumb: SAD ›  Retail Banking Platform ›  Payment Service

Tabs: [Architecture View] [Draw.io] [Document Preview]    [toolbar icons] [◀][▶]
─────────────────────────────────────────────────────────────────────────────────
┌───────────────────┬────────────────────────────────────┬────────────────────┐
│ Components        │  Architecture Diagram Canvas        │ Generated Files    │
│                   │                                     │                    │
│ ▼ API Gateway     │  ┌─────────────┐                   │ payment-svc.md     │
│   Payment Service │  │  API Gateway│────────────────── │ payment-diagram.xml│
│   ▼ Controller    │  └──────┬──────┘    sync →         │ payment-api.yaml   │
│     Service       │         │                           │ draw-io-model.xml  │
│     Repository    │  ┌──────▼──────┐  ┌─────────────┐ │                    │
│   Payment Process │  │Payment Svc  │─▶│Payment Proc │ │ Document Preview   │
│   Adapter         │  └──────┬──────┘  └─────────────┘ │                    │
│   Service         │    async │         ┌─────────────┐ │ 1. Overview        │
│   Database        │  ┌──────▼──────┐  │Notification │ │ This document      │
│   Redis Cache     │  │  Database   │  │   Service   │ │ describes the      │
│   Kafka           │  │(PostgreSQL) │  └─────────────┘ │ Payment Service…   │
│                   │  └─────────────┘                   │                    │
│                   │  ┌────────────┐  ┌──────────────┐ │ 2. Architecture    │
│                   │  │Redis Cache │  │    Kafka     │ │ Diagram            │
│                   │  └────────────┘  └──────────────┘ │                    │
│                   │                                     │ 3. Components      │
│   [View all ▼]    │                                     │ Version 1.0 ▼      │
└───────────────────┴────────────────────────────────────┴────────────────────┘
[↓ Draw.io XML]  [↓ PNG Preview]  [↓ SAD DOCX]          [Submit for Approval →]
```

**Component tree (240px):**
- Hierarchical expand/collapse. `--bg-sidebar` background. Active service highlighted in `--accent-purple` tint.

**Diagram canvas (flex center):**
- Interactive mxGraph XML renderer (or static SVG fallback).
- Node styles:
  - Services: `--accent-blue` border, `--bg-card` fill, rounded, white label.
  - Messaging (Kafka): amber/orange tint.
  - Storage (DB, Redis): dark gray, lighter text.
- Arrow styles: sync = solid, async = dashed, cache = dotted, DB = solid dark.
- Toolbar: zoom in/out, fit to screen, export buttons.

**Generated Files + Document Preview (280px):**
- File list with file-type icons and names. Clickable to download.
- Preview panel: first 3 sections of the SAD document as prose. Version selector `v1.0 ▼` + last-updated timestamp.

---

### Screen 5: Sprint Planner

**Purpose:** Import a sprint plan, generate work items, validate estimates, push to Jira.

**Layout:** Sprint selector + capacity chips → two-panel (backlog tree | Gantt timeline).

```
Sprint: [SPR-34 (Jul 14 – Jul 27) ▼]

[Capacity: 160 pts]  [Committed: 142 pts]  [Completed: 0]  [Remaining: 142 pts]  [Utilization: 89%]
─────────────────────────────────────────────────────────────────────────────────────────
┌────────────────────────────────────────────┬──────────────────────────────────────────┐
│ Sprint Backlog                             │ Sprint Timeline                          │
│                                            │                                          │
│ Task / Subtask       Type  Assign  Est  St │ Jul14  Jul18  Jul22  Jul26  Aug1         │
│ ─────────────────── ───── ─────── ─── ── │ ─────────────────────────────────────    │
│ ▼ RB-101 Implement                        │ ████████████████████ Impl fund transfer  │
│   fund transfer(15p) Epic                 │                                          │
│   ▶ Develop API     Dev   R.Sharma 5p ✓  │   ████ RB-102 Develop API               │
│   ▶ Develop API     Dev   P.Singh  5p ↻  │         ██████ RB-102 Develop API       │
│   ▶ Unit Tests      Test  N.Verma  3p ↻  │               ████ Unit Tests            │
│   ▶ Code Review     Dev   A.Patel  2p ○  │                    ██ Code Review        │
│                                            │                                          │
│ ▶ RB-102 Add manage                       │   ████ RB-103 Add manage benficiaries   │
│   beneficiaries(8p) Story                 │                                          │
│                                            │                                          │
│ ▶ RB-103 Add API    Story                 │                                          │
└────────────────────────────────────────────┴──────────────────────────────────────────┘
[Export as Excel]                                                 [Push to Jira →]
```

**Capacity chips:** Pill badges in a horizontal row. `Capacity` (neutral), `Committed` (blue), `Completed` (green), `Remaining` (amber), `Utilization %` (color-coded: green <80%, amber 80-95%, red >95%).

**Sprint Backlog tree:**
- Hierarchical rows: Epic > Story > Task/Subtask with indent levels.
- Columns: Task/Subtask summary | Type badge (DEV/TST/DOC/REVIEW) | Assignee | Estimate | Status | Jira Issue.
- Status icons: ✓ Done (green), ↻ In Progress (blue), ○ To Do (muted). Status chips on hover expand to full label.
- Validation: subtasks exceeding budget shown with `⚠` icon and `--warning` text. Full amber validation banner if any violations.

**Sprint Timeline (Gantt):**
- Date column headers: day numbers. Horizontal bars colored by status. Task label inside or beside bar. Jira issue number linked.

**Zone 1 — Import (shown before backlog is generated):**
```
Import source:
  ○ Confluence Page   [Page URL: ___________] [Fetch]
  ● Markdown          [paste or drag .md file]
  ○ Excel File        [Browse…]
  ○ Word File         [Browse…]
  ○ Plain Text        [paste text area]
                                        [Parse & Generate ✦]
```

---

### Screen 6: Repository Analyzer

**Purpose:** Scan repositories and identify services, dependencies, code metrics, and impacted files.

**Layout:** Header + scan controls → three-panel (file explorer | dependency graph | code insights).

```
[retail-banking-platform]  Branch: [main ▼]   Scan: 30 Jul 2025 10:30 AM  [Pause] [Rescan]
─────────────────────────────────────────────────────────────────────────────────────────────
┌───────────────────────┬──────────────────────────────────┬─────────────────────────────────┐
│ Project Explorer      │ Dependency Graph                  │ Code Insights                   │
│                       │                                   │                                 │
│ 🔍 Search files…      │                                   │ Impacted Files (13)             │
│                       │    ┌─────────────┐                │                                 │
│ ▼ retail-banking-plat │    │  api-gateway│                │ PaymentService.java      ●      │
│   ▼ api-gateway       │    └──────┬──────┘                │ PaymentController.java   ●      │
│     api-service       │           │                       │ PaymentRepository.java   ●      │
│   ▼ payment-service   │  ┌────────▼───────┐ ┌──────────┐ │ AccountClient.java       ●      │
│     payment-svc       │  │payment-service │→│account   │ │ AccountService.java      ●      │
│   ▼ account-service   │  └────────┬───────┘ └──────────┘ │ + 6 more                        │
│     account-svc       │    async  │                       │                                 │
│   ▼ notification-svc  │  ┌────────▼───────┐               │ Complexity          12.4        │
│   ▼ document-service  │  │notification-svc│───▶ AWS SES  │ (large red number)              │
│                       │  └────────────────┘               │                                 │
│                       │                                   │ Duplication         8.2%         │
│                       │                                   │ Good (yellow)                   │
│                       │                                   │                                 │
│                       │                                   │ Test Coverage                   │
│                       │                                   │ Needs Improvement ⚠             │
│                       │                                   │                                 │
│                       │                                   │ Technical Debt      2.6 days    │
└───────────────────────┴──────────────────────────────────┴─────────────────────────────────┘
[Export Impact Report]                                       [Generate SDD for selected service]
```

**Project Explorer:** File tree with expand/collapse. File-type icons. Search input at top. Background `--bg-sidebar`.

**Dependency Graph:** Node-link diagram rendered inline. Nodes: rounded rectangles in `--bg-card` with `--accent-blue` border for services. External services shown in dashed outline. Arrow labels: sync (solid), async (dashed).

**Code Insights panel:**
- Impacted Files: list with red dot `●` indicator. File names in `--text-secondary`.
- Metric cards (stacked): metric name + large bold number + quality label (colored).
  - Complexity: `12.4` in `--error` (red).
  - Duplication: `8.2%` in `--warning` (amber), "Good" label.
  - Test Coverage: status label "Needs Improvement" in `--warning`.
  - Technical Debt: `2.6 days` in `--text-primary`.

---

### Screen 7: SDD & Unit Test Generator

**Purpose:** Generate software detailed designs and unit tests for selected services.

**Layout:** Left service selector → center test cases + coverage → right document preview.

```
Tabs: [SDD] [Unit Test Generator ●]
─────────────────────────────────────────────────────────────────────────────────
┌──────────────────┬────────────────────────────────────────┬───────────────────┐
│ Services         │ Generated Test Cases — Payment Service  │ SDD Preview       │
│                  │                                         │                   │
│ 🔍 Search…       │ Test Name            Prio  Status  Cov  │ 1. Overview       │
│                  │ ─────────────────── ────  ──────  ───  │                   │
│ Account Service  │ testHandle_Valid     High  Passed   ●   │ This document…    │
│ ● Payment Svc    │ testFundTransfer_In  High  Passed   ●   │                   │
│ Notification Svc │ testHandle_Limit…   Med   Passed   ●   │ 2. Design Details │
│ Audit Service    │ testPayment_Invalid  High  Failed   ●   │                   │
│ API Service      │ testPayment_Timeout  Med   Passed   ●   │ Payment flows     │
│                  │                                         │ through…          │
│                  │  Coverage Overview                      │                   │
│                  │  ┌──────────────────────┐               │                   │
│                  │  │                      │               │                   │
│                  │  │      88%             │               │                   │
│                  │  │      Good            │               │                   │
│                  │  │ ○ green ring         │               │ Generated Files:  │
│                  │  └──────────────────────┘               │ payment-svc-sdd   │
│                  │  Lines Covered:  1,248                  │ payment-tests.json│
│                  │  Lines Missed:   166                    │ test-report.html  │
│                  │  Total Lines:    1,414                  │                   │
│                  │                                         │ [View all]        │
└──────────────────┴────────────────────────────────────────┴───────────────────┘
[↓ Download Tests]  [↓ Download SDD]              [Submit for Approval →]
```

**Service selector (200px):** Scrollable list. Selected item highlighted in `--accent-purple` tint + left purple border. Search input at top.

**Test Cases table:** Columns: Test Name | Priority (High/Med/Low chip) | Status (Passed=green, Failed=red chip) | Coverage (colored dot). Sortable columns.

**Coverage donut:** SVG ring chart. Green ring on `--bg-card`. Percentage + quality label ("Good" / "Fair" / "Low") centered inside ring. Stats below.

**SDD Document Preview:** Prose sections. Generated files list with file-type icons. Scrollable.

---

### Screen 8: Jira Integration

**Purpose:** Search Jira, view issues, perform operations — all writes queued to Approvals Center.

**Layout:** Top query bar → results list (left) + detail pane (right).

```
[project IN (RBP, FNT) AND status != Done AND issuetype = Story  ▼ JQL]  [▶ Run]
[Visual Builder ⇄ JQL Text]
─────────────────────────────────────────────────────────────────────────────────
┌─────────────────────────────────┬───────────────────────────────────────────┐
│ Results (56)          [Select all] │ RB-101  Implement fund transfer API    │
│                                │                                           │
│ ● RB-101 Implement fund…  ↻    │ Status:    In Progress                   │
│   RB-102 Add manage bene  ○    │ Assignee:  Rahul Sharma                  │
│   RB-103 Download state   ○    │ Story Pts: 8                             │
│   RB-104 Real-time update ↻    │ Sprint:    SPR-34                        │
│   RB-105 Security enhanc  ●    │ Labels:    payments, backend, enhance    │
│   ...                          │                                           │
│                                │ SprintForge History:                     │
│                                │  ● SDD attached — 28 Jul  [01ARZ…]      │
│                                │  ● Description updated — 25 Jul [01ARZ…]│
│                                │                                           │
│ [Select all] Bulk: [Transition ▼] │ [Transition Status] [Update SPs]      │
│ [Apply]                        │ [Attach SDD]  [Link to Epic]             │
└─────────────────────────────────┴───────────────────────────────────────────┘
```

**Query bar:** Chip-based visual builder or raw JQL toggle. `[▶ Run]` in `--accent-purple`.

**Results list (virtualized):** Issue key + summary + status icon. Click → detail pane. Multi-select with Ctrl+Click.

**Detail pane:** Full issue fields. "SprintForge History" section lists this tool's prior actions with audit ID links.

---

### Screen 9: Documents

**Purpose:** Browse, compare, export, and manage all generated document versions.

**Layout:** Left tree (220px) + right version list + bottom preview.

```
┌───────────────────┬──────────────────────────────────────────────────────────┐
│ Document Tree     │ Versions — SRS: PROJ-1234 (Payment Processing)           │
│                   │                                                           │
│ ▼ SRS             │ Ver    Date         Format         Hash      Actions      │
│   ▼ PROJ-1234 (3) │ v003 ★ 2025-07-28  DOCX, PDF, MD  abc123   [↓][Compare] │
│   ▼ PROJ-1100 (1) │ v002   2025-07-27  DOCX            def456   [↓][Compare] │
│ ▼ SAD             │ v001   2025-07-26  DOCX            ghi789   [↓][Restore] │
│   ▼ PROJ-1234 (2) │                                                           │
│ ▼ SDD             │ ─────────────────────────────────────────────────────── │
│   ▼ PaymentSvc(4) │ Document Preview (v003)                                  │
│   ▼ AuthSvc (1)   │ ┌──────────────────────────────────────────────────────┐│
│ ▼ Tests           │ │ # SRS: PROJ-1234 — Payment Processing System         ││
│   ▼ PaymentSvc(2) │ │ ## 1. Functional Requirements                        ││
│                   │ │ - FR-001: The system shall support real-time...       ││
│                   │ └──────────────────────────────────────────────────────┘│
└───────────────────┴──────────────────────────────────────────────────────────┘
[Compare v2 vs v3]  [Restore to v2]  [↓ Export PDF]  [↓ Export DOCX]  [🔗 Attach to Jira]
```

**Document tree:** Folder tree with expand/collapse. Each document entry shows `(count)` of versions.

**Version list table:** Version number (latest marked `★`), date, format chips (DOCX/PDF/MD as colored pills), hash truncated, action buttons.

**Preview strip:** Read-only markdown render of selected version. Scrollable.

---

### Screen 10: Audit Center

**Purpose:** Complete, searchable, filterable history of every action the application has taken.

**Layout:** Top search/filter bar → full-width event table → right detail pane (slides in).

```
[🔍 Search audit logs…]  [Filters ▼]  [14 Jul 2025 – 25 Jul 2026 ▼]  [Users ▼]  [Modules ▼]  [⤢]
─────────────────────────────────────────────────────────────────────────────────────────────────────
Timestamp            User              Action          Module      Entity          Status
──────────────────── ──────────────── ─────────────── ─────────── ─────────────── ──────────
30 Jul 10:36 AM      Akshay Patwari   SRS Generation  SRS         Account Mgmt    ● Completed
30 Jul 10:35 AM      Neha Verma        SAD Published   SAD         Payment Service ● Completed
30 Jul 10:22 AM      Rahul Sharma      Unit Tests Gen  Unit Tests  Payment Svc     ● Completed
30 Jul 09:58 AM      Priya Singh       Jira Update     Jira        RB-102          ● Completed
30 Jul 09:45 AM      AI Copilot        Code Committed  Repository  payment-service ● Completed
15 Jul 09:08 AM      Akshay Patwari   Backup Generated System     Backup          ● Completed
```

Status chips: full colored pill badges. Module tags: colored module-specific chips (SRS=blue, SAD=teal, Jira=orange, Tests=green, etc.).

**Detail pane (slides in from right on row click, 400px):**
```
─── Audit Event ──────────────────────────────────────────
Audit ID:    01ARZ3NDEKTSV4RRFFQ69G5FAV           [Copy]
Module:      SRS                Status:  ● Completed
Action:      SrsGeneration      Duration: 12,450 ms
User:        akshay.patwari     Retries:  0
Machine:     ACME-DEV-01        Time (UTC): 2025-07-30 05:06

Inputs:
  Brief:    "Account management system..."
  SRS ID:   PROJ-1234
  Template: Default SRS

Output Files:
  📄 SRS-PROJ-1234.docx   SHA: abc123  [↓]
  📄 SRS-PROJ-1234.pdf    SHA: def456  [↓]

AI Call:
  Model: claude-sonnet-5   Tokens: 1,240   Temp: 0.2

Linked Events:
  ↓ AI Prompt   01ARZ…FAU  [View]
  ↓ Approval    01ARZ…FAX  [View]
  ↓ Jira Update 01ARZ…FAW  [View]

[🔁 Rollback]   [📋 Copy ID]   [↗ Open in Jira]
──────────────────────────────────────────────────
```

**Tabs (above table):** Timeline | Tree | Sessions | Jira | AI Prompts | Files | Approvals | Rollbacks.

---

### Screen 11: Approvals Center

**Purpose:** The only place writes happen. All write operations queue here; nothing executes until the user decides.

**Layout:** Header stats bar + scrollable list of approval item cards.

```
Queue: 3 pending  |  Today: 12 approved, 2 rejected  |  [Approve All]  [Cancel All]
──────────────────────────────────────────────────────────────────────────────────────
```

**Individual approval card:**
```
┌────────────────────────────────────────────────────────────────────────────────┐
│ ⏱ PENDING   [SRS]   Jira: Update description — PROJ-1234                      │
│ Queued: 30 Jul 2025 10:36 AM    Audit ID: 01ARZ3NDEKTSV4RRFFQ69G5FAV  [Copy] │
├───────────────────────────────────┬────────────────────────────────────────────┤
│ Current Values                    │ Proposed Values (AI Suggested)             │
│                                   │                                            │
│ Summary: Implement fund transfer  │ Summary: Implement fund transfer API       │
│          API                      │         with validation                    │
│                                   │                                            │
│ Priority: High                    │ Priority: High                             │
│                                   │                                            │
│ Assignee: Rahul Sharma            │ Assignee: Rahul Sharma                     │
│                                   │                                            │
│ Story Pts: 8                      │ Story Pts: 13                              │
│                                   │                                            │
│ Labels: payments, backend         │ Labels: payments, backend, enhancement     │
├─────────────────────────────────────────────────────────────────────────────────┤
│ ✦ AI Explanation                                                                │
│ It has analysed the requirements and identified the need to change Story        │
│ Points from 8 to 13 due to additional validation work…                         │
│                                                                                 │
│ Affected Files (3):   PaymentService.java   PaymentController.java   +1        │
│                                                                                 │
│ Ask a comment (optional): [_____________________________________________]       │
├─────────────────────────────────────────────────────────────────────────────────┤
│ [✓ Approve]           [✎ Modify]           [✗ Reject]                          │
└─────────────────────────────────────────────────────────────────────────────────┘
```

**Card anatomy:**
- Header: status chip (amber "PENDING") + module tag + description + audit ID.
- Two-column diff: Current Values left, Proposed Values (AI Suggested) right. Changed fields highlighted.
- AI Explanation: `--bg-elevated` panel with sparkle icon, prose explanation, model/token metadata.
- Affected Files: horizontal list of file names.
- Comment field: optional textarea.
- Actions: `[✓ Approve]` (green filled) | `[✎ Modify]` (outline) | `[✗ Reject]` (red text).

**Batch approval card:**
```
┌────────────────────────────────────────────────────────────────────────────────┐
│ ⏱ BATCH   [Sprint]   Jira: Create 32 work items — Sprint 34                   │
│ Batch ID: batch_20250730_001   Queued: 10:25:00                               │
├────────────────────────────────────────────────────────────────────────────────┤
│ ▼ STORY  RB-201  User Auth Integration [8 SP] [5d]                            │
│   ▶ DEV  RB-202  Implement login       [2d]                                   │
│   ▶ DEV  RB-203  Token refresh         [1d]                                   │
│   ▶ TST  RB-204  Unit tests            [1d]                                   │
│ ▼ STORY  RB-205  Payment Gateway [13 SP] [7d]                                 │
│   ... (28 more)                                              [Show all ▼]     │
├────────────────────────────────────────────────────────────────────────────────┤
│ [✓ Approve All in Batch]     [✎ Review individually]     [✗ Reject Batch]    │
└────────────────────────────────────────────────────────────────────────────────┘
```

**Post-decision states (replace action row):**
- `↻ Executing…` with spinner (blue)
- `✓ Completed at 10:37 AM` (green)
- `✗ Failed — [error message]  [Retry]  [View Error]` (red)
- `✗ Rejected at 10:37 AM` (red muted)
- `✕ Cancelled` (gray)

---

### Screen 12: Settings

**Purpose:** Manage all application settings organized in named profiles.

**Layout:** Left: General / Integrations tabs (200px) → Right: sub-tab content area.

```
┌────────────────────┬─────────────────────────────────────────────────────────┐
│ ● General          │ Integrations                                            │
│   Integrations     │ [Jira ●] [AI Provider] [Git Repository] [CI/CD] [Reposi]│
│                    │ ─────────────────────────────────────────────────────── │
│                    │ Repositories                                             │
│                    │   Jira URL:      [https://jira.sdc.com_______________]  │
│                    │   Project Key(s):[RBP, FNT, NOT, AUM_________________]  │
│                    │                                                          │
│                    │ Username:        [akshay.patwari@sdc.com______________]  │
│                    │ API Token:       [•••••••••••••••••••••••]  [👁 Show]   │
│                    │                                                          │
│                    │ Jira Settings                                            │
│                    │   API Dialect:   [Jira Cloud v3 ▼]                      │
│                    │   Auth Mode:     [API Token ▼]                           │
│                    │                                                          │
│                    │ Advanced  [▼]                                            │
│                    │                                                          │
│                    │   [🔌 Test Connection]  ● Connected                     │
│                    │                                                          │
│                    │                              [Save Changes]              │
└────────────────────┴─────────────────────────────────────────────────────────┘
```

**Left tabs:** `General` | `Integrations` — vertical tab list, `--accent-purple` active indicator. Width: `200px`.

**Integrations horizontal sub-tabs:** Jira | AI Provider | Git Repository | CI/CD | Artifact Repository. Pill tab style, `--accent-purple` active.

**Form fields:** `--bg-input` background, `--border` border, `8px` radius. Labels above fields in `--text-secondary`. Secret fields have `[👁 Show]` toggle.

**Test Connection button:** outline style → shows `● Connected` (green chip) or `✗ Failed` (red chip) after test.

**Save Changes button:** `--accent-purple` filled, right-aligned at bottom of content area.

**AI Provider tab:**
```
Providers:
┌──────────────────────────────────────────────────────┐
│  ID: primary   Vendor: [Anthropic ▼]                 │
│  Endpoint: [https://api.anthropic.com___________]    │
│  API Key:  [•••••••••••••••]  [Clear]                │
│  Model:    [claude-sonnet-5_____________________]    │
│  Temperature: [─────●────────] 0.2                   │
│  Max Tokens:  [8192____]                             │
│  [🔌 Test Connection]  ● OK             [Remove]     │
└──────────────────────────────────────────────────────┘
[+ Add Provider]

Default Provider:   [primary ▼]
Fallback Provider:  [fallback ▼]
Cost Alert:         [$50.00__] per session
```

**Sprint tab:**
```
Duration:         [10___] days
Development Days: [7____] days
Buffer Days:      [2____] days
Working Hours:    [8____] hours/day
Workweek: ☑ Mon ☑ Tue ☑ Wed ☑ Thu ☑ Fri ☐ Sat ☐ Sun

Holidays:
  2026-08-15  Independence Day    [Remove]
  2026-10-02  Gandhi Jayanti      [Remove]
  [+ Add holiday]

Capacity preview: 56 working hours in 10-day sprint (after 2 buffer days, 0 holidays)
```

---

## 5. Shared components

### 5.1 Status chip
Pill badge. Background: colored low-opacity tint. Border: matching color. Text: colored.

| Status | Text color | Background | Border |
|---|---|---|---|
| Completed / Connected | `--success` | `--success-bg` | `--success` |
| In Progress | `--accent-blue` | `rgba(59,130,246,0.15)` | `--accent-blue` |
| Pending Review | `--warning` | `--warning-bg` | `--warning` |
| Failed / Rejected | `--error` | `--error-bg` | `--error` |
| Queued | `--accent-purple` | `rgba(124,58,237,0.15)` | `--accent-purple` |
| Cancelled | `--text-muted` | transparent | `--border` |
| High Priority | `--error` | `--error-bg` | `--error` |

Icon (dot or symbol) always precedes the label — color is never the sole indicator.

### 5.2 Diff viewer rows
Three row types with full-width colored backgrounds and left accent border (4px):
- **Added:** `--diff-added-bg` bg, `--diff-added-text` border + tag pill.
- **Removed:** `--diff-removed-bg` bg, `--diff-removed-text` border + tag pill.
- **Modified:** `--diff-modified-bg` bg, `--diff-modified-text` border + tag pill.

Tag pill: small rounded badge ("Added" / "Removed" / "Modified") in matching text color on slightly darker bg.

### 5.3 Version selector
Dropdown: `v003 (latest) — 2025-07-28`, `v002 — 2025-07-27`, etc. Latest marked with `★`. Viewing a non-latest shows amber banner: "You are viewing a previous version. [Restore to this version]".

### 5.4 AI explanation panel
Collapsible panel with `--bg-elevated` background and left `--accent-purple` border (4px). Header: sparkle icon `✦` + "AI Explanation" label. Content: prose explanation + model/tokens/temperature metadata. Collapsed by default; chevron toggle.

### 5.5 Audit ID link
Small monospace chip `#01ARZ…` in `--bg-elevated`, `--text-muted`. `[Copy]` icon beside it. Clicking navigates to Audit Center filtered to that event.

### 5.6 Connection status indicator
`● Connected` (green dot) / `● Disconnected` (red dot) / `↻ Connecting` (amber animated dot) / `⊘ Not configured` (gray).

### 5.7 NEEDS-CLARIFICATION marker
Amber inline block inside document previews: amber left border (4px), `--warning-bg` background, `⚠ NEEDS-CLARIFICATION: [message]` text + `[Resolve]` link. "Submit for Approval" disabled while any unresolved.

### 5.8 Progress bar (long operations)
Full-width strip below action bar. `--accent-purple` filled track. Label: "Scanning: 450 / 1,240 files…". Elapsed time right-aligned. `[✕ Cancel]` button.

### 5.9 Empty state
Centered in content area: icon (large, `--text-muted`), heading (`--text-primary`), subtext (`--text-secondary`), primary action button. Example: "No approvals pending — You're all caught up" + green checkmark illustration.

### 5.10 Circular progress gauge
SVG ring chart. Track: `--border`. Fill: `--accent-purple` (or `--success` for coverage). Percentage label in center: bold, `--text-primary`. Sub-label below: `--text-secondary`. Used for sprint progress and AI coverage.

### 5.11 KPI stat card
`--bg-card` background, `--border` border, `10px` radius, `16px` padding.
- Large number: `28–32px`, bold, `--text-primary`.
- Sub-label: `12px`, `--text-secondary`.
- Trend badge: small pill — green `+N ▲`, red `-N ▼`, amber `⚠ N`.

---

## 6. Interaction patterns

### 6.1 Generate → preview → approve
1. User fills input fields.
2. Clicks Generate / AI button (purple, sparkle icon).
3. Progress strip shows; cancellable via `[✕]`.
4. Preview populates with versioned content.
5. Diff viewer shows changes from previous version.
6. User reviews; resolves NEEDS-CLARIFICATION markers.
7. User clicks "Submit for Approval."
8. Toast: "✓ Queued for approval. Open Approvals Center." Approvals badge increments.

### 6.2 Approvals Center workflow
1. User clicks notifications badge or navigates to Approvals Center.
2. Reviews each card: current vs proposed diff, AI explanation, affected files, audit ID.
3. Clicks Approve / Reject / Cancel per card (or Approve All).
4. Approved items show Executing spinner → Completed / Failed.
5. Failed items: error + Retry.

### 6.3 Rollback
1. Find COMPLETED event in Audit Center.
2. Click `[🔁 Rollback]` in detail pane.
3. New approval card queued: "Rollback: [original action]" with previous state as proposed.
4. Approve rollback in Approvals Center.
5. Rollback executes; new COMPLETED audit event records outcome.

### 6.4 Search → select → act (Jira, SDD)
1. Enter query; click Search/Run.
2. Results in virtualized list.
3. Click row → detail pane.
4. Multi-select with Ctrl+Click / Shift+Click.
5. Choose bulk action from dropdown → queues to Approvals Center.

---

## 7. Accessibility

- WCAG 2.1 AA compliance.
- Full keyboard navigation: Tab order matches visual order; Enter activates; Escape closes panels.
- Screen reader labels on all interactive elements (AutomationId + Name in WPF).
- Focus rings visible on all focusable elements — `2px solid --accent-purple`, `2px offset`.
- Color paired with icon or text — never sole state indicator.
- Font size configurable (Preferences tab: Small / Medium / Large / Extra Large).
- High contrast mode detected via `SystemParameters.HighContrast`; switches to system colors.

---

## 8. Window behavior

- **Minimum window size:** `1280 × 800`.
- **Default window size:** `1440 × 900`.
- Workspace area uses available width; panel dividers are drag-resizable.
- Nav rail collapses to icon-only at window widths below `1200px` (automatic; user can override).
- Three-panel layouts collapse to two panels at widths below `1100px` (rightmost panel hides; accessible via tab).
- Preview zones scroll independently of the shell.
- Audit list and Jira results list use WPF VirtualizingStackPanel — no degradation at 10,000+ rows.

---

## 9. Animation & transitions

- Nav rail collapse/expand: `150ms ease-out` width transition.
- Toast slide-in: `200ms ease-out` from bottom-right.
- Status chip change (Executing → Completed): `300ms` background flash.
- Panel slide-in (detail pane, approval panel): `200ms ease-out` from right.
- Circular gauge fill on load: `600ms ease-in-out` ring draw animation.
- Progress bar: smooth fill, no frame drops.
- All other transitions: `< 150ms` or instant — this is a productivity tool, not a marketing page.
- Respect `prefers-reduced-motion`: drop all transitions except instant state changes when enabled.
