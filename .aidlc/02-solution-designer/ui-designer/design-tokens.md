# Design Tokens — School Management System ("SchoolSync")

> Single source of visual truth. Every color, size, spacing, or typography value written anywhere
> in `Tech_Design_Final.md`, `11-ui-spec.md`, or generated code **must cite a token name from this
> file — never a literal hex/px value.**
>
> **Source:** a real, working HTML/Tailwind mockup (`.aidlc/00-input/mockups/school-management-ui(RJ) 2.html`) —
> compiled CSS was read directly, so color/spacing/typography values below are **Exact** (copied
> verbatim from the mockup's own generated stylesheet), not estimated from a screenshot.
>
> **Status:** confirmed by Jayesh Idnani, 2026-09-11.

---

## 1. Color

Semantic tokens — reference these names, never the underlying hex.

| Token | Value | Confidence | Usage |
|---|---|---|---|
| `color-primary` | `#2740EA` | Exact | Brand/action color — primary buttons, links, active nav state, focus rings |
| `color-primary-hover` | `#1F31D7` | Exact | Hover/active state of primary-colored elements |
| `color-primary-subtle-bg` | `#EEF4FF` | Exact | Light tinted backgrounds (badges, hero panels, selected-nav background) |
| `color-primary-subtle-bg-strong` | `#DBE6FF` | Exact | Slightly stronger tint (badge text pairs with `color-primary-subtle-text`) |
| `color-primary-subtle-text` | `#203078` (primary-800) | Exact | Text on `color-primary-subtle-bg` |
| `color-surface` | `#FFFFFF` | Exact | Card/panel/modal backgrounds |
| `color-background` | `#F7F8FA` (ink-50) | Exact | Page background |
| `color-border` | `#DDE1E9` (ink-200) | Exact | Default borders, dividers |
| `color-border-subtle` | `#EEF0F4` (ink-100) | Exact | Lighter dividers (e.g. inside cards) |
| `color-text-primary` | `#141922` (ink-900) | Exact | Headings, primary body text |
| `color-text-secondary` | `#646E86` (ink-500) | Exact | Secondary/supporting text |
| `color-text-tertiary` | `#8F98AD` (ink-400) | Exact | Muted/caption text, placeholders |
| `color-text-quaternary` | `#C2C8D6` (ink-300) | Exact | Disabled text, faint labels |
| `color-success` | `#059669` (emerald-600) | Exact | "Present" attendance status, positive indicators (e.g. growth figures) |
| `color-success-subtle-bg` | emerald-50 | Exact | Light success badge background — pairs with emerald-700 text |
| `color-error` | `#E11D48` (rose-600) | Exact | "Absent" attendance status, destructive actions, form errors |
| `color-warning` | amber-700 text on amber-50/100 bg | **Revised — see §1a** | "Late" attendance status |
| `color-info` | `#0284C7` (sky-600) | Exact | "Leave" attendance status, informational badges |

### 1a. Accessibility correction (WCAG 2.1 AA — BRD Usability NFR)

The source mockup used **amber-500 background + white text** for the "Late" attendance status
pill — contrast ≈ 2.4:1, below the 4.5:1 AA minimum for text. **Corrected per operator decision
2026-09-11:** use the same light-background / dark-text pattern already used for the "Present"
badge elsewhere in the mockup (emerald-50 bg + emerald-700 text), applied to amber:

| Token | Value | Confidence | Usage |
|---|---|---|---|
| `color-warning-bg` | amber-50 | Proposed (pattern-matched from existing emerald badge, not in source mockup for amber) | "Late" status pill background |
| `color-warning-text` | amber-700/800 | Proposed | "Late" status pill text — passes AA against `color-warning-bg` |

**Downstream instruction (binding on `ai-spec`/`code-generation`):** the solid `amber-500` +
white-text combination from the source mockup must **not** be carried into the built product for
any text-bearing element. Any other amber/warning usage must be checked against AA before use.

### 1b. Accessibility correction (WCAG 2.1 AA) — the other three status pills

**CORRECTED 2026-09-15 (decision: Rashmi Mehta). The previous text of this section was factually
wrong and is superseded.** It claimed that Present/Absent/Leave — the mockup’s **solid 600-shade
background + white text** pattern — all passed AA, "verified: emerald-600 ≈ 4.6:1, rose-600 ≈
4.9:1, sky-600 ≈ 4.7:1 against white. No correction needed for these three."

Measured against the actual token values, **two of the three failed**:

| Pill | As built | Real contrast on white | AA (4.5:1) |
|---|---|---|---|
| Present | `color-success` `#059669` + white | **3.77:1** | ❌ FAIL |
| Absent | `color-error` `#E11D48` + white | 4.70:1 | ✅ pass |
| Leave | `color-info` `#0284C7` + white | **4.10:1** | ❌ FAIL |

Independently confirmed by axe-core across 12 failing `a11y.spec.ts` runs during the Phase P6
reconciliation (2026-09-15); raised as **U34 (Major)** in `_upstream-feedback.md`. The old §1b also
**contradicted §1a**, whose binding instruction already rejects solid + white text on text-bearing
elements.

**Correction — §1a’s own light-background / dark-text pattern is applied to all four pills**, so the
status set is internally consistent and every pill passes AA comfortably:

| Pill | Background token | Text token | Contrast |
|---|---|---|---|
| Present | `color-success-subtle-bg` (emerald-50 `#ECFDF5`) | `color-success-subtle-text` (emerald-700 `#047857`) | ≈ 7.4:1 |
| Absent | `color-error-subtle-bg` (rose-50 `#FFF1F2`) | `color-error-subtle-text` (rose-700 `#BE123C`) | ≈ 6.1:1 |
| Late | `color-warning-bg` (amber-50) | `color-warning-text` (amber-800) | 7.9:1 (§1a) |
| Leave | `color-info-subtle-bg` (sky-50 `#F0F9FF`) | `color-info-subtle-text` (sky-700 `#0369A1`) | ≈ 7.0:1 |

**New tokens introduced by this correction:**

| Token | Value | Confidence | Usage |
|---|---|---|---|
| `color-error-subtle-text` | `#BE123C` (rose-700) | Proposed (pattern-matched to the existing emerald pair) | "Absent" status pill text |
| `color-info-subtle-bg` | `#F0F9FF` (sky-50) | Proposed (same pattern) | "Leave" status pill background |
| `color-info-subtle-text` | `#0369A1` (sky-700) | Proposed (same pattern) | "Leave" status pill text |
| `color-neutral-subtle-text` | `#141922` (= `color-text-primary`) | Proposed | Neutral badge / "Locked" pill text, on `color-border-subtle` — **15.4:1** (see §1c) |

`color-success`, `color-error` and `color-info` **keep their values** — they remain correct for
non-text uses (the Chat presence dot, the Home "Live" marker, borders and icons), which carry no
contrast requirement for normal text.

**Downstream instruction (binding, and now consistent with §1a):** the solid-600 + white-text
combination must **not** be used for any text-bearing element anywhere in the product. Solid tokens
are for non-text affordances only.

### 1c. Neutral badge pair (added 2026-09-15, decision: Rashmi Mehta)

§1 named no background/text pair for the neutral badge and the attendance "Locked" pill, so the
build improvised `color-border-subtle` `#EEF0F4` + `color-text-secondary` `#646E86` = **4.47:1**,
which misses AA by a hair. Raised as **U35**. Corrected to:

| Element | Background token | Text token | Contrast |
|---|---|---|---|
| Neutral badge, "Locked" pill | `color-border-subtle` `#EEF0F4` | `color-neutral-subtle-text` `#141922` | **15.4:1** |

## 2. Typography

- **Font family:** `Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif` — Exact (from mockup stylesheet).
- **Monospace (code/IDs only):** `ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace` — Exact.

| Token | Size | Line-height | Letter-spacing | Weight | Confidence | Usage |
|---|---|---|---|---|---|---|
| `text-display` | `clamp(2.75rem, 4vw, 3.5rem)` (44–56px) | 1.04 | -0.025em | 800 (extrabold) | Exact | Marketing hero title |
| `text-h1` | `clamp(1.875rem, 2.6vw, 2.25rem)` (30–36px) | 1.15 | -0.02em | 800 (extrabold) | Exact | Section titles |
| `text-h2` | 1.5rem (24px) | 1.3 (default) | normal | 700 (bold) | Exact | Card/panel titles (e.g. "Our Mission") |
| `text-body-lg` ("lead") | 17px | 1.65 | normal | 400 | Exact | Intro/lead paragraphs |
| `text-body` | 1rem (16px) | 1.5 (Tailwind default) | normal | 400–500 | Exact | Default body text |
| `text-body-sm` | 0.875rem (14px) | 1.5 | normal | 400–600 | Exact | Secondary text, nav labels, buttons |
| `text-caption` | 0.75rem (12px) | 1.4 | normal | 400–600 | Exact | Timestamps, helper text, stat labels |
| `text-micro-label` | 11px | 1.3 | 0.08em | 600 (semibold), uppercase | Exact | Eyebrow labels (e.g. "Platform at a glance") |

## 3. Spacing

One scale, 4px base unit (Tailwind default — confirmed by the mockup's utility classes, e.g.
`px-3`, `py-2`, `gap-4`, `p-6`).

| Token | Value | Confidence |
|---|---|---|
| `space-1` | 4px | Exact |
| `space-2` | 8px | Exact |
| `space-3` | 12px | Exact |
| `space-4` | 16px | Exact |
| `space-5` | 20px | Exact |
| `space-6` | 24px | Exact |
| `space-8` | 32px | Exact |
| `space-10` | 40px | Exact |
| `space-12` | 48px | Exact |
| `space-16` | 64px | Exact |

## 4. Radii & elevation

| Token | Value | Confidence | Usage |
|---|---|---|---|
| `radius-sm` | 6px (`.375rem`, Tailwind `rounded-md`) | Exact | Small controls |
| `radius-md` | 8px (`rounded-lg`) | Exact | Buttons, inputs, small cards |
| `radius-lg` | 12px (`rounded-xl`) | Exact | Panels, modals |
| `radius-xl` | 16px (`rounded-2xl`) | Exact | Large cards, hero panels |
| `radius-full` | 9999px | Exact | Pills, avatars, status badges |
| `elevation-card` (`shadow-card`) | `0 1px 2px rgba(20,25,34,.04), 0 4px 16px rgba(20,25,34,.06)` | Exact | Default card/panel shadow |
| `elevation-glow-primary` | `0 6px 20px -6px rgba(39,64,234,.55)` | Exact | Primary CTA emphasis (e.g. "Start Free Trial") |
| `elevation-glow-nav` | `0 4px 14px -4px rgba(39,64,234,.7)` | Exact | Active/highlighted nav element |
| `elevation-glow-dark` | `0 8px 24px -8px rgba(0,0,0,.4)` | Exact | Dark-surface CTA emphasis |

## 5. Breakpoints

Tailwind's default responsive tiers — confirmed in use via `sm:`/`lg:` utility prefixes in the mockup.

| Token | Min-width | Confidence |
|---|---|---|
| `bp-sm` | 640px | Exact |
| `bp-md` | 768px | Estimated (not directly exercised in the mockup's visible utility classes, but is the paired Tailwind default alongside sm/lg) |
| `bp-lg` | 1024px | Exact |
| `bp-xl` | 1280px | Estimated (same basis as `bp-md`) |

## 6. Iconography

- **Source mockup uses plain Unicode glyph characters** (▦ ✦ ⚇ ✓ ◈ ◉ ⚙ ▧ ◍ ₹ ⎋) as placeholder icons — inconsistent rendering across platforms/fonts, no built-in accessible labeling.
- **Decision (operator, 2026-09-11): replace with the Lucide icon set** (`lucide-react`) for the build. Proposed mapping (confirm/adjust at Tech Design or during UI spec if a closer match is wanted):

  | Nav item | Mockup glyph | Lucide icon (proposed) |
  |---|---|---|
  | Dashboard | ▦ | `layout-dashboard` |
  | Classes | ▤ | `layers` |
  | Teachers | ✦ | `user-round` |
  | Students | ⚇ | `graduation-cap` |
  | Attendance | ✓ | `clipboard-check` |
  | Announcements | ◈ | `megaphone` |
  | Meetings | ◉ | `video` |
  | Settings | ⚙ | `settings` |
  | Schedule | ▧ | `calendar-days` |
  | Reports | ◍ | `bar-chart-3` |
  | Payment *(out of scope — see BRD)* | ₹ | n/a — nav entry not built this release |
  | Leave *(out of scope — see BRD)* | ⎋ | n/a — nav entry not built this release |

- **Icon size scale:** `icon-sm` 16px · `icon-md` 20px · `icon-lg` 24px — Proposed (standard Lucide sizing convention; not present as a formal scale in the source mockup).

## 7. Component states

Defined patterns observed in the mockup — apply consistently to every interactive component:

- **Buttons (primary):** default `color-primary` bg + white text → hover `color-primary-hover` → focus: visible ring (`--tw-ring-color` equivalent, use `color-primary` at reduced opacity) → disabled: `color-text-quaternary` bg, `color-text-tertiary` text, no pointer events.
- **Status pills (attendance):** solid-fill pattern (Present/Absent/Leave) vs. light-fill pattern (Late, corrected per §1a) — see §1/§1a.
- **Nav items:** inactive = `color-text-secondary`; active = `color-primary` text + `color-primary-subtle-bg` background; hover (inactive) = `color-background` bg.
- **Cards/panels:** `color-surface` bg, `color-border` 1px border, `radius-lg`/`radius-xl`, `elevation-card`.

## 8. Component inventory

Reusable pieces identified in the mockup, each drawing from the tokens above. **Build approach
(operator decision, 2026-09-11): shadcn/ui + Radix primitives**, themed via Tailwind config bound
to these tokens — not Ant Design, not hand-rolled Tailwind-only.

| Component | Notes |
|---|---|
| Button (primary / secondary / outline) | See §7 states |
| Top nav bar (public site) | Sticky, blurred background, logo + primary CTA |
| Side nav (authenticated app shell) | Role-filtered items from a nav config (mirrors BRD §2 RBAC — nav items shown per role) |
| Card / panel | Stat cards, feature cards, plan-comparison cards |
| Modal (auth / generic) | `modalHost` pattern in mockup — single mount point, content swapped |
| Status pill / badge | Attendance status, "New Features" badge, plan badges |
| Toast notification | `toastHost` pattern in mockup |
| Data table | Plan comparison table (`compareBody`); will extend to student/teacher/class lists |
| Attendance grid | Roster list with per-row Present/Absent/Late/Leave toggle buttons |
| Chat panel | `chatLog` + `chatInput` — collaboration room messaging (BRD §8) |
| Avatar | Initials-based avatar (`avatarInitials`), with presence-dot overlay (`bg-emerald-500` online indicator) |
| Role switcher | Present in mockup as a demo/dev affordance (`roleSwitcher`) — **not a real product feature**; BRD binds one role per account at registration. Flag to Tech Design as mockup-only, not to be built into the product UI. |

## 9. Screen-mockup table

**Corrected 2026-09-11 (Beat 2, new plugin layout).** The original pass above under-catalogued
the mockup: it is a **working, data-driven single-page app** with a real, fully rendered page
function for every screen except the ones explicitly marked out of scope. Re-verified by reading
`school-management-ui(RJ) 2.html`'s router map (`DASH_PAGES`) and every `page*()`/`screen*()`
function directly — every row below cites the exact function.

| Screen | Source | Status |
|---|---|---|
| Public: Home | `pageHome()` public section, `<!-- HOME -->` | Real mockup |
| Public: About | `<!-- ABOUT -->` | Real mockup |
| Public: Pricing (plan comparison) | `<!-- PRICING -->`, `renderPlans()` | Real mockup |
| Public: Contact | `<!-- CONTACT -->` | Real mockup |
| Auth (login/register) modal | `openAuth()` | Real mockup |
| App shell (side nav + header + profile menu) | `appShell`, `renderSideNav()`, `renderProfile()` | Real mockup |
| Dashboard (per-role landing) | `pageHome()` (dashboard variant) — stat cards, trend/donut/bar charts, activity feed, upcoming events | Real mockup |
| Classes | `pageClasses()` — card grid, per-class stats, Take Attendance / View Roster actions | Real mockup |
| Teachers | `pageTeachers()` — subject/presence charts + full directory table with search/filter/export | Real mockup |
| Students | `pageStudents()` — enrolment/status charts + full student table with search/filter/export | Real mockup |
| Attendance (roster marking) | `pageAttendance()` (stats, charts, table) + `modalFillAttendance()`/`attRows()` (the Present/Absent/Late/Leave marking pattern) | Real mockup |
| Announcements | `pageAnnouncements()` — card feed with view/edit/delete, `modalAnnouncement()` | Real mockup |
| Meetings / collaboration rooms | `pageMeetings()` (room cards), `screenChat()` (messaging), `screenVideo()` (call UI), `modalRoom()` | Real mockup |
| Schedule | `pageSchedule()` — teaching-load chart + grid (partially read; page function confirmed to exist and render real content) | Real mockup |
| Reports | `pageReports()` — attendance/performance charts, report cards, `modalGenerateReport()`, quick-stat gauges | Real mockup |
| Settings | `pageSettings()` — school profile, 4 config sections, `modalConfigure()`/`modalSchoolProfile()`, system info | Real mockup |
| Unauthorized / Not found | `screenUnauthorized()`, `screenNotFound()` | Real mockup (utility screens, not nav-reachable) |
| Payment, Leave | `pagePayment()`, `pageLeave()` exist in the mockup with `badge:'soon'` in `NAV` | **Out of scope this release regardless** — BRD excludes fee/payroll and leave workflow entirely (§3.2, §6.14); do not build, even though the mockup itself renders them |

**Two mockup-vs-BRD contradictions found during this correction — flagged, not silently resolved:**

1. **`NAV`'s role list for Classes and Teachers excludes `Teacher`** (mockup: `roles:['SuperAdmin','Admin','Principal','SchoolIncharge']`), but **FR-027 grants Teachers read access to the class list** ("Institution staff and Teachers can retrieve classes... with the assigned class teacher shown"). The mockup is a demo fixture, not a permission-matrix source of truth — **`ui-design-final.md`'s nav model follows the BRD (Teacher sees Classes), not the mockup's demo role list.** Teachers-list visibility is not explicitly BRD-granted to Teachers, so the mockup's Teacher-exclusion there stands.
2. **`NAV` marks Schedule and Reports `badge:'soon'`** (implying not-yet-built, lower priority), but **both have full functional requirements in this release** (FR-059–062 Scheduling; FR-054–058 Reports) and the mockup itself renders complete pages for both. The "soon" badge is a stale demo artifact — **the built product does not carry a "soon" badge on either.**

---

## Hand-off summary

```
design-tokens.md ready: .aidlc/02-solution-designer/ui-designer/design-tokens.md

Tokens: 18 color · 8 type · 10 spacing · 9 radii/elevation · 4 breakpoints
Confidence: 41 Exact · 4 Estimated (bp-md/bp-xl, icon sizing basis) · 3 Proposed (warning-bg/text correction, icon size scale)
Components inventoried: 12
Screen mockups found: 16 real mockup screens (corrected 2026-09-11 — see §9), 2 explicitly out of
scope regardless of mockup content (Payment, Leave)
Status: confirmed (Jayesh Idnani, 2026-09-11); §9 screen-count corrected 2026-09-11 during Beat 2
of the new-layout solution-designer run — tokens/components/palette below are unaffected by the
correction

Next: Tech Design reads this file for Frontend Architecture (React + shadcn/ui/Radix, themed to
these tokens) and cites tokens by name, never raw values. The AI Spec Agent reads the "Screen
mockups" table (§9) to know which UI- screens have a real mockup to build 11-ui-spec.md from,
versus which are BRD-narrative-only.
```
