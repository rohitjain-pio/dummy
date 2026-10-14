<!-- pipeline-provenance
produced_by: ai-spec-agent (task-breakdown)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). **Added phase P8 and CT-801/CT-802** (U38 Critical and U39
  Major). **Written back from the build:** API-046 into CT-303's scope (it was in no CT's, U15),
  Hangfire's install into CT-402 not CT-501 (Q-222, U20), API-061/062/069 and the ParentAccess
  retrofit into CT-503 (U26), the rollover supporting endpoints into CT-502, CT-202's recorded
  sweep split (Q-216) and CT-304's UI move to CT-603 (Q-218). **No existing CT- was renumbered
  and no closed phase was reopened.**
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, authorised by Rashmi Mehta, closing
  the Mode D Round-2 audit against 1.0.4). **CT-801's row only** (plus one note on CT-802's
  `CFG-012` unit, m3): its `Specs:` gains §2.12b, §2.2, §1a.1/SEC-009, §4, N9/N18, API-005 and
  CFG-SEC-06; its gate gains TC-215/216/217 (U46); its DoD gains the `CFG-SEC-06` fail-closed
  check, the `char(43)` encoding, API-005's `id`, and the `provisioned_by_code_id` attribution
  query; and **it is re-sized** — the hash + keyed lookup is new machinery, not the copy 1.0.4
  called it (U45d). **No CT- added, renumbered or reopened.**
-->

# 15 — Task Breakdown

**Never shards** — the cross-feature dependency DAG must stay in one file. Scope sizing is in
files/endpoints/entities touched, never wall-clock hours (per skill remit).

## 1. Phase table

| Phase | Theme | Depends on |
|---|---|---|
| P0 | Foundation — data model, multi-tenancy, auth/RBAC skeleton | — |
| P1 | Institutions, Identity self-service, Academic records (students/teachers/classes/subjects) | P0 |
| P2 | Attendance (incl. correction/lock state machine) | P0, P1 (needs students/teachers/classes) |
| P3 | Communication (announcements, rooms, messaging, meetings) | P0, P1 |
| P4 | Operations (dashboards, reporting, scheduling) | P1, P2 (reporting reads attendance) |
| P5 | Configuration & Commercial (institution config, academic-year rollover, subscription/trial) | P0, P1 |
| P6 | UI polish, accessibility, cross-cutting hardening | P1–P5 |
| P7 | Final integration — security scan, dependency audit, NFR performance checks | P0–P6 |
| **P8** | **Gate 4 fix round — privileged-role provisioning (Critical) and rate limiting** *(added 1.0.4)* | P0–P7 (all closed and gate-approved) |

## 2. Parallel map

- P1's three sub-areas (institutions, identity, academic-records) can proceed in parallel once P0
  lands, since they share only the tenant/auth scaffolding, not each other's tables.
- P3 (communication) and P4 (operations, minus reporting) can proceed in parallel with P2
  (attendance) once P1 lands — P4's reporting sub-area is the one exception, gated on P2.
  Attendance's Dashboard tie-in (BR-048/BR-050) and Reporting's attendance-only metrics
  (FR-057/058) are the reason P4-reporting specifically waits on P2.
- P5 can proceed in parallel with P2/P3/P4 once P1 lands (subscription/config don't depend on
  attendance or communication).

## 3. Critical path

P0 → P1 (academic-records) → P2 (attendance) → P4-reporting → P7. Everything else hangs off this
spine without extending it, provided team capacity allows the parallel branches above to run
concurrently.

## 4. Task registry

Each row's **Specs:** field is exhaustive for that task — `code-generation-spec-analyser` loads
only the named files, never a whole concern folder.

### P0 — Foundation

| CT | Scope (files/entities/endpoints) | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| CT-001 | EF Core `DbContext` + all 20 entities in `04-data-model.md` §2; global query filter convention (T-002) | `04-data-model.md` | Compiles, migrations apply clean from zero | Every tenant-owned table has the filter applied via the shared convention, verified by a test that queries without a filter override and gets zero cross-tenant rows | Drop the migration; no other code depends on it yet |
| CT-002 | JWT issuance/validation, password policy, denylist store; `UserAccount` auth endpoints (API-006–010) | `05-api-contracts/identity-access.md`, `07-business-logic/identity-access.md`, `08-error-catalogue.md` (`ERR-2xx`), `09-security-spec.md` §1 | Unit+integration tests green (TC-201–208) | Register/login/logout/refresh work end-to-end against CT-001's schema | Feature-flag the endpoints off; schema unaffected |
| CT-003 | `SaveChanges` audit interceptor + `AuditLog` table (already in CT-001's schema) | `07-business-logic/_shared.md` §3, `09-security-spec.md` §8 | Adversarial test proves no bypass path | Every governed mutation across the whole codebase produces exactly one audit row | Disable interceptor registration; no schema change needed |
| CT-004 | Soft-delete convention (D8) applied to Institution/Student/Teacher/Room/UserAccount | `07-business-logic/_shared.md` §2 | Unit tests: no hard-delete path reachable for these 5 | Reactivate mirrors deactivate exactly | Revert convention; entities unaffected since it's additive |

### P1 — Institutions / Identity / Academic Records

| CT | Scope | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| CT-101 | Institution CRUD (API-001–004b), public directory (API-005) | `02-requirements-spec/institutions.md`, `05-api-contracts/institutions.md`, `07-business-logic/institutions.md`, `07-business-logic/configuration-commercial.md` (BRL-074, Entry-plan auto-assignment), `10-process-flows/institutions.md` (PF-001), `08-error-catalogue.md` (`1xx`) | TC-101–107 green | All 5 endpoints + directory search implemented, duplicate-check (BRL-003) adversarially tested, institution creation auto-assigns Entry/Free plan in the same transaction (BRL-074, Q-211 resolved) — no post-creation window where `subscription_plan_id` is null | Feature-flag institution creation off; existing rows unaffected |
| CT-102 | Student CRUD (API-011–015), guardian fields | `02-requirements-spec/academic-records.md`, `05-api-contracts/academic-records.md` (Students section), `07-business-logic/academic-records.md` | TC-301, 302 | SR/roll uniqueness enforced, guardian fields access-gated per BR-021 | — |
| CT-103 | Parent Link Code + link (API-016–019, T-003) | Same files + `10-process-flows/academic-records.md` (PF-005) | TC-305–309 | Full state machine (Issued→Consumed/Revoked/Expired) adversarially tested | Disable issuance endpoint; existing links unaffected |
| CT-104 | Teacher CRUD + assignments (API-020–025) | `05-api-contracts/academic-records.md` (Teachers), `07-business-logic/academic-records.md` | TC-303, 304 | BR-023/025 guards adversarially tested | — |
| CT-105 | Class/Subject CRUD + mappings (API-026–030), Teacher-read on Classes (FR-027) | Same files (Class/Subject sections) | Contract test confirms Teacher role reaches `GET /classes` | BR-026/028/029 enforced | — |

### P2 — Attendance

| CT | Scope | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| CT-201 | Roster fetch + bulk upsert (API-031/032), NotYetMarked default | `02-requirements-spec/attendance.md`, `05-api-contracts/attendance.md`, `07-business-logic/attendance.md`, `04-data-model.md` §2.6 | TC-401–404 | Upsert proven non-duplicating (D10); default state proven NotYetMarked not Absent | Feature-flag write path; read path unaffected |
| CT-202 | Correction/approval state machine (API-033/034), 30-day lock, **`CorrectionRequest` table (Q-213)** | Same business-logic file + `10-process-flows/attendance.md` (PF-007) + `04-data-model.md` §2.6a | TC-405–414 (full state-machine adversarial suite) | Every boundary (day-0/29/30/31) tested. **Sweep split, recorded (Q-216):** the sweep *logic* is CT-202, the *daily schedule* is CT-501 | Roll back to direct-edit-only if the approval flow must be disabled; document as a recorded deviation, never silent |
| CT-203 | Attendance removal (API-035), retrieval (API-036) | Same | Contract tests | Leadership-only removal enforced | — |

### P3 — Communication

| CT | Scope | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| CT-301 | Announcement CRUD + withdraw-narrowing (API-037–040) | `05-api-contracts/communication.md`, `07-business-logic/communication.md` | TC-505 | BR-038 narrowing adversarially tested | — |
| CT-302 | Room CRUD + join (API-041–045), passcode hashing | Same + `04-data-model.md` §2.8 | TC-501, 502 | Passcode never in any response after creation | — |
| CT-303 | SignalR hub (`/hubs/collaboration`) — message send, presence, typing, join/leave **+ API-046 (revise/withdraw)** | `06-event-contracts.md`, `07-business-logic/communication.md`, `05-api-contracts/communication.md` | TC-503, 510 | EVT-001–004 delivered <1s (NFR-008) | Disable hub; REST-only fallback not built this release — recorded as a real regression if rolled back, not silent |
| CT-304 | ACS Calling token issuance (API-047). **Meeting UI moved to CT-603 (Q-218)** | `05-api-contracts/communication.md`, `10-process-flows/communication.md` (PF-009) | TC-507 (**TC-508/509 → CT-603**) | API-047 issues a token only to a verified member. **UI-014's connecting/reconnecting/error states are CT-603's DoD, not this one's** | Feature-flag meetings off if ACS risks (Tech Design §13.1) block release; chat remains usable |

**Corrections to P3's plan, recorded at 1.0.4 (U15):** **API-046 was in no CT's scope** while its
gate test TC-510 was CT-303's — an orphaned endpoint that got built only because the test pointed
at it. It is now in CT-303's scope where it was built. CT-304's UI half and TC-508/509 moved to
CT-603 by Q-218 (a recorded split at Gate 3-P3, not a silent one).

### P4 — Operations

| CT | Scope | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| CT-401 | Dashboard endpoint + role variants (API-048/049) | `05-api-contracts/operations.md`, `11-ui-spec/operations.md` §UI-015 | Contract tests per role variant | Student/Parent variants never expose institution-wide totals (BR-048) | — |
| CT-402 | Report generation job + types (API-050–052). **Installs Hangfire** (SQL Server storage) — *plan corrected 1.0.4, Q-222* | `07-business-logic/operations.md`, `06-event-contracts.md` §2, `11-ui-spec/operations.md` §UI-017, `04-data-model.md` §2.9 | TC-601, 602, 609 | Student/Teacher performance types read only `AttendanceRecord`; no Satisfaction/Completion field anywhere. **CSV only — PDF deferred (Q-226), a recorded D7/NFR-027 deviation (U21, open)** | — |
| CT-403 | Scheduling CRUD + conflict validation (API-053–055) | `07-business-logic/operations.md`, `04-data-model.md` §2.9 | TC-605–608 | All 3 conflict dimensions checked together | — |

### P5 — Configuration & Commercial

| CT | Scope | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| CT-501 | Institution configuration CRUD (API-056–058) + **registers** the daily `attendance-lock-sweep` (closing CT-202's Q-216 split) and `retention-sweep` recurring jobs. **Hangfire itself was installed in CT-402, not here** (Q-222) | `05-api-contracts/configuration-commercial.md`, `07-business-logic/_shared.md` §4, `12-infrastructure-spec.md` §8 | — | Audit-on-save unconditional (BR-059). **Retention sweep is a count-and-log scaffold — it anonymises and deletes nothing (ADR-0008). CMP-001/002/003 are unimplemented, not partially implemented — U27, open** | — |
| CT-502 | Academic year rollover (API-059/060) **+ API-059a/059b** (`/academic-years`, `/classes/{id}/next-class`), `Class.next_class_id`, `RolloverHoldBack` | `10-process-flows/configuration-commercial.md` (PF-012), `05-api-contracts/configuration-commercial.md`, `04-data-model.md` §2.5/§2.11 | TC-701–703 | Append-only `Enrollment` proven (no historical mutation), **including through rollover and after-the-fact hold-back** | — |
| CT-503 | Subscription/tier model + capacity gate (API-064, BRL-073/063) **+ API-061/062** (in no CT's scope in the original plan) **+ API-069** (plan editing, Q-227) **+ the ParentAccess retrofit onto four approved P1–P4 endpoints** (Q-228) | `07-business-logic/configuration-commercial.md`, `09-security-spec.md` §2 Axis 2 | TC-706, 707 | Both RBAC and tier axes proven independently enforced, **in that order** (403 before 402, never merged) | — |
| CT-504 | Trial lifecycle (API-063, reversion job) | `10-process-flows/configuration-commercial.md` (PF-013) | TC-704, 705 | No billing call exists on the auto-reversion path | — |

### P6 — UI / Cross-cutting hardening

| CT | Scope | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| CT-601 | Public marketing screens + Auth modal + App shell (UI-001–006) | `11-ui-spec/public-auth.md` | a11y scan AA | Nav model matches `09-security-spec.md` §2 exactly | — |
| CT-602 | Academic-records + Attendance screens (UI-007–010) | `11-ui-spec/academic-records.md`, `11-ui-spec/attendance.md` | a11y scan AA | Role-gated icon actions match BR-029/BR-020 | — |
| CT-603 | Communication screens (UI-011–014) | `11-ui-spec/communication.md` | a11y scan AA | Reconnecting/error states present | — |
| CT-604 | Operations + Settings + utility screens (UI-015–020) | `11-ui-spec/operations.md`, `11-ui-spec/configuration-commercial.md`, `11-ui-spec/_shared.md` | a11y scan AA | "Satisfaction"/"Completion" gauges absent; role-gated Settings verified | — |

### P7 — Final integration

| CT | Scope | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| CT-701 | Full-suite security scan, dependency/CVE audit, NFR performance checks | `09-security-spec.md`, `12-infrastructure-spec.md`, `14-test-spec/_shared.md` §6 | All gates green, `verify-gate.sh` PASS | Every NFR numeric target measured at least once against a realistic dataset | N/A — this phase is verification, not new code |

### P8 — Gate 4 fix round (added 1.0.4)

**Not a new delivery phase.** P0–P7 are closed and gate-approved; this is the remediation round the
Gate 4 CHANGES-REQUIRED verdict opened, authorised by Rashmi Mehta on 2026-09-15. It is numbered P8
rather than appended to P7 because **P7 is verification, not new code**, and folding new
implementation into it would make the "all gates green" record of P7 mean two different things.

**Two CTs, not one — a deliberate split.** CT-801 is the Critical and CT-802 is a Major;
separating them lets the tenant-takeover fix ship without waiting on the rate limiter, they touch
different layers (domain + auth service + SPA vs. middleware), they have different rollback
stories, and a combined CT would have a DoD nobody could partially satisfy. **CT-801 does not
depend on CT-802, and CT-802 does not depend on CT-801** — they can be built in parallel.

| CT | Scope | Specs: | Gate | DoD | Rollback |
|---|---|---|---|---|---|
| **CT-801** | **Privileged-role provisioning (U38 / REV-001, Critical).** `LeadershipCode` entity + migration + the `IgnoreQueryFilters()` ADR; API-006 rewritten to BRL-075's two branches; API-065/066/067/068; API-001 returns the first code; UI-005's two-mode Register panel. **Removes `Role` and `InstitutionId` from the anonymous register payload.** ~10 backend files, 2 SPA files, 1 migration, 4 new endpoints, 1 new table. **Re-sized at 1.0.5 (U45d): add ~1 backend file and one new column.** The `code_hash` + keyed `code_lookup` scheme is **NOT a copy of `ParentLinkCode`** — that one stores its code **plaintext**, so this is **new, unbuilt, untested machinery** (a keyed-HMAC service, the `CFG-SEC-06` secret binding and its fail-closed startup check, and the normalisation shared with VAL-027). Only the *lifecycle* is copied. 1.0.4's "copied, already tested and audited" framing under-sized exactly the riskiest part of this task. Also: `UserAccount.provisioned_by_code_id` (nullable FK) in the same migration — **flagged as an addition beyond defect-fixing, strikeable by the operator**, see `04-data-model.md` §2.2 | `02-requirements-spec/identity-access.md` (SPEC-FR-006), `05-api-contracts/identity-access.md` (API-006, API-065–068), `05-api-contracts/institutions.md` (API-001), `07-business-logic/identity-access.md` (BRL-075/076/077, VAL-007/027/028, the sign-up-code state machine, E3–E5), `04-data-model.md` §2.12a, `08-error-catalogue.md` (ERR-206/207/208), `09-security-spec.md` §1, §1a, §2, §3 (SEC-007), §5, §6 (N11–N15), `10-process-flows/identity-access.md` (PF-002, PF-014), `11-ui-spec/public-auth.md` (UI-005), `12-infrastructure-spec.md` §7 (CFG-010) **and §6 (CFG-SEC-06 — 1.0.5)**, `14-test-spec/identity-access.md` (TC-204, TC-209–212, **TC-215/216/217 — 1.0.5**). **Added at 1.0.5:** `04-data-model.md` **§2.12b** (the lookup key, the `char(43)` Base64url encoding, and the rotation rule) and **§2.2** (`provisioned_by_code_id`); `09-security-spec.md` **§1a.1 / SEC-009** (the accepted PlatformOperator bootstrap escalation), **§4** (the sign-up code's data class), **§6 N9/N18**; `05-api-contracts/institutions.md` **API-005** (the `id` mode A submits — **without it mode A cannot be built at all**) | **TC-209 green — and it must be seen to fail against the current `AuthService.RegisterAsync` before the fix lands.** A test that has never been red proves nothing here. Plus TC-204, TC-210, TC-211, TC-212; **and, added at 1.0.5, TC-215 (revoke — including that a revoked code already in a third party's hands creates no account), TC-216 (API-068 never returns `code`/`code_hash`, and shows PlatformOperator-issued codes to the tenant) and TC-217 (E5, a deactivated institution)** — these were the only containment/visibility controls in the bundle with zero tests (U46); full suite green; `dotnet list package --vulnerable` clean | **No `role` or `institutionId` from the request body reaches `UserAccount` on any path** — proven by TC-209 asserting the *absence of PII access*, not merely a 400. One code creates exactly one account under concurrent redemption. The code's plaintext appears in exactly one response and is stored hashed. Every issue/revoke/consume writes an `AuditLog` row. **Added at 1.0.5:** (a) `CFG-SEC-06` is bound from configuration and **the app fails to start if it is unset** — no default, no fallback, because a defaulted HMAC key is not a key; (b) `code_lookup` is Base64url of the full HMAC-SHA256, stored `char(43)`, **not truncated**; (c) **`GET /public/institutions` returns `id`** and UI-005 mode A submits it; (d) every account created by redemption has a non-null `provisioned_by_code_id`, so `SELECT … WHERE role NOT IN ('Student','Parent') AND provisioned_by_code_id IS NULL` returns **only pre-1.0.5 accounts** — that query is the artifact U43's human reviewer runs | **Not feature-flaggable, and deliberately so.** A flag that restores the old behaviour is a flag that restores the vulnerability. Roll back by reverting the commit and the migration; the pre-patch state is the Critical, so a rollback is an incident, not a fallback. **Any existing account whose role is privileged and whose creation cannot be traced to a code must be reviewed by a human before this ships** — the exploit window was open from CT-002 to now |
| **CT-802** | **Rate limiting (U39 / REV-002 + U16, Major).** ASP.NET Core `AddRateLimiter` + `UseRateLimiter`; three partitioned policies on API-006, API-007 and API-043; `ErrCatalogue.Err904` (the 9xx band currently stops at 903, so ERR-904 is unreachable); `Retry-After`; the audited trip event; UI-005's 429 banner. ~5 backend files, 1 SPA file, 0 migrations, 0 new endpoints | `09-security-spec.md` §10 (and §3 SEC-008, §6 N16/N17), `08-error-catalogue.md` (ERR-904), `07-business-logic/identity-access.md` (BRL-078), `12-infrastructure-spec.md` §7 (CFG-011/012/013 — **note `CFG-012`'s unit was corrected at 1.0.5 (m3): it counts REQUESTS to API-006 regardless of outcome, not accounts; the limiter runs before any insert, so it cannot count accounts**), `14-test-spec/identity-access.md` (TC-213/214), `14-test-spec/communication.md` (TC-511), `05-api-contracts/identity-access.md` (API-006/007), `05-api-contracts/communication.md` (API-043) | TC-213, TC-214, TC-511 green; full suite green | **`ERR-904` is reachable** from all three surfaces — it is currently reachable from none. The limiter runs **before** the credential check, the account insert and the passcode comparison (SEC-008), asserted by each test checking that the underlying operation did not occur. **All three thresholds are read from configuration** — each test lowers its `CFG-` value and asserts the trip point moves, which fails against a hardcoded constant | Disable `UseRateLimiter` — this one **is** safely flaggable: removing it returns the product to today's unlimited behaviour, which is a known Major, not a new one. Record any such rollback as a deviation with a named owner |

**Known-stale work this round invalidates** (see `00-spec-index.md` §14): **CT-001, CT-002,
CT-101, CT-601** against CT-801's specs; **CT-002, CT-302** against CT-802's. **CT-701** (the
full-surface security sweep) must be re-run after both, because its anonymous-surface enumeration
and its negative-authorization sweep were both measured against the pre-fix contract.

## 5. ADR obligations

Any architecturally significant decision made *during* the build (not already recorded as a `T-`/
`D-` in Tech Design) is recorded in `.aidlc/03-development/adr/` per `company.md` §5 — the task
breakdown does not pre-empt those, it only names where they belong when they arise.
