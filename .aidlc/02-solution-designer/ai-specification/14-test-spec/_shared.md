<!-- pipeline-provenance
produced_by: ai-spec-agent (test-spec-writer)
generated: 2026-09-11
-->

# 14 — Test Spec — `_shared`

## 1. Per-layer strategy (company.md §7)

| Layer | Tooling | Scope |
|---|---|---|
| Unit | xUnit (backend), Jest/React Testing Library (frontend) | Domain/Application logic in isolation — `BRL-`/`VAL-` rules, state machines, decision tables |
| Integration | xUnit | API ⇄ EF Core/SQL boundary — global query filter, upsert semantics, audit interceptor |
| Contract | xUnit (API contract tests) | Every `API-` endpoint's request/response shape and error codes |
| E2E | Playwright | User-facing flows — one per `PF-` at minimum |

## 2. Coverage policy

Judged by **lens completeness**, not a numeric percentage (company.md §7): happy path, error
paths, edge cases, security (negative-authorization), idempotency. Every `FR-`/`NFR-` traces to
≥1 `TC-` via `SPEC-FR-`/`SPEC-NFR-`.

## 3. Adversarial-fixture rule (company.md §7, binding, verbatim)

*"For every branch a test claims to cover, at least one case must fail against the pre-fix
code. A suite that only agrees with the implementation proves nothing."*

Applied with particular force (Tech Design §10, restated here as the test-spec's own obligation)
to:
- the `institution_id` global-query-filter tests — must prove cross-tenant data is **actually
  blocked**, not merely that same-tenant data is returned (see N3 in `09-security-spec.md` §6);
- the attendance lock/correction-approval state machine (FR-033–035) — must prove a day-31
  correction attempt is rejected, a day-29 attempt requires approval, and a same-day attempt does
  not;
- the Parent Link Code single-use/expiry logic (T-003) — must prove a second consumption
  attempt, an expired code, and a cross-institution code all fail;
- the attendance upsert-not-duplicate rule (BR-031, D10) — must prove a resubmission updates
  the existing row rather than creating a second;
- the audit-interceptor coverage (NFR-028) — must prove a direct write bypassing the interceptor
  is not exposed in production code (structural proof, not merely "a normal save produces a row").

**No test suite in this codebase is accepted on the basis that it merely exercises a mock.**

## 4. Test data & fixtures

- **Synthetic data only** (company.md §8) — never real learner/guardian/staff PII in any fixture,
  log, or ticket.
- **Per-worker isolation** (company.md §7) — parallel test runs use per-worker DB instances/
  schemas; no shared mutable fixture across parallel workers.
- **Seed fixtures:** at minimum, 2 institutions (to exercise cross-tenant isolation tests), each
  with leadership + Teacher + Student + Parent accounts, ≥2 classes, ≥1 room, ≥1 announcement,
  attendance history spanning day-0/day-15/day-31 boundaries (to exercise the correction state
  machine across all three regions in one fixture set).

## 5. Negative-authorization matrix

The full matrix (N1–N10) is authored once, in `09-security-spec.md` §6, and referenced from every
feature's `TC-` file — never re-specified per feature, to avoid ten slightly-different copies
drifting apart.

## 6. Performance scenarios (tied to NFR numeric targets)

| Scenario | NFR | Target | Method |
|---|---|---|---|
| List/detail load under normal load | NFR-006 | p95 <2000ms | Load test, realistic dataset size per institution |
| Dashboard render | NFR-007 | p95 <3000ms | Load test with the presence-cache (A2) warm and cold |
| Chat fan-out | NFR-008 | p95 <1000ms | SignalR load test, N participants in one room |
| Attendance-day peak | NFR-022 | No degradation at simulated peak | Bulk-upsert load test, N classes submitting concurrently at school-day start |
| Concurrent institutions | NFR-020 | No cross-tenant leakage or contention at N=50 | Multi-tenant load test |

## 7. `TC-` numbering convention

Each feature file's `TC-` range is reserved in `00-spec-index.md`'s master registry; no number is
reused across feature files.
