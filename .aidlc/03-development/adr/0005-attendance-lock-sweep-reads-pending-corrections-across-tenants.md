# ADR-0005 — The attendance lock sweep reads pending corrections across tenants

**Status:** Accepted
**Date:** 2026-09-14
**Context:** CT-202 (Phase P2)

## Decision

`AttendanceRepository.ListPendingCorrectionsAcrossInstitutionsAsync` calls `.IgnoreQueryFilters()`
on `CorrectionRequests` (with their `AttendanceRecord`) and on `InstitutionConfigurations`. Its only
caller is `AttendanceService.RunLockSweepAsync`, which enforces Q-206: a correction request still
pending when its record locks is auto-rejected. ADR-0004 said a further direct use of
`IgnoreQueryFilters()` must be argued on its own terms, so this ADR makes that argument.

## Why

The sweep is a scheduled job (Q-212 / Q-216; the Hangfire registration lands in CT-501). It runs
with **no caller, no JWT and no `institution_id` claim**, so there is no tenant for the filter to
scope to. Under the filter every tenant-owned row is invisible, the sweep would find nothing, and
TC-410 could never pass. Running the job once per institution with a synthetic tenant would need a
cross-tenant list of institutions first, so the boundary gets crossed either way. This version
crosses it once, in one named method.

The case is close to ADR-0002's (no tenant exists), not ADR-0004's (a known tenant crossing
deliberately).

## Scope of the exception

- **Rows read:** only `CorrectionRequest` rows with `Status = Pending`, their `AttendanceRecord`,
  and the owning institution's `TimeZone` string.
- **The only write the caller can make** is `Pending → AutoRejected` on the request and
  `CorrectionStatus → None` on the record. The record's `Outcome` is never changed. Nothing is
  returned to any HTTP caller, because the method has no HTTP path.
- **Each institution's own calendar decides.** "Locked" is computed from that institution's
  configured time zone, or the Q-215 fallback, never a single global zone. One run cannot lock
  a record early for an institution whose local day has not yet turned over.
- The method name carries `AcrossInstitutions`, so a reader sees the exception at every call site.

## Consequences

- The cross-tenant path is proven by a test: `The_sweep_reaches_other_institutions_pending_requests`
  seeds a pending request under a different institution from the test's tenant and asserts the
  sweep rejects it.
- Audit rows written by the sweep carry `ActorUserId = Guid.Empty`, CT-003's existing convention
  for a system actor. `CorrectionRequest.ResolvedByUserId` is left null for `AutoRejected`.
- CT-501 must invoke this through Hangfire without an `HttpContext`. `HttpCurrentTenant` and
  `HttpAuditContext` already tolerate a null context, but CT-501 should confirm it end to end.
