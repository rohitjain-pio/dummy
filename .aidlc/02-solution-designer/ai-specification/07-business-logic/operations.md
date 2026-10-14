<!-- pipeline-provenance
produced_by: ai-spec-agent (business-logic-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch). **Written back from the P4 build
  (U20):** the Q-223 metric formulas (the suite named four metrics and defined none), the Q-224
  per-role dashboard payloads, and Q-225's `HH:mm` and publish semantics on VAL-023. **The code is
  the source of truth; no code change is implied.** ASM-002 is recorded as moot — no cache was
  built. E2/Q-208 (room as free text) is unchanged and still provisional.
-->

# 07 — Business Logic — `operations`

## BRL — Dashboards / Reporting

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-048 (BR-048) | Institution dashboard requested | scoped strictly to caller's institution | — |
| BRL-049 (BR-049) | Platform-wide summary requested | aggregate, non-identifying figures only, publicly viewable | never per-institution breakdown |
| BRL-050 (BR-050) | Presence figures computed | always reflect current calendar date (institution time zone) | recomputed/invalidated on same-day attendance write, not on a fixed schedule (Tech Design A2) |
| BRL-051 (BR-051) | Report requested | leadership: all classes; Teacher: own classes only | else denied |
| BRL-052 (BR-052) | Report generated | exportable (PDF/CSV, D7) | — |
| BRL-090 (FR-057) | Student-performance report computed | derived **solely** from `AttendanceRecord` percentage | no homework/participation/behavioural field is read, because none exists in the schema |
| BRL-091 (FR-058) | Teacher-performance report computed | derived **solely** from attendance-compliance + submission-timeliness | same constraint |

**Numbering note:** `BRL-090`/`BRL-091` are deliberately outside the `BR-001..066` range — they
encode logic for `FR-057`/`FR-058` (functional requirements), not for a numbered `BR-` business
rule, and `057`/`058` were already taken by unrelated `BR-057`/`BR-058` (institution
configuration scoping, `07-business-logic/configuration-commercial.md`). Using `BRL-057`/`BRL-058`
here would have silently collided with those — flagged and avoided rather than left ambiguous.

## Metric formulas (written back at 1.0.4 — Q-223, built in CT-401/402, U20)

**The suite named four metrics and defined none of them.** These are the formulas the build asked
for and the operator chose; they are implemented once, in a single `AttendanceMetrics` component,
so a report and a dashboard can never disagree about what "attendance %" means.

| Metric | Formula | Notes |
|---|---|---|
| **Attendance %** | `(Present + Late) ÷ (Present + Late + Absent)` | **`Late` counts as present. `Leave` is excluded from the denominator entirely** — it is neither attendance nor absence, so counting it either way misstates the figure. A mutation putting `Leave` in the denominator failed four tests. |
| **Teacher compliance** | `% of (own class × Mon–Fri day in range) that has any record` | Measures *whether the teacher marked*, not what they marked. |
| **Teacher timeliness** | `% of the teacher's records marked on the attendance date`, in **institution time** (`CFG-009` fallback) | A record made after local midnight counts as not on time. |
| **Course analytics** | average attendance % of the classes mapped to each subject | Attendance-derived only, per BRL-090/091. |

**Days with no countable records are omitted from a trend, never reported as 0%** — a school
holiday is not a day of zero attendance.

## Dashboard payload per role (written back at 1.0.4 — Q-224, built in CT-401, U20)

Computed **live**, no cache. *(This makes **ASM-002** — "presence cache invalidates on same-day
attendance writes only" — moot: no cache was built, per Tech Design A2's own preference for
traceable data. Recorded rather than left as a dangling assumption.)*

| Role | Payload |
|---|---|
| **Leadership** | totals, present today, average attendance, 7-day trend, per-class breakdown, gender split, recent and upcoming announcements |
| **Teacher** | the same, **scoped to own classes**, with **no teacher totals** |
| **Student** | own attendance % and today's status, plus announcements |
| **Parent** | per **linked child**: attendance % and today's status, plus announcements. Requires the `ParentAccess` tier (Q-228) |
| **PlatformOperator** | **none** — `403 ERR-102`. API-048's role list has no such variant, and the platform summary (API-049) is the operator's view |

**Structural, not conditional rendering:** serialisation omits a variant's absent members
**entirely** (`WhenWritingNull`) rather than sending them as zero. A Student's raw JSON contains
**none** of the nine institution-wide keys — asserted adversarially on the raw payload, not on a
deserialised object. This is BR-048 enforced at the wire, which is the only place it counts.
**"Recent"** announcements have an effective date ≤ today; **"upcoming"** > today; withdrawn ones
are excluded from both.

## BRL — Scheduling

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-053 (BR-053) | Period scheduled for a teacher | no other period exists for that teacher in the same time slot | reject `ERR-701` "teacher double-booked" |
| BRL-054 (BR-054) | Period scheduled for a class | no other period exists for that class in the same time slot | reject `ERR-702` "class double-booked" |
| BRL-055 (BR-055) | Period scheduled for a room | no other period exists for that room in the same time slot | reject `ERR-703` "room double-booked" |
| BRL-056 (BR-056) | Schedule mutated | caller role ∈ leadership | Teacher/learner: read-only |

## Decision table — schedule conflict check (evaluated together at save time, BR-053/054/055)

| Teacher conflict | Class conflict | Room conflict | Result |
|---|---|---|---|
| N | N | N | Save succeeds |
| Y | — | — | Reject — teacher conflict (checked first) |
| N | Y | — | Reject — class conflict |
| N | N | Y | Reject — room conflict |

All three checks run in the same validation pass — a single save attempt reports **all**
conflicts found, not just the first, so the user isn't sent through three round-trips to
discover three separate problems.

## VAL — field validation

| VAL ref | Field | Rule |
|---|---|---|
| VAL-021 | `report.date_range` | `from <= to`; both required |
| VAL-022 | `report.format` | ∈ {PDF, CSV} — matches D7. **`PDF` is accepted by validation and then refused by the endpoint with `ERR-901` (Q-226)** — CSV only in this release, a recorded D7/NFR-027 deviation, **U21 open** |
| VAL-023 | `timetable_period.day_of_week` + `time_slot` | Required; combination is the conflict-check key (BRL-053–055). **`time_slot` is a 24-hour `HH:mm` start time** (Q-225, written back 1.0.4) — `"9am"`, `"9:00"` and `"24:00"` are all rejected with `ERR-901`. Class, subject and teacher must each exist **in the caller's institution** (PF-011's pre-condition), else `ERR-901` |

## Edge-case register

| # | Case | Resolution |
|---|---|---|
| E1 | A report is requested for a report-type/scope combination that yields zero underlying data (e.g. brand-new institution, no attendance yet) | Report still generates — an empty/zero-value report, not an error; matches NFR-004's "meaningful empty state" applied to a generated artifact rather than a live screen |
| E2 | A teaching room is deleted (Tech Design has no explicit "Room" master-data entity beyond a text field on `TimetablePeriod` per FR-060) | `[NEEDS ANSWER — Q-208]` — the BRD names "teaching room" only as an attribute of a period (FR-060), never as its own manageable master-data entity with a delete lifecycle; provisional choice: room is a free-text/reference field on `TimetablePeriod`, not a first-class entity, so "deleting a room" isn't an operation this release supports at all — conflicts are checked against the room *value*, not a room *record*. Confirm before any UI implies room management as a separate CRUD screen. |
