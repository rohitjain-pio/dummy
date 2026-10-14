<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
generated: 2026-09-11
-->

# 02 — Requirements Spec — `attendance`

Traces: BRD §6.6 · ENT-008 · ROLE-002–005. Design trace: Tech Design §3.6, §5.3, §5.9, §9, §10.

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-030 | FR-030 | Teacher selects a class, marks daily attendance for the complete roster in one session | `PUT /api/v1/attendance` bulk-upserts all roster rows for `(class_id, date)` in one call |
| SPEC-FR-031 | FR-031 | Outcome ∈ {Present, Absent, Late, Leave} per learner; "Leave" is a plain marker only | `AttendanceRecord.Outcome` is a closed 4-value enum; no `LeaveRequest`/approval/balance entity exists anywhere in the schema |
| SPEC-FR-032 | FR-032 | Leadership records staff attendance, same outcome set | Same endpoint, `person_type=Teacher`, caller role ∈ {Administrator, Principal, SchoolInCharge} |
| SPEC-FR-033 | FR-033 | Teacher self-corrects own entries until end of attendance day, no approval | `PUT /api/v1/attendance/{id}` succeeds directly when `today == attendance_date` and caller is the original recording teacher |
| SPEC-FR-034 | FR-034 | Correction from day+1 onward requires approval (Principal/VP/Academic Administrator) | Same endpoint on `attendance_date < today` creates a `CorrectionRequest` in `Pending` state instead of writing directly; only an approver role can resolve it |
| SPEC-FR-035 | FR-035 | Record locked to correction after 30 calendar days (fixed platform constant, `CFG-004`) | Any correction attempt where `today - attendance_date > 30` returns `ERR-3xx "locked"`, no `CorrectionRequest` created |
| SPEC-FR-036 | FR-036 | Leadership removes an erroneous attendance entry | `DELETE /api/v1/attendance/{id}`, leadership roles only |
| SPEC-FR-037 | FR-037 | Retrieve attendance individually, per class, per date, as institutional list | `GET /api/v1/attendance` with `personId`/`classId`/`date` filters |

## Business rules

| BRL ref | Statement |
|---|---|
| BR-030 | Attendance recorded against one person + one calendar date |
| BR-031 | One outcome per person per date; repeat submission updates (upsert, D10) |
| BR-032 | Attendance only for learners/staff of caller's own institution |
| BR-033 | Student attendance: teachers + leadership; staff attendance: leadership only |
| BR-034 | Roster opens with every learner defaulted to **not-present** (unmarked), never silently "Absent" |
| BR-035 | History retained, corrections traceable to the correcting user |

## NFR references

SPEC-NFR-022 (attendance-day peak absorption via bulk upsert), SPEC-NFR-011 (no data loss),
SPEC-NFR-028 (audit).

## Full detail

`07-business-logic/attendance.md` (the correction/lock state machine lives here) ·
`05-api-contracts/attendance.md` · `08-error-catalogue.md` (`ERR-4xx`) ·
`10-process-flows/attendance.md` (PF-006 daily marking, PF-007 correction/approval) ·
`11-ui-spec/attendance.md`.
