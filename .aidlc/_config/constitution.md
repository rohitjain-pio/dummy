<!-- pipeline-provenance
produced_by: ai-spec-agent (constitution-writer)
plugin_version: 0.3.0 (sdlc-greenfield-v3)
generated: 2026-09-11
source_suite: .aidlc/02-solution-designer/ai-specification/ (spec_suite_version 1.0.0)
supersedes: a prior constitution.md found on disk at the start of this run that referenced a
  `03-ai-specs/` path layout and a `Q-201`–`Q-206` set inconsistent with this run's actual output
  (`ai-specification/_questions.md` records the discrepancy; that file was not produced by this
  run and is fully replaced here).
-->

# Constitution — School Management System ("SchoolSync")

> The durable rulebook. Read by the brownfield plugin and any future maintenance/extension work
> long after this greenfield pipeline has finished. Distilled from `final-brd.md`,
> `final-tech-design.md`, `ui-design-final.md` and the `ai-specification/` suite — never
> contradicts them; if this file and a spec ever disagree, the spec (and ultimately
> `requirements.yaml`) wins and this file is stale and must be regenerated.

## 1. What this system is

A multi-tenant SaaS for school administration: institution/user/student/staff/class/attendance/
announcement records, real-time collaboration (chat + live meetings), dashboards/reporting,
scheduling, institution configuration, and a 3-tier commercial subscription model. One ASP.NET
Core Web API + one React SPA, Azure-hosted, shared SQL Server database with `institution_id`
discriminator-column multi-tenancy. No pre-existing institutional system is integrated with; no
data migration capability exists in this product (FR-088 excluded).

## 2. Non-negotiable invariants (never violate these in any future change)

1. **Tenant isolation is absolute** except the Platform Operator's single, explicitly-named
   cross-tenant path (`PlatformOperatorQueryService`) and the public institution directory
   (name + city/region only). Any new tenant-owned table MUST carry `institution_id` and MUST go
   through the global query filter. (NFR-016, T-002, SEC-002)
2. **The seven roles are closed**: PlatformOperator, Administrator, Principal, SchoolInCharge,
   Teacher, Student, Parent. Never add an eighth role without re-opening a BRD-level decision —
   the entire RBAC matrix (`09-security-spec.md` §2) is keyed to exactly these seven. (BR-007)
3. **Reversible deactivation, never hard-delete** for Institution, Student, Teacher, Room,
   UserAccount. (BR-005, D8)
4. **The attendance correction state machine is fixed**: same-day self-correct, day 1–29
   requires leadership approval, day 30+ locks for everyone, permanently, platform-wide, not
   institution-configurable. (FR-033–035, `CFG-004` = 30 days)
5. **The 4 attendance outcomes (Present/Absent/Late/Leave) are closed** — "Leave" carries no
   request/approval/balance mechanics; that mechanism is out of scope by design, not by omission.
   (FR-031, FR-082 excluded)
6. **Every audited entity's changes flow through the `SaveChanges` interceptor** — no handler
   writes its own audit row, ever, except the one documented exception (sign-in/out/permission-
   denial, logged directly by auth middleware since these are not entity changes). (D9,
   NFR-028–033)
7. **Student/teacher performance metrics are attendance-only** — do not add homework/
   participation/behavioural fields without a new BRD-level decision reversing Q-H3/Q-C1.
   (FR-057/058) **The "Student Satisfaction" and "Course Completion" gauges seen in the source
   mockup were deliberately never built** — they trace to no FR/BR and must not be silently
   reintroduced by a future maintainer who finds them in an old mockup file.
8. **No fee/payment, leave-workflow, biometric attendance, grading/assignment, transport/hostel/
   library/inventory, or native-mobile capability exists in this product.** Adding any of these is
   a new BRD-level scope decision, never a Tech-Design or code-level addition. (FR-076–088
   excluded)
9. **Collaboration rooms are gated by authentication + hashed passcode + RBAC only** — no
   safeguarding-specific moderation/supervision mechanism is present by design (BR-047). Do not
   silently add one; if the business decides one is needed, that is a new BRD-level requirement.
10. **Parent access is mediated exclusively by `ParentStudentLink`**, created only via the
    single-use, time-limited `ParentLinkCode` mechanism (T-003). Never grant Parent access by
    matching name/email/guessable identifiers.
11. **Password policy (`CFG-001`) and the 30-day attendance lock (`CFG-004`) are platform-wide
    constants**, not per-institution configurable, regardless of what FR-064's Settings UI
    exposes — see `ai-specification/_questions.md` Q-207 for the still-open exact boundary
    between FR-064's institution-level account settings and these fixed constants.
12. **Subscription tier entitlement (BR-063) is a second, independent authorization axis from
    RBAC** — a request must clear both. A future maintainer adding a new tier-gated capability
    must wire both checks, not assume RBAC alone is sufficient. (`07-business-logic/
    configuration-commercial.md` §Cross-reference)

## 3. Fixed platform constants (`CFG-` — change only via a recorded decision, never silently)

| Constant | Value | Source |
|---|---|---|
| Password: min length / char classes | 10 / ≥3 of 4 | D1, `CFG-001` |
| JWT access / refresh expiry | 30 min / 14-day sliding | D2, `CFG-002` |
| Message length bound | 4,000 characters | D3, `CFG-003` |
| Attendance correction lock | 30 calendar days | FR-035, `CFG-004` |
| Parent Link Code length / expiry | 8 chars / 7 days | T-003, `CFG-005` |
| Trial duration | 30 days | FR-070, `CFG-006` |
| Retention: student/guardian/staff PII | 7 years post-departure/termination | CMP-001, `CFG-007` |
| Subscription-end export grace period | 90 days | CMP-002, `CFG-008` |

Full detail: `.aidlc/02-solution-designer/ai-specification/12-infrastructure-spec.md` §6–7.

## 4. Standing technical decisions (do not re-litigate without a recorded reason)

Clean/Onion layering (Domain/Application/Infrastructure/Api, D13); RESTful JSON, URL-versioned
(D5); SignalR + Azure SignalR Service for real-time (T-001); Hangfire in-process/SQL storage for
background jobs (T-005); Azure Communication Services for email/SMS (T-004) and live meetings
(T-006, 5 carried-forward risks still open — see §5); Azure Blob Storage + SQL metadata for report
snapshots (D11); React + TanStack Query + Zustand + React Router + shadcn/ui/Radix themed to
`design-tokens.md` (D6). Full rationale: `final-tech-design.md` §12.

## 5. Open items a future maintainer must not silently resolve

1. **5 ACS Calling risks** (participant cap, tier-pricing fit, no-recording posture, browser
   compatibility, media-session reconnection) — named, owned, non-blocking, but not closed. Any
   change to the meeting feature must re-check these against `final-tech-design.md` §13.1.
2. **14 non-blocking provisional choices** in `ai-specification/_questions.md` (Q-201–Q-214) —
   the attendance-approver role mapping (Q-204/205/206) and the FR-064-vs-`CFG-001` boundary
   (Q-207) are the two most load-bearing; confirm before extending those areas, don't assume the
   provisional choice is permanent doctrine.
3. **Two mockup-derived artifacts deliberately excluded, not deferred:** the Reports screen's
   "Student Satisfaction"/"Course Completion" gauges, and the Pricing screen's per-tier
   feature-list *copy* (the tier/capability *model* itself, BR-061/062, is built). Neither should
   be reintroduced from a future reading of the original mockup file without first tracing it to
   an approved FR/BR.

## 6. Compliance posture

No named external data-protection regime; treated as confidential-by-default (`company.md` §8).
7-year PII retention post-departure; 90-day export grace period on subscription end, then archive,
then delete/anonymise (CMP-001/002/003). Any future regime adoption (e.g. a named jurisdiction's
law) is a `project.md` §4/§8 update, not a silent code change.

## 7. Where to look for detail

Requirements: `ai-specification/02-requirements-spec/` (sharded by feature: `institutions`,
`identity-access`, `academic-records`, `attendance`, `communication`, `operations`,
`configuration-commercial`, plus `_shared.md` for NFR/CMP). Business rules & validation:
`ai-specification/07-business-logic/` (same shard map). Data model:
`ai-specification/04-data-model.md`. Errors: `ai-specification/08-error-catalogue.md`. API/event
contracts: `ai-specification/05-api-contracts/` + `ai-specification/06-event-contracts.md`.
Security/RBAC: `ai-specification/09-security-spec.md`. UI:
`ai-specification/11-ui-spec/`. Tests: `ai-specification/14-test-spec/`. Tasks:
`ai-specification/15-task-breakdown.md`. Full index and shard map:
`ai-specification/00-spec-index.md`.
