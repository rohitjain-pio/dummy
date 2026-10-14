<!-- pipeline-provenance
produced_by: ui-enrichment (Beat 2, solution-designer)
plugin_version: sdlc-greenfield-v3
generated: 2026-09-11 (retroactively — these 9 items were raised and resolved live in the Beat 2
  checkpoint conversation rather than a workbook round-trip; this file is the machine record
  shared-workbook-io/answers-filled require, backfilled from that conversation's actual content)
-->

# UI Enrichment Questions — School Management System

All 9 items below are genuine unknowns/corrections found while reconciling `design-tokens.md`
§9's screen-mockup table against the real mockup (`school-management-ui(RJ) 2.html`) and the
finalized BRD. None were asked via `ui-design-questions.xlsx` — they were resolved live at the
Beat 2 ◆ checkpoint, per the operator's explicit approval. This file is the after-the-fact
machine record of that conversation, not a re-ask.

---

### Q-001 · Blocking · Nav role-gating: Classes must include Teacher
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  The mockup's NAV config excludes 'Teacher' from the Classes nav item's role list, but FR-027
  grants Teachers read access to the class list ("Institution staff and Teachers can retrieve
  classes... with the assigned class teacher shown"). Follow the BRD or the mockup's demo roles?
answer: >
  Apply the correction: Classes nav includes Teacher, per FR-027. The mockup is a demo fixture,
  not a permission-matrix source of truth.

### Q-002 · non-blocking · Schedule/Reports "soon" badges are stale
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  The mockup's NAV marks Schedule and Reports with badge:'soon' (implying not-yet-built), but
  both have full functional requirements in this release (FR-059-062, FR-054-058) and the mockup
  itself renders complete pages for both. Carry the "soon" badge into the product, or drop it?
answer: >
  Drop it. The built product does not carry a "soon" badge on either nav item — both are real,
  in-scope requirements for this release.

### Q-003 · Blocking · Attendance default-state bug
severity: critical
status: answered
answered_by: Jayesh Idnani
question: >
  The mockup's modalFillAttendance() defaults every student to 'Absent' when a roster is opened
  for marking, but BR-034 requires every learner to default to a not-present/unmarked state until
  explicitly marked, not a recorded outcome. Is this mockup behavior a real design choice, or a
  defect to correct?
answer: >
  Defect — correct it. The real build must show a neutral/unmarked state (all four toggle buttons
  unselected) until a teacher actively picks one; nothing is silently recorded as 'Absent' by
  default.

### Q-004 · Blocking · Attendance correction workflow is not modeled in the mockup
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  FR-033/034/035 define a same-day self-correct / next-day-onward approval / 30-day lock
  workflow for attendance corrections, but the mockup has no UI for this at all (it only shows
  the initial-marking flow). Design it from BRD text, or treat as out of scope for this pass?
answer: >
  Design it. Editing a >1-day-old record shows a "Request correction" action instead of a direct
  toggle, routes to a leadership-approval step, and shows a pending-approval badge until
  resolved; after 30 days the row renders read-only with a "Locked" indicator.

### Q-005 · Blocking · No Student/Parent dashboard variant in the mockup
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  The mockup's dashboard (pageHome()) is leadership-shaped only (institution-wide stats), but
  BRD §6.9/§6.13 (FR-057/074/075, BR-048) requires Student/Parent dashboards to show only their
  own/linked-children's attendance and academic reports, never institution-wide totals. Confirm a
  narrower Student/Parent dashboard variant (reusing the same components at reduced scope)?
answer: >
  Confirmed as a firm requirement. Build the narrower variant (own/linked-children attendance
  trend + today's status + recent announcements) using the same card/chart components, not a
  different visual language.

### Q-006 · non-blocking · Settings screen needs role-gating
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  The mockup's pageSettings() shows the full leadership settings screen (School Profile + 4
  config sections + System Information) unconditionally, but BR-058 restricts configuration
  changes to institution leadership only. Should non-leadership roles see a different, smaller
  Settings screen?
answer: >
  Yes. Leadership sees the full settings screen as mocked up; every other role sees an
  own-account-only variant (name/contact, password change, own notification preferences) — a
  materially smaller screen, not the leadership view with sections hidden.

### Q-007 · non-blocking · Announcements delete-icon visibility
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  The mockup's pageAnnouncements() shows the delete icon to the same 'canPost' role set used for
  create/edit (leadership + Teacher), but BR-038 restricts withdrawal to institution leadership
  specifically — narrower than "can post". Narrow the delete-icon visibility to leadership only?
answer: >
  Yes. Gate the delete icon to leadership only, per BR-038; create/edit stay available to
  leadership + Teacher per BR-037.

### Q-008 · non-blocking · Reports' "Student Satisfaction"/"Course Completion" gauges
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  The mockup's pageReports() renders "Student Satisfaction" and "Course Completion" quick-stat
  gauges, but BRD §6.9 grounds reporting solely in attendance and attendance-compliance per
  Q-H3's resolution (homework/engagement metrics were explicitly dropped from scope). Build these
  two gauges, or treat them as unsupported by any requirement?
answer: >
  Treat as a validation-required mockup artifact, not a requirement or implementation commitment.
  ai-spec must NOT spec these two gauges into 11-ui-spec.md or any other spec file as evidence of
  a real capability unless independently traced to an approved FR/BR — the mockup's own visual
  content is not sufficient grounds on its own. If untraceable, omit entirely (do not build, do
  not merely defer).

### Q-009 · non-blocking · Pricing page's plan-feature-list claims
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  The mockup's Pricing page (plan cards + comparison table) lists specific per-tier feature
  claims. These map to BR-061/062's tier/capability model, but the mockup's own copy might not
  exactly match whatever Tech Design's real entitlement model enforces server-side. Should
  ai-spec treat the mockup's feature-list copy as a specification of what must be enforced?
answer: >
  No. The mockup's plan-card features and comparison-table rows are a validation-required mockup
  artifact, not requirements evidence. ai-spec must not cite any individual feature claim from
  this page in any spec file unless it independently traces to an approved FR/BR (BR-061/062).
  Build the Pricing screen's structure (it's a real screen) but source the comparison table's
  actual values from the entitlement model, never from mockup copy.

---

## Summary

| Severity | Count | Answered |
|----------|-------|----------|
| Critical | 1 | 1/1 |
| High | 3 | 3/3 |
| Medium | 5 | 5/5 |

**All 9 items answered by Jayesh Idnani, 2026-09-11.** Items 1-7 are corrections applied as firm
requirements (verified present in `ui-design-final.md`). Items 8-9 are explicitly **not**
requirements — validation-required mockup artifacts that `ai-spec` must not spec in without
independent FR/BR traceability (verified honored by the Mode D independent spec audit,
`.aidlc/04-code-review/Spec_Audit.md`).
