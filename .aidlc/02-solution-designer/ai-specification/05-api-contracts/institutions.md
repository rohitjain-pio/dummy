<!-- pipeline-provenance
produced_by: ai-spec-agent (contract-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). API-001's success body now carries the first leadership
  sign-up code (Q-238 / U38 bootstrap path). No other endpoint in this file changed; API-005's
  public directory is unchanged and still publishes institution ids (Q-235) — that is now safe
  because the id alone no longer buys a privileged account.
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, authorised by Rashmi Mehta,
  closing the Mode D Round-2 audit against 1.0.4). **API-005's success shape now lists the `id`
  it always returned** (U44) — 1.0.4 asserted the property in this stamp and then omitted the
  field from the contract body. No other endpoint in this file changed; the NFR-016 exclusions
  are unchanged.
-->

# 05 — API Contracts — `institutions`

All endpoints versioned `/api/v1`. Auth: Bearer JWT unless marked anonymous. Errors reference
`08-error-catalogue.md` only.

## API-001 — Register institution

`POST /api/v1/institutions`
**Auth:** PlatformOperator only (BRL-001).
**Request:**
```json
{ "name": "string", "registrationNumber": "string", "contactEmail": "string",
  "telephone": "string", "postalAddress": "string", "postalCode": "string(6)",
  "city": "string", "region": "string" }
```
**Validation:** VAL-001–004.
**Success:** `201 Created` → `{ "id": "guid", ...same fields, "isActive": true,
"subscriptionPlanId": "guid (Entry/Free tier, auto-assigned, BRL-074, Q-211 resolved)",
"leadershipCode": { "codeId", "code", "grantedRole": "Administrator", "expiresAtUtc" } }` — the
plan is never null in this response; the institution is operational immediately within Entry
limits without any further call.

**`leadershipCode` added at 1.0.4 (Q-238 / U38).** Creating an institution now also issues its
first `Administrator` sign-up code **in the same transaction** (BRL-076), and this is the **only**
response that ever carries the plaintext `code` — it is stored hashed and no read path returns it
again (API-068 lists codes without them). The PlatformOperator hands it to the school; the first
Administrator redeems it at API-006. This closes the bootstrap gap that previously made
self-elected leadership registration look necessary. **Re-issue is API-065** if the code is lost
or expires unused. CT-101 is STALE against this response shape.
**Errors:** `ERR-101` (409, duplicate), `ERR-102` (403), `ERR-901` (400, field validation), `ERR-902` (404 n/a).
**Idempotency:** client-supplied `Idempotency-Key` header; a repeated key within 24h returns the
original `201` body rather than creating a second row (protects against retry-induced duplicates
distinct from the BR-003 business-duplicate check, which is content-based not key-based).
**Example:** register "Green Valley School", `contactEmail: admin@greenvalley.edu`,
`postalCode: 560001` → `201` with a new `id`.

## API-002 — Update institution

`PUT /api/v1/institutions/{id}`
**Auth:** Administrator/Principal/SchoolInCharge where `id == caller.institution_id`, or
PlatformOperator (any id).
**Request:** same shape as API-001, partial fields allowed.
**Success:** `200 OK` with the updated resource.
**Errors:** `ERR-102` (403 — wrong institution or role), `ERR-902` (404, per the no-enumeration
policy in `07-business-logic/institutions.md` E2 — a mismatched id returns 403 not 404 to avoid
leaking existence; 404 is reserved for a genuinely-nonexistent id after the scope check passes
for a PlatformOperator caller).

## API-003 — Change subscription plan

`PUT /api/v1/institutions/{id}/subscription`
**Auth:** PlatformOperator only (BRL-064, shared with `configuration-commercial`).
**Note:** this is an upgrade/change path only — every institution already has a plan (Entry/Free,
auto-assigned at creation by BRL-074) before this endpoint is ever called; it is never used to
give a plan-less institution its first plan.
**Request:** `{ "subscriptionPlanId": "guid", "startDate": "date" }`
**Success:** `200 OK`.
**Errors:** `ERR-102`.

## API-004 — Withdraw institution

`POST /api/v1/institutions/{id}/withdraw`
**Auth:** PlatformOperator only.
**Success:** `200 OK` → `{ "id", "isActive": false, "deactivatedAtUtc" }`.
**Errors:** `ERR-102`.
**Note:** reversible — see API-004b.

## API-004b — Reactivate institution

`POST /api/v1/institutions/{id}/reactivate`
**Auth:** PlatformOperator only.
**Success:** `200 OK` → `{ "id", "isActive": true, "deactivatedAtUtc": null }`.

## API-005 — Public institution directory search

`GET /api/v1/public/institutions?query={string}`
**Auth:** anonymous (NFR-014's public-info carve-out).
**Success:** `200 OK` → `[{ "id": "guid", "name": "string", "city": "string", "region": "string" }]`
— **never** `contactEmail`/`postalAddress`/`registrationNumber` (NFR-016's bounded exception).

**`id` restored to the written contract at 1.0.5 (U44).** It was never absent from the product —
the built endpoint returns it (`InstitutionsController.cs`), UI-005 mode A submits it as
`institutionId`, Q-235 names it, and this file's own 1.0.4 stamp asserts the directory "still
publishes institution ids". The 1.0.4 text simply failed to list it, so **a builder implementing
CT-801's mode A from this contract as written could not submit an `institutionId` at all.** This
is the Part-3 write-back rule (the built behaviour is the source of truth) applied to the one
contract 1.0.4 missed. **The NFR-016 exclusions above are unchanged** — publishing the id is safe
because, after the U38 fix, the id alone no longer buys a privileged account (SEC-007); it buys
only the `Student`/`Parent` self-service branch that FR-006 intends.
**Errors:** `ERR-901` on a malformed query param.
