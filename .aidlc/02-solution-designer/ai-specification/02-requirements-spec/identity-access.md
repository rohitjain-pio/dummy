<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). SPEC-FR-006 corrected: it specified the self-elected-role
  behaviour REV-001 / U38 proved to be a tenant-takeover hole. Added the SPEC-NFR-017
  cross-reference for rate limiting. No SPEC- id was renumbered and FR-006 itself (Stage 1,
  read-only) is unchanged — this is the spec-layer expansion of it being corrected, not the
  requirement.
-->

# 02 — Requirements Spec — `identity-access`

Traces: BRD §6.2 · ENT-002 · ROLE-001–007 (all seven roles). Design trace: Tech Design §3.3,
§4.5, §8.1–8.3.

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-006 | FR-006 | **Corrected at 1.0.4 (Q-238/Q-239, U38 Critical).** Self-registration with immediate access and **no approval step — for the two unprivileged roles only**. A registrant may self-select `Student` or `Parent` against an institution chosen from the public directory. **`Teacher`, `Administrator`, `Principal` and `SchoolInCharge` are not self-selectable**: they are obtained by redeeming a leadership/staff sign-up code, from which the role **and** the institution are read. `PlatformOperator` is not obtainable by registration at all. | `POST /api/v1/auth/register` creates the account and an active session in one call, and **no `pending` state exists** — FR-006's "no approval step" is preserved intact for the roles it was written for, and for a code redeemer the code *is* the prior approval. **Adversarially:** an anonymous request naming `role=Administrator` (or any privileged role) in the body returns `ERR-901` and issues no session — TC-209. A request carrying a valid `staffCode` receives the code's role and institution, and any conflicting `role`/`institutionId` in the body is ignored — TC-210. *(Prior text read "Self-registration against a chosen institution + role"; taken literally and implemented faithfully, that let an unauthenticated caller become Administrator of any institution in the published directory — REV-001.)* |
| SPEC-FR-007 | FR-007 | Sign in, establish session, sign out | `POST /api/v1/auth/login` issues JWT access + refresh; `POST /api/v1/auth/logout` revokes both (denylist) |
| SPEC-FR-008 | FR-008 | Retrieve own identity, institution, roles | `GET /api/v1/auth/me` returns `{userId, institutionId, role}` for any authenticated caller |
| SPEC-FR-009 | FR-009 | Every function/nav destination authorised by role | Server-side authorization policy exists for every controller action (structural — no unguarded action); client nav config is a UX mirror, never the enforcement point |
| SPEC-FR-010 | FR-010 | Clear message when user lacks permission | 403 response maps to `ERR-4xx` "You don't have permission to do this" class, never a raw stack trace or HTTP-only response |

## Business rules

| BRL ref | Statement | Enforcement |
|---|---|---|
| BR-007 | Seven closed roles: PlatformOperator (ROLE-001), Administrator (ROLE-002), Principal (ROLE-003), SchoolInCharge (ROLE-004), Teacher (ROLE-005), Student (ROLE-006), Parent (ROLE-007) | `Role` enum, no eighth value; `09-security-spec.md` §2 permission matrix keyed to exactly these |
| BR-008 | Every account bound to exactly one institution at registration, permanently | `UserAccount.InstitutionId` non-nullable, immutable after create |
| BR-009 | Access filtered by institution; cross-institution prohibited except PlatformOperator | EF Core global query filter (T-002); PlatformOperator's sole named bypass (`PlatformOperatorQueryService`) |
| BR-010 | Administrator/Principal/SchoolInCharge: full CRUD on students/teachers/classes/attendance/announcements within institution | `09-security-spec.md` §2 matrix rows |
| BR-011 | Teacher: view students/classes, record/amend attendance, publish announcements; no create/amend/remove of student/teacher/class master records | Same matrix |
| BR-012 | Student/Parent: read announcements + own relevant info; may participate in rooms | Same matrix |
| BR-013 | Only PlatformOperator operates across institutions | Same as BR-009 |
| BR-014 | Password complexity policy enforced; never displayed/transmitted readable | D1: min 10 chars, ≥3/4 classes, blocklist, no forced rotation (`CFG-001`) |
| BR-015 | Sessions expire after a defined validity period | D2: JWT 30-min access / 14-day sliding refresh (`CFG-002`) |
| BR-016 | Sign-out immediately terminates session | Refresh-token denylist entry written synchronously on logout |

## Cross-reference — FR-017 (Parent-to-Student association)

FR-017 is deferred by the BRD to Tech Design and **resolved there** (§3.8, T-003) via
`ParentLinkCode`/`ParentStudentLink`. Because the mechanism is student-record-centric (issued
against a student, consumed by a self-registering Parent), its full spec lives in
`02-requirements-spec/academic-records.md` (SPEC-FR-017) rather than here — this file only notes
the cross-reference so the identity-access reader isn't left wondering where FR-017 went.

## Cross-reference — privileged-role provisioning (Q-238, added 1.0.4)

The leadership/staff sign-up code introduced by this patch is the **second** instance of the
issue-and-redeem pattern in this product, and it is deliberately the same machinery as the first
(`ParentLinkCode`, T-003). Its state machine and guards are in
`07-business-logic/identity-access.md`; its contracts are API-065–068 plus the `staffCode` branch
of API-006; its table is `04-data-model.md` §2.12; its errors are `ERR-206/207/208`; its controls
are `SEC-007`; its tests are TC-209–212. **No new `ENT-` was minted** — like `ParentLinkCode`, it
is a mechanism entity derived from a design decision, not a business entity from the BRD, and
`ENT-` is Stage 1's to assign.

## NFR references

SPEC-NFR-014/015/016/017 (security), SPEC-NFR-031 (sign-in/out/denial logging).
**SPEC-NFR-017** additionally requires the rate limits specified in `09-security-spec.md` §10
(`CFG-011`/`CFG-012`/`CFG-013`, error `ERR-904`) — added at 1.0.4 (Q-240 / U39); the requirement
had no enforcement point named anywhere in the suite before this patch, which is why nothing was
built.

## Full detail

`07-business-logic/identity-access.md` · `05-api-contracts/identity-access.md` ·
`08-error-catalogue.md` (`ERR-2xx`) · `09-security-spec.md` (this is the primary source file for
the permission matrix) · `11-ui-spec/public-auth.md` (Auth modal).
