<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
generated: 2026-09-11
-->

# 02 — Requirements Spec — `operations`

Traces: BRD §6.9 (Dashboards/Reporting), §6.10 (Scheduling) · ENT-013/014 · ROLE-002–005. Design
trace: Tech Design §5.4, §9 (A2), §7.

## Dashboards, Reporting, Analytics (FR-052–058)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-052 | FR-052 | Institution dashboard: total students/teachers/classes, present-today counts | `GET /api/v1/dashboard` scoped to caller's institution (BR-048) |
| SPEC-FR-053 | FR-053 | Platform-wide public summary (institutions/students/teachers/classes) | `GET /api/v1/platform/summary` — anonymous, aggregate-only (BR-049) |
| SPEC-FR-054 | FR-054 | Leadership + Teachers generate standard reports (class attendance, student performance, course activity, teacher performance) | `POST /api/v1/reports` enqueues a Hangfire job; Teacher scope limited to own classes (BR-051) |
| SPEC-FR-055 | FR-055 | View last-generated time, access prior snapshots, retained indefinitely | `GET /api/v1/reports` lists `ReportRun` rows; no expiry job removes them outside the general CMP-001/002 sweep |
| SPEC-FR-056 | FR-056 | Dashboard presents headline indicators (e.g. average attendance) | Included in `GET /api/v1/dashboard` payload |
| SPEC-FR-057 | FR-057 | Student performance = attendance percentage **only** | `ReportRun` "Student Performance" type computes solely from `AttendanceRecord`; **no homework/participation field exists anywhere in the schema to compute from** |
| SPEC-FR-058 | FR-058 | Teacher performance = attendance compliance + submission timeliness **only** | Same constraint — computed from `AttendanceRecord.RecordedAtUtc` vs. the school day, not from any assignment-management or engagement source |

## Scheduling (FR-059–062)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-059 | FR-059 | Maintain a weekly timetable of periods by day/time slot | `POST/PUT /api/v1/timetable` |
| SPEC-FR-060 | FR-060 | Record class/subject/room/teacher per period | Fields on `TimetablePeriod` |
| SPEC-FR-061 | FR-061 | Publish class/exam schedules to staff and learners | `POST /api/v1/timetable/{id}/publish` |
| SPEC-FR-062 | FR-062 | Staff/learners view timetable by class and by teacher | `GET /api/v1/timetable?classId=`, `?teacherId=` |

## Business rules

| BRL ref | Statement |
|---|---|
| BR-048 | Institution dashboards scoped strictly to caller's institution |
| BR-049 | Platform-wide figures are aggregate, non-identifying, public |
| BR-050 | Presence figures always reflect current calendar date |
| BR-051 | Reports: leadership full access; Teacher — own classes only |
| BR-052 | Report outputs exportable for offline distribution |
| BR-053 | A teacher not scheduled to two periods in the same time slot |
| BR-054 | A class not scheduled to two periods in the same time slot |
| BR-055 | A teaching room not double-booked in the same time slot |
| BR-056 | Schedules maintained by leadership, read-only to teachers/learners |

## Explicitly excluded from this slug (validation-required mockup artifacts — do not build)

Per the operator's binding constraint carried into this stage: the Reports screen's **"Student
Satisfaction"** and **"Course Completion"** gauges (`ui-design-final.md` §11) trace to no FR/BR —
Q-H3 explicitly dropped homework/engagement-style metrics from scope. **No `SPEC-FR-` row exists
for either gauge, and `11-ui-spec/operations.md` must not render them.** Only the
attendance-vs-performance chart, grade-distribution (pass/fail-rate) donut, attendance-by-class
and subject-engagement-by-attendance charts are built, because those trace to FR-054/056/057.

## NFR references

SPEC-NFR-007 (dashboard <3s), SPEC-NFR-027 (export formats), SPEC-NFR-028 (audit on schedule
changes — treated as an institution-configuration-adjacent mutation).

## Full detail

`07-business-logic/operations.md` · `05-api-contracts/operations.md` ·
`08-error-catalogue.md` (`ERR-7xx`) · `10-process-flows/operations.md` (PF-010 report generation,
PF-011 schedule-conflict validation) · `11-ui-spec/operations.md` (Dashboard, Reports, Schedule).
