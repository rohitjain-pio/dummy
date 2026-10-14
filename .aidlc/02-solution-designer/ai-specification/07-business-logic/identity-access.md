<!-- pipeline-provenance
produced_by: ai-spec-agent (business-logic-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). Added BRL-075/076/077/078, VAL-027/028, the leadership
  sign-up-code state machine, and edge cases E4/E5. Corrected VAL-007 and E3, which specified the
  self-elected-role behaviour that REV-001 / U38 proved to be a tenant-takeover hole (Q-238,
  Q-239). Added the rate-limiting rule BRL-078 (Q-240 / U39, folds in U16). No existing BRL-/VAL-
  was renumbered.
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, authorised by Rashmi Mehta, closing
  the Mode D Round-2 audit against 1.0.4). Three text corrections, no new rule and nothing
  renumbered: **BRL-075/VAL-027 now define the branch selector** (`staffCode` absent ⇔ omitted or
  `null`; `""`/whitespace is present → `ERR-206`), which 1.0.4 left with two honest readings (m2);
  the state-machine guards name **SEC-009** (the PlatformOperator issue-and-redeem escalation is
  accepted and audited, U47) and `provisioned_by_code_id`; and the **"copied from an already-built,
  tested mechanism" claim is corrected** — the lifecycle is copied, the hash + keyed lookup is new
  machinery, because `ParentLinkCode.Code` is stored plaintext (U45d).
-->

# 07 — Business Logic — `identity-access`

## BRL — business rule logic

| BRL ref | IF | THEN | ELSE | Enforcement |
|---|---|---|---|---|
| BRL-007 (BR-007) | Any role is assigned | Role ∈ {PlatformOperator, Administrator, Principal, SchoolInCharge, Teacher, Student, Parent} | reject — no eighth role | `Role` closed enum |
| BRL-008 (BR-008) | Account created | `institution_id` set at registration, immutable thereafter | reject any later attempt to change it | No `PUT` field for `institution_id` on account update |
| BRL-009 (BR-009) | Any data access | filtered to caller's institution | except PlatformOperator via named path | `_shared.md` §1 |
| BRL-010 (BR-010) | Caller role ∈ {Administrator,Principal,SchoolInCharge} | may create/amend/remove students, teachers, classes, attendance, announcements within institution | else denied per matrix | `09-security-spec.md` §2 |
| BRL-011 (BR-011) | Caller role == Teacher | may view students/classes, record/amend attendance, publish announcements | may NOT create/amend/remove student/teacher/class master records | Same matrix |
| BRL-012 (BR-012) | Caller role ∈ {Student, Parent} | read access to announcements + own/linked info; may participate in rooms | no write access to master records | Same matrix |
| BRL-013 (BR-013) | Cross-institution operation requested | Caller role == PlatformOperator | else denied | Same as BRL-009 |
| BRL-014 (BR-014) | Password set/changed | meets `CFG-001` policy (min 10 chars, ≥3 of 4 classes, blocklist-checked) | reject with `ERR-201` | ASP.NET Core Identity password validator |
| BRL-015 (BR-015) | Session token issued | expires per `CFG-002` (30-min access / 14-day sliding refresh) | re-authentication required after expiry | JWT expiry claim |
| BRL-016 (BR-016) | Sign-out requested | refresh token immediately denylisted, access token treated as invalid on next validation | — | Denylist check on every request |
| BRL-075 (Q-238, Q-239) | An **anonymous** registration is submitted (API-006) | the account's role and institution are derived from **exactly one** of two sources: (a) **no `staffCode` supplied** → role ∈ {Student, Parent}, taken from the request's `role` field, and institution taken from the request's `institutionId`; (b) **a valid `staffCode` supplied** → role **and** institution are read **from the redeemed code row**, and any `role`/`institutionId` present in the request body is **ignored entirely, never merged** | reject `ERR-901` when neither branch resolves (e.g. `role` is absent and no code was supplied) | Server-side in `AuthService.RegisterAsync`. **The request body is never a source of a privileged role or of an institution for a privileged role.** This is the U38 fix: the prior rule let the caller name both. **Branch selector defined at 1.0.5 (m2):** `staffCode` is **absent** if and only if the JSON property is **omitted or `null`**. **Any other value — including `""` and a whitespace-only string — is PRESENT**, and therefore goes to branch (b) and is evaluated by VAL-027, which rejects it as `ERR-206`. 1.0.4 keyed BRL-075 on "absent" and VAL-027 on "when present" and defined neither, leaving two readings; **both readings are safe** (neither yields a privileged role), but two readings mean two implementations, so this picks one. The rule chosen is the stricter of the two: a caller who sends the field, even empty, is asserting they hold a code, and is answered as such rather than silently downgraded into self-service. |
| BRL-076 (Q-238) | A leadership/staff sign-up code is issued | issuer is either the **PlatformOperator** (at or after institution creation, API-001/API-065 — the bootstrap path, because no account of that institution exists yet) **or an authenticated leadership user of that same institution** (API-066, for further staff). The code row records `institution_id`, the `granted_role` it confers, the issuer, `issued_at_utc`, and `expires_at_utc = issued_at_utc + CFG-010` | any other caller — Teacher, Student, Parent, or leadership of a **different** institution — is denied `ERR-102` | Mirrors `ParentLinkCode` issuance (BRL rules in `07-business-logic/academic-records.md`) deliberately, so the two share one audited, tested mechanism |
| BRL-077 (Q-238) | A sign-up code is redeemed at registration | the code must exist, be `status == Issued`, and `now < expires_at_utc`; on success it becomes `Consumed` (terminal), recording `consumed_at_utc` and `consumed_by_user_id`, and the new account is created with the code's `granted_role` and `institution_id` | unknown / revoked / already-consumed → `ERR-206`; expired → `ERR-207`. **No account is created and no session is issued on any failure branch** | One code redeems exactly once — the same terminal-state discipline as `ParentLinkCode` (T-003) |
| BRL-078 (Q-240) | A request arrives on a rate-limited surface | the limiter counts it against the partition key and window defined in `09-security-spec.md` §10 | over the threshold → reject `ERR-904` (429) with `Retry-After`, **before** the credential check, the account write, or the passcode comparison runs | ASP.NET Core rate-limiting middleware, registered globally; thresholds bound to `CFG-011`/`CFG-012`/`CFG-013`, never hardcoded |

## State machine — session lifecycle

```
Unauthenticated --[register or login: success]--> Authenticated (access+refresh issued)
Authenticated --[access token expires, refresh valid]--> Authenticated (silent refresh)
Authenticated --[refresh expires OR sign-out OR password change]--> Unauthenticated (denylisted)
Authenticated --[permission-denied on an action]--> Authenticated (FR-010/NFR-003 message shown, session unaffected)
```

**Guards:** silent refresh only succeeds while the refresh token is neither expired nor denylisted;
a password change denylists **all** outstanding refresh tokens for that user, not just the current
session (defense-in-depth — a leaked-password scenario should not leave other sessions alive).

## State machine — leadership/staff sign-up code (Q-238, resolves U38)

**Deliberately the same shape as `ParentLinkCode` (T-003).** The mechanism is copied, not
reinvented, because that one is already built, tested and audited (CT-103).

```
(none) --[PlatformOperator: institution created (API-001) or POST /institutions/{id}/leadership-codes (API-065)]--> Issued (code, granted_role, institution_id, expires_at = now + CFG-010)
(none) --[leadership of the institution: POST /leadership-codes (API-066)]--> Issued (same shape)
Issued --[issuer or leadership: POST /leadership-codes/{id}/revoke (API-067)]--> Revoked (terminal)
Issued --[registrant: POST /auth/register {staffCode} before expiry]--> Consumed (terminal; account created with granted_role + institution_id)
Issued --[expires_at passes, unconsumed]--> Expired (terminal)
```

**Guards:**
- Redemption succeeds only when the code exists, `status == Issued`, and `now < expires_at_utc`
  (BRL-077). There is **no institution-mismatch guard** here, unlike `ParentLinkCode`'s — because
  the institution is taken *from* the code rather than compared against a caller who already has
  one. That asymmetry is intentional and is the whole point of the fix.
- `granted_role` ∈ {Administrator, Principal, SchoolInCharge, Teacher} — the **code-gated set**
  (Q-239). **`PlatformOperator` is never a `granted_role`**: it is platform-level, is not
  institution-scoped, and is provisioned only by an existing PlatformOperator through an internal
  path (the Q-202 rule, unchanged and now permanent rather than provisional).
- A `Consumed`, `Revoked` or `Expired` code is terminal: it can never be redeemed, revoked or
  regenerated. Revoke or regenerate on a consumed code → `ERR-208`.
- The bootstrap case is the reason the PlatformOperator path exists at all: at institution
  creation **no account of that institution exists yet**, so there is nobody in-institution who
  could issue the first code. API-001 is already PlatformOperator-only (BRL-001).
- **Redemption is audited** like every other governed mutation (NFR-028) — the `AuditLog` row for
  the account create carries the code id, and (from 1.0.5) the account row itself carries
  `provisioned_by_code_id` (`04-data-model.md` §2.2), so "how did this Administrator come to
  exist?" survives the CMP-001/002/003 retention sweep rather than living only in a JSON snapshot.
- **A PlatformOperator issuing and then redeeming their own code is an accepted, audited,
  tenant-visible escalation, not a blocked one** — stated at 1.0.5 as **SEC-009**
  (`09-security-spec.md` §1a.1, boundary row in §5, **N18**). Nothing in this state machine
  prevents it and nothing should: the bootstrap case above is exactly why it exists.

**What is copied here, and what is not — corrected at 1.0.5 (U45d).** This file and
`04-data-model.md` both described the mechanism as copied from `ParentLinkCode` "because that one
is already built, tested and audited (CT-103)". **The lifecycle above genuinely is** — issue,
redeem, revoke, expiry, terminal states, the filtered-unique index, and the `IgnoreQueryFilters()`
precedent (ADR-0004) are all proven code. **The storage scheme is not.**
`ParentLinkCode.Code` is stored **PLAINTEXT** (`src/SchoolSync.Domain/Entities/ParentLinkCode.cs`),
so `code_hash` + keyed `code_lookup` is **new, unbuilt, untested machinery with no precedent in
this codebase** — and it is the part most likely to be got wrong, precisely because the earlier
wording invited a builder to treat it as transcription. CT-801 is sized and gated accordingly.

## VAL — field validation

| VAL ref | Field | Rule |
|---|---|---|
| VAL-005 | `password` | Per `CFG-001` — min 10 chars, ≥3 of {upper, lower, digit, symbol}, not on the blocklist |
| VAL-006 | `username`/`email` at registration | Must be unique within the platform (login identity is global, not institution-scoped — a user picks their institution as a separate field, per FR-006/Auth modal's Register form) |
| VAL-007 | `role` at registration | **Corrected at 1.0.4 (Q-239, U38).** Accepted values are **`Student` and `Parent` only**. Any other value — including `Teacher`, the three leadership roles, and `PlatformOperator` — is rejected with `ERR-901`; those roles are reachable **only** by redeeming a `staffCode` (BRL-075/077), never by naming them. The field is ignored outright when a `staffCode` is supplied. *(Prior text accepted all seven roles subject to a PlatformOperator carve-out; that is the specified behaviour REV-001 exploited.)* |
| VAL-027 | `staffCode` at registration | Optional. **"Present" means the property is supplied and not JSON `null` (BRL-075's branch selector, 1.0.5/m2) — `""` and `"   "` are present, not absent.** When present: trim, upper-case invariantly, then require exactly `CFG-010`'s length and alphanumeric-only. A malformed value — wrong length (including zero), symbols, whitespace-only — is rejected as `ERR-206`, **byte-identical to an unknown code and never as a distinguishable "wrong format" error**, so the endpoint does not become a code-format oracle. The same normalisation is what feeds `code_lookup` (`04-data-model.md` §2.12b(b)); **it is defined once here and must not be re-derived at the lookup site**, or a code could validate and then fail to resolve |
| VAL-028 | `grantedRole` on code issue (API-065/066) | Required; ∈ {Administrator, Principal, SchoolInCharge, Teacher}. `PlatformOperator`, `Student` and `Parent` are rejected with `ERR-901` — the first because it is platform-level, the latter two because they need no code |

## Edge-case register

| # | Case | Resolution |
|---|---|---|
| E1 | Two users at different institutions both register with the same `username` | Rejected on the second — `username` uniqueness is platform-global (VAL-006), not institution-scoped, since login must resolve to exactly one account without also asking "which institution" at the login screen |
| E2 | A Student/Parent self-registers before any staff member has enrolled the corresponding Student record or issued a Parent Link Code | Registration still succeeds (FR-006's "no additional approval step" is unconditional) — the account exists with no linked student/teacher record until FR-016/FR-017 links it later; the Dashboard/Attendance screens show an empty/unlinked state until then |
| E3 | A self-registering user names a privileged role (`PlatformOperator`, `Administrator`, `Principal`, `SchoolInCharge`, or `Teacher`) in the request body | **RESOLVED at 1.0.4 — Q-238/Q-239, operator-confirmed (Rashmi Mehta, 2026-09-15). Rejected with `ERR-901`; no account is created and no session is issued.** Privileged roles are reachable only by redeeming a `staffCode` (BRL-075/077), and `PlatformOperator` is not reachable that way either (it is provisioned by an existing PlatformOperator through an internal path). **This supersedes the Q-202 provisional choice**, which excluded only `PlatformOperator` and left the three leadership roles and `Teacher` self-selectable — the behaviour REV-001 exploited to obtain institution-administrator access to any tenant from two anonymous calls. Final rule, not provisional. |
| E4 | A registrant supplies **both** a valid `staffCode` **and** a `role`/`institutionId` in the body, and they disagree | The code wins, silently and totally — the body fields are not read at all on that branch (BRL-075b). **The request is not rejected for the conflict**, because rejecting would tell a prober which institution a code belongs to. Asserted by TC-210. |
| E5 | A `staffCode` is redeemed for an institution that has since been deactivated (`is_active = 0`, D8) | Rejected `ERR-206`. A dormant tenant must not gain new staff accounts through a code issued before it was withdrawn. |
