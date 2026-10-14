<!-- pipeline-provenance
produced_by: ai-spec-agent
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, authorised by Rashmi Mehta, closing
  the Mode D Round-2 audit against 1.0.4). **Q-241 appended** — keep or strike
  `UserAccount.provisioned_by_code_id`, the one change in 1.0.5 that closes no defect. Non-blocking,
  provisional answer KEEP, and it sits on CT-801's migration so it is cheapest answered before
  CT-801 is built. Five decisions taken without a question are recorded alongside it. **No Q- was
  renumbered and no prior answer was altered.** The `Q-` range is now 201–241.
-->

# AI Spec — Questions Log

**No Blocking question exists.** Generation proceeded in a single pass, per the charter's
Phase-1 branch point. Of the 14 items below, **9 remain non-blocking and provisional**, each
marked inline at its source location as `[NEEDS ANSWER — Q-###]`. **Q-204, Q-205, Q-206, Q-207,
and Q-211 have been answered** by the operator (Jayesh Idnani, 2026-09-11) following the Mode D
spec audit and are no longer provisional — see their rows below and the corresponding spec files
(`07-business-logic/attendance.md`, `10-process-flows/attendance.md`,
`05-api-contracts/attendance.md`, `11-ui-spec/attendance.md`, `14-test-spec/attendance.md`,
`02-requirements-spec/configuration-commercial.md`, `10-process-flows/institutions.md` PF-001,
`07-business-logic/configuration-commercial.md` BRL-074). None of the remaining items touches a
capability the BRD did not already commit to; all are narrow technical figures, boundary rules,
or a UI-authoring convenience left unstated by all three source documents.

**Please review and confirm, amend, or explicitly defer each** before the corresponding area is
hardened past its provisional choice at Stage 3 build (`company.md`'s "no TODO/HACK left at a
phase gate without a tracked work item" applies equally to an unconfirmed provisional choice).

| Q ID | Location | Question | Provisional choice | Confirm before hardening... |
|---|---|---|---|---|
| Q-201 | `07-business-logic/institutions.md` VAL-003 | What telephone format/pattern should `VAL-003` enforce? Neither BRD nor Tech Design names one. | Loose E.164-tolerant validation (7–15 digits, optional `+`) | CT-101 |
| Q-202 | `07-business-logic/identity-access.md` E3 | Should the public Register form's Role dropdown allow selecting `PlatformOperator`? | No — excluded from self-registration; provisioned only via an internal path | CT-002 |
| Q-203 | `07-business-logic/academic-records.md` VAL-010 | Should student date-of-birth have a plausible-age guard? | Reject DOB implying age <3 or >21 at enrolment (data-entry-error guard, not a hard rule) | CT-102 |
| Q-204 | `07-business-logic/attendance.md` (correction lifecycle guards) | ~~FR-034 names "Principal, Vice Principal, or Academic Administrator" as approvers, but BR-007's closed role set has no distinct VP/Academic-Administrator role. Which existing role(s) satisfy this?~~ **ANSWERED** — see below | **Operator-confirmed 2026-09-11 (Jayesh Idnani):** "Administrator, Principal, and SchoolInCharge are the valid approval roles." | CT-202 (resolved, no longer provisional) |
| Q-205 | `07-business-logic/attendance.md` (decision table) | ~~Does a leadership user's own day 1–29 correction still route through the pending/approval step, or apply immediately?~~ **ANSWERED** — see below | **Operator-confirmed 2026-09-11 (Jayesh Idnani):** "Leadership corrections apply immediately and do not require self-approval." | CT-202 (resolved, no longer provisional) |
| Q-206 | `07-business-logic/attendance.md` E2, `10-process-flows/attendance.md` PF-007 | ~~What happens to an open `CorrectionPending` request when the record reaches the 30-day lock boundary before resolution?~~ **ANSWERED** — see below | **Operator-confirmed 2026-09-11 (Jayesh Idnani):** "Pending correction requests automatically expire/reject when the 30-day lock is reached." | CT-202 (resolved, no longer provisional) |
| Q-207 | `02-requirements-spec/configuration-commercial.md` (SPEC-FR-064) | ~~Where exactly is the boundary between FR-064's institution-level "password policy" setting and the fixed platform-wide `CFG-001` constant (BR-014, D1)?~~ **ANSWERED** — see below | **Operator-confirmed 2026-09-11 (Jayesh Idnani), accepted as originally stated:** FR-064's UI exposes account/permission settings only; the actual password-complexity constant (`CFG-001`) is never institution-overridable — FR-064's "password policy" language refers to account-management actions (e.g. forcing a reset), not the complexity rule itself. | CT-501 (resolved, no longer provisional) |
| Q-208 | `07-business-logic/operations.md` E2 | Is "teaching room" (FR-060) a first-class, manageable entity, or a plain text/reference field on `TimetablePeriod`? | Plain field, not a separate CRUD entity, this release | CT-403 |
| Q-209 | `07-business-logic/configuration-commercial.md` VAL-025 | What languages does `institution_config.language` support? | English only this release; field present, single-valued, extensible later | CT-501 |
| Q-210 | `07-business-logic/configuration-commercial.md` E1 | What happens when an institution downgrades tier while already over the new, lower capacity limit? | Existing over-limit records are grandfathered; no further capacity-consuming action permitted until usage drops below the new limit | CT-503 |
| Q-211 | `10-process-flows/institutions.md` PF-001 | ~~Does a newly registered institution start with no subscription plan, or a default Entry-tier plan, before FR-003 explicitly assigns one?~~ **ANSWERED** — see below | **Operator-confirmed 2026-09-11 (Jayesh Idnani):** "A newly registered institution shall automatically receive the Entry/Free plan at registration and become operational immediately within that plan's limits. Platform Operators may later upgrade or change the assigned plan." Applied as BRL-074; institution creation now auto-assigns Entry tier in the same transaction, `subscription_plan_id` never null. | CT-101 (resolved, no longer provisional) |
| Q-212 | `10-process-flows/attendance.md` PF-007 | Should the 30-day lock be enforced only by a scheduled daily sweep, only by a lazy per-request check, or both? | Both — the sweep is a consistency/UX convenience; the approve/reject action itself re-checks the boundary at the moment of action regardless of the sweep's last run | CT-202 |
| Q-213 | `11-ui-spec/public-auth.md` UI-006 | The side-nav width has no named design token (`design-tokens.md` flagged this gap, unresolved at hand-off). What should it be? | Add a new structural token `layout-sidenav-width = 16rem` to `design-tokens.md` at build time | CT-601 |
| Q-214 | `11-ui-spec/operations.md` UI-017 | The mockup's Generate-report modal offers PDF/Excel/CSV, but Tech Design D7 names only PDF+CSV. | Build PDF+CSV per D7; omit Excel from the format dropdown until D7 is revised | CT-402 |

## Non-blocking items on must-have paths — surfaced per charter, not buried

Three of the above sat on a **must-have path** and got explicit attention even though none
blocked generation. **All three are now resolved** — operator-confirmed 2026-09-11 (Jayesh
Idnani):

1. ~~**Q-204/Q-205/Q-206 (attendance correction approver mapping and edge timing)**~~ —
   **RESOLVED.** The attendance correction workflow is FR-033/034/035, a firm, BRD-committed
   capability; these three questions narrowed *how* it's implemented at its edges, not *whether*
   it exists. Operator confirmed the role mapping (Q-204: Administrator/Principal/SchoolInCharge),
   the self-approval question (Q-205: applies immediately, no self-approval loop), and the
   pending-request/lock-boundary interaction (Q-206: auto-expires/rejects at day 30) — all as
   originally proposed. See `07-business-logic/attendance.md`, `10-process-flows/attendance.md`,
   `05-api-contracts/attendance.md`, `11-ui-spec/attendance.md`, `14-test-spec/attendance.md`.
2. ~~**Q-207 (FR-064 vs. `CFG-001` boundary)**~~ — **RESOLVED.** Operator confirmed, as originally
   stated: an institution admin's "password policy" setting in Settings covers account-management
   actions only; `CFG-001`'s complexity constant is never institution-overridable. See
   `02-requirements-spec/configuration-commercial.md`.
3. ~~**Q-211 (default subscription plan at registration)**~~ — **RESOLVED.** Operator confirmed
   (2026-09-11, Jayesh Idnani) that a newly registered institution auto-receives the Entry/Free
   plan at registration and is operational immediately; this removes the contradiction with the
   BRD's "onboarded and made fully operational within one working day" success criterion (§5)
   that the prior provisional choice (null plan, zero-capacity until a separate assignment step)
   would have created. See `10-process-flows/institutions.md` PF-001 and
   `07-business-logic/configuration-commercial.md` BRL-074.

## Contradictions found and resolved during this pass (not re-opened here)

None beyond what `ui-design-final.md` and `design-tokens.md` already resolved at Beat 2 (the 7
UI corrections, the "Late" pill contrast fix, the two validation-required mockup artifacts). This
suite treats all of those as firm, operator-approved requirements per the task's binding
instructions, and treats the Reports gauges and Pricing feature-copy as **omitted** (not
deferred) per the same instructions — see `01-source-catalogue.md` §5.

## Discrepancy noted, not a question

`.aidlc/_config/constitution.md` as found on disk **before this run** referenced a
`03-ai-specs/` path layout (`03-ai-specs/04-business-logic/`, `03-ai-specs/05-data-model.md`,
etc.) and a set of already-answered `Q-201`–`Q-206` items, despite
`02-solution-designer/ai-specification/` not existing yet at the start of this run. That file
appears to be a stale artifact from a differently-pathed prior/simulated pass, not a file this
run produced. It is **regenerated in full by this run** (see the constitution-writer's output),
using this run's actual paths and this run's actual `Q-` numbering — the two are not related past
the coincidental overlap in `Q-20x` numbers. Flagged here for the operator's awareness; not
something this agent can resolve on its own authority.

## Build-time questions — start-development Phase P2 (asked live, 2026-09-14)

Raised by code-generation (halt-and-ask) because the specs did not answer them. Each answer was
given by **Rashmi Mehta** on 2026-09-14 and built against; the spec text is **not yet corrected**
(tracked as U11 in `_upstream-feedback.md`).

| Q | Where | Question | Answer (final) | Built in |
|---|---|---|---|---|
| Q-213 | `04-data-model.md` §2.6 vs API-033/034 | Where do correction requests live? The data model has only `correction_status`. | A new `CorrectionRequest` table: requested outcome, reason, requester, status, resolver, timestamps. | CT-202 |
| Q-214 | `07-business-logic/attendance.md` decision table, TC-408 vs TC-410 | Is a record exactly 30 days old correctable or locked? | Still correctable. The lock applies at `> 30` days; the window is days 1–30; the sweep auto-rejects from day 31. | CT-201/202 |
| Q-215 | "Guards, precisely"; `InstitutionConfiguration.time_zone` | Time zone format, and what applies when there is no configuration row? | An IANA id. Fall back to `Asia/Kolkata`, proposed as **CFG-009**. | CT-201 |
| Q-216 | CT-202 DoD vs CT-501 (P5) | Build the Hangfire daily sweep now? | Sweep logic now (`RunLockSweepAsync`) plus a lock re-check on approve/reject. The daily schedule is wired in CT-501. | CT-202 |
| Q-217 | API-031 vs API-014 | Both define `GET /classes/{id}/roster`. | API-031 moves to `GET /api/v1/attendance/roster?classId=&date=`. API-014 is unchanged. | CT-201 |

## Build-time questions — start-development Phase P3 (asked live, 2026-09-14)

Answered by **Rashmi Mehta** on 2026-09-14 and built against. The spec text is **not yet corrected** (tracked as U15).

| Q | Where | Question | Answer (final) | Built in |
|---|---|---|---|---|
| Q-218 | CT-304 DoD, TC-508/509 vs CT-601/603 (P6) | No frontend exists. Where do the meeting UI states and browser tests go? | API-047 now. UI-014 states and TC-508/509 move to CT-603. | CT-304 |
| Q-219 | Room-membership state machine, API-044/047, EVT authorization | What is "membership", and does a disconnect end it? | A `RoomMembership` table. Created by a correct-passcode join, removed by an explicit leave or room removal. A disconnect only changes presence to offline. | CT-302/303 |
| Q-220 | `Meeting` (§2.8, mapped one-to-one), API-047 | Meeting lifecycle and ACS identity? | Many meetings per room, one live at a time. API-047 reuses or starts it and returns `groupCallId`. It ends on room removal or last-member leave. A new ACS identity per token request. | CT-304 |
| Q-221 | CFG-SEC-03/04, INF-003 | Azure SignalR and ACS are unavailable locally. | Config-gated: in-process SignalR, adding Azure SignalR when configured; the ACS issuer when configured, else 503 `ERR-903`; fakes in tests. | CT-303/304 |

## Build-time questions — start-development Phase P4 (asked live, 2026-09-14)

Answered by **Rashmi Mehta** on 2026-09-14 and built against. The spec text is **not yet corrected** (tracked as U20).

| Q | Where | Question | Answer (final) | Built in |
|---|---|---|---|---|
| Q-222 | API-050 / A1 / T-005 / D11, CT-501 scope | Hangfire isn't installed and Blob Storage isn't configured. How do reports run and store? | Hangfire now (SQL Server storage). Retries then `Failed` plus an audit row. Blob Storage when configured, else local disk (dev/test only). | CT-402 |
| Q-223 | FR-057/058, BRL-090/091 | How are attendance %, teacher compliance/timeliness and course analytics computed? | % = (P+L)/(P+L+A), Leave excluded. Compliance = own class × Mon–Fri days with any record. Timeliness = own records marked on the attendance date (institution time zone). Course = average class % per subject. | CT-401/402 |
| Q-224 | UI-015 / API-048, A2 | Dashboard content with no data source; trend window; cache? | Traceable data only: a 7-day trend; announcements stand in for "recent activities" and "upcoming events". Teacher view scoped to own classes; Student/Parent views personal only. Computed live, no A2 cache. | CT-401 |
| Q-225 | API-054, §2.9 TimeSlot | What does "publish" mean, and what format is the time slot? | `IsPublished` per period (`{id}` = period id); non-leadership sees published only; `HH:mm` slots; exam schedules not modelled. | CT-403 |
| Q-226 | D7 / NFR-027 | Which PDF library? | **CSV only for now**; PDF export deferred as a recorded deviation. | CT-402 |

## Build-time questions — start-development Phase P5 (asked live, 2026-09-14)

Answered by **Rashmi Mehta** on 2026-09-14 and built against. The spec text is **not yet corrected** (tracked as U26).

| Q | Where | Question | Answer (final) | Built in |
|---|---|---|---|---|
| Q-227 | BR-062/063, §2.10 seed values, FR-073 | The seeded tier limits and capability allocation have no source. What do the gates enforce? | PlatformOperator-editable per plan (seeds are provisional defaults). Learner capacity is gated on enrolment and reactivation; the storage gate is deferred (no metering). | CT-503 |
| Q-228 | BRL-063 / TC-707 vs P1–P4 Parent endpoints | Which Parent operations are tier-gated? | All Parent endpoints: code redemption, linked students, linked attendance, the Parent dashboard. Staff code issuance is not gated. Retrofitted. | CT-503 |
| Q-229 | FR-067 "next class level", PF-012 | How is the next class determined, who creates academic years, and what about final-year students? | `Class.NextClassId` (null = final year). Rollover creates the next year if missing, appends Enrollment, updates current placement. Final-year students get no new enrolment and stay active. | CT-502 |
| Q-230 | API-063 (anonymous, any institutionId), PF-013 | Who may start a trial, which plan does it grant, and where is it stored? | Own-institution leadership or a verified PlatformOperator, once, from Entry. Grants Mid for 30 days via trial fields on Institution. Daily reversion to Entry, no billing. A purchase ends the trial. | CT-504 |

## Phase P6 (UI) — recorded retrospectively 2026-09-15

*These seven were decided by the 2026-09-14 session that built the SPA and ended before
checkpointing. They were cited in code comments but recorded in no workbook, no build log and no
state file. Recovered from the code during the P6 reconciliation and **attributed to Rashmi Mehta
by their own direction (2026-09-15)**, matching Q-233, the only one that already carried an
attribution. **Q-232 and Q-236 are flagged below as substantive and are called out for explicit
review at Gate 3-P6.***

| ID | Where | Question | Answer | Task |
|---|---|---|---|---|
| Q-231 | `11-ui-spec` routes, `12-infrastructure-spec.md` | Hash routes (as the mockup used) or path routes, and how does the SPA reach the API? | **Path routes**, served by the API's SPA fallback. The ui-spec route names are kept (`/dashboard/meeting`, `/chat/:roomId`, `/video-call/:roomId`). Every API call is a same-origin relative path, so no CORS origin and no API base URL is configured. | CT-601 |
| Q-232 | NFR-017, `09-security-spec.md` | Where does the access token live in the browser? | **In memory only** — no persist middleware, no `localStorage`, no `sessionStorage`, no script-readable cookie. The session is restored once per page load from the httpOnly refresh cookie. Asserted by an adversarial e2e test that reads both web storages and `document.cookie` after login. **SUBSTANTIVE — this is a security posture; confirm at Gate 3-P6.** | CT-601 |
| Q-233 | UI-004 Pricing | Are plan prices shown, given BR-062/U28 says the tier numbers are unsourced placeholders? | **No price is shown; every plan's CTA is "Contact sales".** *(already attributed to Rashmi Mehta)* | CT-601 |
| Q-234 | UI-014, T-006, TC-508/509 | How are the ACS Calling states tested without a live ACS subscription? | The SDK sits behind a `callingAdapter` interface. A **fake adapter is compiled in for the `e2e` build mode only** — never in a production bundle — and drives UI-014's connecting / connected / reconnecting / error states deterministically for TC-508/509. | CT-603 |
| Q-235 | UI-005 register, API-001 | How does a registering user identify their school? | The register form submits the **id of the school chosen from the public directory**, not a typed name. | CT-601 |
| Q-236 | UI-007/008/012/015/017/018, FR-064 | What happens where the ui-spec describes an element the API cannot supply? | **The screen omits the element rather than inventing data**, and the gap is filed. Affects: per-class attendance rate/sparkline; meeting participant avatar stack; Reports attendance-vs-performance, grade-distribution and subject-engagement charts; Teachers staff-by-subject chart and subject filter; Settings school profile and the FR-064 account-management actions; "Forgot password?". **SUBSTANTIVE — this omits UI the spec describes; confirm at Gate 3-P6.** Overlaps U33. | CT-602/603/604 |
| Q-237 | UI-004 Pricing | Keep the mockup's monthly/yearly billing toggle? | **No.** With no prices shown (Q-233) the toggle would re-render nothing, so it is omitted. | CT-601 |

## Gate 4 round 1 — decisions on REV-001/U38 and REV-002/U39, 2026-09-15

*Taken by Rashmi Mehta at the Gate 4 CHANGES-REQUIRED verdict. Q-238 and Q-239 together replace the
role-and-institution-from-the-request-body behaviour that API-006 and SPEC-FR-006 currently
specify — the spec and the code were wrong together, so both change.*

| ID | Where | Question | Answer | Task |
|---|---|---|---|---|
| Q-238 | API-006, SPEC-FR-006, `09-security-spec.md` §2, BRL-001 | How are privileged (leadership) roles provisioned, given an anonymous caller could self-elect to Administrator of any institution (REV-001 / U38, Critical)? | **Reuse the existing issue-and-redeem link-code pattern.** The PlatformOperator issues a **leadership code** when creating the institution (API-001 is already PlatformOperator-only, BRL-001); the first Administrator redeems that code during registration. Thereafter leadership issues codes in-app for further staff. **The role and institution come FROM THE CODE, never from the request body** — `Role` and `InstitutionId` are removed from the anonymous register payload. Chosen over a bespoke mechanism because `ParentLinkCode` (FR-017, CT-103) already implements issue / redeem / revoke / expiry, is tested and audited, and is therefore a known quantity. | Gate 4 round 1 |
| Q-239 | API-006, UI-005 | Which roles remain self-selectable at anonymous registration? | **Student and Parent only.** Teacher is code-gated with leadership: a Teacher account carries read access to student and guardian records across the school (`Roles.LeadershipAndTeacher` on API-014/API-028), so self-electing to Teacher is a smaller version of the same breach. A self-registered Parent still sees nothing until they redeem a student link code, which is already enforced. | Gate 4 round 1 |
| Q-240 | `09-security-spec.md`, ERR-904, U16, U39 | Rate limiting — REV-002 found ERR-904 defined in the error catalogue, attributed to a security-spec section that does not exist, and nothing built. | **In scope for this same round.** Without it, any residual weakness in registration or login is brute-forceable across the whole published institution directory, which is what made REV-001 scriptable. Also closes **U16** (unlimited room-passcode guesses). `09-security-spec.md` gains the missing rate-limiting section, and ERR-904 becomes reachable. | Gate 4 round 1 |

**Deferred from this round, to be re-reviewed later, each needing a named owner:** REV-003
(`Teacher.RowVersion` is `varbinary(max)`, not a concurrency token), REV-004 (no `ERR-` and no
handler for a concurrency conflict — currently an unhandled 500), REV-005 (all 179 frontend tests
run against hand-written mocks with nothing binding them to the real controller shapes), REV-006
and REV-007 (Minor).

**Status, 2026-09-15:** **Q-238, Q-239 and Q-240 are APPLIED to the spec suite in patch 1.0.4.**
The spec half is done; the code half is `CT-801` (Q-238/239) and `CT-802` (Q-240) in
`15-task-breakdown.md` §4 P8, and is **not yet built** — the vulnerability Q-238 answers is still
live in the running code. **Q-202 is superseded by Q-239** (it excluded only `PlatformOperator`
from the register dropdown; that provisional choice is what left the three leadership roles and
Teacher self-selectable). **Q-235 is superseded for privileged roles only** by Q-238 — the
register form still submits a directory-chosen institution id on the Student/Parent path.

**No new question was raised by the 1.0.4 patch.** Two things it could not decide for itself were
routed as owned work items rather than answered silently: **U42** (the monospace family has no
token name — a Beat-2 file this agent does not write) and **U43** (the accounts created during the
U38 exposure window cannot be shown to be legitimate, which is an operator action no `CT-` DoD can
assert). Both are in `_upstream-feedback.md`.

## Patch 1.0.5 — one new question (non-blocking), and the decisions taken without one

*2026-09-15. Patch 1.0.5 closes the Mode D Round-2 audit findings (U44–U47, m1–m4) against the
1.0.4 patch. **Zero Blocking questions.** Every defect fix in it was directed by the audit and
needed no operator decision. One item is not a defect fix and therefore does need one.*

| ID | Where | Question | Provisional answer | Severity |
|---|---|---|---|---|
| **Q-241** | `04-data-model.md` §2.2 (`UserAccount.provisioned_by_code_id`), CT-801 | **Keep or strike the nullable `provisioned_by_code_id` FK?** It is the one change in 1.0.5 that closes no defect. The auditor recommended it under U43; it was added **on the orchestrator's judgement**, on the reasoning that specifying it now — while CT-801 is writing `LeadershipCode` and its migration — is far cheaper than retrofitting it onto a populated `UserAccount` later. | **KEEP.** *Basis:* after CT-801 a privileged account's provenance lives **only** in an `AuditLog` JSON snapshot, in the very table the CMP-001/002/003 retention sweep prunes. This column makes *"which privileged accounts cannot be accounted for?"* a durable one-line query that stays true for seven years and gives the human doing U43's review something to actually run, and it keeps the pre-1.0.5 (U38 exposure-window) set **permanently distinguishable** rather than merely absent from a log. *Correct-if-wrong:* **striking it costs one line in CT-801 and the boxed note in §2.2, and nothing else** — no API returns it, no `UI-` shows it, and no back-fill is attempted (a back-fill would be a guess; the value of the column is that `NULL` honestly means *"we do not know"*). | **Non-blocking, but it sits on CT-801's migration**, so it is cheapest to answer **before CT-801 is built** and materially more expensive after. |

**Decisions taken inside this patch without an operator question, recorded so none is invisible.**
Each follows directly from the audit's own direction and from behaviour already built; none
changes a requirement, a role, an endpoint's purpose or a priority:

1. **`CFG-SEC-06` is a dedicated secret, not the JWT signing key** (U45a). The audit named the
   coupling as the defect; separating them is the only fix that removes it.
2. **`code_lookup` is Base64url of the full HMAC-SHA256, `char(43)`, not truncated** (U45c).
   Truncating would have required a collision argument; not truncating costs nothing. Recorded as
   **ASM-008** (`00-spec-index.md` §10) because the audit asked for *an* encoding, not this one.
3. **Rotating `CFG-SEC-06` invalidates every outstanding code, with a mandatory
   revoke-then-reissue procedure and no dual-key read window** (U45b, **ASM-008**). A second
   "previous key" read path doubles the brute-force surface to buy at most `CFG-010` (7 days) of
   convenience.
4. **`staffCode` is absent iff omitted or `null`; `""` and whitespace are present and yield
   `ERR-206`** (m2). The audit confirmed **both** readings are safe and asked only that one be
   picked; the stricter one is chosen, and the reason is stated at each of the three sites.
5. **No preventive control is added for the PlatformOperator bootstrap escalation** (U47) — it is
   named as accepted, audited and tenant-visible instead. The audit's own framing was that the
   design is probably acceptable and the defect is the silence. A same-actor issue/redeem
   correlation alert was considered and rejected **in writing**, because the redeeming caller is
   anonymous and a rule defeated by a second browser is worse than an honest "audited, not
   prevented".

**Nothing else in the suite was touched.** U21, U27, U28, U30–U33, U36, U37, U40, U41, U42, U43 and
REV-003/004/005/006/007 remain open and are **not** in this patch.
