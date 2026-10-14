<!-- pipeline-provenance
produced_by: ai-spec-agent (business-logic-writer)
generated: 2026-09-11
-->

# 07 — Business Logic — `academic-records`

## BRL — Students

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-017 (BR-017) | Student created/placed | belongs to exactly one institution, placed in exactly one class at any time | reassignment replaces the prior placement, never adds a second |
| BRL-018 (BR-018) | Student enrolled | (SR number, roll number) each unique within institution | reject duplicate `ERR-301` |
| BRL-019 (BR-019) | Attendance recorded for a student | student has a class placement | reject `ERR-302` "no class placement" |
| BRL-020 (BR-020) | Student record accessed | caller is authorised staff of the same institution | else denied |
| BRL-021 (BR-021) | Guardian contact fields accessed | same access rule as the student record itself (no separate, looser rule) | — |

## BRL — Teachers

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-022 (BR-022) | Teacher registered | belongs to exactly one institution | — |
| BRL-023 (BR-023) | Class-teacher assignment made | class has ≤1 class teacher AND teacher is class-teacher of ≤1 class | reject the second assignment `ERR-303` until the first is released |
| BRL-024 (BR-024) | Subject-teaching assignment made | teacher may hold multiple (subject, class) pairs | — |
| BRL-025 (BR-025) | Teacher removal requested | teacher holds no active class-teacher/subject assignment | reject `ERR-304` "reassign or release existing assignments first" |

## BRL — Class/Subject

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-026 (BR-026) | Class created | (name, section) unique within institution | reject duplicate `ERR-305` |
| BRL-027 (BR-027) | Class created | belongs to exactly one institution | — |
| BRL-028 (BR-028) | Class removal requested | zero students currently placed in it | reject `ERR-306` "students still placed" |
| BRL-029 (BR-029) | Class structure mutated | caller role ∈ leadership set | Teacher role: read-only, 403 on write |

**Subject deletion (1.0.3, U5):** deleting a subject (API-029 `DELETE /subjects/{id}`) removes its
`SubjectTeacherClassMapping` rows with it. Existing mappings do not block the delete and no error
is raised; no `ERR-` applies. The mapping rows have no meaning apart from their subject.

## BRL — Student/Parent access

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-065 (BR-065) | Student/Parent requests fee/leave-request/homework data | — | always denied — no such endpoint/entity exists at all (not merely permission-denied; the capability itself is absent, FR-077–079 excluded) |
| BRL-066 (BR-066) | Student or Parent requests information | scoped to own record (Student) or linked children only (Parent, via `ParentStudentLink`) | never another learner's data |

## State machine — Parent Link Code (T-003, resolves FR-017)

```
(none) --[staff: POST /students/{id}/parent-link-codes]--> Issued (code, expires_at = now+7d)
Issued --[staff: regenerate, before consumption]--> Issued (new code, old code invalidated)
Issued --[staff: revoke]--> Revoked (terminal)
Issued --[parent: POST /parent-links {code}, before expiry]--> Consumed (ParentStudentLink row created)
Issued --[expires_at passes, unconsumed]--> Expired (terminal)
```

**Guards:**
- Consume only succeeds when: code exists, `status == Issued`, `now < expires_at`, and the
  submitting parent's `institution_id` matches the student's institution (T-003, prevents a code
  leaked outside the institution from being used cross-tenant).
- A consumed code can never be consumed again, revoked, or regenerated (terminal).
- If the submitting Parent is already linked to the student, a further valid code is still
  consumed and the existing `ParentStudentLink` is returned unchanged — idempotent, no error, no
  duplicate link (1.0.3, U6).
- Revoking or regenerating an already-consumed code is a no-op error (`ERR-307` "code already
  consumed") — staff must instead remove the `ParentStudentLink` directly if unlinking is needed
  (a separate, explicit staff action, not implied by code revocation).

## Decision table — Parent Link Code consumption

| code status | expired? | institution match? | Result |
|---|---|---|---|
| Issued | N | Y | Consumed — link created |
| Issued | Y | — | Rejected `ERR-308` "code expired" |
| Issued | N | N | Rejected `ERR-309` "institution mismatch" |
| Revoked/Expired/Consumed | — | — | Rejected `ERR-310` "code not valid" |
| Issued, and the Parent is **already linked** to this student | N | Y | Code consumed; existing `ParentStudentLink` returned unchanged — idempotent, no error, no duplicate link (1.0.3, U6) |

## VAL — field validation

| VAL ref | Field | Rule |
|---|---|---|
| VAL-008 | `sr_number` (school register number) | Unique within institution (BR-018); required |
| VAL-009 | `roll_number` | Unique within institution (BR-018); required |
| VAL-010 | `date_of_birth` | Valid past date, plausible age range for a K-12 learner (no numeric bound stated in BRD; **[NEEDS ANSWER — Q-203]**: provisional choice — reject DOB implying age <3 or >21 at enrolment as a data-entry-error guard, not a hard business rule; confirm before treating as permanent) |
| VAL-011 | `guardian_contact_email`, `guardian_phone`; teacher `contact_email`, `telephone` (API-020) | Same format rules as VAL-001 (email) / VAL-003 (phone) (institutions.md), applied per-guardian and per-teacher. Teacher scope added in 1.0.3 (U4). |
| VAL-012 | `parent_link_code` | Exactly 8 characters, single-use, 7-day expiry (`CFG-005`, T-003) |

## Edge-case register

| # | Case | Resolution |
|---|---|---|
| E1 | A student is withdrawn (FR-014) while a `ParentLinkCode` is still `Issued` for them | Code remains valid/consumable — withdrawal does not auto-revoke it; if the withdrawal is meant to also cut off parent access, staff must revoke separately (explicit action, not implicit) |
| E2 | A teacher who is a class's class-teacher is removed (FR-019) without prior reassignment | Rejected per BRL-025 — removal blocked until the class-teacher assignment is released or reassigned |
| E3 | A student changes class placement mid-term | Historical attendance/class-assignment rows remain attached to the *prior* class for the dates they occurred (BR-014/FR-013's "amend... and change class placement" does not retroactively rewrite history) |
