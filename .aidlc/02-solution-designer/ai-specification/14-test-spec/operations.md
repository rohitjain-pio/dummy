<!-- pipeline-provenance
produced_by: ai-spec-agent (test-spec-writer)
generated: 2026-09-11
-->

# 14 — Test Spec — `operations`

| TC ref | Layer | Given | When | Then | Trace | Adversarial? |
|---|---|---|---|---|---|---|
| TC-601 | Integration | Student-performance report requested | Generation runs | Computed solely from `AttendanceRecord`; no homework/participation column read | FR-057 | **Yes** — fails if any non-attendance source is queried |
| TC-602 | Integration | Teacher-performance report requested | Generation runs | Computed solely from attendance compliance/timeliness | FR-058 | **Yes** |
| TC-603 | Integration | Teacher requests a report for a class not their own | `POST /reports` | Denied/scoped out (BR-051) | BRL-051 | **Yes** |
| TC-604 | E2E | Report generation job fails all retries | Job exhausts Hangfire retry policy | `ReportRun` marked `Failed`, surfaced to ops alerting, not silently dropped | NFR-011 (async) | **Yes** |
| TC-605 | Unit | Teacher already scheduled at (Mon, 9am) elsewhere | New period submitted for same teacher/slot | `ERR-701` | BRL-053 | **Yes** |
| TC-606 | Unit | Class already scheduled at (Mon, 9am) | New period submitted for same class/slot | `ERR-702` | BRL-054 | **Yes** |
| TC-607 | Unit | Room already booked at (Mon, 9am) | New period submitted for same room/slot | `ERR-703` | BRL-055 | **Yes** |
| TC-608 | Unit | A submission conflicts on teacher AND room simultaneously | Submitted | Both `ERR-701` and `ERR-703` reported in one response | Decision table | **Yes** — fails against an implementation that stops at the first conflict found |
| TC-609 | Contract | Reports screen API response | `GET /dashboard` / `GET /reports` payload inspected | No "Student Satisfaction"/"Course Completion" field exists anywhere in the response schema | `01-source-catalogue.md` §5 | **Yes** — fails if either untraced metric is present in the API surface at all |
