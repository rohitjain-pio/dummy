<!-- pipeline-provenance
produced_by: ai-spec-agent (business-logic-writer)
generated: 2026-09-11
-->

# 07 — Business Logic — `_shared` (cross-cutting, applies to every feature file below)

## 1. Institution-scoping enforcement (BR-006, BR-008, BR-009, BR-013, NFR-016)

**IF** a request targets any tenant-owned entity **THEN** the query/command MUST be filtered by
`institution_id == caller.institution_id` **ELSE** (caller role == PlatformOperator AND the call
goes through `PlatformOperatorQueryService`) → cross-tenant read permitted, logged.

**Enforcement layer:** EF Core global query filter (`HasQueryFilter`), applied via a shared
`IEntityTypeConfiguration<T>` base convention so no per-entity omission is possible (Tech Design
T-002). This is a structural control, never a per-handler `if` check.

**Edge cases:**
- A caller's JWT `institution_id` claim and the entity's `institution_id` column both exist but
  a stale/tampered JWT presents a different value than the account's actual row → reject (401/403),
  never trust the claim over a fresh DB read of the account's current institution binding for
  security-sensitive operations (subscription change, withdrawal).
- PlatformOperator's cross-tenant path is used **only** through the named service class — an
  adversarial test (per `company.md` §7) must prove a handler cannot call `IgnoreQueryFilters()`
  directly and succeed.

## 2. Reversible deactivation ("soft delete") pattern (BR-005, D8) — applies to Institution, Student, Teacher, Room, UserAccount

**State machine:**

```
Active --[withdraw/remove/deactivate]--> Inactive (IsActive=false, DeactivatedAtUtc=now)
Inactive --[reactivate]--> Active (IsActive=true, DeactivatedAtUtc=null)
```

- **Guard on deactivate:** none required at the entity level (deactivation is always permitted);
  BR-025's teacher-removal guard (§Academic-records) is a *feature*-level guard layered on top —
  reassign/release active class-teacher/subject assignments first, or the removal call fails with
  `ERR-3xx "has active assignments"`.
- **Guard on reactivate:** none — reactivation restores exactly the deactivated state, no
  re-validation of fields that may have gone stale (e.g. a re-activated institution's contact
  details are not re-validated against BR-004 at reactivation time — only at the next edit).
- **No physical delete path exists** for these five entity types anywhere in the API surface.
  This is enforced by omission (no `DELETE` verb maps to a hard delete for any of the five) —
  a `DELETE` verb where it appears (e.g. `DELETE /classes/{id}`) always resolves to this same
  soft-delete pattern for entities in this list, or a genuine hard delete only for entities NOT
  in this list where the BRD is silent on reversibility (none currently — every deletable entity
  in this release is one of these five, or subject to its own explicit rule, e.g. attendance
  records via BR-036/FR-036 which ARE hard-removable since BR-036 states "remove an erroneous
  entry" with no reversibility language attached).

## 3. Audit logging (D9, NFR-028–033, BR-039, BR-044, BR-059)

**IF** any entity governed by NFR-028 (student/staff/class/attendance/announcement) is
created/amended/removed **THEN** an `AuditLog` row is written **in the same transaction**, via the
EF Core `SaveChanges` interceptor — never by individual handler code.

**AuditLog row shape:** `actor_user_id`, `institution_id`, `action` (Create/Update/Delete/
Withdraw/Reactivate/etc.), `entity_type`, `entity_id`, `before_snapshot` (JSON, nullable on
Create), `after_snapshot` (JSON, nullable on Delete/Withdraw), `timestamp_utc`, `correlation_id`.

**Exception — sign-in/out/permission-denial (NFR-031):** these are not entity changes, so they are
written directly by the auth middleware, not the interceptor. This is the **one** documented
exception to "no handler writes its own audit row" — the interceptor cannot see an event that
never touches `SaveChanges`.

**Adversarial test requirement (company.md §7, binding):** at least one test must attempt to
mutate a governed entity through a path that bypasses the interceptor (e.g. raw SQL in a test
harness) and demonstrate that path is not exposed in production code — the suite may not merely
assert "a normal save produces an audit row."

## 4. Retention & deletion sweep (CMP-001/002/003)

**Decision table:**

| Trigger | Condition | Action |
|---|---|---|
| Student/Guardian/Staff record reaches 7 years post-departure/termination | `DeactivatedAtUtc + 7y <= today` | Secure delete or anonymise personal fields; deletion event itself writes an `AuditLog` row (CMP-003) |
| Institution subscription ends | `Institution.DeactivatedAtUtc` set | Data remains exportable for 90 days (CMP-002), then archived (read-only, no further mutation), then subject to the same 7-year/anonymise rule as above once the underlying retention period completes |
| Audit log entries reach institution's retention period | Same institution-level clock | Anonymise (strip `actor_user_id`/PII from snapshots) rather than delete — audit *history* is retained, only the personal-data content is scrubbed |

**Enforcement:** Hangfire scheduled job, daily (Tech Design §3.5). Never a manual/ad hoc process.

## 5. Validation rule numbering convention

`VAL-` IDs are assigned per field-group within each feature file (e.g. `VAL-001`–`VAL-004` for
Institution fields in `institutions.md`). No `VAL-` number is reused across feature files —
each feature file's `VAL-` block starts where the previous file's ended, per the master range
table in `00-spec-index.md`.
