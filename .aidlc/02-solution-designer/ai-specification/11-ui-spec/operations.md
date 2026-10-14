<!-- pipeline-provenance
produced_by: ai-spec-agent (ui-spec-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, U20). **UI-016 written back from the
  P4 build:** per-period publish state and its role visibility (Q-225), the `HH:mm` field rule,
  the multi-conflict 409 array, and what is deliberately not built. Every value cites a named
  token from `design-tokens.md`; no literal hex or px introduced. UI-015 and UI-017 are unchanged
  — **Q-236's omissions there (the charts the API cannot supply) remain open as U33**, which this
  patch does not resolve.
-->

# 11 — UI Spec — `operations`

## UI-015 — Dashboard (`/dashboard`)

**Roles:** all 7, content varies materially by role (a real gap the mockup doesn't model —
resolved and approved 2026-09-11, `ui-design-final.md` §3, final and binding).

**Leadership variant (Administrator/Principal/SchoolInCharge/PlatformOperator-institution-view —
`pageHome()` fidelity):** welcome header + date + "Take Attendance"/"New Announcement" quick
actions → 4 stat tiles (Total/Present Teachers, Total/Present Students, with sparklines) →
Attendance Trend area chart (Students vs Teachers) + Today's Attendance donut → Students-per-Class
bar chart + Gender Split donut → Recent Activities feed + Upcoming Events list. **The "Upcoming
Features" roadmap-teaser panel is dropped** — a marketing/demo affordance with no BRD backing,
not built into the product.

**Teacher variant:** same component family, scoped to the teacher's own classes only — no
institution-wide totals (BR-048 scopes leadership dashboards to leadership; a Teacher is not
leadership).

**Student variant:** own attendance percentage + today's status + recent announcements
(FR-057, FR-074) — reuses the same card/chart components at reduced scope, **not a different
visual language** (operator decision, final). No institution-wide teacher/student totals ever
shown.

**Parent variant:** same as Student, for each linked child (a child-switcher if >1 linked child),
per FR-075.

**State matrix:** empty (brand-new institution, 0 students/teachers — every stat tile shows 0,
charts show a flat/empty state rather than a broken axis, improved-beyond-mockup) · loading
(skeleton stat tiles + chart placeholders — dashboards aggregate multiple entities, so a
spinner-only state would look broken for several hundred ms) · success (per-role variant above).
**API:** API-048. **Trace:** FR-052, FR-053, FR-056, FR-057, FR-074, FR-075, BR-048, BR-050.

## UI-016 — Schedule (`/dashboard/schedule`)

**Roles:** leadership (maintain, BR-056); Teacher/learner (view only).
**Layout:** class selector, "+ Add Schedule" action, teaching-load-by-staff chart, weekly period
grid (day × time-slot) showing class/subject/room/teacher per period.
**Conflict validation:** a period-slot conflict (teacher, class, or room double-booked) is a
**blocking validation error at save time** — all conflicts found are reported together, never a
silent overwrite (improved-beyond-mockup — the mockup's "Schedule entry form opened" toast stub
does not model this at all).
**State matrix:** empty (no periods scheduled yet) · loading · success · conflict-error (inline,
per-conflict-dimension messages using `ERR-701`/`702`/`703` — the 409 body is an **array**,
`{errors:[…]}`, so the screen renders **every** conflict at once rather than the first).
**Publish state (written back at 1.0.4 — Q-225, U20):** publish is **per period**, not per
timetable. A leadership user sees drafts and published periods, visually distinguished using the
**neutral badge pair** (`color-border-subtle` background + `color-neutral-subtle-text`, per
`design-tokens.md` §1c) for a draft. **Teacher, Student and Parent see published periods only** —
a draft is absent from their grid, not greyed out, because the API does not return it.
Publishing twice is a no-op; editing a published period keeps it published.
**Time entry:** `timeSlot` is a 24-hour `HH:mm` **start** time; the field rejects `9am`, `9:00`
and `24:00` inline before submit.
**Not built this release:** no delete action for a period (no endpoint is specified), and **exam
schedules (FR-061) are not modelled** (Q-225). Recorded, not silently dropped.
**API:** API-053–055. **Trace:** FR-059–062, BR-053–056, Q-225.

## UI-017 — Reports (`/dashboard/reports`)

**Roles:** leadership full access; Teacher — own classes only (BR-051).
**Layout:** attendance-vs-performance area chart + grade-distribution (pass/fail-rate) donut →
attendance-by-class + subject-engagement-by-attendance bar charts → 4 report-type cards (Student
Performance, Class Attendance, Course Analytics, Teacher Performance) each with Generate/View
History.

**Binding exclusion (operator decision, `ui-design-final.md` §11, carried forward as a hard
constraint on this spec):** the mockup's **"Student Satisfaction" and "Course Completion" quick-
stat gauges are NOT built.** Neither traces to an approved FR/BR — Q-H3 explicitly dropped
homework/engagement-style metrics from scope, and no capturing mechanism exists for either metric
anywhere in `04-data-model.md`. **This spec omits them entirely**, rather than deferring or
flagging them for later — carrying them forward on the strength of the mockup's own visual
content alone is not sufficient grounds, per the binding instruction. Only the
attendance-vs-performance chart, grade-distribution donut, and the two attendance/subject-
engagement bar charts remain, because those trace to FR-054/FR-056/FR-057.

**Generate-report modal:** report type (read-only), date range, scope (whole school / one class),
format (PDF/Excel/CSV — **[NEEDS ANSWER — Q-214, non-blocking]**: Tech Design D7 names PDF+CSV
only; the mockup's modal additionally offers "Excel" — provisional choice: build PDF+CSV per D7,
omit the Excel option from the format dropdown until/unless D7 is revised, since introducing a
third export format server-side is a Tech Design decision, not a UI-spec-level addition).
**State matrix:** loading (report generation is not instant — a progress/spinner state on
Generate, not the mockup's instant toast) · success (report ready, added to history) · empty (no
reports generated yet).
**API:** API-050–052. **Trace:** FR-054–058, BR-051/052.
