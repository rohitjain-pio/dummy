<!-- pipeline-provenance
produced_by: ai-spec-agent (ui-spec-writer)
generated: 2026-09-11
-->

# 11 — UI Spec — `attendance`

## UI-010 — Attendance (`/dashboard/attendance`)

**Roles:** leadership + Teacher (mark); Student/Parent see only their own/linked-children's
records via the Dashboard's narrower variant, not this full-roster screen (BR-032/033).
**Layout:** class selector + date picker + "Fill Attendance" action → 4 stat tiles (Roster,
Present, Absent, Late/Leave — `color-success`/`color-error`/`color-warning-bg`+`color-warning-text`/
`color-info` pill tokens per `design-tokens.md` §1/§1a) → attendance-over-time area chart +
today's-breakdown donut → full attendance `DataTable` (roll no., name, date, status pill) with
export.

**Fill-attendance modal (`AttendanceGrid`):** bulk "Mark all Present/Absent" + per-student toggle
row. **Every row opens with all four toggle buttons unselected (NotYetMarked) — never
pre-selected "Absent"** (BRL-034; this corrects a defect in the source mockup, which defaulted
every student to `'Absent'` — `ui-design-final.md` §7 correction 3, final and binding).

**Correction/lock states (designed here, not inherited from the mockup — BRL FR-033/034/035):**
- **Same-day (day 0):** direct toggle edit, no approval — identical UX to initial marking.
- **Day 1–29:** editing an entry shows a **"Request correction"** action instead of the direct
  toggle; submitting opens a small reason field, creates a `CorrectionPending` request
  (API-033); the row shows a **pending-approval badge** (`color-warning-bg`/`-text`) until an
  approver resolves it (API-034, roles per Q-204, confirmed: Administrator/Principal/SchoolInCharge).
- **Day 30+:** the toggle/request action is **removed entirely** — row renders read-only with a
  **"Locked"** indicator (`color-text-tertiary` badge, `radius-full`).

**State matrix:** empty (class with 0 students — "This class has no students enrolled yet") ·
loading (skeleton table + stat tiles) · success · pending-approval (per row) · locked (per row,
after 30 days) · error (`ERR-401` locked, `ERR-403` future date, on save attempt).

**Accessibility:** the four outcome toggles are a radio-group per row (`role="radiogroup"`,
labeled by student name) — not four independent checkboxes, since exactly one outcome (or none
yet) is valid per person per date (BR-031).

**API:** API-031–036. **Trace:** FR-030–037, BR-030–035, NFR-002 (≤3-step task: open roster →
toggle/bulk-mark → save).
