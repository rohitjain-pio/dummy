<!-- pipeline-provenance
produced_by: ai-spec-agent
plugin_version: 0.3.0 (sdlc-greenfield-v3)
model: claude-sonnet-5
generated: 2026-09-11
inputs:
  - .aidlc/01-brd-analysis/brd-analyst/final-brd.md (sha256 4c1c753552d1d7e42520590e850180ef2e7c6a4b50fbf91d5af013d6297a0897 — the source BRD it analysed)
  - .aidlc/01-brd-analysis/brd-analyst/requirements.yaml (214 IDs: 75 FR active + 13 FR excluded, 66 BR, 33 NFR, 3 CMP, 17 ENT, 7 ROLE — all priority: must, priority_source: confirmed)
  - .aidlc/02-solution-designer/technical-design/final-tech-design.md
  - .aidlc/02-solution-designer/ui-designer/design-tokens.md
  - .aidlc/02-solution-designer/ui-designer/ui-design-final.md
spec_suite_version: 1.0.5
suite_hash_sha256: 80fe55d289231497bf64c7b68af9cb2f041728634f2001ab698c71bda6e75133
suite_hash_scope: >-
  sha256 of the concatenation (sorted path order) of every .md file in this directory tree
  EXCLUDING this file. Verify with:
    find . -type f -name "*.md" ! -name "00-spec-index.md" | sort | xargs cat | sha256sum
  CHANGED AT 1.0.4, deliberately. The prior scope included this file, which is self-referential
  — recording a hash inside a file that is itself hashed can never converge, which is why 1.0.3
  gave up and carried the 1.0.2 value forward unchanged. Excluding this one file makes the value
  reproducible and therefore actually checkable.
  NOTE: `scripts/verify-gate.sh` C8 uses a DIFFERENT scope — top-level `*.md` only, self-inclusive
  — so its short hash will not match the value above. That is expected, not a discrepancy; C8's
  value is a change detector for the build, not an identity for the suite.
revision_note: "1.0.5 — NARROW PATCH, not a regeneration. Authorised by Rashmi Mehta 2026-09-15, closing the `code-reviewer` Mode D **Round 2** audit (CHANGES-REQUIRED: 4 Major, 4 Minor, zero Critical, zero Blocking) against the 1.0.4 patch. **The U38 Critical is NOT reopened** — Round 2 independently re-derived the attack and confirmed 1.0.4 closes it, and that TC-209 tests the real API boundary; every item here is an adjacent defect in the same patch. Fixes: **U44** API-005's contract omitted the institution `id` that UI-005 mode A and the built code both require (it blocked CT-801 mode A); **U45** the keyed `code_lookup` was specified with no key, no rotation rule and a column too narrow to hold it — now `CFG-SEC-06`, Base64url HMAC-SHA256 in `char(43)`, with rotation stated — and 1.0.4's claim that the scheme was a copy of already-built machinery is corrected (`ParentLinkCode.Code` is plaintext; this is new, unbuilt machinery); **U46** revoke/list/`ERR-208`/E5 had zero TCs while §9a claimed full coverage — TC-215/216/217 added and the matrix corrected; **U47** the PlatformOperator bootstrap escalation is now named as accepted, audited and tenant-visible (SEC-009), with N9 corrected and N18 added. Minors m1–m4: the day 1–30 window in two missed places, TC-209's numeric-enum class plus the `staffCode` branch selector, `CFG-012` counting requests not accounts, and a data class for the plaintext code. **One addition beyond defect-fixing, flagged for the operator to strike: `UserAccount.provisioned_by_code_id` (Q-241).** Every existing ID preserved; nothing renumbered; no closed phase reopened; Gate 2 not reopened. 1.0.4 — NARROW PATCH, not a regeneration. Authorised by Rashmi Mehta 2026-09-15 at the Gate 4 CHANGES-REQUIRED verdict, following the 1.0.3 narrow-patch precedent. Three parts: (1) U38/REV-001 Critical — the suite specified a tenant-takeover hole in API-006 and the code implemented it faithfully; the contract is replaced and the leadership sign-up code added (Q-238/Q-239). (2) U39/REV-002 — 09-security-spec.md §10 (rate limiting) written; it was cited by ERR-904 and had never existed, so nothing was built (Q-240, folds in U16). (3) U11/U15/U20/U26 — the built behaviour of P2-P5 written back into the suite; the code is the source of truth and no code change is implied. Every existing ID preserved; nothing renumbered; no closed phase reopened. 1.0.3 — upstream feedback U1–U6 accepted as built (decided by Rashmi Mehta, 2026-09-14); spec text corrected to match the implemented and tested behaviour. 1.0.2 — Q-204/205/206/207 operator sign-off applied (all four accepted as originally proposed); hedging language firmed to final wherever those questions were cited. 1.0.1 was the prior post-Mode-D-audit fix pass (Q-211 resolved; Mi1/Mi2/Mi3 minor fixes; ROLE-003-006 citation fix). See §13 for the itemized list."
-->

# 00 — Spec Index

**Written last**, per charter. This is the suite's single entry point — provenance, registry,
both reading orders, architecture profile, shard map, coverage matrix, DoD result, glossary,
assumption register, question log, and the spec-suite version/hash.

> ## ⚠ Revision 1.0.5 — read §14 before using this suite
>
> **1.0.5 closes the Mode D Round-2 audit against the 1.0.4 patch** (4 Major, 4 Minor; zero
> Critical, zero Blocking). **It does not reopen the U38 Critical** — Round 2 re-derived the attack
> independently and confirmed 1.0.4 closes it. What it fixes are four adjacent defects in that same
> patch, two of which **blocked CT-801 on day one**: API-005's contract omitted the institution
> `id` mode A submits (**U44**), and the keyed code lookup was specified with **no key, no rotation
> rule and a column too narrow to hold its own value** (**U45**). It also adds the tests for the
> only containment control a leaked code has (**U46**) and names the PlatformOperator bootstrap
> escalation as accepted-and-audited rather than leaving the suite silent about it (**U47**).
>
> **One change in 1.0.5 is not a defect fix: `UserAccount.provisioned_by_code_id`** — added on the
> orchestrator's judgement from the auditor's U43 recommendation, boxed in `04-data-model.md` §2.2,
> and **explicitly strikeable by the operator (Q-241)**.
>
> **The warning below still stands in full: `CT-801` is still not built.**
>
> ## ⚠ Revision 1.0.4 — the Critical this suite specifies is still not in the code
>
> **A Critical security correction is in these specs and is NOT in the running code.** Patch 1.0.4
> replaced API-006's contract, which specified a tenant-takeover hole that the build implemented
> faithfully (U38 / REV-001). **`CT-801` is not built.** Until it is, an unauthenticated caller can
> still obtain institution-administrator access to any tenant in the deployed system. §14 lists
> every stale `CT-` and what unblocks them.
>
> This was a **narrow patch, not a regeneration**: 27 CTs across eight closed, gate-approved phases
> were built against this suite. Every existing ID is preserved and nothing was renumbered.

**Revision 1.0.2** — operator sign-off on Q-204/205/206/207 applied (all confirmed as originally
proposed, 2026-09-11, Jayesh Idnani); every place those questions were cited as provisional now
reads as a confirmed final rule. **Revision 1.0.1** (prior) was a fix pass on the 1.0.0 suite
following `code-reviewer` Mode D's independent spec audit (`.aidlc/04-code-review/Spec_Audit.md`)
and the operator's decisions on its findings. See §13 for the itemized list of what changed and
why in each revision. Per `shared-artifact-conventions` this revises the existing files in place
— no `_v2`/dated copy was created.

## 1. Provenance stamp

Produced by the AI Spec Agent (Beat 3 of `solution-designer`) after Beat 1 (Tech Design) and
Beat 2 (UI Enrichment) were both operator-approved. Reads `final-brd.md` + `requirements.yaml`
(Stage 1, read-only), `final-tech-design.md` (Stage 2/Beat 1, read-only), `design-tokens.md` +
`ui-design-final.md` (Stage 2/Beat 2, read-only). Two binding operator constraints carried into
this run (Reports gauges, Pricing feature-copy) are honored throughout — see §7.

## 2. Generation order (the order this suite was actually built in)

1. `01-source-catalogue.md` — placement check, feature-slug map, architecture flags
2. `03-system-design.md` — architecture profiler verdict (UI=yes, ASYNC=yes, LEGACY_DATA=no,
   DATA_PIPELINE=no, ML=no, multi-service=no)
3. Sharding decided (feature-slug axis, 7 slugs — no service axis)
4. `07-business-logic/` (7 feature files + `_shared.md`) — generated early; API/security/
   validation all depend on it
5. `04-data-model.md`
6. `08-error-catalogue.md`
7. `05-api-contracts/` (7 feature files) + `06-event-contracts.md` (ASYNC trigger)
8. `09-security-spec.md`
9. `10-process-flows/` (7 feature files)
10. `11-ui-spec/` (7 files: `public-auth`, `academic-records`, `attendance`, `communication`,
    `operations`, `configuration-commercial`, `_shared`) — UI trigger
11. `12-infrastructure-spec.md`
12. `14-test-spec/` (7 feature files + `_shared.md`)
13. `15-task-breakdown.md`
14. Coverage gate (§8 below)
15. `.aidlc/_config/constitution.md` (constitution-writer, last of all)
16. `00-spec-index.md` (this file, absolutely last)
17. `_questions.md` (maintained throughout, finalized alongside this file)

## 3. Reading order (the order a new reader — human or `code-generation` — should consume this suite)

1. `00-spec-index.md` (this file) → 2. `01-source-catalogue.md` → 3. `02-requirements-spec/` →
4. `03-system-design.md` → 5. `04-data-model.md` → 6. `05-api-contracts/` +
`06-event-contracts.md` → 7. `07-business-logic/` → 8. `08-error-catalogue.md` →
9. `09-security-spec.md` → 10. `10-process-flows/` → 11. `11-ui-spec/` →
12. `12-infrastructure-spec.md` → 13. `14-test-spec/` → 14. `15-task-breakdown.md`.
**In practice, `code-generation` never reads this whole order for one task** — it resolves only
the exact files a `CT-`'s `Specs:` field names (§9 below), per `shared-traceability-ids`.

## 4. Architecture profile (full detail: `03-system-design.md`)

Full-stack, multi-tenant SaaS. One ASP.NET Core Web API + one React SPA. UI = yes (20 screens).
ASYNC = yes (SignalR + Hangfire). LEGACY_DATA = no. DATA_PIPELINE = no. ML = no.
multi-service = no (feature-slug sharding only, no per-service shard axis).

## 5. Feature slug list and shard map — THE authority for resolving any sharded ID

| Slug | `02-requirements-spec/` | `05-api-contracts/` | `07-business-logic/` | `10-process-flows/` | `11-ui-spec/` | `14-test-spec/` |
|---|---|---|---|---|---|---|
| `institutions` | ✅ | ✅ | ✅ | ✅ (PF-001) | — (folded into `public-auth`/`configuration-commercial`) | ✅ (TC-101–106) |
| `identity-access` | ✅ | ✅ | ✅ | ✅ (PF-002/003/014) | — (folded into `public-auth`) | ✅ (TC-201–214) |
| `academic-records` | ✅ | ✅ | ✅ | ✅ (PF-004/005) | ✅ (`academic-records`) | ✅ (TC-301–309) |
| `attendance` | ✅ | ✅ | ✅ | ✅ (PF-006/007) | ✅ (`attendance`) | ✅ (TC-401–414) |
| `communication` | ✅ | ✅ | ✅ | ✅ (PF-008/009) | ✅ (`communication`) | ✅ (TC-501–511) |
| `operations` | ✅ | ✅ | ✅ | ✅ (PF-010/011) | ✅ (`operations`) | ✅ (TC-601–609) |
| `configuration-commercial` | ✅ | ✅ | ✅ | ✅ (PF-012/013) | ✅ (`configuration-commercial`) | ✅ (TC-701–707) |
| `_shared` | ✅ (NFR/CMP) | — | ✅ (tenant scoping, soft-delete, audit, retention) | — | ✅ (Unauthorized/NotFound + cross-cutting patterns) | ✅ (strategy, adversarial rule, perf scenarios) |

**UI shard note:** `institutions` and `identity-access` have no dedicated `11-ui-spec/` file —
their screens (public directory search, Auth modal, institution profile edit) are physically
hosted in `11-ui-spec/public-auth.md` and `11-ui-spec/configuration-commercial.md` because those
are where the *screens* naturally group (Auth modal, Settings), even though the *requirement*
belongs to a different feature slug. This is the one place in the suite where the UI shard axis
diverges from the requirement/logic/contract shard axis — resolve a `UI-` ID via §6's screen
table below, never by assuming it lives in the same-named file as its FR.

**Six concerns never shard** (confirmed applied): `03-system-design.md`, `04-data-model.md`,
`08-error-catalogue.md`, `09-security-spec.md`, `12-infrastructure-spec.md`,
`15-task-breakdown.md`.

## 6. Screen (`UI-`) → file resolution table

| UI ID | Screen | File |
|---|---|---|
| UI-001–004 | Home, About, Contact, Pricing | `11-ui-spec/public-auth.md` |
| UI-005 | Auth modal | `11-ui-spec/public-auth.md` |
| UI-006 | App shell | `11-ui-spec/public-auth.md` |
| UI-007–009 | Classes, Teachers, Students | `11-ui-spec/academic-records.md` |
| UI-010 | Attendance | `11-ui-spec/attendance.md` |
| UI-011–014 | Announcements, Meetings (room list), Chat, Video call | `11-ui-spec/communication.md` |
| UI-015–017 | Dashboard, Schedule, Reports | `11-ui-spec/operations.md` |
| UI-018 | Settings | `11-ui-spec/configuration-commercial.md` |
| UI-019–020 | Unauthorized, Not Found | `11-ui-spec/_shared.md` |

All 20 screens from `ui-design-final.md` are accounted for. None omitted, none invented.

## 7. Two binding operator constraints — honored, and where

1. **Reports gauges** ("Student Satisfaction", "Course Completion") — omitted entirely from
   `11-ui-spec/operations.md` §UI-017, `05-api-contracts/operations.md` (API-050's report-type
   list), and `01-source-catalogue.md` §5. Not built, not deferred, not flagged as a future
   enhancement.
2. **Pricing feature-copy** — `11-ui-spec/public-auth.md` §UI-004 builds the Pricing screen's
   *structure* (toggle, 3 cards, comparison table, FAQ) but sources the comparison table's cell
   values from `SubscriptionPlan.capabilities` (the real entitlement model, BR-061/062) at render
   time, never from the mockup's own copy — stated explicitly in that section as a load-bearing
   constraint, not a footnote.

## 8. Master ID registry (ranges, for quick lookup — full definitions live in the concern files)

| Prefix | Range used | Defined in |
|---|---|---|
| `SPEC-FR-` | 001–075 (mirrors `FR-` 1:1; 076–088 excluded, no SPEC- minted) | `02-requirements-spec/` |
| `SPEC-NFR-` | 001–033 (mirrors `NFR-` 1:1) | `02-requirements-spec/_shared.md` |
| `DES-` | not separately minted this suite — design elements are covered by `03-system-design.md`'s component inventory directly | `03-system-design.md` |
| `BRL-` | 001–066 (mirrors `BR-` 1:1) + 067, 070, 071, 073, 074 (FR-derived, no BR collision) + **075–078 (1.0.4 — role provisioning and rate limiting)** + 090–091 (FR-057/058, deliberately outside the BR range to avoid colliding with BR-057/058) | `07-business-logic/` |
| `VAL-` | 001–026 + **027–028 (1.0.4)** | `07-business-logic/` (sequential across feature files, no reuse) |
| `API-` | 001–064 + **065–068 (1.0.4, leadership sign-up code)** + **069 (1.0.4, numbering the plan editor built unnumbered in CT-503)**, plus the suffixed **059a/059b** (rollover supporting endpoints, built in CT-502 — suffixed rather than appended so API-060–064 did not have to move) | `05-api-contracts/` |
| `EVT-` | 001–006 | `06-event-contracts.md` |
| `ERR-` | 101–102, 201–205 + **206–208 (1.0.4)**, 301–310, 401–403, 501–506, 701–703, 801–802, 901–904 | `08-error-catalogue.md` |
| `SEC-` | 001–006 + **007–008 (1.0.4)** + **009 (1.0.5 — the accepted, audited PlatformOperator bootstrap escalation, U47)** | `09-security-spec.md` |
| `N-` | N1–N10 + **N11–N17 (1.0.4)** + **N18 (1.0.5 — the matrix's first expected-to-SUCCEED row, U47)** *(negative-authorization matrix rows; not a spec-item series, listed for navigation)* | `09-security-spec.md` §6 |
| `PF-` | 001–013 + **014 (1.0.4)** | `10-process-flows/` |
| `UI-` | 001–020 (**no new screen at 1.0.4** — UI-005 was revised in place) | `11-ui-spec/` (§6 table) |
| `CFG-` | 001–008 + **009 (recorded 1.0.4, proposed by the build at Q-215)** + **010–013 (1.0.4)** (constants) + `CFG-SEC-01..05` + **`CFG-SEC-06` (1.0.5 — the leadership-code lookup key, U45a)** (secret references) | `12-infrastructure-spec.md` |
| `TC-` | 101–106, 201–208 + **209–214 (1.0.4)** + **215–217 (1.0.5 — revoke, list, and the deactivated-institution edge case, U46)**, 301–309, 401–412 + **413–414 (1.0.4)**, 501–510 + **511 (1.0.4)**, 601–609, 701–707 | `14-test-spec/` |
| `CT-` | 001–004 (P0), 101–105 (P1), 201–203 (P2), 301–304 (P3), 401–403 (P4), 501–504 (P5), 601–604 (P6), 701 (P7), **801–802 (P8, 1.0.4 — the Gate 4 fix round)** | `15-task-breakdown.md` |
| `Q-` | 201–240 + **241 (1.0.5 — keep or strike `provisioned_by_code_id`)** *(corrected at 1.0.4 — this row read "201–214" and was 26 questions out of date: Q-215–Q-230 were the P2–P5 pre-build decisions, Q-231–Q-237 the P6 retrospective record, Q-238–Q-240 the Gate 4 decisions)* | `_questions.md` |
| `ASM-` | 001–005 + **006–007 (1.0.4)** + **008 (1.0.5 — the `code_lookup` encoding and rotation scheme)** | §10 below (001–005 carried from Tech Design §11, restated here for this suite's own completeness) |
| `U-` | 001–047 *(not this agent's series — `code-review` and `code-generation` mint it; listed for navigation. U44–U47 are the Mode D Round-2 findings this patch closes)* | `_upstream-feedback.md` |

**ENT- was deliberately not minted at 1.0.4.** `LeadershipCode` is a mechanism table derived from a
design decision, exactly as `ParentLinkCode` and `RoomMembership` are — `ENT-` belongs to Stage 1
(the BRD Analyst) and is not this agent's to assign.

## 9. Multi-lens coverage matrix

| Lens | Result |
|---|---|
| **Requirement coverage** | 75/75 active `FR-`, 66/66 `BR-`, 33/33 `NFR-`, 3/3 `CMP-` placed into exactly one feature slug and expanded into a `SPEC-`/`BRL-`/`VAL-` row. 13/13 excluded `FR-` correctly carried as excluded, never built. **PASS.** |
| **Design coverage** | Every Tech Design decision referenced (D1–D14, T-001–T-006, SP-001) traces into at least one spec file — see `01-source-catalogue.md` §3 join table and inline citations throughout. **PASS.** |
| **API/contract coverage** | All representative endpoints in Tech Design §7 are expanded into full `API-` contracts (request/response/validation/errors/idempotency/example) in `05-api-contracts/`. **PASS.** |
| **Error coverage** | Every `ERR-` cited anywhere in the suite resolves to exactly one row in `08-error-catalogue.md`; no error worded inline elsewhere. **PASS** (spot-checked: institutions, identity-access, academic-records, attendance, communication, operations, configuration-commercial ranges all present). |
| **Security coverage** | Full 7-role × capability matrix + tier-entitlement axis in `09-security-spec.md` §2; negative-authorization matrix (N1–N10) in §6, referenced by every `14-test-spec/` file rather than re-specified. **PASS.** |
| **UI coverage** | 20/20 screens from `ui-design-final.md` specified; every visual value cites a `design-tokens.md` token name — zero literal hex/px found on inspection of `11-ui-spec/`. **PASS**, with one open UI-authoring gap (Q-213, sidenav width token) explicitly flagged, not silently guessed. |
| **Test coverage** | Every `SPEC-FR-`/`SPEC-NFR-` traces to ≥1 `TC-`; adversarial-fixture rule applied explicitly to the five highest-risk mechanisms (tenant filter, attendance lock/correction, parent-link code, attendance upsert, audit interceptor) per `14-test-spec/_shared.md` §3. **PASS.** |
| **Task coverage** | Every feature slug's endpoints/entities/screens map to at least one `CT-`; phase table + parallel map + critical path stated. **PASS.** |
| **Traceability (both directions)** | Every spec-layer ID cites a Stage 1/2 source ID; every active Stage 1 ID is cited by ≥1 spec-layer ID. Spot-checked bidirectionally for `FR-035` (attendance lock) and `BR-063` (tier gating) — both resolve cleanly in both directions. **PASS.** |


### 9a. Coverage re-check for the 1.0.4 patch only (the §9 matrix above stands unchanged)

A narrow patch does not re-run the whole matrix — nothing in the requirement spine moved, no `FR-`
was added or dropped, and `requirements.yaml` is untouched. What was re-checked is what the patch
touched:

| Lens (patch scope) | Result |
|---|---|
| **Requirement coverage** | No change. **FR-006 itself is unchanged** — Stage 1 is read-only. What changed is `SPEC-FR-006`, its spec-layer expansion, which had over-read FR-006's "a chosen institution and role" as *chosen by the registrant*. FR-006's "no additional approval step" is preserved on both branches. **214/214 active requirement ids still appear in the suite** (verify-gate C3 PASS). |
| **Error coverage** | **PASS.** Every `ERR-` referenced anywhere in the suite resolves to exactly one row in `08-error-catalogue.md` — re-checked mechanically across all 34 files after the patch, 0 dangling. ERR-206/207/208 added; **ERR-904's dangling cross-reference was the one pre-existing failure of this lens and is now fixed.** No error is worded outside `08`. |
| **Security coverage** | **PASS**, and materially stronger: the matrix now answers "how does a caller *come to hold* a role" (§1a/SEC-007), which it did not, and that gap is the whole of U38. Negative matrix extended N11–N17. |
| **Test coverage** | ~~**PASS.** Every new `BRL-`/`VAL-`/`API-`/`SEC-` traces to ≥1 new `TC-`.~~ **THIS CLAIM WAS FALSE AS WRITTEN AT 1.0.4, and is corrected here rather than quietly rewritten (U46).** A mechanical re-check by the Round-2 auditor found **`API-067` (revoke) 0 TCs, `API-068` (list) 0 TCs, `ERR-208` 0 TCs, and edge case `E5` 0 TCs** — and revocation is the *only* containment control for a sign-up code that leaks in transit, which PF-014 step 2 was simultaneously leaning on. **A self-computed coverage matrix that overstates is worse than no matrix**, because it converts an absence into a recorded assurance; this row is the one place in the 1.0.4 patch where that happened, and stating it plainly is the point. **Now PASS at 1.0.5**, on the same claim, verified by re-running the same check: TC-215 (API-067, ERR-208, and a revoked code already in a third party's hands), TC-216 (API-068, including that it never returns `code`/`code_hash` and that PlatformOperator-issued codes are visible to the tenant) and TC-217 (E5). TC-209 remains adversarial by construction and asserts absence-of-PII-access rather than a status code. |
| **Traceability (both directions)** | **PASS.** Every ID minted here cites a Q-/U-/REV- source; every changed spec item names what changed and why. Spot-checked bidirectionally for `SPEC-FR-006` and `CFG-012`. |
| **Cross-file consistency** | **PASS.** "Session token for the SignalR hub group" (contradicted by `RoomMembership`) and the day-1–29-vs->30 contradiction in `07-business-logic/attendance.md` were the two live self-contradictions; both resolved. One near-collision caught and avoided: the plan editor would have taken `API-065`, already used by the leadership-code endpoint — it is `API-069`. |
| **Task coverage** | **PASS.** Both new spec bundles map to a `CT-` with `Specs:`, gate, DoD and rollback. Every stale `CT-` is listed in §14. |
| **No scope creep** | **PASS.** Four new endpoints, all of which exist solely to serve Q-238's operator decision; no feature, entity or role absent from the three sources was added; **no `ENT-` and no `FR-` minted.** |
### 9b. Coverage re-check for the 1.0.5 patch only (§9 and §9a both stand as written above)

Same discipline as §9a: a narrow patch re-checks what it touched. **Nothing in the requirement
spine moved** — `requirements.yaml` is untouched, no `FR-`/`NFR-`/`BR-`/`CMP-` was added, dropped
or re-prioritised, and no `ENT-` was minted. **Re-run before writing this table, not derived from
§9a**, because §9a is precisely the row that overstated last time.

| Lens (patch scope) | Result |
|---|---|
| **Test coverage** | **PASS**, and this time checked the way the auditor checked it rather than asserted. Mechanical grep of `14-test-spec/` for every ID the 1.0.4/1.0.5 bundles introduce: `API-065` → TC-212; `API-066` → TC-212; **`API-067` → TC-215**; **`API-068` → TC-216**; `ERR-206` → TC-210/211/215/217; `ERR-207` → TC-211; **`ERR-208` → TC-215**; `ERR-904` → TC-213/214/511; `BRL-075/076/077/078` → TC-202/209/210/211/212/213/214; `VAL-007` → TC-209; `VAL-027` → TC-209/211; `VAL-028` → TC-212; `SEC-007` → TC-209; `SEC-008` → TC-213/214/511; **`SEC-009` → TC-216 (+N18)**; `E3` → TC-204/209; `E4` → TC-210; **`E5` → TC-217**. **Zero at 0 TCs.** |
| **Contract coverage** | **PASS.** API-005's success shape now lists the `id` the built controller returns, UI-005 mode A submits and Q-235 names (U44) — the one contract where 1.0.4's own provenance stamp asserted a property the contract body then omitted. API-006's branch table defines "absent" and states the failing-code branch. API-068 gains its security contract. |
| **Security coverage** | **PASS**, and honest where it previously was not. `CFG-SEC-06` is in **both** the secret inventory (`09-security-spec.md` §7) and the `CFG-` matrix (`12-infrastructure-spec.md` §6) — 1.0.4 had it in neither, so the build was told to key an HMAC with a *class* of secret. §4 now classifies the plaintext sign-up code. The PlatformOperator bootstrap escalation is **named** (SEC-009), N9's over-broad claim is corrected, and N18 is the matrix's first expected-to-succeed row. |
| **Cross-file consistency** | **PASS.** Three live contradictions closed: API-005's stamp vs its body (U44); `nvarchar(16)` vs an HMAC-SHA256 digest (U45c); `CFG-012` counted in accounts vs §10.2's requirement that the limiter run before any insert (m3). Two stale texts corrected: the day 1–29 window in `09-security-spec.md` §2 and TC-406's trace (m1). One **claim** corrected rather than a contradiction: "the mechanism is already built, tested and audited" — `ParentLinkCode.Code` is plaintext (U45d). |
| **Traceability (both directions)** | **PASS.** Every ID minted at 1.0.5 (`SEC-009`, `CFG-SEC-06`, `TC-215/216/217`, `N18`, `ASM-008`, `Q-241`) cites its U-/m- source and is cited from at least one other file. Nothing renumbered. |
| **No scope creep** | **PASS, with one declared exception that is NOT creep-by-stealth.** No feature, entity, endpoint or role was added; no `FR-`, no `ENT-`. **`UserAccount.provisioned_by_code_id` is one nullable FK added on the orchestrator's judgement, not to close a defect** — it is boxed in `04-data-model.md` §2.2, carries `Q-241`, and is explicitly strikeable. Declaring it is the difference between an addition and creep. |

## 10. Definition of Done — result

| DoD item | Status |
|---|---|
| No Blocking question outstanding | ✅ — zero Blocking; 14 non-blocking, all inline-marked and logged |
| Every active requirement placed | ✅ (§9 requirement-coverage row) |
| Every spec item testable | ✅ — every `SPEC-`/`BRL-`/`API-` row states an acceptance criterion or is directly exercised by a named `TC-` |
| No scope creep | ✅ — `01-source-catalogue.md` §5 names every exclusion explicitly; the two operator-flagged mockup artifacts are omitted, not built |
| Six non-sharding concerns are single files | ✅ |
| Cross-file consistency | ✅ — `ERR-`/`BRL-`/`VAL-`/`PF-` cross-references checked and corrected during this pass (two numbering collisions found and fixed: `BRL-057/058` FR-vs-BR collision resolved as `BRL-090/091`; several stale `PF-`/file cross-references from early-drafted files corrected to match final numbering) |
| Provenance stamp on every file | ✅ |
| `spec_suite_version` + suite hash recorded | ✅ (header of this file) |

**Assumption register (`ASM-`, carried from Tech Design §11, restated for this suite's traceability):**

| ASM | Statement | Affects |
|---|---|---|
| ASM-001 | Report generation is async-enqueue/complete, not a blocking HTTP call | FR-054/055, PF-010 |
| ASM-002 | Dashboard presence cache invalidates on same-day attendance writes only | FR-052, BR-050 |
| ASM-003 | `ParentLinkCode` length/expiry (8/7-day) mirrors the room-passcode pattern's implied scale | T-003, `CFG-005` |
| ASM-004 | Trial-to-entry reversion reuses the rollover scheduler class | FR-070/071, PF-013 |
| ASM-005 | `PublicInstitutionDirectoryProjection` refreshes synchronously on institution create/update/withdraw | FR-005, D12 |
| **ASM-006** | **`LeadershipCode` length/expiry (8 chars / 7 days, `CFG-010`) mirrors `ParentLinkCode`'s.** *Basis:* the mechanism is a deliberate copy of `ParentLinkCode` (Q-238), so the same scale applies; nothing in the BRD, Tech Design or `requirements.yaml` gives a figure for either. *Correct-if-wrong:* change `CFG-010`; it is a configuration value read at issue time, so no data migration follows. **This assumption may be WRONG** — a leadership code is higher-value than a parent link code and an argument exists for a shorter window. It is recorded, not decided. | `CFG-010`, BRL-076/077, API-065/066 |
| **ASM-007** | **The three rate-limit thresholds (`CFG-011`/`012`/`013`) are engineering defaults with no source.** *Basis:* NFR-017 requires a limit and names no figure; nothing in the BRD, Tech Design or `requirements.yaml` supplies one. Chosen to sit comfortably above ordinary human use (three mistyped passwords still get you in; a family behind one connection can register several children) and far below a useful attack rate. *Correct-if-wrong:* they are configuration — an operator tunes them against real traffic with no code change, which is exactly why the spec forbids hardcoding them and each test asserts the trip point moves when the value does. **Getting the numbers wrong is recoverable; having none at all was not, and that is what U39 found.** | `CFG-011`–`013`, BRL-078, SEC-008, §10 |

| **ASM-008** | **The `code_lookup` scheme's encoding and rotation behaviour are engineering choices, not sourced decisions.** *Statement:* `code_lookup = BASE64URL(HMAC-SHA256(CFG-SEC-06, normalise(code)))`, stored untruncated as `char(43)`; rotating `CFG-SEC-06` invalidates every outstanding `Issued` code, with a mandatory revoke-then-reissue procedure and **no dual-key read window**. *Basis:* the Round-2 audit required *an* encoding, *a* width and *a* rotation rule (U45); it did not specify these. Untruncated because truncation would demand a collision argument and buys nothing at 43 bytes. No dual-key window because a second read path doubles the brute-force surface to buy at most `CFG-010` (7 days) of convenience. *Correct-if-wrong:* **the encoding and width are NOT recoverable by configuration** — changing them after CT-801 ships is a migration over a column whose plaintext inputs no longer exist, i.e. a forced revoke-and-reissue of every live code. **This is the one assumption in this suite that is expensive to reverse, which is why it is recorded rather than left implicit.** The rotation *procedure* is cheap to change. | `04-data-model.md` §2.12b, `CFG-SEC-06`, BRL-077, API-006/065/066/067 |

**ASM-002 is moot as of 1.0.4** — "the dashboard presence cache invalidates on same-day attendance
writes only" assumed a cache. **No cache was built**: CT-401 computes live, per Q-224's preference
for traceable data over a cached figure nobody can explain. Recorded here rather than left as a
dangling assumption that reads like a live design constraint.

## 11. Glossary (business terms only — carried from `final-brd.md` §16; no new terms coined here)

Institution, Platform Operator, Institution Administrator, Guardian, Class, Attendance outcome,
Leave (attendance outcome), Collaboration Room, Trial, Subscription Tier — full definitions in
`final-brd.md` §16. This suite introduces no new business vocabulary; spec-layer terms
(`SPEC-`, `BRL-`, etc.) are ID conventions, not domain terms.

## 12. Question log summary

14 non-blocking questions were raised (`Q-201`–`Q-214`), zero Blocking. **Q-204, Q-205, Q-206,
Q-207, and Q-211 are now resolved** — all operator-confirmed 2026-09-11 (Jayesh Idnani), Q-211
following the Mode D spec audit and Q-204/205/206/207 in the operator's follow-up sign-off, each
accepted as originally proposed. The remaining 9 (`Q-201`–`203`, `Q-208`–`Q-210`, `Q-212`–`Q-214`)
stay provisional. Full text and current status: `_questions.md`.

**Updated at 1.0.4.** The count above was written at 1.0.2 and is long out of date — the log now
runs to **Q-240**. What matters for this patch:

- **Q-238, Q-239, Q-240 (Rashmi Mehta, 2026-09-15)** are the operator's binding decisions behind
  it. All three are **applied to the spec** and **not yet built** (`CT-801`, `CT-802`).
- **Q-202 is SUPERSEDED by Q-239.** Its provisional choice excluded only `PlatformOperator` from
  the register dropdown, leaving the three leadership roles and Teacher self-selectable. **That
  provisional choice was built against, hardened, and shipped through eight gates** — it is the
  Critical. Worth stating plainly: the mechanism for surfacing provisional choices worked (it was
  marked `[NEEDS ANSWER — Q-202]` in `07-business-logic/identity-access.md` the whole time); what
  failed is that nobody was required to answer it before it was hardened.
- **Q-235 is SUPERSEDED for privileged roles only.** The register form still submits a
  directory-chosen institution id on the Student/Parent path.
**Updated at 1.0.5.** The log now runs to **Q-241**.

- **Patch 1.0.5 raised exactly one new question, `Q-241` — non-blocking, and it is about the one
  change in the patch that closes no defect:** keep or strike
  `UserAccount.provisioned_by_code_id`. Provisional answer **KEEP**, with the basis and the
  correct-if-wrong both stated. **It sits on CT-801's migration, so it is cheapest to answer before
  CT-801 is built and materially more expensive after** — which is exactly the shape of thing this
  suite is supposed to surface rather than bury, given that Q-202 was a marked provisional choice
  that got hardened through eight gates and turned out to be the Critical.
- **Five decisions were taken inside the patch without a question** (the lookup key's separation
  from the JWT key, the encoding and width, the rotation procedure, the `staffCode` branch
  selector, and the choice to name the PlatformOperator escalation rather than prevent it). All
  five follow directly from the audit's own direction; all five are written out at the end of
  `_questions.md`, and the two with lasting consequences are **ASM-008**.
- **Zero Blocking questions**, at 1.0.5 as at 1.0.4.
- **The 1.0.4 patch raised no new question.** Two things it could not decide were routed as owned
  work items instead of answered silently: **U42** (the monospace family has no token name — a
  Beat-2 file this agent does not write) and **U43** (accounts created during the U38 exposure
  window cannot be shown to be legitimate — an operator action no `CT-` DoD can assert).
## 13. Gate result and independent-verification notice

### 1.0.5 gate result (2026-09-15) — **PASS, scoped to this patch**

**Coverage gate: PASS.** Three checks, in order:

1. **Per-file self-check** — run before each of the 12 amended files was written. All pass.
2. **Cross-file reconciliation** — the three live contradictions this patch existed to close are
   closed (API-005's stamp vs its body; `nvarchar(16)` vs an HMAC-SHA256 digest; `CFG-012`'s unit
   vs §10.2's ordering rule), and no new one was introduced. `CFG-SEC-06` resolves in both files
   that must name it. `char(43)` appears in exactly one authority (`04-data-model.md` §2.12b) and
   is cited, not restated, everywhere else. Every `TC-` cited from CT-801's gate exists.
3. **Multi-lens matrix + DoD** — §9b. **Zero Blocking questions. No `[NEEDS ANSWER]` marker blocks
   any `CT-`.** `Q-241` is non-blocking and names the single line it affects.

**What this gate does NOT say.** It does not re-certify §9's full-suite matrix (unchanged, and out
of this patch's scope), it does not reopen Gate 2, and it does not assert that the twelve
still-open upstream items (U21, U27, U28, U30–U33, U36, U37, U40–U43, REV-003/004/005/006/007) are
resolved. **They are not, and none was touched.**

**`scripts/verify-gate.sh` could not be run: it is not present in this repository** — the Round-2
auditor recorded the same absence. The C3/C5/C8/C9/C10 results quoted for 1.0.4 were never
independently reproduced, and that remains true at 1.0.5. **C8 will WARN** until a `CT-` is
recorded against the new hash; that warning is a genuine signal, not noise.

> **This is my own assessment of my own output — and the 1.0.4 patch is the reason to take that
> caveat literally rather than as boilerplate.** At 1.0.4 this same section self-reported
> BUILD-READY with a test-coverage row claiming full `TC-` coverage of every new `API-`; an
> independent Mode D pass then found four `API-`/`ERR-`/edge-case IDs at **zero** tests, plus a
> contract that would have blocked CT-801 on its first day. **A self-graded gate did not catch
> either.** Run `bash scripts/verify-gate.sh` once it exists, and **have the code-review agent run
> Mode D again against 1.0.5** before this suite is treated as authoritative for CT-801. I approve
> no gate.

### 1.0.4 gate result (2026-09-15)

**Self-assessed status: BUILD-READY for `CT-801` and `CT-802`.** §9a's patch-scope lenses all
pass, no Blocking question is outstanding, and no `[NEEDS ANSWER]` marker blocks either new task —
the three decisions this patch needed (Q-238/239/240) were taken by the operator before it ran.

**`scripts/verify-gate.sh` — run after this patch: 1 FAIL, 1 WARN.** Both stated in full;
**neither is papered over:**

| Check | Result | Reading |
|---|---|---|
| **C5** | **FAIL** | `TestResults-P0-phase-gate.trx` predates `CallerClaims.cs`. **Pre-existing and not caused by this patch** — it is `REV-006`, and Gate 4 re-ran both suites green at HEAD, so the *substance* passes and the *artifact* is stale. This patch touches no test artifact and cannot clear it. **Still open, owned elsewhere.** |
| **C8** | **WARN** | "build log records no row against the current spec-suite hash (`891ae954e510`)." **This is now a true signal, and the first time it has been** — every prior occurrence was investigated and traced to record-file churn. The suite genuinely moved; §14 lists the six stale `CT-`s. `Review_Gate4.md`'s addendum predicted exactly this. |
| **C9** | **PASS** (was FAIL) | Gate 4 reported "sharded folders missing from shard map: Management ×6" and correctly diagnosed it as the script word-splitting a repo path containing a space. **It passes on this run.** I did not change the script and make no claim to have fixed it — the finding was a script artefact then and its absence now is the same artefact behaving differently. **Do not read this as evidence the shard map was broken and is now repaired.** |

**I am therefore NOT stamping the suite `DRAFT-GATED`.** The gate's criterion is an unresolved
*suite* defect, and C5 is not one — it is a stale test artifact belonging to the build stage.
**If you read that differently, treat the suite as gated: the FAIL is stated here in full
precisely so the judgement is yours and not silently mine.**

**What this patch did NOT do, restated so it cannot be mistaken for completeness:** the Critical is
fixed **in the specification only**. `CT-801` is not built. U21, U27, U28, U30–U33, U36, U37, U40,
U41 and REV-003/004/005/006/007 were explicitly out of scope and **all remain open**. Two new
items were raised rather than decided: **U42** and **U43** — U43 is a **Major** and is the one most
likely to be overlooked, because it is an operator action rather than a code change.

**This is my own assessment of my own output, on a patch I wrote.** Run
`bash scripts/verify-gate.sh` for an independent check, and have the code-review agent run its
spec audit (**Mode D**) against 1.0.4 before `CT-801` is built — **the last time a role-provisioning
rule went un-audited it shipped through eight gates as a Critical.**

---

**Self-assessed status: BUILD-READY.** Every coverage-matrix lens (§9) and DoD item (§10) passed
on this agent's own review; no `[NEEDS ANSWER]` item blocks any `CT-` from starting (9 remain
non-blocking with a stated provisional choice `code-generation` can build against; 5 are resolved,
see §12).

**Revision history:**

- **1.0.1 — post-Mode-D-audit fix pass**, following `code-reviewer` Mode D's `Spec_Audit.md`
  (DRAFT-GATED verdict) and the operator's decisions on it: (1) Q-211 resolved (auto-assigned
  Entry/Free plan at registration, BRL-074); (2) fixed mistraced citation in
  `07-business-logic/identity-access.md`'s session-lifecycle diagram (was `NFR-010`, now
  `FR-010/NFR-003`); (3) normalized the `BR-057` business-logic row id from `BRL-057cfg` to
  `BRL-057` in `07-business-logic/configuration-commercial.md`; (4) normalized
  `09-security-spec.md` §6 row N4 to an explicit `403 Forbidden` (dropped the silent
  Student-variant-fallback alternative), matching every other row in that matrix. Also fixed:
  `ROLE-003`–`ROLE-006` now explicitly cited in `09-security-spec.md` §2 and
  `02-requirements-spec/identity-access.md` (a prior `verify-gate.sh` C3 finding, resolved before
  the Mode D audit).
- **1.0.2 — Q-204/205/206/207 operator sign-off**: all four accepted as originally proposed
  (Administrator/Principal/SchoolInCharge as approvers; leadership corrections apply immediately,
  no self-approval; pending corrections auto-expire at the 30-day lock; FR-064 covers
  account-management only, `CFG-001` never institution-overridable). Every citation of these four
  questions across `07-business-logic/attendance.md`, `10-process-flows/attendance.md`,
  `05-api-contracts/attendance.md`, `11-ui-spec/attendance.md`, `14-test-spec/attendance.md`, and
  `02-requirements-spec/configuration-commercial.md` was firmed from "provisional"/"NEEDS ANSWER"
  wording to a confirmed final rule.
- **1.0.3 — upstream feedback U1–U6 accepted as built** (decided by Rashmi Mehta, 2026-09-14;
  source `_upstream-feedback.md`). Spec text corrected to match behaviour already implemented and
  tested; no endpoint removed, no new ID minted. U1: API-030 declared an intended alias of API-025
  (reciprocal note on API-025). U2: API-018 and SPEC-FR-017 parent-link citation PF-008 → PF-005.
  U3: API-012 errors `ERR-102` → `ERR-902`, role denial noted as a framework bodyless 403.
  U4: VAL-011 scope extended to teacher `contact_email`/`telephone` (API-020). U5: subject deletion
  removes its `SubjectTeacherClassMapping` rows, no error (business logic + API-029). U6: Parent
  Link Code decision table and guards — already-linked Parent redeeming a further valid code is
  idempotent. `suite_hash_sha256` in the header is the 1.0.2 value and was not recomputed in this
  patch.
- **1.0.4 — the Gate 4 narrow patch** (authorised by Rashmi Mehta, 2026-09-15; decisions Q-238,
  Q-239, Q-240). Files amended, all **in place with a revision stamp and no `_v2`/dated copy**:
  `02-requirements-spec/identity-access.md` · `04-data-model.md` · `05-api-contracts/`
  {`identity-access`, `institutions`, `attendance`, `communication`, `operations`,
  `configuration-commercial`} · `06-event-contracts.md` · `07-business-logic/`
  {`identity-access`, `attendance`, `operations`} · `08-error-catalogue.md` ·
  `09-security-spec.md` · `10-process-flows/identity-access.md` · `11-ui-spec/`
  {`public-auth`, `operations`} · `12-infrastructure-spec.md` · `14-test-spec/`
  {`identity-access`, `attendance`, `communication`} · `15-task-breakdown.md` ·
  `_questions.md` · `_upstream-feedback.md` · this file. **The suite hash WAS recomputed this
  time** (header), on a scope that can actually converge — see `suite_hash_scope`.

**This is my own assessment of my own output.** Run `bash scripts/verify-gate.sh` for an
independent, script-based check, and have the code-review agent run its spec audit (Mode D) at
this gate before treating this suite as authoritative for the build.

## 14. Rebuild required — `CT-`s made STALE

### 1.0.5 — **no `CT-` newly became STALE**

Checked rather than assumed, by routing every ID this patch touched to the `CT-`s whose `Specs:`
field names it:

| Touched at 1.0.5 | Which built `CT-` names it | Newly stale? |
|---|---|---|
| API-005 (`id` added to the success shape, U44) | **CT-101** (institutions) — **already STALE** since 1.0.4 for API-001 | **No.** Already on the list, and the built controller **already returns `id`** — the spec moved to match the code, so no code change is implied by this fix. |
| `04-data-model.md` §2.12a/§2.12b, `CFG-SEC-06`, `09-security-spec.md` §1a.1/§4/§7/N9/N18, API-006/067/068, BRL-075, VAL-027, TC-209/211/215/216/217, PF-014 | **CT-801 / CT-802 — not built** | **No.** Nothing can be stale against a spec for a task that has not run; this *is* CT-801's input, which is the whole point of landing it now. |
| `UserAccount.provisioned_by_code_id` | **CT-001** (schema) — **already STALE** since 1.0.4 for §2.12a | **No.** The column lands in CT-801's migration alongside `LeadershipCode`, which is why adding it now is cheap. |
| `CFG-012`'s unit (m3) | **CT-802 — not built** | **No.** |
| `09-security-spec.md` §2 matrix day 1–30, TC-406's trace (m1) | **CT-202** (attendance correction) | **No.** Text-only. **The code was already right** — U11 corrected the window everywhere that governs behaviour; these two were stale *prose* that had been missed. Marking CT-202 stale would trigger a rebuild to change nothing. |
| **CT-701** (the security sweep) | already **STALE** since 1.0.4 | **No change, but the scope of its re-run grows:** it must now also re-establish its findings against **N18 and SEC-009**, not only N11–N17. A sweep that reports "PlatformOperator cannot act institution-internally" would now be reporting a property the product does not have. |

**The 1.0.4 list below is unchanged and still authoritative: CT-001, CT-002, CT-101, CT-302,
CT-601 and CT-701 remain STALE.**

### 1.0.4 — `CT-`s made STALE

Per `shared-spec-version`: every already-built `CT-` whose `Specs:` names a changed ID is STALE
and re-enters the build loop rather than silently diverging from a contract that moved.

| CT | Phase | Status | Changed IDs it was built against | What must happen |
|---|---|---|---|---|
| **CT-002** | P0 | **STALE — Critical** | API-006, SPEC-FR-006, BRL-075, VAL-007, ERR-206/207/208, `09-security-spec.md` §1 | Rebuilt as part of CT-801. **The endpoint as built is the vulnerability.** |
| **CT-001** | P0 | **STALE** | `04-data-model.md` §2.12a (`LeadershipCode`) | New table + migration, in CT-801. |
| **CT-101** | P1 | **STALE** | API-001 (success body now carries the first leadership code) | Issue-at-create, in CT-801. |
| **CT-601** | P6 | **STALE** | UI-005 (Role dropdown and institution picker removed; code field added) | Register panel rebuilt, in CT-801. |
| **CT-302** | P3 | **STALE** | API-043 (adds `ERR-904`) | Rate limit on room join, in CT-802. |
| **CT-701** | P7 | **STALE** | `09-security-spec.md` §1a, §6 (N11–N17), §10 | **Re-run after both.** Its anonymous-surface enumeration and negative-authorization sweep were measured against the pre-fix contract, so the "7 anonymous endpoints, 6 safe" finding must be re-established, not assumed to carry. |

**Deliberately NOT stale:** every `CT-` touched only by the Part-3 write-backs (CT-201/202/203,
CT-301/303/304, CT-401/402/403, CT-501/502/503/504). **The spec moved to match the code; the code
did not move.** The Gate 4 review found zero conformance findings attributable to that drift and
judged the code the better record, so marking those STALE would trigger 14 rebuilds to change
nothing. Two exceptions worth naming because they are *corrections* rather than transcriptions —
the day 1–29 window and TC-410's day-30 assertion — and in both cases **the code was already
right** and only the text was wrong, so still no rebuild.

**`verify-gate.sh` C8 will WARN** until a CT is recorded against the new hash. That warning is
now a genuine signal, exactly as `Review_Gate4.md`'s addendum predicted it would become the moment
this patch ran.

