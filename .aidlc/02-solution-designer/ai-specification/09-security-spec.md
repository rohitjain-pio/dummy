<!-- pipeline-provenance
produced_by: ai-spec-agent (security-spec-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). §1 step 1 rewritten, §2 gains the role-provisioning axis and
  two matrix rows, §3 gains SEC-007/008, §5 gains the anonymous-registration trust boundary, §6
  gains N11–N17 (the self-elected-role vector was outside this matrix's frame, which is why
  REV-001 went uncaught), §2a gains the ParentAccess tier retrofit (U26), and **§10 —
  rate limiting — is new** (it was cited by ERR-904 and had never been written, U39/U16).
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, authorised by Rashmi Mehta, closing
  the Mode D Round-2 audit against 1.0.4). **§1a.1 and SEC-009 are new** — the PlatformOperator
  issue-and-redeem bootstrap escalation is now named as accepted, audited and tenant-visible
  instead of left silent (U47); §2's footnote and **N9** are corrected accordingly (both asserted
  a property the product no longer has), and **N18** is added as the matrix's first
  expected-to-succeed row. §4 gains a data class for the plaintext sign-up code (m4). §7 gains
  `CFG-SEC-06`, the leadership-code lookup key, with its rotation coupling stated (U45a).
  §2's permission matrix "day 1-29" corrected to "day 1–30" (m1). §10.3's `CFG-012` unit corrected
  from accounts to requests (m3). **No SEC-, N- or CFG- was renumbered.**
-->

# 09 — Security Spec

**Never shards** — one permission matrix, referenced by every feature file above via `SEC-` IDs.

## 1. Authentication flow

1. **Register** (`API-006`, anonymous, rate-limited per §10) → password validated against
   `CFG-001` (BRL-014) → **the account's role and institution are resolved by BRL-075, from the
   request body only for `Student`/`Parent` and otherwise from a redeemed sign-up code** → account
   created, bound to one institution + one role permanently (BRL-008) → session issued immediately
   (FR-006, no approval step).

   **Corrected at 1.0.4 (Q-238/Q-239 — U38, Critical).** This step previously read "account
   created, bound to one institution + one role" with no statement of where that role and
   institution came from, and API-006 took both from an anonymous request body. The result was
   that an unauthenticated caller could mint a genuine `Administrator` claim for any institution
   in API-005's public directory; ADR-0001's tenant filter then admitted them as a legitimate
   member. **NFR-016's "isolation is absolute" was never breached — it was walked through the
   front door.** See §1a.

2. **Login** (`API-007`, anonymous, rate-limited per §10) → credentials checked → JWT access token (30-min, `CFG-002`)
   + refresh token (14-day sliding, rotated on use) issued.
3. **Every subsequent request** carries `Authorization: Bearer <access token>`; claims:
   `sub` (user id), `role`, `institution_id`.
4. **Refresh** (`API-009`) rotates both tokens; old refresh token invalidated on use (replay
   protection — a stolen, already-used refresh token cannot be replayed).
5. **Logout** (`API-008`) denylists the refresh token immediately (BRL-016); access tokens are
   short-lived enough (30 min) that no separate access-token revocation list is required for the
   ordinary sign-out case — but **password change** additionally denylists all outstanding refresh
   tokens for that user (defense-in-depth beyond BR-016's literal "sign-out" wording).

## 1a. Role provisioning — how a caller comes to hold a role at all (SEC-007, added 1.0.4)

**A permission matrix answers "what may this role do". It does not answer "how did this caller
get that role".** The absence of a rule for the second question is the whole of U38, so it is
stated here as a named control rather than left implicit in the registration contract.

| Role | How it is obtained | Who authorises it |
|---|---|---|
| `PlatformOperator` (ROLE-001) | Internal provisioning only — **never** by registration, and **never** a `grantedRole` on a sign-up code | An existing PlatformOperator |
| `Administrator` (ROLE-002), `Principal` (ROLE-003), `SchoolInCharge` (ROLE-004) | Redeem a sign-up code at API-006 (BRL-077) | PlatformOperator at institution create (API-001) or re-issue (API-065); thereafter leadership of that institution (API-066) |
| `Teacher` (ROLE-005) | Redeem a sign-up code at API-006 | Leadership of that institution (API-066) — **code-gated per Q-239**, because a Teacher reads student and guardian records school-wide (`Roles.LeadershipAndTeacher` on API-014/API-028), so self-election to Teacher is a smaller version of the same breach |
| `Student` (ROLE-006), `Parent` (ROLE-007) | Anonymous self-registration, `role` in the request body (VAL-007) | Nobody — this is FR-006's no-approval-step self-service, preserved |

**The invariant, stated once:** *for every role above `Student`/`Parent`, the role and the
institution are read from a server-side row that somebody with authority created. They are never
read from an anonymous request body.* Any future endpoint that accepts a role from a caller is a
violation of SEC-007 regardless of what else it checks.

**Residual exposure, named rather than glossed:** a self-registered `Parent` still sees nothing
until they redeem a student link code (API-018, already enforced — this patch adds nothing there),
and a self-registered `Student` account is unlinked until staff link it (API-015). Both were
already true; neither is weakened. **The `Student`/`Parent` branch remains the only anonymous
account-creation path in the product, and §10's `CFG-012` bounds its rate.**

### 1a.1 The bootstrap escalation — named as accepted and audited (SEC-009, added 1.0.5, U47)

**State it plainly, because an accepted risk that is not named is how U38 happened.** The table
above gives a PlatformOperator API-065 — mint an `Administrator` code for **any** institution,
without that institution's consent. Nothing stops the same person then redeeming that code at
API-006 themselves. **The result is a PlatformOperator holding a genuine, fully-capable
institution-scoped `Administrator` account at a tenant they chose.**

**This is accepted, not overlooked, and the design is not being changed.** A bootstrap path
*must* cross the tenant boundary: at institution creation there is by definition nobody inside the
institution who could consent, and requiring consent would make the first account unreachable. The
alternative — no platform-side bootstrap at all — is worse, because it puts the product back where
U38 found it.

**What is corrected is the spec's silence, and one claim that 1.0.4 left standing and that is now
false.** §2's footnote previously read as though a PlatformOperator simply *could not* obtain
institution-internal capabilities. It cannot obtain them **directly, on its own claim** — that
remains true and enforced. It **can** obtain them **by provisioning a second, separate account**,
and that second account is an ordinary `Administrator` in every respect. See the corrected footnote
in §2, the new boundary row in §5, and the corrected **N9** plus new **N18** in §6.

**SEC-009 — the compensating control, and it is deliberately detective, not preventive:**

| Control | Rule |
|---|---|
| **Audited at issue** | Every API-065 issue writes an `AuditLog` row naming the PlatformOperator as `actor_user_id` and the target `institution_id` (§8, `04-data-model.md` §2.12a). |
| **Audited at redemption** | Redemption writes a second row, and the resulting account carries `provisioned_by_code_id` (`04-data-model.md` §2.2) — so *"which code made this Administrator, and who issued it?"* is one join, not a search through JSON snapshots in a table the CMP-001/002/003 retention sweep prunes. |
| **Visible to the tenant itself — this is the part that matters** | **API-068 lists every code for an institution, including PlatformOperator-issued ones**, to that institution's own leadership. A PlatformOperator cannot mint a code into a tenant *invisibly*: the school's own Administrator sees the row, its `grantedRole`, its status, and that `issuedByUserId` is null — which API-068's consumers must render as **"Platform Operator"**, never as blank. Detection by the affected party is a stronger control than detection by the platform, because the affected party is the one with the incentive. |
| **Revocable by the tenant** | API-067 is available to that institution's leadership for any code in their institution, **including one a PlatformOperator issued**. An unexpected, unredeemed code can be killed by the school without platform involvement. |
| **Not claimed** | There is **no preventive control**, and none is added here. A same-actor issue-and-redeem correlation alert was considered and **deliberately not specified**: the redeeming caller is anonymous and the resulting account is brand new, so there is no reliable actor identity to correlate on at redemption time, and a detection rule defeated by using a second browser is worse than an honest *"this is audited, not prevented"*. |

**The boundary of the acceptance:** a PlatformOperator may **provision** a privileged account at
any institution, audited and visible to that institution. A PlatformOperator's **own** claim still
buys nothing institution-internal (§2). Anyone who weakens those audit rows, or filters
PlatformOperator-issued codes out of API-068's listing, is removing the entire compensating control
for this path.

## 2. Authorization model — the permission matrix (SEC-001)

Two independent axes evaluated on every request, **both** must pass:

**Axis 1 — RBAC (role → capability).** Seven closed roles (BR-007): PlatformOperator (ROLE-001,
platform-level); the other six are institution-scoped — Administrator (ROLE-002), Principal
(ROLE-003), SchoolInCharge (ROLE-004), Teacher (ROLE-005), Student (ROLE-006), Parent (ROLE-007).
ROLE-003 and ROLE-004 hold the identical permission set as ROLE-002 (Administrator) per their
own requirement statements, so the matrix below collapses them into one column.

| Capability | PlatformOperator (ROLE-001) | Administrator/Principal/SchoolInCharge (ROLE-002/003/004) | Teacher (ROLE-005) | Student (ROLE-006) | Parent (ROLE-007) |
|---|---|---|---|---|---|
| Create/withdraw institution | ✅ | ❌ | ❌ | ❌ | ❌ |
| Update own institution | ❌* | ✅ (own only) | ❌ | ❌ | ❌ |
| Assign subscription | ✅ | ❌ | ❌ | ❌ | ❌ |
| CRUD student/teacher/class master records | ❌* | ✅ (own institution) | ❌ (read only) | ❌ | ❌ |
| Record/amend attendance | ❌* | ✅ (student+staff) | ✅ (student only, own class) | ❌ | ❌ |
| Approve attendance correction (day 1–30) | ❌* | ✅ | ❌ | ❌ | ❌ |
| Create/amend announcement | ❌* | ✅ | ✅ | ❌ | ❌ |
| Withdraw announcement | ❌* | ✅ | ❌ | ❌ | ❌ |
| Create/join/message in room | ✅ | ✅ | ✅ | ✅ | ✅ |
| Remove room | ❌* (unless creator) | ✅ (own institution) | ✅ (own rooms only) | ✅ (own rooms only) | ✅ (own rooms only) |
| View institution dashboard | ❌* | ✅ | ❌ | ❌ (own variant instead) | ❌ (own variant instead) |
| View own/linked dashboard variant | n/a | n/a | n/a | ✅ | ✅ |
| Generate reports | ❌* | ✅ (all classes) | ✅ (own classes) | ❌ | ❌ |
| Maintain schedule | ❌* | ✅ | ❌ (read only) | ❌ (read only) | ❌ (read only) |
| Maintain institution configuration | ❌* | ✅ | ❌ | ❌ | ❌ |
| Assign/change subscription | ✅ | ❌ | ❌ | ❌ | ❌ |
| Cross-institution read | ✅ (named path only) | ❌ | ❌ | ❌ | ❌ |
| **Issue a leadership/staff sign-up code** (API-065/066) | ✅ (API-065 only, at/after institution create — the bootstrap path) | ✅ (API-066, own institution, `grantedRole` ∈ leadership + Teacher) | ❌ | ❌ | ❌ |
| **Revoke / list sign-up codes** (API-067/068) | ✅ | ✅ (own institution) | ❌ | ❌ | ❌ |

\* PlatformOperator's remit is platform-level administration (institutions, subscriptions,
cross-tenant oversight). **A `PlatformOperator` claim does not carry institution-internal
operational capabilities** (marking attendance, publishing an announcement) — the BRD never
describes a PlatformOperator doing day-to-day school operations, and no endpoint accepts that role
for one.

**Scope of that statement, corrected at 1.0.5 (U47).** It is a property of the **claim**, not of
the **person**. Since 1.0.4, a PlatformOperator may issue an `Administrator` code for any
institution (API-065) and redeem it at API-006, ending up with a **separate, ordinary,
institution-scoped account** that holds every capability in the ROLE-002 column. That path is
**accepted, audited and tenant-visible** — see §1a.1 / SEC-009. Before 1.0.5 this footnote read as
an unqualified denial, which was simply no longer true; it is restated here rather than deleted,
because the claim-level property it describes is real, enforced, and worth keeping.

**Axis 2 — Tier entitlement (BR-063).** `AdvancedAnalytics`, `ParentAccess`, `CustomReporting` are
gated by `SubscriptionPlan.capabilities` **in addition to** RBAC — see
`07-business-logic/configuration-commercial.md` §Cross-reference. A request must clear **both**
axes; failing either yields a 403 (RBAC) or 402 (tier, `ERR-802`).

**Axis-2 scope, written back at 1.0.4 (Q-228, built in CT-503 — U26).** `ParentAccess` gates
**every Parent-role operation**, which required retrofitting four endpoints approved in P1–P4:

| Endpoint | Gate | Note |
|---|---|---|
| `POST /api/v1/parent-links` (API-018) | `ParentAccess` | Redemption by the Parent. **Staff code *issuance* (API-016/017) is deliberately NOT gated** — a school must be able to prepare codes regardless of tier |
| `GET /api/v1/students/linked` (API-019) | `ParentAccess` | |
| `GET /api/v1/attendance/linked` (API-036) | `ParentAccess` | |
| `GET /api/v1/dashboard`, Parent variant (API-048) | `ParentAccess` | Enforced **in the controller**, not by an attribute, because the route is shared across roles and an attribute would gate leadership too |

**Ordering is load-bearing:** the tier check is an action filter that runs **after**
authorization, so RBAC is genuinely evaluated first and a caller gets `403` before `402`, never
merged. A Teacher on a `ParentAccess` plan still gets `403`, not `200`.

**`AdvancedAnalytics` and `CustomReporting` currently gate nothing** — no feature in this suite is
mapped to either (recorded as U31, **still open, not resolved by this patch**). They are listed in
the capability model and enforced by the same filter the moment a feature names them.

## 3. Row/field-level rules

| SEC ref | Rule | Trace |
|---|---|---|
| SEC-002 | Every tenant-owned row filtered by `institution_id == caller.institution_id`, structurally (EF Core global query filter), except PlatformOperator's named service | NFR-016, T-002 |
| SEC-003 | Guardian contact fields (`guardian_*` on Student) follow the exact same access rule as the Student row — no separate, looser ACL | BR-021 |
| SEC-004 | Room passcode never selected into any response DTO after creation (write-only field, stored hashed) | FR-043, Q-M4 |
| SEC-005 | Student/Parent scoped `GET` endpoints filter by `student_id == caller's linked student` (Student role) or `student_id IN (ParentStudentLink rows for caller)` (Parent role) — never a broader institution-wide read | BR-066 |
| SEC-006 | `AuditLog` has no `UPDATE`/`DELETE` endpoint — append-only at the API surface | NFR-033 |
| SEC-007 | **A privileged role (`Teacher` and above) and its institution are read only from a server-side sign-up-code row created by an authorised issuer — never from a request body on an anonymous endpoint** (§1a, BRL-075/076/077). The code's plaintext appears in exactly one response (API-001 / API-065 / API-066) and is stored hashed; no read path returns it. | Q-238, Q-239, NFR-016, U38 |
| SEC-008 | **Rate-limited surfaces reject with `ERR-904` before the credential check, the account write, or the passcode comparison executes** (§10, BRL-078) — so the limiter is a bound on the operation, not just on its response. Thresholds are `CFG-`-bound and operator-editable, never hardcoded. | NFR-017, Q-240, U39, U16 |
| **SEC-009** | **The PlatformOperator bootstrap escalation (API-065 issue → API-006 redeem) is an accepted, audited, tenant-visible path, not a prevented one.** Both the issue and the redemption write `AuditLog` rows; the resulting account carries `provisioned_by_code_id`; **API-068 shows PlatformOperator-issued codes to the institution's own leadership**, who may revoke them (API-067). No preventive control is claimed. Full statement and its boundary: §1a.1. | U47, §1a, §2 footnote, §5, N9/N18, `04-data-model.md` §2.2/§2.12a |

## 4. Data classification

| Class | Examples | Handling |
|---|---|---|
| **Sensitive personal (learner/guardian/staff PII)** | Student DOB/email, guardian contact details, teacher address/contact, attendance records tied to a named person | Encrypted at rest (Azure TDE) + in transit (TLS); institution-scoped access only; 7-year retention (CMP-001) |
| **Credentials** | Password hash, refresh tokens | Hashed (never plaintext), never logged, never returned in any API response |
| **Credentials — single-disclosure** *(added 1.0.5, m4)* | **The plaintext leadership/staff sign-up code** (`LeadershipCode`) | **It is a credential: whoever holds it can create a privileged account.** Stored **hashed only** (`code_hash`, plus the keyed `code_lookup` of §2.12b) — the plaintext is never persisted in any form. **Returned in exactly three responses and nowhere else: API-001, API-065, API-066**, each of which is the single moment of disclosure to the issuer. **Never logged** — not in request/response logging, not in an exception message, not in an `AuditLog` `before_snapshot`/`after_snapshot` (the audit rows record the code's **id** and lifecycle, never its value), and not in a SignalR or notification payload. It must never appear in a list, a read, an export or a support tool: **API-068 exists precisely as the id-finding list that does *not* return it.** This is SEC-004's room-passcode discipline applied to a second secret, and it is stated as a class here because 1.0.4 gave it "stored hashed" only — leaving nothing in the suite that forbade it landing in a log. |
| **Institution-confidential** | Institution contact/address, subscription/billing status | Institution-scoped; PlatformOperator has platform-wide visibility by design |
| **Public/aggregate** | Directory name+city/region, platform-wide summary figures | Anonymous access by design (NFR-014's carve-out), no PII ever included |

## 5. Trust boundaries — one threat per boundary

| Boundary | Threat | Mitigation |
|---|---|---|
| **Anonymous caller ⇄ registration (added 1.0.4)** | **Self-elected privilege** — an unauthenticated party names the role and tenant they wish to hold, and the system mints a genuine claim for it. The tenant filter then works perfectly and admits them. | **SEC-007**: role and institution for every privileged role come from a server-side code row an authorised issuer created (§1a); `role` in the body is constrained to `Student`/`Parent` by VAL-007 and ignored outright on the redemption branch. **SEC-008**: `CFG-012` bounds account-creation rate, so the remaining `Student`/`Parent` path is not scriptable across the public directory. Negative tests N11–N15. |
| Browser ⇄ API | Token theft via XSS; MITM | TLS 1.2+ everywhere (company.md §3); token storage hardened against script-readable persistence (NFR-017, and Q-232's in-memory-only access token); CSP headers (ties to `12-infrastructure-spec.md`) |
| API ⇄ SQL Server | SQL injection; cross-tenant query bypass | Parameterized queries/EF Core only (company.md §3); global query filter cannot be omitted per-entity (structural, T-002) |
| API ⇄ Azure SignalR Service | Unauthorized group join; message spoofing | Group join gated by `API-043`'s passcode check; hub methods re-validate membership (§Event Contracts §1) |
| API ⇄ Azure Communication Services | Token leakage exposing a meeting to an unauthorized party | Short-lived, per-meeting/per-participant ACS tokens issued only to verified room members (BRL-046); tokens never logged |
| API ⇄ Azure Blob Storage | Unauthorized report/document access | Blob access via time-limited signed URL issued only to an authorized report requester, not public blob access |
| PlatformOperator cross-tenant path | Privilege misuse — an over-broad query silently returns another tenant's data | Confined to one named service class (`PlatformOperatorQueryService`); every use is itself an audited action (NFR-032-adjacent) |
| **PlatformOperator ⇄ institution bootstrap (added 1.0.5, U47)** | **Privilege *acquisition* rather than privilege misuse** — a PlatformOperator mints an `Administrator` code for a tenant that never asked for it (API-065) and redeems it themselves (API-006), acquiring institution-internal capabilities their own claim deliberately lacks. 1.0.4's §5 covered only the *anonymous* caller crossing this boundary; this caller is authenticated and authorised, which is why the earlier row does not reach it. | **Accepted and audited, not prevented — SEC-009 (§1a.1).** A bootstrap path must cross the tenant boundary; there is nobody inside a new institution to consent. Controls are detective and, critically, **tenant-side**: `AuditLog` at issue and at redemption, `provisioned_by_code_id` on the account, the code visible to that institution's own leadership in API-068 with `issuedByUserId` rendered "Platform Operator", and revocable by them at API-067. **N18.** |

## 6. Negative-authorization test matrix (feeds `14-test-spec/`)

**Frame correction, 1.0.4.** N1–N10 all reason about an **authenticated** caller holding the
wrong role. The self-elected-role vector sat outside that frame entirely, which is precisely why
no test caught REV-001 and why the code was a faithful implementation of a wrong spec. **N11–N17
extend the matrix to the anonymous account-creation surface and to rate limits.** When adding a
row here in future, ask not only "can this role do that" but "can a caller *become* that role".

**Second frame correction, 1.0.5 (U47).** A negative matrix can only assert denials, so a path
that is **deliberately allowed but must be audited** has nowhere to live — and it silently becomes
an unstated assumption, which is the shape of U38 all over again. **N18 is therefore a row whose
expected result is success**, with its assertions on the compensating controls. N9's scope is
corrected in the same pass: it asserted a property the product no longer has.

| # | Actor | Attempted action | Expected result |
|---|---|---|---|
| N1 | Teacher | `POST /students` (create) | 403 `ERR-102` |
| N2 | Teacher | `POST /announcements/{id}/withdraw` | 403 `ERR-501` |
| N3 | Administrator of Institution A | `GET /students` scoped to Institution B (via crafted id) | 403/404 (no cross-tenant data returned) |
| N4 | Student | `GET /dashboard` (leadership variant) | 403 Forbidden (never the leadership payload) |
| N5 | Parent | `GET /students/{id}` for a student not in their `ParentStudentLink` set | 403/404 |
| N6 | Anonymous | any `/api/v1/*` route except `/public/*` | 401 |
| N7 | Any authenticated user | join a room with an incorrect passcode | 403 `ERR-502`, no group join |
| N8 | Institution at Entry tier | request a `ParentAccess`-gated read | 402 `ERR-802` even though RBAC alone would allow it |
| N9 | PlatformOperator | attempt an institution-internal operational action (e.g. mark attendance) **using the PlatformOperator token itself** | 403 — the `PlatformOperator` **claim** has no such capability (§2 footnote). **Scope corrected at 1.0.5 (U47):** this row asserts a property of the claim only. It is **not** a claim that the person cannot obtain the capability — N18 covers the route by which they can. A test written from the 1.0.4 wording would have asserted something the system does not guarantee. |
| **N18** | **PlatformOperator** | **issue an `Administrator` code for institution B (API-065), then redeem it at API-006 and mark attendance at B** | **`201`, then `201`, then `200` — this path SUCCEEDS, by design (SEC-009, §1a.1).** The assertions are on the **controls**, not on a denial: (1) an `AuditLog` row exists for the issue, naming the PlatformOperator and institution B; (2) an `AuditLog` row exists for the redemption; (3) the new account's `provisioned_by_code_id` points at that code row; (4) **institution B's own Administrator sees that code in API-068 with `issuedByUserId: null`**; and (5) B's Administrator can revoke it (API-067) before redemption. **Tested by TC-215/TC-216.** *An expected-403 test here would be asserting a property the product does not have, and would fail honestly — which is exactly why this row replaces the silence rather than extending N9.* |
| N10 | Teacher | attempt a direct write to a >30-day-old attendance record | 423 `ERR-401` |
| N11 | **Anonymous** | `POST /auth/register` with `role=Administrator` and an `institutionId` taken from API-005's public directory | **400 `ERR-901`. No account row, no session, no token.** The returned body must not contain `accessToken`. **This is the U38 regression test — TC-209** |
| N12 | **Anonymous** | `POST /auth/register` with `role=Teacher`, `Principal`, `SchoolInCharge`, or `PlatformOperator` | 400 `ERR-901` each (Q-239 — Teacher is code-gated with leadership) |
| N13 | **Anonymous** | `POST /auth/register` with a **revoked**, **consumed**, or **expired** `staffCode` | `ERR-206` / `ERR-206` / `ERR-207`. **No account created on any branch** — a burned code must not still buy an account |
| N14 | Teacher | `POST /leadership-codes` (API-066) | 403 `ERR-102` — a Teacher cannot mint a Teacher, let alone an Administrator |
| N15 | Leadership of Institution A | `POST /institutions/{B}/leadership-codes` (API-065) | 403 `ERR-102` — API-065 is PlatformOperator-only; API-066 has no `institutionId` parameter at all, so there is no cross-tenant issue path to attempt |
| N16 | **Anonymous** | `POST /auth/login` repeated past `CFG-011` against a known username | 429 `ERR-904` with `Retry-After`; **the limiter fires before the credential check**, so the response is identical for an unknown username |
| N17 | Any authenticated user | `POST /rooms/{id}/join` repeated past `CFG-013` with wrong passcodes | 429 `ERR-904`; closes **U16** |

## 7. Secrets management

Per `company.md` §4: ACS connection strings, Azure SignalR connection string, SQL connection
string, JWT signing key, **and the leadership-code lookup key (`CFG-SEC-06`, added 1.0.5)** — all
sourced from environment variables via the CI/CD platform's secret store (non-local) or a
gitignored `.env`/.NET user-secrets (local dev). Never hardcoded, never logged. Full binding:
`12-infrastructure-spec.md` §6.

**`CFG-SEC-06` — `LEADERSHIP_CODE_LOOKUP_KEY` (added 1.0.5, U45a).** 32 random bytes, Base64. It
is the HMAC-SHA256 key behind `LeadershipCode.code_lookup` (`04-data-model.md` §2.12b) and it is
**a secret of its own — explicitly NOT the JWT signing key (`CFG-SEC-02`)**. 1.0.4 specified the
lookup "under the JWT-signing-key-class secret", which named a class and bound nothing, and left
this inventory — the authority for what secrets exist — with no entry at all. Keeping them separate
is not tidiness: **rotating `CFG-SEC-02` is routine and is mandatory after a suspected token leak,
and if the two were one key that rotation would silently make every outstanding leadership code
fail with `ERR-206`.** Rotating `CFG-SEC-06` itself invalidates every outstanding `Issued` code and
carries a mandatory revoke-then-reissue procedure — `04-data-model.md` §2.12b(c). There is **no
default and no fallback value**: the build must fail closed at startup if it is unset, because a
defaulted HMAC key is not a key.

## 8. Audit-log schema (restated from `04-data-model.md` §2.12 for this file's own completeness)

`actor_user_id, institution_id, action, entity_type, entity_id, before_snapshot (JSON),
after_snapshot (JSON), timestamp_utc, correlation_id`. Populated via the `SaveChanges`
interceptor for entity changes; written directly by auth middleware for sign-in/out/denial events
(the one documented exception, `07-business-logic/_shared.md` §3).

## 9. Compliance mapping (CMP-)

| CMP | Enforcement point in this spec |
|---|---|
| CMP-001 (7-year retention) | `07-business-logic/_shared.md` §4 decision table; Hangfire retention sweep (`12-infrastructure-spec.md`) |
| CMP-002 (90-day export grace, then archive/delete) | Same sweep, triggered off `Institution.DeactivatedAtUtc` |
| CMP-003 (secure deletion/anonymisation, audited) | Deletion event itself writes an `AuditLog` row (§8) |

## 10. Rate limiting (SEC-008, BRL-078) — added 1.0.4

**This section did not exist.** `ERR-904` was defined in `08-error-catalogue.md` and attributed to
"Rate limiting (auth endpoints, per `09-security-spec.md`)" — a **dangling cross-reference**: the
authority it named never wrote the rule, so nothing was built, `ErrCatalogue` has no `Err904`, and
the code is unreachable by any path in the product (REV-002 / U39). NFR-017 has had no enforcement
point in this suite since 1.0.0. Written now per **Q-240** (Rashmi Mehta, 2026-09-15), which also
**folds in U16** (unlimited room-passcode guesses) — U16 is closed here and tracked under U39 from
this point.

### 10.1 The three limited surfaces

| # | Surface | Partition key (what is counted per) | `CFG-` | Why this key |
|---|---|---|---|---|
| RL-1 | `POST /auth/login` (API-007) | **`(normalised username, client IP)`, and independently `client IP` alone** | `CFG-011` | The username partition bounds a targeted guess against one known account; the IP partition bounds a spray across many usernames from one source. Either tripping returns `ERR-904`. Counting only by IP lets a botnet grind one account; counting only by username lets one source enumerate the whole user table. |
| RL-2 | `POST /auth/register` (API-006) | **`client IP`** | `CFG-012` | There is no prior identity to partition by. **This is the limit that matters most to U38**: it is what made the self-elected-role attack *scriptable across every institution in API-005's public directory in one pass* rather than a one-off. It remains necessary after the U38 fix, because anonymous `Student`/`Parent` creation is still open by design (FR-006). |
| RL-3 | `POST /rooms/{id}/join` (API-043) | **`(authenticated user id, room id)`** | `CFG-013` | Closes **U16**. The caller is authenticated, so the user id is the honest key; scoping to the room means a member of many rooms is not throttled by activity in another. A passcode is `VAL-018`-length and would otherwise be brute-forceable at wire speed. |

**Not limited, and deliberately so:** the remaining authenticated write surface is already bounded
by RBAC, tenant scoping and idempotency keys, and a general limiter there would throttle a
legitimate bulk attendance upsert (API-032 submits a 30-row roster in one call, NFR-022). **If a
future endpoint needs a limit, add a row here — do not invent a threshold at the call site.**

### 10.2 Behaviour

1. **The limiter runs before the work** (SEC-008): before the credential check on RL-1, before any
   uniqueness query or account insert on RL-2, and before the passcode comparison on RL-3. A 429
   must cost the attacker a request and cost the server nothing else. It also means an RL-1 429 is
   **identical for a known and an unknown username**, preserving `ERR-202`'s deliberate
   non-disclosure (never "wrong password" vs "unknown user").
2. **Rejection is `ERR-904` (429)** with a `Retry-After` header in seconds. No other code is used
   for a rate rejection anywhere in the product, and `ERR-904` is never worded inline — see
   `08-error-catalogue.md`.
3. **Successful requests still count.** A limiter that only counts failures can be reset by
   interleaving one valid call, which is trivially automatable.
4. **A trip is an audited security event**, written by the auth middleware on the same path as
   sign-in/out/denial (§8, `07-business-logic/_shared.md` §3): actor (user id where authenticated,
   otherwise null), surface, partition key, `correlation_id`. **The raw client IP is recorded on
   the audit row and never in an ordinary application log** (data classification §4 — it is
   personal data, not operational telemetry).
5. **No account lockout.** Q-240 specifies rate limiting only. A durable lockout is a denial-of-
   service lever against a named user — an attacker who knows a principal's username can lock them
   out of their own school day — and the BRD asks for none. **This is a stated decision, not an
   omission:** NFR-017 is met by bounding the attempt rate, not by disabling the account. Revisit
   only with an owner decision, never silently at build time.
6. **Thresholds are configuration, not constants** (§10.3). An operator raises them for a school
   sitting behind one NAT gateway without a code change.
7. **The limiter is in-process per instance** as specified here. Under horizontal scale-out the
   effective limit is `threshold × instance count`. **Stated, not hidden:** a distributed limiter
   backed by a shared store is not designed in this release, and the deployment topology it would
   depend on is itself deferred to Operations (`12-infrastructure-spec.md` §9). If that topology
   lands at more than one instance, this becomes a real weakening and needs an owner.

### 10.3 Thresholds — `CFG-` bound, never hardcoded

Values live in `12-infrastructure-spec.md` §7. They are **defaults an operator may change**, and
the build must read them from configuration; a literal in a controller or middleware is a class-11
review finding.

| `CFG-` | Surface | Default | Window |
|---|---|---|---|
| `CFG-011` | Login (API-007) | 10 attempts per username+IP, and 50 per IP | 5 minutes, sliding |
| `CFG-012` | Registration (API-006) | **5 requests per IP, counted regardless of outcome** *(unit corrected 1.0.5, m3 — it counts requests, not accounts: §10.2(1) runs the limiter before any account insert, and §10.2(3) counts successes too)* | 60 minutes, fixed |
| `CFG-013` | Room join (API-043) | 5 passcode attempts per user+room | 15 minutes, sliding |

**Basis for the numbers (ASM-007):** none of the three has a source in the BRD, the Tech Design or
`requirements.yaml` — NFR-017 requires a limit and names no figure. These are engineering defaults
chosen to be comfortably above ordinary human use (a person who mistypes a password three times
still gets in; a family sharing one connection can register several children) and far below a
useful attack rate. **They are recorded as an assumption rather than presented as a requirement**,
and an operator should tune them against real traffic. Getting them wrong is recoverable by
configuration; having none at all was not.

### 10.4 Tests

`TC-213` (login), `TC-214` (registration) in `14-test-spec/identity-access.md`; `TC-511` (room
join) in `14-test-spec/communication.md`. Each must assert the 429 **and** that the underlying
operation did not occur — no session issued, no account row, no membership row — because a limiter
that returns 429 after doing the work is worse than none: it hides the fact from the caller.
