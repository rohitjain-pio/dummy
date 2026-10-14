# BRD Gap Questions — School Management System

**Source analysed:** `.aidlc/00-input/BRD.md` (full BRD — no scope slicing; see `.aidlc/01-brd-analysis/brd-scope/scope-decision.md`).
**Coverage:** all 12 functional areas (Institution Management, Identity/Access, Student Records, Staff Records,
Class/Section/Subject, Attendance, Announcements, Collaboration/Messaging/Meetings, Dashboards/Reporting,
Scheduling, Institution Configuration, Commercial/Subscription) contain Purpose, Key Capabilities, Business
Rules and User Actions — a full workbook is warranted (this is not a thin-intake case).

**Important note on the BRD's own "Answers – Proposed Business Decisions" section:** the BRD already proposes
answers to its own 6 "Open Questions." These were treated as candidate answers, not confirmed decisions, and
each has now been explicitly confirmed, amended, or rejected below by a named approver.

All Critical, High and Medium items below are now answered. Governing principle applied throughout: use only
requirements explicitly defined in the BRD; do not add new functional or non-functional requirements beyond
what is needed to resolve a genuine contradiction or gap; where contradictions exist, align with the original
Scope and Out-of-Scope sections rather than expanding scope.

**Reformatted 2026-09-11** from the old sdlc-greenfield-v3 layout's free-form heading/bold-field convention
(`### C1 — ...` / `**Answered by:**`) into the current `### Q-NNN · Class · Topic` / lowercase-field convention,
so `questions-sheet.mjs` can parse it. No question, source, or answer text was altered — only the structural
markup changed. Each block's original short ID (C1, H1, M1, ...) is kept in its Topic field for traceability.

---

## CRITICAL (must all be answered — Gate 1 blocker)

### Q-001 · Blocking · C1 — Contradiction: Student/Parent access scope vs. Out of Scope section
severity: critical
status: answered
answered_by: Jayesh Idnani
question: >
  Contradiction: Student/Parent access scope vs. Out of Scope section.
context: >
  "Out of Scope" lists "Fee collection, payroll, salary disbursement and any financial transaction
  processing", "Leave application, approval workflow and leave-balance tracking", and "Learning content
  authoring, assignment submission, grading workflows and examination marking" as explicitly excluded from this
  release. Yet the BRD's own proposed decision on Student/Parent Access (Answers §4) grants students and parents
  access to "Fee summaries" / "Fee information", states parents "may submit leave requests," and grants
  access to "Homework and assignments."
answer: >
  The proposed Student/Parent access list is trimmed to remove fee summaries/fee information,
  parent leave requests, and homework/assignment access. Student and Parent access is limited to: attendance
  records, class timetable, examination schedules, school announcements, and academic reports derived from
  operational indicators actually captured by in-scope capabilities. Fee, leave-request and homework/assignment
  capabilities remain excluded per the Out-of-Scope section, which stands as written and is not amended.

### Q-002 · Blocking · C2 — Personal data retention period (BRD Open Question 2)
severity: critical
status: answered
answered_by: Jayesh Idnani
question: >
  Personal data retention period (BRD Open Question 2).
context: >
  BRD Open Question 2 and Proposed Decision 2 propose 7-year retention for student/guardian and
  staff records, a 90-day export grace period on subscription end, then archival/deletion — but names no
  applicable regulation or jurisdiction.
answer: >
  Accepted as proposed: Student and Guardian records retained 7 years after the student leaves
  the institution; Staff records retained 7 years after employment termination. On subscription end, data remains
  accessible for export during a 90-day grace period, then is archived, then securely deleted or anonymised once
  the applicable retention period completes (unless a specific legal obligation requires longer). No specific
  named data-protection regulation/jurisdiction is attached to this rule — it is adopted as a general good-practice
  business rule.

### Q-003 · Blocking · C3 — Guardian consent capture is a new capability, not in the original Scope list
severity: critical
status: answered
answered_by: Jayesh Idnani
question: >
  Guardian consent capture is a new capability, not in the original Scope list.
context: >
  BRD Open Question 6 / Proposed Decision 6 introduces consent capture (privacy notice acceptance,
  processing consent, photo/media consent, emergency-contact authorisation, communication consent, with dated,
  versioned, attributable consent records) and states "Enrollment is considered complete only after required
  guardian consents have been captured." None of this appears in the BRD's "Scope" list of 18 capabilities or
  in the Student Records Management functional requirement (§3).
answer: >
  Out of scope for this release. Open Question 6 and Proposed Decision 6 (guardian consent
  capture) are dropped entirely and are not carried into the final BRD as an accepted rule. Student enrolment
  completion is not gated on consent capture in this release.

### Q-004 · Blocking · C4 — Child-safeguarding controls for live meetings and messaging involving Students
severity: critical
status: answered
answered_by: Jayesh Idnani
question: >
  Child-safeguarding controls for live meetings and messaging involving Students.
context: >
  §8 Collaboration, Messaging and Live Meetings allows any authenticated user (including Students,
  who are minors) to create rooms, join live audio/video meetings, and exchange messages, gated only by a room
  passcode. No business rule addresses staff supervision, restrictions on a Student initiating a private
  1:1 meeting with an individual Teacher, moderation, or any other child-safeguarding control.
answer: >
  No additional safeguarding-specific business rules are added beyond what the BRD already
  states. Collaboration rooms and meetings remain governed solely by the existing rules: authentication required,
  passcode-gated access, and the existing role-based access model. No new moderation, supervision, or leadership
  audit-access rule is introduced for this release.

### Q-005 · Blocking · C5 — Mechanism for linking a Parent account to their child's Student record
severity: critical
status: answered
answered_by: Jayesh Idnani
question: >
  Mechanism for linking a Parent account to their child's Student record. (Note: answered here as
  "not defined at BRD level" — the concrete implementation mechanism was deferred to Tech Design
  and is resolved there as T-003/Q-003 in architect-decision-questions.xlsx.)
context: >
  Proposed Decision §4 states Parents access "information related to linked children," and §3
  Student Records Management allows the student record to be "optionally link[ed] to a platform user account" —
  but no functional requirement anywhere describes how a Parent account becomes linked to a specific Student
  record: who initiates the link, who verifies the guardianship relationship, and who approves it.
answer: >
  (revised 2026-09-10) The BRD includes Parent and Student roles but does not define the
  parent-student association process. The implementation approach is outside the scope of the current BRD and
  will be determined during solution design.

  Superseded answer (withdrawn): "The Parent-to-Student link is staff-initiated at enrolment, consistent
  with the existing pattern in §3 where Administrator/Principal/School In-charge staff already manage student
  enrolment and the optional user-account link. Institution staff link the Parent user account to the Student
  record as part of enrolment or subsequent account setup, and staff are responsible for verifying the
  guardianship relationship." Withdrawn — the explicit staff-initiated-at-enrolment business rule has been
  removed at the operator's request in favour of the neutral, BRD-aligned wording above.

---

## HIGH

### Q-006 · non-blocking · H1 — Academic year rollover: automatic or per-student promotion decision? (BRD Open Question 1)
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Academic year rollover: automatic or per-student promotion decision? (BRD Open Question 1)
context: >
  Proposed Decision 1 describes a "controlled promotion process" but does not say whether promotion
  is automatic for the whole roster or requires a per-student decision by leadership (e.g., a student held back).
answer: >
  Confirmed: Academic Year → Term/Semester hierarchy as proposed. Promotion at year-end is
  automatic for the full roster by default, with institution leadership able to override and hold back individual
  students before or after the automatic rollover. New class rosters are generated for the new academic year;
  historical class allocations, enrolment, roster and attendance data remain preserved and available read-only.

### Q-007 · non-blocking · H2 — Attendance correction cut-off and approval (BRD Open Question 3)
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Attendance correction cut-off and approval (BRD Open Question 3).
context: >
  Proposed Decision 3: same-day self-correction by teachers, leadership approval required from the
  following day, records locked after 30 calendar days.
answer: >
  Confirmed as proposed: teachers may self-correct attendance until the end of the attendance
  day; corrections from the following day onward require approval from an authorised academic leader (Principal,
  Vice Principal, or Academic Administrator); records are locked to further correction after 30 calendar days.
  The 30-day lock period is a fixed platform-wide constant, not configurable per institution.

### Q-008 · non-blocking · H3 — Student/teacher performance reporting basis (BRD Open Question 5) — tied to C1
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Student/teacher performance reporting basis (BRD Open Question 5) — tied to C1/Q-001.
context: >
  Proposed Decision 5 bases performance reporting on "homework completion rate," "assignment
  submission rate," and similar indicators — but homework/assignment functionality is not otherwise in scope
  (see Q-001/C1) and grading/assessment is explicitly out of scope.
answer: >
  Following the Q-001/C1 resolution (homework/assignment access excluded), performance reporting is
  limited to metrics the in-scope capabilities actually produce: Student performance = attendance percentage only.
  Teacher performance = attendance compliance and timeliness of attendance submission only. Homework completion,
  assignment submission, participation and engagement indicators are dropped from the metric list as no capturing
  mechanism exists for them in this release.

### Q-009 · non-blocking · H4 — Subscription tier capacity/storage enforcement
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Subscription tier capacity/storage enforcement.
context: >
  §12 states each tier "defines the institution's learner capacity, storage allowance... capabilities
  not included in a tier are unavailable," but no business rule states what happens when an institution attempts
  to exceed its tier's learner capacity or storage allowance (e.g., enrolling a student that would exceed the
  cap).
answer: >
  The platform blocks the action that would exceed the tier's learner capacity or storage
  allowance (e.g., refuses a new enrolment) until the institution's subscription plan is upgraded. No grace
  overage is provided.

### Q-010 · non-blocking · H5 — Trial duration and default post-trial plan
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Trial duration and default post-trial plan.
context: >
  §12: "A trial may be started without payment details; entitlements revert to the selected plan
  when the trial ends" — no trial length is stated, nor which plan is "the selected plan" (the plan the
  prospect originally chose to trial, or a default free/entry tier).
answer: >
  Standard trial duration is 30 days. If no purchase decision is made by the end of the trial,
  entitlements revert to the entry (lowest-cost) tier rather than automatically billing the institution for the
  plan it trialled.

### Q-011 · non-blocking · H6 — Self-registration approval for sensitive roles
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Self-registration approval for sensitive roles.
context: >
  §2: "Self-registration of a user account against a chosen institution and role" — no business rule
  describes any verification or approval step confirming that the self-registering individual is genuinely
  entitled to the role they select (e.g., could someone self-register as "Principal" or "Teacher" at any
  institution found in the public directory, and gain access immediately)?
answer: >
  Self-registration grants immediate access for all roles, as the BRD currently describes, with
  no additional approval step introduced for this release.

### Q-012 · non-blocking · H7 — Public institution directory vs. institution data isolation
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Public institution directory vs. institution data isolation.
context: >
  §1: "Provide a searchable public directory of institutions to support user self-registration" vs.
  the NFR "Absolute isolation of each institution's data" and Success Criterion "No user is able to view or
  modify data belonging to another institution."
answer: >
  The public institution directory exposes institution name and city/region only, for
  disambiguation purposes. No contact details or full postal address are exposed publicly. This is confirmed as
  an intentional, bounded exception to the "absolute isolation" NFR — it exposes institution existence/identity
  only, never learner, staff, or other institutional business data.

---

## MEDIUM

### Q-013 · non-blocking · M1 — Duplicate institution check: all three fields, or any match?
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  Duplicate institution check: all three fields, or any match?
context: >
  §1: "duplicate registrations of the same institution (same name, contact email and postal code)
  are rejected."
answer: >
  All three fields (name, contact email, postal code) must match an existing institution for a
  new registration to be rejected as a duplicate.

### Q-014 · non-blocking · M2 — Reversibility of Student withdrawal and Teacher removal
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  Reversibility of Student withdrawal and Teacher removal.
context: >
  §1 states institution withdrawal is explicitly "a reversible deactivation... never destroyed."
  §3 (Student) and §4 (Teacher) describe "withdraw"/"remove" without stating whether these are reversible in the
  same way, or how historical attendance/class-assignment references remain valid after withdrawal/removal.
answer: >
  Student withdrawal and Teacher removal are reversible deactivations, consistent with the
  Institution withdrawal model. Historical attendance and class-assignment records remain intact and attributable
  regardless of the current active/withdrawn status of the student or teacher.

### Q-015 · non-blocking · M3 — "Leave" attendance outcome vs. the out-of-scope Leave workflow
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  "Leave" attendance outcome vs. the out-of-scope Leave workflow.
context: >
  §6 Attendance defines outcome values "Present, Absent, Late or Leave." Out of Scope explicitly
  excludes "Leave application, approval workflow and leave-balance tracking."
answer: >
  Confirmed: "Leave" is a plain attendance-outcome marker recorded directly by a teacher or
  institution leadership on the attendance record for a given person and date. It carries no underlying
  leave-request, approval workflow, or leave-balance tracking — those remain out of scope as stated.

### Q-016 · non-blocking · M4 — Collaboration room passcode distribution
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  Collaboration room passcode distribution.
context: >
  §8: "A room may only be joined by presenting the correct passcode" — no business rule describes
  how the passcode reaches its intended participants.
answer: >
  The room's passcode is shared out-of-band by the room's creator (e.g. verbally, via an
  existing communication channel outside the platform); the platform does not display the passcode in the room
  list to browsing users.

### Q-017 · non-blocking · M5 — Retention of previously generated reports
severity: medium
status: answered
answered_by: Jayesh Idnani
question: >
  Retention of previously generated reports.
context: >
  §9: "View when each report was last generated and access previously generated reports" — no stated
  retention period for how long generated report snapshots remain accessible.
answer: >
  Previously generated report snapshots are retained indefinitely and remain accessible, subject
  only to the platform's general data retention rules (see Q-002/C2).

---

## LOW / hand-off to Tech Design

These remain flagged for completeness but are technical-figure decisions, not business ones — per operating
principles they are left for the Tech Design Agent's pass rather than decided here. Not formatted as question
blocks (no severity blocks this stage), carried forward as plain notes:

- **L1 — Password minimum complexity policy:** §2 references a "minimum complexity policy" without specifying
  it. Hand-off: Tech Design.
- **L2 — Session expiry duration:** §2 references sessions expiring "after a defined period" without stating
  the period. Hand-off: Tech Design.
- **L3 — Message length bound:** §8 states message length "is bounded" without a figure. Hand-off: Tech Design.
- **L4 — Institution configuration audit retention:** §11 states configuration changes "are recorded for
  audit" — governed by the same audit retention rule as the rest of the platform (NFR Auditability); no
  separate business decision needed.

---

## Summary

| Severity | Count | Answered |
|----------|-------|----------|
| Critical | 5 | 5/5 |
| High | 7 | 7/7 |
| Medium | 5 | 5/5 |
| Low (hand-off) | 4 | n/a |

**Gate 1 status: UNBLOCKED for Phase 2.** All Critical, High and Medium questions have a Final Answer and a
named Answered by (Jayesh Idnani). Proceed to Phase 2: merge these answers into `final-brd.md` and
`requirements.yaml`.
