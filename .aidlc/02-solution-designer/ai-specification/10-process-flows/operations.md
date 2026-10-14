<!-- pipeline-provenance
produced_by: ai-spec-agent (process-flow-writer)
generated: 2026-09-11
-->

# 10 — Process Flows — `operations`

## PF-010 — Report generation

**Trigger:** Leadership or Teacher requests a report.
**Actors:** Administrator/Principal/SchoolInCharge (all classes), Teacher (own classes, BR-051).
**Pre-conditions:** caller authorized for the requested scope.
**Post-conditions:** `ReportRun` row `Ready`, blob written, retained indefinitely (FR-055).

**Steps:**
1. Caller submits report type/date-range/scope/format (API-050) → `202 Accepted`, `ReportRun`
   created `Pending`.
2. Hangfire job computes the report **solely** from `AttendanceRecord` data for
   Student/Teacher-performance types (FR-057/058 — no other source table is read for these two
   types).
3. Job writes the output to Blob Storage, updates `ReportRun` to `Ready` with the blob pointer.
4. Client is notified (poll or SignalR notice, assumption A1) and can download (API-052).

**Exception flows:**
- Job fails → `ReportRun` marked `Failed`, retried per Hangfire policy, surfaced to ops alerting
  if retries exhaust (never silently dropped, NFR-011 extended to async).

**Exercised by:** `TC-601`–`TC-604`.

## PF-011 — Schedule authoring with conflict validation

**Trigger:** Leadership adds/edits a timetable period.
**Actors:** Administrator/Principal/SchoolInCharge.
**Pre-conditions:** class, subject, teacher exist.
**Post-conditions:** period saved with no teacher/class/room conflict, or rejected with all
conflicts named in one response.

**Steps:**
1. Leadership submits a period (day, time slot, class, subject, teacher, room) via API-053.
2. System checks all three conflict dimensions in one pass (decision table,
   `07-business-logic/operations.md`).
3. If clear, period is saved.
4. Leadership publishes the schedule (API-054) — now visible to staff/learners (read-only,
   BR-056).

**Exception flows:**
- Any of the three conflicts trip → save rejected, **all** tripped conflicts reported together
  (`ERR-701`/`702`/`703` as applicable, not just the first found).

**Exercised by:** `TC-605`–`TC-609` (each conflict type individually, plus a combined-conflict
case proving all three are reported in one response).
