# ADR-0003 — Marker interfaces + static-analysis "guard tests" as the pattern for cross-cutting rules

**Status:** Accepted
**Date:** 2026-09-11
**Context:** CT-003, CT-004

## Decision

Two cross-cutting rules — "every governed entity is audited" (CT-003, `IAuditable`) and "no
soft-deletable entity has a reachable hard-delete path" (CT-004) — are each implemented as:

1. A marker interface (`IAuditable`, `ISoftDeletable`) an entity implements to opt in.
2. A single mechanism that discovers implementers structurally (the `SaveChanges` interceptor;
   `SoftDeleteExtensions`), not a hand-maintained list of entity types.
3. An **adversarial static-analysis test** that scans every `.cs` file under `src/` for the
   specific forbidden pattern (`ExecuteUpdate`/`ExecuteDelete` bypassing the audit interceptor;
   `.Remove()`/`.RemoveRange()` against a soft-deletable `DbSet`) and fails the build if it is
   ever introduced — a mechanical guarantee that a future CT cannot silently reintroduce the
   bypass, rather than a code-review convention someone has to remember to enforce.

## Why

Both rules are exactly the kind of property that is easy to state, easy to honor by accident for
a while, and easy to violate once a later CT is under time pressure and reaches for the "obvious"
EF Core API that happens to skip the rule (bulk `ExecuteUpdate`, a raw `.Remove()`). A runtime
test only proves the rule holds for the code paths it happens to exercise; a static-analysis test
proves the *forbidden pattern doesn't exist anywhere*, which is what "no bypass path"/"no hard
delete path exists ... anywhere in the API surface" actually require as stated in
`07-business-logic/_shared.md` §§2–3.

## Consequences

- This is now the established pattern for any future cross-cutting "X must never happen" rule
  (e.g. a future rule about never logging PII) — reach for a marker interface + guard test before
  inventing a different enforcement mechanism.
- The guard tests only scan `src/`, deliberately excluding `tests/` — the adversarial tests
  themselves need to demonstrate the forbidden pattern's actual effect (see
  `AuditLoggingInterceptorTests.ExecuteUpdate_bulk_operation_bypasses_the_interceptor_...`), which
  requires calling it once, in test code, on purpose.
