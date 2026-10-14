<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
generated: 2026-09-11
-->

# 02 — Requirements Spec — `configuration-commercial`

Traces: BRD §6.11 (Institution Configuration + Academic Year), §6.12 (Commercial/Subscription) ·
ENT-015/016/017 · ROLE-001–004. Design trace: Tech Design §3.5, §3.7, §5.6, §9 (A4).

## Institution configuration + academic year (FR-063–068)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-063 | FR-063 | Maintain general settings (academic year, time zone, language) | `PUT /api/v1/institution-config` |
| SPEC-FR-064 | FR-064 | Maintain roles/permissions/account settings/password policy (institution-level account settings, **not** the platform-wide constants) | Same endpoint. **Q-207, ANSWERED** (operator-confirmed, Jayesh Idnani, 2026-09-11, as originally stated): FR-064's UI exposes account-management actions only (e.g. forcing a reset); the `CFG-001` password-complexity constant is never institution-overridable. Final boundary, not provisional — see `_questions.md`. |
| SPEC-FR-065 | FR-065 | Configure notification preferences (email/SMS/in-platform) | `PUT /api/v1/institution-config/notifications` |
| SPEC-FR-066 | FR-066 | Maintain academic settings (grading scale, course categories, assessment types, report-card format) | `PUT /api/v1/institution-config/academic` |
| SPEC-FR-067 | FR-067 | Year-end auto-promotes full roster by default; leadership overrides/holds back individuals | `POST /api/v1/academic-years/{id}/rollover` runs the promotion job; `POST /api/v1/academic-years/{id}/rollover/hold-back {studentIds}` applied before or after |
| SPEC-FR-068 | FR-068 | New rosters generated; historical data preserved read-only | Rollover creates new `Enrollment` rows (append-only, D14); no historical row is ever mutated |

## Commercial / Subscription (FR-069–073)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-069 | FR-069 | Browse public product info/plans/comparison, submit enquiry | `GET /api/v1/public/plans`, `POST /api/v1/public/enquiries` — anonymous |
| SPEC-FR-070 | FR-070 | Start a 30-day trial, no payment details | `POST /api/v1/trials` (`CFG-006` = 30 days) |
| SPEC-FR-071 | FR-071 | No purchase by trial end → auto-revert to entry tier (never auto-bill) | Hangfire scheduled job (reuses the rollover scheduler class, A4) sets `SubscriptionPlan = Entry` at trial expiry; no billing event fires |
| SPEC-FR-072 | FR-072 | Platform Operator assigns/amends subscription plan + date | `PUT /api/v1/subscriptions` |
| SPEC-FR-073 | FR-073 | Block an action exceeding tier capacity/storage; no grace overage | Enrolment/storage-consuming endpoints check current usage vs. `SubscriptionPlan` limits before commit; over-limit → `ERR-8xx`, transaction not committed |

## Business rules

| BRL ref | Statement |
|---|---|
| BR-057 | Configuration applies only to the institution it's set in |
| BR-058 | Only institution leadership may change configuration |
| BR-059 | Configuration changes effective immediately for all users, recorded for audit (`L4`, governed by NFR-033 — no separate decision) |
| BR-060 | Rollover creates new records, preserves all historical data |
| BR-061 | Three tiers: entry, mid, bespoke |
| BR-062 | Each tier defines capacity/storage/support/capabilities; excluded capabilities unavailable |
| BR-063 | Advanced analytics, parent access, custom reporting reserved for higher tiers |
| BR-064 | Only Platform Operator assigns/changes subscription |

**Note on BR-063 vs. the built product:** "Parent access" being tier-gated (BR-063) means the
Parent role (ROLE-007) and FR-075 are only reachable when the institution's tier includes it —
`09-security-spec.md` §2's permission matrix must intersect role-based RBAC with tier
entitlement, not RBAC alone. This is a real cross-cutting rule, not a UI-only concern; see
`07-business-logic/configuration-commercial.md` for the enforcement point.

## NFR references

SPEC-NFR-021/023 (tier capacity/storage scaling), SPEC-NFR-032 (subscription-change audit).

## Full detail

`07-business-logic/configuration-commercial.md` · `05-api-contracts/configuration-commercial.md` ·
`08-error-catalogue.md` (`ERR-8xx`/`ERR-9xx`) ·
`10-process-flows/configuration-commercial.md` (PF-012 rollover, PF-013 trial lifecycle) ·
`11-ui-spec/configuration-commercial.md` (Settings screen) and `11-ui-spec/public-auth.md` (Pricing screen).
