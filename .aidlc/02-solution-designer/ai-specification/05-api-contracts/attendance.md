<!-- pipeline-provenance
produced_by: ai-spec-agent (contract-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch). **Written back from the P2 build
  (U11)** — API-031's route, the day-30 correction boundary, API-035's lock rule and API-036's
  response shape. These describe behaviour CT-201/202/203 already built and tested; **the code is
  the source of truth and no code change is implied.**
-->

# 05 — API Contracts — `attendance`

## API-031 — Get roster for marking

`GET /api/v1/attendance/roster?classId={id}&date={date}` — Auth: Teacher (own class) + leadership.

> **Route corrected at 1.0.4 (Q-217, U11).** This contract and API-014 both claimed
> `GET /classes/{id}/roster`, and the host rejected the duplicate at **runtime** with
> `AmbiguousMatchException` — it did not show at compile time. API-031 moved; **API-014 is
> unchanged**. Recorded here so the collision cannot be reintroduced by someone reading the old
> text.

**"Own class"** for a Teacher = **class teacher of that class OR holding any subject mapping for
it** (PF-006's pre-condition, verbatim).
**Success:** `200` → `[{studentId, name, currentOutcome: null}]` — `currentOutcome: null` means
NotYetMarked (BRL-034), never defaults to `"Absent"` in the payload.
**Errors:** `ERR-102` (403, a class the Teacher does not teach), `ERR-902` (404, a class in
another institution — never 403, per the no-enumeration rule).

## API-032 — Bulk mark/upsert attendance

`PUT /api/v1/attendance` — Auth: Teacher (Student records, own class) + leadership (Student or
Teacher records).
**Request:** `{ "classId": "guid", "date": "date", "entries": [{"personId","personType","outcome"}] }`
**Validation:** VAL-013/014/015.
**Success:** `200` → per-entry result; upsert semantics (BRL-031, D10) — no duplicate rows on
resubmission.
**Errors:** `ERR-401` (locked, 423), `ERR-402` (needs approval — see API-033), `ERR-403` (future
date), `ERR-901`.
**Idempotency:** the `(personId, attendanceDate)` unique constraint itself makes this endpoint
naturally idempotent — no separate `Idempotency-Key` needed.
**Example:** teacher marks 30 students Present/Absent/Late/Leave in one call for today → `200`,
30 rows upserted in one transaction (satisfies NFR-022's bulk-write pattern).

## API-033 — Request a correction (day 1–30)

> **Window corrected at 1.0.4 (Q-214, U11).** The heading previously read "day 1–29". **Day 30 is
> still correctable** — the lock applies at `> 30` days (`CFG-004`), so the correctable window is
> days 1–30 inclusive and day 31 is the first locked day. The `>29` reading was an off-by-one that
> the decision table never had; CT-202 tested days 1, 29, 30 and 31, and a mutation from `>30` to
> `>=30` failed five tests.

`POST /api/v1/attendance/{id}/correction-requests` — Auth: Teacher or leadership.
**Request:** `{ "requestedOutcome": "Present|Absent|Late|Leave", "reason": "string" }` — `reason`
is **required** (PF-007 step 4).
**Success:** `201` → a `CorrectionRequest` row in `Pending` (§2.6a; see
`07-business-logic/attendance.md` state machine). Leadership's own request applies immediately per
Q-205 (confirmed final rule) and is recorded as **`AppliedDirectly`**, not `Pending`. On **day 0**
the original recorder or leadership likewise gets `AppliedDirectly`; a non-recorder Teacher gets
`403 ERR-102` (SPEC-FR-033 names the *original recording teacher*).
**Errors:** `ERR-401` (423, > 30 days), `ERR-901` (an open request already exists — edge case E3;
no dedicated `ERR-` is minted for it, per the catalogue's own fallback rule).

## API-034 — Approve/reject a correction request

`POST /api/v1/attendance/correction-requests/{id}/approve` · `.../reject` — Auth: Principal/
SchoolInCharge/Administrator only (Q-204, confirmed role mapping).
**Request (reject):** `{ "reason": "string" }` — **required** (PF-007 step 4).
**Success:** `200`.
**Errors:** `ERR-401` (423 — the lock is **re-checked at the moment of action**, and a request
found past it is auto-rejected in the same call, Q-206/Q-212), `ERR-901` (the request is already
resolved).
**Side effect (written back 1.0.4):** a leadership **direct** correction while a Teacher's request
is open closes that request as **`Superseded`** — otherwise a later approval would overwrite
leadership's newer outcome with a stale one.

## API-035 — Remove an attendance entry

`DELETE /api/v1/attendance/{id}` — Auth: leadership only.
**Success:** `204`.
**Errors:** `ERR-401` (423 — *written back 1.0.4*: a locked entry cannot be removed either;
"Locked (terminal — no further mutation by anyone)" covers removal), `ERR-902` (404).
**Side effect:** the record's `CorrectionRequest` rows are **loaded and removed explicitly, each
audited** — not left to the FK cascade, which on SQL Server would delete them where the audit
interceptor never sees them (found and fixed in CT-203).

## API-036 — Retrieve attendance

`GET /api/v1/attendance?personId=&classId=&date=` — Auth: leadership + Teacher (own class) +
Student/Parent (own/linked, via `/api/v1/attendance/me` and `/api/v1/attendance/linked`
narrower routes — see `academic-records.md` API-019 pattern).
**Success (shape written back at 1.0.4 — the contract specified none):** `200` →
`[{id, personId, personType, attendanceDate, outcome, recordedByUserId, recordedAtUtc,
correctionStatus, locked}]`. **`locked` is derived at read time** from `attendanceDate` +
`CFG-004`, never stored (§2.6).
**Scoping rules written back:** a **Teacher must name a class they teach** — "own class" leaves no
institution-wide grant, and **staff attendance records never appear to a Teacher**. `classId`
filters on the student's **current** placement (the record carries no class column; §2.6's index
is `class_id_via_student`).
**Tier gate:** `/attendance/linked` additionally requires `ParentAccess` (Q-228, `09-security-spec.md`
§2 Axis 2) — a P1–P4 retrofit applied in CT-503.
