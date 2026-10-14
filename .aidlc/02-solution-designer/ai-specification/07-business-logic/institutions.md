<!-- pipeline-provenance
produced_by: ai-spec-agent (business-logic-writer)
generated: 2026-09-11
-->

# 07 — Business Logic — `institutions`

Cross-cutting rules that also apply here: `_shared.md` §1 (institution scoping — N/A to
Institution itself except read-scoping for non-PlatformOperator roles), §2 (soft-delete), §3
(audit).

## BRL — business rule logic

| BRL ref | IF | THEN | ELSE | Enforcement |
|---|---|---|---|---|
| BRL-001 (BR-001) | Action is create/withdraw institution | Caller role == PlatformOperator | 403 `ERR-102` | Authorization policy |
| BRL-002 (BR-002) | Action is update institution `{id}` | Caller role ∈ {Administrator,Principal,SchoolInCharge} AND caller.institution_id == `{id}` | 403 `ERR-102` | Authorization policy + scope check |
| BRL-003 (BR-003) | New institution registration submitted | (name, contact_email, postal_code) tuple **all three** match an existing active-or-inactive institution | Reject as duplicate `ERR-101` | Composite uniqueness check — **not** any single field |
| BRL-004 (BR-004) | Institution create/update | contact_email valid RFC-5322, telephone matches a valid phone pattern, postal_code is exactly 6 digits | else reject with field-level `VAL-` error | See VAL-001–003 |
| BRL-005 (BR-005) | Institution withdrawn | `IsActive=false`, `DeactivatedAtUtc=now`, all child records remain queryable read-scoped | never physically deleted | `_shared.md` §2 |
| BRL-006 (BR-006) | Any tenant-owned record created | `institution_id` set from the creating context, never client-supplied | reject if client attempts to set a different `institution_id` | Structural — DTO never exposes `institution_id` as a settable field |

## VAL — field validation

| VAL ref | Field | Rule |
|---|---|---|
| VAL-001 | `contact_email` | RFC-5322 format; required |
| VAL-002 | `(name, contact_email, postal_code)` | Composite duplicate check (BRL-003) — evaluated together, never as independent unique constraints |
| VAL-003 | `telephone` | Valid contact-number pattern (country-agnostic digit/format check — Tech Design does not name a specific national format; **[NEEDS ANSWER — Q-201]**: no specific phone-format regex is stated in BRD/Tech Design; provisional choice: E.164-tolerant loose validation, min 7 / max 15 digits, optional `+` prefix) |
| VAL-004 | `postal_code` | Exactly 6 numeric digits (BR-004, sourced) |

## Decision table — duplicate check (BRL-003)

| name match | email match | postal match | Result |
|---|---|---|---|
| Y | Y | Y | Reject — duplicate |
| Y | Y | N | Accept — not all three |
| Y | N | Y | Accept |
| N | Y | Y | Accept |
| Y | N | N | Accept |
| N | N | N | Accept |

## Edge-case register

| # | Case | Resolution |
|---|---|---|
| E1 | Institution withdrawn, then a new registration submitted with the exact same (name, email, postal) | Still rejected as duplicate — BR-003's uniqueness check runs against active **and** inactive institutions (BR-005 says records are "retained," so the duplicate-check universe includes them) |
| E2 | Institution Administrator attempts to update a *different* institution's details by guessing an ID | 403, not 404 — existence is not leaked to an unauthorized caller beyond "you can't do this here"; institution existence-checking behavior is a `09-security-spec.md` decision (avoid ID enumeration oracle) |
