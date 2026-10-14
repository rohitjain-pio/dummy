<!-- pipeline-provenance
produced_by: ai-spec-agent (test-spec-writer)
generated: 2026-09-11
-->

# 14 — Test Spec — `institutions`

| TC ref | Layer | Given | When | Then | Trace | Adversarial? |
|---|---|---|---|---|---|---|
| TC-101 | Integration | Valid PlatformOperator session | `POST /institutions` with valid fields | 201, row created | SPEC-FR-001 | — |
| TC-102 | Unit | Two institutions differ only in postal code | Third submitted matching name+email but different postal code | 201 (not a duplicate) | BRL-003 | **Yes** — fails against a naive single-field-uniqueness implementation |
| TC-103 | Unit | Same name+email+postal as an existing (even withdrawn) institution | Submitted | `ERR-101` 409 | BRL-003, E1 | **Yes** — fails against an implementation that only checks active institutions |
| TC-104 | Integration | Administrator of Institution A | `PUT /institutions/{B.id}` | 403 `ERR-102`, no data changed | BRL-002 | **Yes** — fails against an implementation that only checks role, not institution match |
| TC-105 | Contract | Anonymous caller | `GET /public/institutions?query=green` | 200, only name/city/region fields present | SPEC-FR-005 | **Yes** — fails if `contactEmail`/`postalAddress` leak into the response |
| TC-106 | Integration | PlatformOperator | `POST /institutions/{id}/withdraw` then `/reactivate` | `is_active` toggles, `deactivated_at_utc` set/cleared, row never deleted | BRL-005 | — |
| TC-107 | Integration | Valid PlatformOperator session, no separate subscription call made | `POST /institutions` with valid fields | 201; `subscription_plan_id` is set to the Entry/Free tier in the same response, never null; a capacity-consuming action (e.g. enrol one student) succeeds immediately without any prior `PUT /subscriptions` call | BRL-074 (Q-211 resolved) | **Yes** — fails against an implementation that leaves `subscription_plan_id` null until a separate operator action, reintroducing the zero-capacity gap the operator explicitly rejected |
