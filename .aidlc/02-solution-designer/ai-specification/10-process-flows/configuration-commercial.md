<!-- pipeline-provenance
produced_by: ai-spec-agent (process-flow-writer)
generated: 2026-09-11
-->

# 10 — Process Flows — `configuration-commercial`

## PF-012 — Academic year rollover

**Trigger:** Institution's configured academic-year end, or an on-demand leadership trigger.
**Actors:** system (scheduled job) or leadership (on-demand, API-059).
**Pre-conditions:** current academic year exists with an active roster.
**Post-conditions:** new `Enrollment` rows for the new year; prior year's data intact, read-only.

**Steps:**
1. Job/leadership triggers rollover for an `AcademicYear`.
2. System promotes the full roster by default (FR-067) — for each active student, a new
   `Enrollment` row is created against the next class level.
3. Leadership applies hold-back overrides (API-060), before or after step 2, for specific
   students — these students' new-year `Enrollment` row places them in the same class level
   again rather than the next one.
4. Prior year's `Enrollment`, attendance, and class-allocation rows are never mutated — queryable
   read-only (BR-060).

**Exception flows:**
- Hold-back list references a student not in this institution → `ERR-901`.

**Exercised by:** `TC-701`–`TC-703`.

## PF-013 — Trial lifecycle

**Trigger:** A prospective customer starts a trial.
**Actors:** prospective customer, system (scheduled reversion job).
**Pre-conditions:** none (public, no payment details, FR-070).
**Post-conditions:** either a purchased subscription, or an automatic reversion to Entry tier at
day 30.

**Steps:**
1. Customer starts a trial (API-063) — `trialExpiresAtUtc = now + 30d` (`CFG-006`).
2. Customer explores the platform under the trialled tier's entitlements.
3a. Customer purchases a plan before expiry (API-064, PlatformOperator-executed per BR-064 — the
    trial UI directs the customer to contact/enquiry rather than self-service purchase, since no
    payment-processing capability exists in this release, FR-081 excluded) → subscription set,
    trial closed.
3b. Trial expires with no purchase → scheduled job (reuses the rollover scheduler class, A4) sets
    `subscription_plan_id = Entry` — **no billing event fires** (FR-071's explicit "never auto-bill").

**Exception flows:** none distinct — this is a closed two-outcome lifecycle by design.

**Exercised by:** `TC-704`–`TC-706` (purchase-before-expiry, expiry-without-purchase, and the
adversarial fixture proving no billing/payment call is ever made on path 3b).
