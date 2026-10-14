<!-- pipeline-provenance
produced_by: ai-spec-agent (test-spec-writer)
generated: 2026-09-11
-->

# 14 — Test Spec — `configuration-commercial`

| TC ref | Layer | Given | When | Then | Trace | Adversarial? |
|---|---|---|---|---|---|---|
| TC-701 | Integration | Rollover triggered | For each active student | New `Enrollment` row created; prior year's rows unchanged | BRL-060 | **Yes** — fails against an implementation that mutates the prior row |
| TC-702 | Integration | Hold-back list includes Student X | Rollover runs | Student X's new-year `Enrollment` places them at the same class level, not the next | FR-067 | — |
| TC-703 | Unit | Hold-back list references a student from another institution | Submitted | `ERR-901` | VAL-026 | **Yes** |
| TC-704 | Integration | Trial started | Purchase made before day 30 | Subscription set, trial closed, no reversion job effect | PF-013 path 3a | — |
| TC-705 | Integration | Trial started, no purchase | Day 30 passes | `subscription_plan_id` set to Entry; **no billing/payment API call is made anywhere in the code path** | FR-071 | **Yes** — fails if any billing call exists on this path at all |
| TC-706 | Integration | Institution at Entry tier's learner capacity | New student enrolment attempted | `ERR-801`, transaction not committed, no grace overage | BRL-073 | **Yes** — fails against an implementation allowing even one over-capacity row |
| TC-707 | Integration | Institution's tier excludes `ParentAccess` | A Parent-role account attempts a Parent-scoped read that RBAC alone would allow | `ERR-802`, even though RBAC passed | BRL-063, Cross-reference §configuration-commercial | **Yes** — fails against an implementation that checks RBAC only and forgets the tier axis |
