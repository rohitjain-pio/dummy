<!-- pipeline-provenance
produced_by: ai-spec-agent (business-logic-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch). **Written back from the P2 build
  (U11):** the correction window corrected from "day 1–29" to **day 1–30** (Q-214 — it
  contradicted the `>30` lock guard on the adjacent line), the `CorrectionRequest` table and its
  six statuses (Q-213), the time-zone fallback `CFG-009` (Q-215), the twice-enforced lock (Q-212)
  and edge cases E4/E5. **The code is the source of truth for all of these**; CT-201–203 built and
  tested them and no code change is implied. Nothing in the constitution's #4 invariant changed:
  `CFG-004` is still 30 and still platform-fixed.
-->

# 07 — Business Logic — `attendance`

This is the suite's most consequential state machine — the attendance correction/lock lifecycle
(FR-033/034/035) is a **fixed platform-wide invariant** per `constitution.md` §2.4. Nothing here
is institution-configurable.

## BRL — business rule logic

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-030 (BR-030) | Attendance recorded | tied to exactly one person + one calendar date | — |
| BRL-031 (BR-031) | A second submission arrives for the same (person, date) | update (upsert) the existing record | never insert a duplicate row |
| BRL-032 (BR-032) | Attendance recorded | for a learner/staff member of the caller's own institution | else denied |
| BRL-033 (BR-033) | Attendance submitted for a Student | caller role ∈ {Teacher, leadership} | for a Teacher (staff) record — caller role ∈ leadership **only**, Teacher cannot record their own or a peer's attendance |
| BRL-034 (BR-034) | A roster is opened for marking | every row defaults to **NotYetMarked** (a fourth, non-recorded UI state — distinct from the 4 recordable outcomes) | never silently defaults to "Absent" |
| BRL-035 (BR-035) | Attendance corrected | traceable to the correcting user, retained in history | — |

## State machine — attendance record correction lifecycle (FR-030–035, the constitution's #4 invariant)

```
NotYetMarked --[teacher marks Present|Absent|Late|Leave]--> Recorded (day 0)
Recorded (day 0, same day) --[teacher self-corrects]--> Recorded (day 0, updated, no approval)
Recorded (day 0) --[end of attendance day passes]--> Recorded (day 1-30, correction-restricted)
Recorded (day 1-30) --[teacher or leadership requests a correction]--> CorrectionPending
CorrectionPending --[Principal/VP/Academic-Admin approves]--> Recorded (updated, day N, approval logged)
CorrectionPending --[Principal/VP/Academic-Admin rejects]--> Recorded (unchanged, rejection logged)
Recorded (any day) --[30 calendar days elapse since attendance_date]--> Locked (terminal — no further mutation by anyone)
```

**Guards, precisely:**
- **The institution's time zone is an IANA id** on `InstitutionConfiguration.time_zone` (VAL-024).
  **With no configuration row, or an unresolvable id, the fallback is `CFG-009` = `Asia/Kolkata`**
  — *written back at 1.0.4 (Q-215, built in CT-201)*. This is load-bearing rather than cosmetic:
  a UTC fallback pushes a teacher marking at 11:58pm local into the wrong attendance day and
  therefore into the approval-required branch.
- "Same day" = `today == attendance_date` in the institution's configured time zone (FR-063) —
  the attendance day's boundary is midnight in that time zone, not UTC midnight, since a
  teacher marking at 11:58pm and correcting at 12:02am local time must not be silently pushed
  into the approval-required branch by a UTC/local mismatch.
- **"Day 1–30" = `1 <= (today - attendance_date) <= 30` calendar days.** *(Corrected at 1.0.4 —
  Q-214, U11. This read "day 1–29", which contradicted the "Locked" guard on the very next line:
  if the lock is `> 30`, day 30 is correctable. Two adjacent lines specified an off-by-one against
  each other. The decision table below and `CFG-004` were always right; this line was not. CT-202
  tested days 1, 29, 30 and 31 and a `>30` → `>=30` mutation failed five tests, so the built
  behaviour is `1–30`.)*
- "Locked" = `(today - attendance_date) > 30` calendar days — **so day 31 is the first locked
  day** — the boundary is a **fixed platform
  constant (`CFG-004` = 30)**, never read from `InstitutionConfiguration` even though FR-064's UI
  exposes institution-level settings elsewhere (constitution.md §2.11 — do not let a future
  maintainer wire this to a per-institution setting).
- Only Principal, Vice Principal, or Academic Administrator may resolve a `CorrectionPending`
  request (FR-034) — **note the role-name gap**: BR-007's closed role set has no distinct "Vice
  Principal" or "Academic Administrator" role; both are named only in FR-034's text.
  **Q-204, ANSWERED** (operator-confirmed, Jayesh Idnani, 2026-09-11): "Administrator, Principal,
  and SchoolInCharge are the valid approval roles." FR-034's "Principal, Vice Principal, or
  Academic Administrator" wording is satisfied by any of the existing leadership roles
  (Administrator, Principal, SchoolInCharge); no new role is added to the closed BR-007 set. This
  is final doctrine, not a provisional choice.
- `Locked` is terminal for **every** role, including PlatformOperator acting on a specific
  institution's data — the lock is a data-integrity rule, not a permission the PlatformOperator's
  administrative reach overrides.

## Decision table — can this attendance record be corrected right now?

| Days since attendance_date | Caller role | Result |
|---|---|---|
| 0 (same day) | Teacher (original recorder) | Direct correction, no approval |
| 0 (same day) | Leadership | Direct correction, no approval (leadership can always record/correct per BR-033) |
| 1–30 | Teacher | Creates a `Pending` `CorrectionRequest` (§2.6a), awaits leadership approval |
| 1–30 | Leadership (Administrator/Principal/SchoolInCharge) | Applies immediately, no pending/approval step — **Q-205, ANSWERED** (operator-confirmed, Jayesh Idnani, 2026-09-11): "Leadership corrections apply immediately and do not require self-approval." Final rule, not provisional. Recorded as `AppliedDirectly`. If a Teacher's request is open, it closes as `Superseded`. |
| 0 | Teacher who is **not** the original recorder | `403 ERR-102` — *written back 1.0.4*. SPEC-FR-033 names the *original recording teacher*; a peer may not overwrite someone else's same-day outcome. |
| > 30 (i.e. day 31+) | Any role, **including PlatformOperator** | Rejected `ERR-401` "locked". Removal (API-035) is refused too — Locked is terminal for every mutation, not only for correction. |

**Past-day bulk write (API-032) by a Teacher** → `ERR-402`, per the catalogue's own trigger text:
the pending flow is the only route, and a direct write bypassing it is the error. Leadership
applies directly on days 1–30 (Q-205). *Written back at 1.0.4 from CT-201.*

## VAL — field validation

| VAL ref | Field | Rule |
|---|---|---|
| VAL-013 | `outcome` | ∈ {Present, Absent, Late, Leave} — closed set, no free text |
| VAL-014 | `attendance_date` | Not in the future (cannot pre-mark tomorrow's attendance) |
| VAL-015 | `person_id` + `person_type` | Must resolve to an active Student or Teacher of the caller's institution |

## Edge-case register

| # | Case | Resolution |
|---|---|---|
| E1 | A teacher marks a roster, then the student is withdrawn (FR-014) before day-30 lock | The attendance record remains correctable per the normal state machine — student's active/withdrawn status does not change the record's own lock timeline (BR-014's "historical...records remain intact and attributable regardless of the student's active/withdrawn status") |
| E2 | A `CorrectionPending` request is still open when the lock is reached | **Q-206, ANSWERED** (operator-confirmed, Jayesh Idnani, 2026-09-11): "Pending correction requests automatically expire/reject when the 30-day lock is reached." The lock check applies to the *original* `attendance_date`, so an unresolved pending request is auto-rejected (not silently approved) once **day 31** arrives, and the record becomes Locked — an approver cannot approve a request after the record has locked. Final rule, not provisional. **Enforced twice (Q-212's "both", built as such):** the daily `attendance-lock-sweep` job sets `AutoRejected`, **and** approve/reject re-check the lock at the moment of action and auto-reject there too — so a missed or delayed sweep cannot let a stale approval through. |
| E3 | Two correction requests submitted for the same record before the first is resolved | Second request rejected — only one open request per record at a time. **Enforced in the database** by the filtered unique index on `attendance_record_id WHERE status='Pending'` (§2.6a), not only in application logic. Returns `ERR-901`: no dedicated code is minted, per the catalogue's own fallback rule. *(Written back 1.0.4.)* |
| E4 | Leadership writes a direct correction while a Teacher's request is open | The open request closes as **`Superseded`** (§2.6a). *Written back at 1.0.4 from CT-202: without this, a later approval would overwrite leadership's newer outcome with a stale one.* |
| E5 | An attendance record is removed while it has correction requests | The requests are loaded and removed **explicitly in application code, each audited** — never left to the FK cascade, which on SQL Server deletes rows the audit interceptor never observes, losing the request history with no audit row. *Written back 1.0.4 (a real defect found and fixed by a test in CT-203).* |
