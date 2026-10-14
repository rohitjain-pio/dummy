<!-- pipeline-provenance
produced_by: ai-spec-agent (contract-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). **API-006's contract was replaced, not annotated** — the
  prior one took `role` and `institutionId` from an anonymous request body and is the
  vulnerability REV-001 / U38 reported. Added API-065–068 (leadership/staff sign-up code
  lifecycle, Q-238/Q-239) and the rate-limit note. No existing API- id was renumbered.
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, authorised by Rashmi Mehta, closing
  the Mode D Round-2 audit against 1.0.4). **API-006's branch table now defines "absent"** and
  states the failing-code branch explicitly (m2 — 1.0.4 left `""`/whitespace with two honest
  readings). **API-068 gains its security contract** (U47/SEC-009): it must list
  PlatformOperator-issued codes to the institution's own leadership, with `issuedByUserId: null`
  rendered "Platform Operator" — that visibility is the compensating control for the bootstrap
  escalation. No API- added or renumbered.
-->

# 05 — API Contracts — `identity-access`

## API-006 — Register

`POST /api/v1/auth/register` — anonymous. **Rate-limited per `09-security-spec.md` §10
(`CFG-012`).**

> **Contract replaced at 1.0.4 (Q-238/Q-239 — U38, Critical).** The prior contract read
> `{username, email, password, role, institutionId}` with `role` excluding only
> `PlatformOperator`. Two unauthenticated calls — one to API-005's public directory for an
> institution `id`, one here with `role=Administrator` — yielded a live leadership session for
> that tenant (REV-001). **`role` is no longer a free choice and `institutionId` is no longer a
> source of truth for a privileged account.** Both CT-001 (schema) and CT-002 (this endpoint) are
> STALE against this file — see `00-spec-index.md` §14.

**Request:** `{ "username", "email", "password", "role"?, "institutionId"?, "staffCode"? }`

Exactly one of two branches applies (BRL-075) — the server decides which, the client does not:

| Branch | Condition | Role comes from | Institution comes from |
|---|---|---|---|
| **Self-service** | `staffCode` absent | `role`, which must be `Student` or `Parent` (VAL-007) | `institutionId` — the **`id`** of a school from API-005's public directory (Q-235; API-005's contract lists that `id` as of 1.0.5, U44) |
| **Code redemption** | `staffCode` present and valid | the **code row's** `grantedRole` | the **code row's** `institutionId` |
| **Code redemption, failing** | `staffCode` present and **not** valid | nothing — `ERR-206` (unknown/malformed/revoked/consumed) or `ERR-207` (expired), per BRL-077/VAL-027 | nothing. **It never falls back to the self-service branch** |

**"Absent" is defined, as of 1.0.5 (m2):** `staffCode` is absent **if and only if** the JSON
property is omitted or `null`. **`""` and `"   "` are PRESENT** and therefore select the redemption
branch, where VAL-027 rejects them as `ERR-206`. 1.0.4 keyed BRL-075 on "absent" and VAL-027 on
"when present" without defining either; both readings were safe (neither yields a privileged role)
but they are different behaviours, and a contract that permits two implementations is not a
contract. The stricter reading is chosen deliberately: a caller who sends the field is asserting
they hold a code, and is answered as such rather than silently downgraded into self-service.

On the code-redemption branch, `role` and `institutionId` in the body are **ignored entirely** —
not validated, not compared, not merged (edge case E4). A conflict is not an error, because
rejecting on mismatch would tell a prober which institution a code belongs to.

**Validation:** VAL-005 (password), VAL-006 (uniqueness), VAL-007 (`role` ∈ {Student, Parent}),
VAL-027 (`staffCode` shape).
**Success:** `201 Created` → `{ "accessToken", "refreshToken", "user": {id, username, role, institutionId} }`
— immediate session, **no approval step and no `pending` state** (FR-006 preserved). For a code
redeemer the code *is* the prior approval, issued by someone who already held authority in that
institution.
**Errors:** `ERR-203` (409 duplicate), `ERR-201` (400 password policy), `ERR-206` (410 code
unknown / revoked / already used), `ERR-207` (410 code expired), `ERR-904` (429 rate limit),
`ERR-901` (400 — including **any privileged role named in `role`**).
**Side effects:** on the redemption branch the code transitions to `Consumed` (BRL-077) in the
**same transaction** as the account insert; a failure on either side rolls back both, so a code
is never burned without producing an account and never produces an account twice.
**Examples:**
- Self-service — `{username, email, password, role:"Parent", institutionId:"<INST>"}` → `201`,
  session active as Parent. *(The Parent still sees nothing until they redeem a student link code,
  API-018 — already enforced, unchanged by this patch.)*
- Redemption — `{username, email, password, staffCode:"K7M2QX4B"}` → `201`, session active as
  the code's `grantedRole` at the code's institution.
- **Refused** — `{username, email, password, role:"Administrator", institutionId:"<INST>"}` →
  `400 ERR-901`, **no account, no session**. This is TC-209, the regression test for the Critical.

## API-065 — Issue a leadership code (PlatformOperator, bootstrap)

`POST /api/v1/institutions/{id}/leadership-codes` — Auth: **PlatformOperator only** (BRL-001,
BRL-076).
**Why a separate endpoint from API-066:** at institution creation **no account of that
institution exists yet**, so there is nobody in-institution who could issue the first code. This
is the bootstrap path and the only one that crosses the tenant boundary.
**Request:** `{ "grantedRole" }` — VAL-028, ∈ {Administrator, Principal, SchoolInCharge, Teacher}.
**Success:** `201` → `{ codeId, code, grantedRole, institutionId, expiresAtUtc }` (`CFG-010`:
8 chars, 7-day expiry — the same shape as API-016's parent link code).
**The plaintext `code` is returned in this response and never again** — it is stored hashed and
no later read path can retrieve it. Same discipline as the room passcode (SEC-004).
**Errors:** `ERR-102` (403 — any non-PlatformOperator caller), `ERR-902` (404 — unknown or
deactivated institution), `ERR-901`.
**Cross-reference:** **API-001** (institution create) issues the first `Administrator` code
automatically in the same transaction and returns it in its response — see
`05-api-contracts/institutions.md`. API-065 exists for re-issue when that first code is lost,
expires unused, or a second bootstrap account is needed.

## API-066 — Issue a staff code (leadership, in-app)

`POST /api/v1/leadership-codes` — Auth: **leadership of the caller's own institution**
(Administrator / Principal / SchoolInCharge). Institution-scoped like every other in-app write;
there is **no `institutionId` parameter** — it is taken from the caller's claim, never the body.
**Request:** `{ "grantedRole" }` — VAL-028.
**Success:** `201` → same body as API-065.
**Errors:** `ERR-102` (403 — Teacher, Student, Parent, and PlatformOperator acting
institution-internally per §2's footnote), `ERR-901`.
**Idempotency:** accepts `Idempotency-Key` — a retried issue returns the existing code row rather
than minting a second live code.

## API-067 — Revoke a code

`POST /api/v1/leadership-codes/{id}/revoke` — Auth: leadership of the code's institution, or
PlatformOperator.
**Success:** `204`; the code becomes `Revoked` (terminal, BRL-077).
**Errors:** `ERR-208` (409 — already consumed; staff must instead deactivate the resulting account,
an explicit separate action), `ERR-102`, `ERR-902`.

## API-068 — List codes

`GET /api/v1/leadership-codes` — Auth: leadership of the caller's own institution, or
PlatformOperator scoped by `?institutionId=`.
**Success:** `200` → `[{ codeId, grantedRole, status, issuedByUserId, issuedAtUtc, expiresAtUtc,
consumedAtUtc, consumedByUserId }]`. **Never includes `code` or its hash** — this is the list
API-067 needs to find an id, not a way to recover a plaintext code (`09-security-spec.md` §4,
Credentials — single-disclosure).

**This list is a security control, not a convenience — added to the contract at 1.0.5 (U47,
SEC-009).** It **must return every code for the institution, including those a PlatformOperator
issued at API-065**, to that institution's own leadership. That is the compensating control for the
bootstrap escalation: a PlatformOperator can provision a privileged account into any tenant, but
**cannot do it invisibly to that tenant**, and the tenant can revoke an unexpected code at API-067
before it is redeemed. **`issuedByUserId` is `null` for a PlatformOperator-issued code and consumers
must render it as "Platform Operator", never as blank or "—"** — a blank reads as missing data and
defeats the point. **Filtering PlatformOperator-issued codes out of this response removes the
entire control** and is a class-5 review finding. Tested by TC-216.

## API-007 — Login

`POST /api/v1/auth/login` — anonymous. **Rate-limited per `09-security-spec.md` §10 (`CFG-011`).**
**Request:** `{ "username", "password" }`
**Success:** `200 OK` → same session shape as API-006.
**Errors:** `ERR-202` (401 — deliberately generic, never "wrong password" vs "unknown user"),
`ERR-904` (429 — added 1.0.4, Q-240 / U39. **The limiter runs before the credential check**, so a
429 reveals nothing about whether the username exists, preserving ERR-202's generic-failure
property).
**Idempotency:** N/A (not a mutating resource create).

## API-008 — Logout

`POST /api/v1/auth/logout` — authenticated.
**Success:** `204 No Content`; refresh token denylisted immediately (BRL-016).

## API-009 — Refresh token

`POST /api/v1/auth/refresh`
**Request:** `{ "refreshToken" }`
**Success:** `200 OK` → new access+refresh pair (rotated, D2).
**Errors:** `ERR-204` (401, expired/denylisted).

## API-010 — Get current identity

`GET /api/v1/auth/me` — authenticated.
**Success:** `200 OK` → `{ "userId", "institutionId", "role" }` (FR-008).

## Cross-reference

Permission-denial response shape and the full negative-authorization matrix live in
`09-security-spec.md` §5 — every endpoint in this suite that returns `ERR-102`/`ERR-205`/`403`
is tested against that matrix, not re-specified per endpoint.
