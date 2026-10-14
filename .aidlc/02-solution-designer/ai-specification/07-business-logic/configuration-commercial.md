<!-- pipeline-provenance
produced_by: ai-spec-agent (business-logic-writer)
generated: 2026-09-11
-->

# 07 — Business Logic — `configuration-commercial`

## BRL — Institution configuration / academic year

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-057 (BR-057) | Configuration changed | applies only to the institution it was set in | never leaks to another institution |
| BRL-058 (BR-058) | Configuration change requested | caller role ∈ leadership | else denied |
| BRL-059 (BR-059) | Configuration changed | effective immediately for all users of the institution, audit row written (`_shared.md` §3) | the audit-write is unconditional — **not** a user-toggleable checkbox even though the mockup's `modalConfigure` shows one (`ui-design-final.md` §12) |
| BRL-060 (BR-060) | Academic year rollover runs | new `Enrollment` rows created; all historical academic/attendance/enrolment data preserved read-only | no historical row is ever mutated in place |
| BRL-067 (FR-067) | Year-end rollover triggers | full roster auto-promoted by default | leadership may override/hold back individual students, before or after |

## BRL — Subscription / Commercial

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-061 (BR-061) | Subscription tier assigned | one of {Entry, Mid, Bespoke} | no fourth tier |
| BRL-062 (BR-062) | Tier defines capacity/storage/support/capabilities | capabilities not included in the tier are unavailable, not merely hidden | enforced server-side, not a client-side hide (see BRL-073) |
| BRL-063 (BR-063) | Advanced analytics / parent access / custom reporting requested | only available if the institution's tier includes it | else `ERR-802` "not available on your plan" — this gate sits **in addition to** ordinary RBAC, not instead of it (a Parent-role account still can't do anything if the *tier* excludes Parent access, independent of whether the *role* would otherwise permit it) |
| BRL-064 (BR-064) | Subscription assignment/change requested | caller role == PlatformOperator | else denied |
| BRL-070 (FR-070) | Trial started | 30-day trial (`CFG-006`), no payment details collected | — |
| BRL-071 (FR-071) | Trial ends, no purchase decision | entitlements auto-revert to Entry tier | never auto-bill |
| BRL-073 (FR-073) | Action would exceed tier's learner-capacity or storage allowance | block the action, transaction not committed | no grace overage — the check runs **before** commit, not as a post-hoc warning |
| BRL-074 (FR-003, Q-211 resolved) | Institution created (`10-process-flows/institutions.md` PF-001) | `subscription_plan_id` auto-set to the Entry/Free tier at the same transaction, institution operational immediately within Entry limits | never left null — a Platform Operator may subsequently upgrade/change the plan (SPEC-FR-003), but this is optional and never a precondition for usability (operator-confirmed 2026-09-11, Jayesh Idnani) |

## Decision table — tier-capacity gate (BRL-073), evaluated on every capacity-consuming write

| Current usage vs. tier limit | Action type | Result |
|---|---|---|
| Below limit | Enrol student / consume storage | Proceed |
| At or above limit | Enrol student / consume storage | Reject `ERR-801` "tier capacity exceeded — upgrade required" |
| Below limit | Any other write | Proceed (gate only applies to capacity/storage-consuming actions) |

## Cross-reference — role×tier intersection (BR-063)

`09-security-spec.md` §2's permission matrix is **role-based**; this tier gate is a **second,
independent** axis evaluated after RBAC passes. A request must clear both: `RBAC(role, action)
== allow` AND `TierEntitlement(institution.tier, capability) == included`. Neither axis alone is
sufficient — this is stated explicitly here because it is easy to build RBAC correctly and still
ship a tier-bypass bug by forgetting the second check exists.

## VAL — field validation

| VAL ref | Field | Rule |
|---|---|---|
| VAL-024 | `institution_config.time_zone` | Valid IANA time zone identifier |
| VAL-025 | `institution_config.language` | From a supported-languages list — **[NEEDS ANSWER — Q-209]** BRD/Tech Design name no specific list; provisional choice: English only for this release, field present but single-valued, extend later without a schema change |
| VAL-026 | `academic_year.hold_back_student_ids` | Each ID must reference an active student of the same institution |

## Edge-case register

| # | Case | Resolution |
|---|---|---|
| E1 | Institution downgrades tier while already over the lower tier's capacity | **[NEEDS ANSWER — Q-210, non-blocking]** FR-073 blocks *new* capacity-consuming actions but the BRD is silent on an institution already over a newly-lower limit. Provisional choice: existing over-limit records are grandfathered (not force-deactivated), but **no further** capacity-consuming action is permitted until usage drops below the new limit — consistent with FR-073's "blocks the action" framing, which is forward-looking, not retroactive. |
| E2 | Trial reverts to Entry tier while an in-progress action (e.g. large report generation) is mid-flight | The in-flight action completes; the tier check only gates the *next* new capacity-consuming action, not one already past its own gate |
