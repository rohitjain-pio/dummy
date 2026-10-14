<!-- pipeline-provenance
produced_by: code-review-agent (Mode D — spec-gate audit)
plugin_version: 0.3.0 (sdlc-greenfield-v3)
model: claude-sonnet-5
generated: 2026-09-11
mode: D (spec-gate audit, before any code exists)
session_context: ran in the coordinator's session (team_mode: team per project.md §1a permits
  this); did not observe the ai-spec build conversation — read only the artifacts on disk
inputs:
  - .aidlc/01-brd-analysis/brd-analyst/final-brd.md
  - .aidlc/01-brd-analysis/brd-analyst/requirements.yaml (214 IDs)
  - .aidlc/02-solution-designer/technical-design/final-tech-design.md
  - .aidlc/02-solution-designer/ui-designer/design-tokens.md
  - .aidlc/02-solution-designer/ui-designer/ui-design-final.md
  - .aidlc/02-solution-designer/ai-specification/ (all 54 files; full suite read for index,
    source-catalogue, security-spec, data-model, error-catalogue, infrastructure-spec,
    task-breakdown, questions-log; full feature-slice read for attendance, academic-records,
    identity-access, configuration-commercial across requirements-spec/business-logic/
    process-flows/api-contracts/ui-spec/test-spec; institutions/communication/operations
    slices spot-checked, not read line-by-line in every concern file)
  - .aidlc/_config/constitution.md
  - .aidlc/_config/company.md, project.md, paths.md
suite_under_audit: spec_suite_version 1.0.0, suite_hash_sha256
  1ca23d50f12720c85d2dc59c3cb35672fc3b897b444679b68525ccb0145adca0 (per 00-spec-index.md)
self_assessed_status_in_suite: BUILD-READY (00-spec-index.md §13)
this_audit_verdict: DRAFT-GATED
-->

# Mode D Spec-Gate Audit — School Management System — Gate 2

**Mode:** D (spec-gate audit, independent of the agent that wrote the suite).
**Session context:** I ran in the coordinator's session (`team_mode: team` per `project.md` §1a,
which permits this). I did **not** watch any part of the ai-spec build conversation — I read only
the artifacts on disk (BRD, requirements.yaml, Tech Design, UI design/tokens, and all 54 files of
the spec suite, plus config) and formed my own judgment before comparing it to the suite's own
self-assessment in `00-spec-index.md` §13.

**What I could not independently check:** I did not re-run `verify-gate.sh` myself in this pass
(the mechanical C1–C10 results were supplied as context, not independently reproduced by me here).
I did not read every one of the 54 files line-by-line — I read the full BRD/requirements.yaml/Tech
Design/UI docs, the entire index/source-catalogue/security-spec/data-model/error-catalogue/
infrastructure-spec/task-breakdown/questions-log/constitution, and the attendance,
academic-records, identity-access, and configuration-commercial slices of business-logic/
requirements-spec/test-spec/process-flows/API-contracts/UI-spec in full (the highest-risk areas:
multi-tenancy, RBAC, the attendance state machine, parent-linking, tier gating, and both binding
operator constraints). I did **not** line-by-line re-verify the institutions/communication/
operations slices of every sharded concern, nor every `TC-`/`API-` row across all 7 feature slugs
— I'm relying on the consistent quality pattern found everywhere I did look, plus the suite's own
internal cross-checks, for those unread portions. This is a real (small) coverage gap in my own
review, flagged rather than glossed over.

---

## Findings

### Blocking
None.

### Major

**M1 — Q-211's provisional choice conflicts with a stated BRD success criterion, on the critical path.**
`ai-specification/_questions.md` Q-211 / `10-process-flows/institutions.md` PF-001: the
provisional choice is "no subscription plan assigned at registration; a null plan is treated as
zero-capacity, blocking all capacity-consuming actions until [the Platform Operator's] FR-003
[assignment] runs." `final-brd.md` §5 states as a Success Criterion: *"An institution can be
onboarded and made fully operational — profile, classes, staff, students and user accounts —
within one working day."* If a freshly-registered institution literally cannot enrol a single
student (FR-011, a capacity-consuming action) until a second, separate Platform Operator step
happens, that is a real tension with "fully operational" on day one — not a hypothetical one,
since P1/CT-101 (institution CRUD) sits on the suite's own stated critical path
(`15-task-breakdown.md` §3). ai-spec itself correctly flagged this as one of three "must-have
path" items deserving explicit confirmation (`_questions.md`, closing section, item 3) — so this
is not a silently-buried gap, but it is genuinely unresolved and load-bearing enough that I'm
elevating it to a Major finding for Gate 2: **get an explicit operator answer to Q-211 before
CT-101 is marked done**, not merely noted as a non-blocking provisional choice.

### Minor

**Mi1 — Mistraced citation in the session-lifecycle state machine.**
`07-business-logic/identity-access.md`, session-lifecycle diagram:
`Authenticated --[permission-denied on an action]--> Authenticated (NFR-010 message shown,
session unaffected)`. NFR-010 is *"The platform maintains 99.5% availability during the
institutional working day"* (`requirements.yaml`) — it has nothing to do with a permission-denial
message. The correct citation is **FR-010** ("The platform informs a user clearly when they lack
permission…") and/or NFR-003. This is exactly the kind of "ID present but doesn't say what the
citation claims" defect the traceability spot-check is meant to catch — a genuine, if small,
sourcing error.

**Mi2 — Inconsistent ID spelling for BR-057's business-logic row across three files.**
`00-spec-index.md` §10 states the BRL-057/058 FR-vs-BR numbering collision was "resolved as
BRL-090/091" (i.e., FR-057/058 moved to 090/091, freeing plain `BRL-057`/`BRL-058` for
BR-057/058). But `07-business-logic/configuration-commercial.md:12` actually writes the row as
**`BRL-057cfg`** (not `BRL-057`), while `07-business-logic/operations.md:23` cross-references it
as plain **`BRL-057`/`BRL-058`** (no suffix), and BR-058's own row in the same
`configuration-commercial.md` file is written as plain `BRL-058` (no suffix) right next to the
suffixed `BRL-057cfg`. Three different spellings for what should be one frozen ID. It's cosmetic
today, but this is precisely the kind of one-hop-ID-resolution promise (`shared-traceability-ids`
§3) that a future `CT-`'s `Specs:` field or `code-generation-spec-analyser` run depends on being
exact. Fix: settle on `BRL-057` everywhere and drop the leftover `cfg` suffix.

**Mi3 — Ambiguous acceptance criterion in the negative-authorization matrix.**
`09-security-spec.md` §6, row N4: *"Student → `GET /dashboard` (leadership variant) → 403, or
silently served the Student-variant endpoint instead (never the leadership payload)."* Every
other row in this matrix (N1–N3, N5–N10) and every `TC-` I read commits to exactly one expected
outcome, consistent with the suite's own adversarial-fixture discipline. N4 alone accepts two
different behaviors as passing, which weakens what a `TC-` built against it can actually assert.
`CT-401`'s DoD line ("Student/Parent variants never expose institution-wide totals") is precise
enough to compensate in practice, but the source-of-truth matrix itself should pick one behavior.

### Observation

**O1 — Both binding operator constraints are genuinely honored, independently re-verified.** I did
not just trust the suite's own claim in `01-source-catalogue.md` §5 and `00-spec-index.md` §7 — I
read `11-ui-spec/operations.md` (Reports) and `11-ui-spec/public-auth.md` (Pricing) directly. The
"Student Satisfaction"/"Course Completion" gauges are omitted outright, not deferred or
relabeled. The Pricing comparison table is explicitly specified to source its cell values from
`SubscriptionPlan.capabilities` at render time, with the mockup's own feature-list copy explicitly
named as non-authoritative. Both constraints pass.

**O2 — Requirement-coverage count independently recomputed, matches.** From `requirements.yaml`'s
own footer (75 functional + 66 business_rule + 33 non_functional + 3 compliance + 17 entity + 7
role active, + 13 excluded FR = 214 total) against the spec suite's claimed 214 — I did not just
read the suite's coverage-matrix row and accept it; I recomputed from the BRD-side spine directly
and it agrees.

**O3 — The attendance correction/lock state machine (the area most likely to hide a real defect)
is thoroughly and adversarially specified.** All four day-boundary cases (day-0, day-29, day-30,
day-31) plus the pending-request-vs-lock race (E2/TC-410) and the leadership-self-approval
question (Q-205) are each named, each has a stated resolution, and each has an adversarial `TC-`
that would fail against a plausible off-by-one or silently-agreeable implementation. This is the
standard the rest of the suite should be held to, and mostly is.

**O4 — 14 non-blocking `Q-` items are real, narrow, and honestly surfaced — not a disguised
BLOCKED gap.** Three of them (Q-204/205/206 attendance-approver mapping, Q-207 FR-064-vs-CFG-001
boundary, Q-211 above) are correctly self-flagged by ai-spec as "must-have path" items needing
operator confirmation before their owning `CT-` hardens. I agree with that self-assessment on all
three, and elevate Q-211 to Major above given its critical-path position and direct tension with a
stated BRD success metric.

**O5 — Multi-tenancy isolation is structurally enforced, not merely tested for the happy path.**
EF Core global query filters (T-002) applied via a shared convention with the PlatformOperator's
bypass confined to one named service class (`PlatformOperatorQueryService`); the
adversarial-fixture rule is explicitly applied to this mechanism (`14-test-spec/_shared.md` §3,
`constitution.md` §2.1). This is the single highest-value control given NFR-016, and it's designed
correctly.

---

## Verdict: **DRAFT-GATED**

This diverges from `00-spec-index.md` §13's self-assessed **BUILD-READY** — that is the point of
this audit.

The suite is genuinely close to build-ready: requirement/design/API/error/security/UI/test/task
coverage all independently check out, both binding operator constraints are honored in fact (not
just in prose), the highest-risk mechanisms (attendance state machine, tenant isolation,
parent-linking, tier gating) are specified with real rigor and real adversarial tests, and I found
no scope drift, no dishonored constraint, and no missing state/error/edge-case lens. Nothing here
is Blocking.

But **M1 (Q-211 vs. the BRD's day-one-operational success criterion)** is a genuine, unresolved
business-facing contradiction sitting on the stated critical path
(P0→P1→P2→P4-reporting→P7), not a cosmetic gap — and it deserves an explicit human answer before
Gate 2 is treated as fully closed, not a provisional default carried silently into CT-101. The two
Minor ID/citation defects (Mi1, Mi2) should be cleaned up before they propagate into code, and
Mi3's ambiguous test row should be tightened. None of these individually justifies
CHANGES-REQUIRED (no restructuring of the suite is needed, no file needs to be rewritten), but
together they mean this is cheaper to close now, at the spec gate, than to discover mid-build —
which is exactly the class of finding this audit exists to catch before it becomes a live defect,
per the charter's own stated rationale.

**Recommended action:** resolve Q-211 explicitly (and ideally Q-204/205/206 and Q-207 at the same
time, since they're already flagged as sitting on must-have paths) and fix Mi1/Mi2/Mi3, then
re-run this Mode D check or have the operator sign off on the residual items by name before Gate 2
is recorded as approved. Build work on phases not touching institution-registration-then-
operability (e.g., P0 foundation, most of P1's academic-records/identity sub-areas) is not blocked
by M1 and can proceed in parallel with that confirmation.

**Key files referenced:**
- `.aidlc/02-solution-designer/ai-specification/_questions.md` (Q-211, Q-207, Q-204–206)
- `.aidlc/02-solution-designer/ai-specification/10-process-flows/institutions.md` (PF-001)
- `.aidlc/01-brd-analysis/brd-analyst/final-brd.md` (§5, Success Criteria)
- `.aidlc/02-solution-designer/ai-specification/07-business-logic/identity-access.md`
  (session-lifecycle diagram, Mi1)
- `.aidlc/02-solution-designer/ai-specification/07-business-logic/configuration-commercial.md`
  and `operations.md` (Mi2)
- `.aidlc/02-solution-designer/ai-specification/09-security-spec.md` (§6, N4, Mi3)

---

# Mode D Spec-Gate Audit — Round 2 — the 1.0.4 patch — 2026-09-15

**Mode:** D (spec-gate audit — the specification is the artifact under audit; no code is being
gated). **Round 2 appended to this file, not a new copy** (`verify-gate.sh` C10).

**Session context.** `project.md` §1a is `team_mode: team`, so running in the coordinator's
session is permitted. I did **not** observe the conversation that produced the 1.0.4 patch. I read
the artifacts on disk and formed the attack re-derivation before reading the patch's own
self-assessment (`00-spec-index.md` §9a/§13). Where I read the running code, it was to test the
Part-3 write-backs and one contract claim — not to review the build.

**Suite under audit:** `spec_suite_version` 1.0.4, suite hash `891ae954e510` (per
`00-spec-index.md`). **Patch self-assessed:** BUILD-READY for CT-801/CT-802.

## What I could NOT check, and why

1. **`scripts/verify-gate.sh` is not present in this repository** (`ls scripts` returns nothing;
   no `verify-gate.sh` anywhere under the project root). I could not independently reproduce the
   C3/C5/C8/C9/C10 results quoted in `00-spec-index.md` §13. The ID-collision, dangling-`ERR-` and
   TC-coverage checks below are my own greps, not the script's.
2. **I did not re-read all 54 files line by line.** I read in full: `00-spec-index.md`,
   `04-data-model.md`, `09-security-spec.md`, `08-error-catalogue.md`,
   `12-infrastructure-spec.md`, `15-task-breakdown.md`, `_upstream-feedback.md`, and the whole
   `identity-access` slice (requirements-spec, business-logic, api-contracts, process-flows,
   test-spec) plus `05-api-contracts/institutions.md`, `11-ui-spec/public-auth.md`, and the
   attendance/communication files the patch touched. The `operations` and
   `configuration-commercial` Part-3 write-backs were spot-checked, not fully verified.
3. **I cannot verify operational facts.** Whether the U38 window was ever reachable from a
   deployed environment, who holds any code, and whether any privileged account was self-elected
   are outside any artifact here (this is exactly U43's point, and I agree with it).
4. **Threshold values cannot be audited, only their provenance.** ASM-007 is correctly recorded as
   an assumption; I have no basis to say 5/10/50 are right or wrong and I make no claim either way.

## VERDICT: CHANGES-REQUIRED

**4 Major, 4 Minor. Zero Critical, zero Blocking.**

**Read this correctly: the CHANGES-REQUIRED is not against the U38 fix.** The fix itself is sound
(see §1). It is against four adjacent defects in the same patch — one of which will break CT-801's
own UI branch, one of which hands the build a security primitive with no key, and one of which is
a coverage claim in §9a that is not true. All four are cheaper here than at P8.

---

## 1. Is the Critical genuinely closed by this specification? **YES.**

I re-derived REV-001 against the new contract adversarially. Every path I could construct is
refused, and the refusals are structural rather than incidental:

| Attack I tried against the 1.0.4 contract | Where it dies |
|---|---|
| `role=Administrator` + directory `institutionId`, no code | VAL-007 is an **allowlist** ({Student, Parent}), not an exclusion list — the Q-202 shape that failed. `ERR-901`, no account, no session (BRL-075 ELSE, E3, N11/N12) |
| Valid `staffCode` **plus** a conflicting `role`/`institutionId` | Body fields are "ignored entirely — not validated, not compared, not merged" (API-006, BRL-075b, E4). Role and institution are read from the code row. TC-210 asserts it, including that the conflict is *not* rejected (which would be an institution oracle) |
| Numeric / undefined enum (`"99"`, the old hole) | An allowlist rejects any value that is not one of two, whether the binder produced a name or a number. Safe **by construction** — but only tested for names, see m2 |
| Empty / whitespace / malformed `staffCode` falling through to the self-service branch | Both readings are safe: treated as present → `ERR-206` (VAL-027); treated as absent → self-service branch → VAL-007 → `ERR-901`. **No reading yields a privileged role.** The branch selector is still under-specified (m2) |
| Code replay, post-expiry, post-revocation | BRL-077 + the state machine make Consumed/Revoked/Expired terminal; `Expired` derived at read time, so there is no sweep to miss. TC-211 covers all three, and asserts no account on any failure branch |
| Code from institution A redeemed for institution B | Impossible — `institution_id` is a column on the code row and is never compared against a body value. The deliberate absence of an institution-mismatch guard is correct here and is stated as intentional |
| Concurrent double-redemption | `04-data-model.md` §2.12a: filtered unique index **plus** conditional `UPDATE … WHERE status='Issued'`; loser gets 0 rows and `ERR-206`. Same treatment as ADR-0009's capacity race. Properly specified, not hand-waved |
| Self-register as Student/Parent, then escalate elsewhere | No other endpoint in `05-api-contracts/` accepts a role from a caller (grepped): `grantedRole` appears only on API-065/066, both authenticated and both `VAL-028`-constrained with `PlatformOperator` excluded. SEC-007 states the invariant prospectively |
| Deactivated tenant | E5 → `ERR-206`. Specified — but untested (M3) |

**TC-209 does not repeat TC-204's mistake.** It is an API-boundary integration test issuing the
exact two-call sequence, asserts on the **raw JSON** (not a DTO that could drop a field), asserts
**no `UserAccount` row**, repeats across all five privileged roles, and then asserts the
**absence of PII access** with whatever token the response yields. TC-204 has been correctly
demoted to "the UI mirrors the boundary" and says so in its own row. This is the right shape, and
the CT-801 gate requiring it to be **seen red first** is the right discipline.

**§1a/SEC-007 is the real fix.** The patch's own diagnosis — that a permission matrix answers
"what may this role do" and never answered "how did this caller get that role" — is correct, and
naming it as a control rather than leaving it implicit in a contract is what stops the next
endpoint reintroducing it.

---

## 2. Findings

### Major

**M1 — `API-005`'s contract does not return the `id` that the entire self-service branch depends
on. CT-801 builds mode A against it.**
`05-api-contracts/institutions.md:88` — success shape `[{ "name", "city", "region" }]`. No `id`.
But `11-ui-spec/public-auth.md` mode A submits `{username, email, password, role, institutionId}`
with the institution taken from "typeahead against `GET /public/institutions`, API-005"; Q-235
(`_questions.md:150`) says the form submits "the **id** of the school chosen from the public
directory"; the 1.0.4 provenance stamp **on that same file** (`institutions.md:6-8`) asserts
"API-005's public directory is unchanged and **still publishes institution ids**"; and the built
code returns `{ id, name, city, region }`
(`src/SchoolSync.Api/Controllers/InstitutionsController.cs:135`).
Four places say `id`; the contract says three fields and none of them is it. This is precisely the
Part-3 class ("write the built behaviour back") applied everywhere except the one contract the
patch's own narrative leans on twice. **A builder implementing CT-801's mode A from API-005 as
written cannot submit an `institutionId` at all.** One line to fix. → **U44**

**M2 — the keyed lookup is specified without a key, and the column cannot hold what it describes.**
`04-data-model.md:331` — `code_lookup` `nvarchar(16)`, "HMAC of the normalised code under the
**JWT-signing-key-class secret**". Three gaps in one row:
- **No key binding.** There is no `CFG-` for it (`12-infrastructure-spec.md` §7 adds only
  CFG-010–013), and it is absent from `09-security-spec.md` §7's secret inventory, which names the
  ACS, SignalR, SQL and JWT-signing secrets and nothing else. "Key-*class*" tells the build what
  kind of secret to use, not which one.
- **No rotation rule.** If the build resolves the ambiguity the obvious way and reuses the JWT
  signing key, then **rotating that key — a routine operation, and mandatory after a suspected
  leak — silently makes every outstanding leadership code return `ERR-206`**, and leaves the
  filtered-unique index spanning two key epochs. Nothing in the suite warns of this coupling.
- **Width vs content.** An HMAC-SHA256 digest is 32 bytes; `nvarchar(16)` holds 16 characters. No
  encoding and no truncation length is specified, so two honest implementations disagree on the
  stored value, and on the uniqueness domain.

Related and worth stating: the suite repeatedly justifies this table as a mechanism "copied, not
reinvented, because that one is already built, tested and audited (CT-103)"
(`07-business-logic/identity-access.md`, `04-data-model.md:321`). The *table shape* and the
*IgnoreQueryFilters precedent* are genuinely copied — I verified ADR-0004 and
`ParentLinkRepository.FindCodeForConsumptionAsync` do exactly what §2.12a claims, and the
exception is correctly scoped (one indexed lookup returning one row; it opens no tenant-boundary
hole). But `ParentLinkCode.Code` is stored **plaintext**
(`src/SchoolSync.Domain/Entities/ParentLinkCode.cs:13`), so **hash + keyed lookup is new, unbuilt,
untested machinery** — the strongest and least-proven part of the patch is the part described as a
copy. → **U45**

**M3 — the only containment control for a disclosed code has no test, and §9a says every new API-
has one.**
Mechanical check of `14-test-spec/`: `API-067` (revoke) — **0 TCs**. `API-068` (list) — **0 TCs**.
`ERR-208` — **0 TCs**. `E5` (redemption against a deactivated institution) — **0 TCs**.
`00-spec-index.md` §9a Test-coverage row: *"Every new `BRL-`/`VAL-`/`API-`/`SEC-` traces to ≥1 new
`TC-`."* That is **false as written** — I report it against the matrix, not only against the gap,
because a self-computed matrix that overstates is the failure mode this audit exists for.
It matters beyond bookkeeping: PF-014 step 2 concedes the out-of-band delivery channel is
unspecified and names "short-lived and single-use" as the compensating control — but the *actual*
control for a code that leaks in transit is revocation (API-067), and revocation is the one path
nothing asserts. TC-211 proves a revoked code cannot be redeemed; nothing proves a code can be
revoked, that `ERR-208` fires on a consumed one, or that a dormant tenant cannot gain staff. → **U46**

**M4 — the bootstrap path silently defeats §2's deliberate PlatformOperator carve-out, and the
suite still asserts the old property.**
`09-security-spec.md` §2's footnote: PlatformOperator "is **not** granted institution-internal
operational capabilities (marking attendance, publishing an announcement) **which would require
impersonating an institution-scoped role**". After 1.0.4 that is exactly what API-065 provides: a
PlatformOperator may mint an `Administrator` code for **any** institution (with no consent from it
— by design, since at bootstrap there is nobody to consent) and redeem it at API-006 themselves.
N9 still asserts the denial as though the property held. The residual control is detection-only:
the `AuditLog` issue row and the account-create row.
I am **not** saying the design is wrong — a bootstrap path must cross the tenant boundary, and the
audit trail is real. The finding is that the spec is **silent where it was previously explicit**:
§1a lists who authorises each role but never says that a PlatformOperator issuing *and* redeeming
is an accepted, audited escalation; §5's new trust boundary covers the anonymous caller and not
this one; and nothing names a detection (e.g. flagging an issue/redeem pair by the same actor, or
binding a code to a named recipient). An accepted risk that is named is a decision; an accepted
risk that is not named is how U38 happened. → **U47**

### Minor

**m1 — the day 1–29 correction was applied in two files and missed in two others.** U11 corrected
the correction window to 1–30 in `07-business-logic/attendance.md:5,53` and TC-409/410. Still
reading the old, wrong window: `09-security-spec.md:84` — *"Approve attendance correction (day
1-29)"* in the permission matrix — and `14-test-spec/attendance.md:19` — TC-406's trace, *"State
machine, day 1–29 branch"*. Same defect class the patch itself called out as "not stale but
actively wrong", left standing in the file that is the primary authority for the permission matrix.

**m2 — TC-209 tests only canonical role *names*, which is not where the old hole was.** Its step
(3) enumerates `Principal`, `SchoolInCharge`, `Teacher`, `PlatformOperator` as strings. The
historical defect class in this codebase was a **numeric-string** role. Add `"role": 2`,
`"role": 99`, a case variant, and `"staffCode": ""` / `"   "` to TC-209/TC-211. Relatedly,
`BRL-075` keys its branch on "`staffCode` absent" while `VAL-027` keys on "when present" — neither
defines empty-or-whitespace. **I re-derived both readings and neither yields a privileged role**
(which is why this is Minor, not Major), but the branch selector should be one sentence, not two
readings.

**m3 — `CFG-012`'s unit contradicts §10.2's ordering rule.** `12-infrastructure-spec.md:92` binds
it as "**5 accounts** per IP", and TC-214 inherits that wording. §10.2(1) requires the limiter to
run **before any uniqueness query or account insert**, and §10.2(3) requires successful requests to
count — i.e. it counts **requests**, and cannot count accounts. §10 resolves the ambiguity, but the
`CFG-` row is what a builder binds. State the unit as requests-to-API-006-regardless-of-outcome.

**m4 — the plaintext sign-up code is a credential that §4 does not classify.** It is returned in
three responses (API-001, API-065, API-066). §4's **Credentials** row covers "password hash,
refresh tokens — never logged, never returned in any API response"; the sign-up code falls in none
of the four classes. SEC-004 gave the room passcode an explicit never-in-a-response-DTO discipline;
this one got "stored hashed" only, so nothing in the suite forbids it landing in request/response
logging or an audit snapshot. One row in §4.

---

## 3. The other pressure points, and what I found there

**Bootstrap and lifecycle (beyond M4).** Complete and unambiguous on the paths that matter:
API-001 issues the first `Administrator` code in the same transaction and is the only response
carrying its plaintext; lost/expired → API-065 re-issues; leaked → API-067 revokes (untested, M3);
never redeemed → the institution simply has no leader until a re-issue, which is recoverable and
**not** a window where anything is weakened. Out-of-band delivery is explicitly unspecified with
its compensating control named (PF-014 step 2) rather than glossed — I accept that framing for
this release. `issued_by_user_id` nullable-at-bootstrap is correct and reasoned.

**Coverage, traceability, IDs.** Re-run independently before reading §9a. No ID was renumbered. No
collisions: `API-065`–`068`, `BRL-075`–`078`, `VAL-027/028`, `ERR-206`–`208`, `SEC-007/008`,
`CFG-010`–`013`, `PF-014`, `TC-209`–`214`/`TC-511`, `N11`–`N17` each define exactly once
(`API-004`/`API-004b` is a pre-existing suffix, not a duplicate). The `API-065`/`API-069`
near-collision is genuinely avoided. Every new `ERR-` resolves to exactly one catalogue row, and
ERR-904's dangling cross-reference is genuinely fixed in both directions. The only coverage claim
that fails is §9a's test-coverage row (M3). One trivial mistrace worth no more than this sentence:
`ERR-208`'s catalogue cause cites BRL-076 where the terminal-state rule it enforces is BRL-077.

**Rate limiting.** Partition keys are sound. RL-1's dual key is the right answer and the reasoning
given for it is correct. RL-3 keyed on `(user, room)` cannot starve a tenant. RL-2 on IP alone is
the only starvation surface — a school behind one NAT — and the spec anticipates exactly that and
makes the threshold operator-tunable for it. **The no-lockout decision is right and its stated
reason is right**; note that it leaves the limiter itself as the only victim-targeted lever, and
RL-1's username partition can be tripped deliberately against a named user — but it expires in 5
minutes and the IP partition means the attacker pays too, which is materially different from a
durable lockout. Does limiting account creation stop the scripted-directory attack? **After the
U38 fix it no longer needs to** — the attack yields no privileged role at any rate. RL-2's honest
value now is bounding anonymous Student/Parent account flooding, which is what §5's trust-boundary
row claims. §10.2(7) discloses the in-process/scale-out weakening rather than hiding it; correct
treatment.

**Part 3 honesty — spot-checked against the build log and the code, and it holds.** CFG-009's
`Asia/Kolkata` matches `AttendanceCalendar.DefaultTimeZoneId:18`. The CT-202-logic/CT-501-schedule
sweep split matches `build-log.md:708,791,1428`, including the partial-DoD record. The ParentAccess
retrofit matches the code exactly, **including the claim I most expected to be normalised** — §2a
says API-048 is gated "in the controller, not by an attribute, because the route is shared", and
`DashboardController.cs:22-24` does precisely that with the same reasoning in a comment, while
API-018/019/036 carry `[RequiresPlanCapability]`. ADR-0004 and
`ParentLinkRepository.FindCodeForConsumptionAsync` match §2.12a's citation of them. ASM-002 is
retired as moot rather than left reading like a live constraint. **I found no write-back that
improved the story.** The one thing Part 3 missed is M1, and that is an omission, not a
normalisation.

**U43 — assessed, not re-reported.** The framing is right, including the part most likely to be
resisted: this is an operator action, no `CT-` DoD can assert it, and closing it as
not-applicable in writing if nothing is deployed is a legitimate close. **One thing a spec *can*
assert here and currently does not:** after CT-801 an account's provenance lives only in an
`AuditLog` row — a JSON snapshot in the table the CMP-001/002/003 retention sweep is designed to
prune. There is no column on `UserAccount` recording which `LeadershipCode` created it. A nullable
`provisioned_by_code_id` FK (null = created before 1.0.4, i.e. **not attributable**) would make
"which privileged accounts cannot be accounted for?" a one-line query that stays true for seven
years, would give the human doing U43's review something to run, and would keep the legacy set
permanently distinguishable instead of merely absent from a log. That is a data-model change with
an owner, so it is recorded as a recommendation on U43 rather than a new item.

---

## 4. Counts, and what unblocks

| Severity | Count |
|---|---|
| Critical | 0 |
| **Major** | **4** — M1/U44, M2/U45, M3/U46, M4/U47 |
| **Minor** | **4** — m1–m4 (recorded here; no `U-` number — they are text corrections owned by the same patch author, listed so none is silently dropped) |
| Blocking questions | 0 |

**Is the Critical closed by this specification? Yes.** No finding above reopens it.

**What unblocks CT-801:** M1 (one line in `05-api-contracts/institutions.md` API-005) and M2 (a
`CFG-` for the lookup key with its rotation consequence stated, plus the column width/encoding).
Both are inputs CT-801 needs on day one — it builds the register panel against API-005 and the
table against §2.12a.

**What unblocks the P8 gate rather than its start:** M3 (TCs for API-067/068, ERR-208, E5) and M4
(name the PlatformOperator issue-and-redeem path as accepted and audited, or add the control), plus
m1–m4.

**CT-802 is not blocked by anything here.** m3 is a one-line wording fix in `CFG-012` that should
land with it.

**I am not approving any gate.** This audit is one input to the human who does.
