<!-- pipeline-provenance
produced_by: ai-spec-agent (contract-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch). **Written back from the P4 build
  (U20):** API-050's body shape and allowed values, API-051/052's scoping and the 409-before-Ready
  rule, API-053's multi-conflict `{errors:[…]}` body, API-054's period-level publish semantics and
  `HH:mm`. **The code is the source of truth; no code change is implied.** U21 (PDF deferral) and
  U33 (FR-064 has no API) are referenced but **remain open and are not resolved here**.
-->

# 05 — API Contracts — `operations`

## Dashboards / Reporting

**API-048** `GET /api/v1/dashboard` — Auth: leadership + (a narrower Student/Parent variant per
`ui-design-final.md` §3, served by the same route with role-conditional payload shape). Success
`200`, p95 <3s (NFR-007).
**API-049** `GET /api/v1/platform/summary` — Auth: anonymous (BR-049 aggregate/public).
**API-050** `POST /api/v1/reports` — Auth: leadership (all classes) + Teacher (own classes,
BR-051). Success `202 Accepted` (async — Hangfire enqueue, A1) → `{reportRunId}`.

**Request body (written back at 1.0.4 — U20; the contract gave field names but no shapes):**
```json
{ "reportType": "ClassAttendance|StudentPerformance|CourseAnalytics|TeacherPerformance",
  "dateRange": { "from": "date", "to": "date" },
  "scope":     { "classId": "guid|null" },
  "format":    "CSV" }
```
- `from <= to` (VAL-021), else `ERR-901`.
- **`format`: `CSV` only in this release.** `PDF` is a valid enum value but is **refused with
  `ERR-901`** rather than silently served as CSV — a recorded D7/NFR-027 deviation (Q-226,
  **U21, still open**). *(U21 is out of scope for this patch; recorded here only so the contract
  matches what the endpoint does. **The refusal wording is poor** — a user asking for a PDF is
  told they made a mistake — but fixing that needs a new `ERR-`, which belongs with U21.)*
- **A Teacher** may request only a class they teach; `scope.classId: null` (whole school) is
  refused for a Teacher, and their `TeacherPerformance` report covers **themselves only**.
- An untraced `reportType` → `ERR-901`. **No "Student Satisfaction" or "Course Completion" type
  exists** — see below.

**API-051** `GET /api/v1/reports` · `GET /api/v1/reports/{id}` — list/detail of `ReportRun`,
retained indefinitely (FR-055). **A Teacher sees only runs they requested**; leadership sees all.
**API-052** `GET /api/v1/reports/{id}/download` — returns the blob (or a redirect to a
time-limited blob URL).
**Errors (written back 1.0.4):** **`409 ERR-901` when the run is `Pending` or `Failed`** — there
is no file to return, and the closed catalogue has no "not ready" code, so it falls back per the
catalogue's own rule rather than minting one. `ERR-902` (404, unknown run or another
institution's).

**Report types built (traced):** Class Attendance (FR-054), Student Performance — attendance % only
(FR-057), Course Analytics/Activity (FR-054, attendance-derived), Teacher Performance — attendance
compliance/timeliness only (FR-058). **No "Student Satisfaction" or "Course Completion" report
type exists** — untraceable to any FR/BR (`01-source-catalogue.md` §5).

## Scheduling

**API-053** `POST /api/v1/timetable` · `PUT /api/v1/timetable/{id}` — Auth: leadership. Errors:
`ERR-701`/`702`/`703` (all three checked in one pass, `07-business-logic/operations.md` decision
table), `ERR-901` (unknown class/subject/teacher, or a `timeSlot` failing `HH:mm`).

**Multi-conflict 409 body (written back at 1.0.4 — U20).** One save can trip up to **three**
`ERR-` codes, but the standard error shape carries one `{errorCode, message, traceId}`. A conflict
therefore returns:
```json
{ "errors": [ {"errorCode":"ERR-701","message":"…"}, {"errorCode":"ERR-703","message":"…"} ] }
```
Each entry is a **verbatim catalogue body** — no error is re-worded here — in **teacher → class →
room** order. This is the one place in the suite where an error response is an array, and it
exists because the decision table's whole point is that a single save reports *all* conflicts
rather than sending the user through three round-trips. A mutation stopping at the first conflict
failed two tests in CT-403.
**Room matching:** case- and whitespace-insensitive, so the application check and the
partial-unique index backstop agree with SQL Server's default collation. Room remains free text
(Q-208, unchanged).

**API-054** `POST /api/v1/timetable/{id}/publish` — Auth: leadership. **`{id}` is a *period* id**
(Q-225): publish state is per period, not per timetable (§2.9). Publishing twice is a no-op; an
edit preserves the publish state.
**API-055** `GET /api/v1/timetable?classId=` · `?teacherId=` — Auth: any authenticated user
(read), leadership (maintain). **Non-leadership callers see only *published* periods** — a draft
is invisible to Teacher, Student and Parent until published.
**No delete endpoint exists for a period**, because none is specified; none was built. **Exam
schedules (FR-061) are not modelled this release** (Q-225) — recorded, not silently dropped.

## Idempotency

`POST /reports` accepts `Idempotency-Key` — a retried report-generation request with the same key
returns the existing `reportRunId` rather than enqueuing a duplicate job.
