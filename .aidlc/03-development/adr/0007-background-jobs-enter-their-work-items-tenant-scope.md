# ADR-0007 — Background jobs enter their work item's tenant scope explicitly

**Status:** Accepted
**Date:** 2026-09-14
**Context:** CT-402 (Phase P4), Hangfire report generation (T-005, Q-222)

## Decision

1. A job loads **its own work item by id** with `IgnoreQueryFilters()`, and nothing else. For
   CT-402 that is `ReportJobRunner` and `ReportFailureRecorder` loading one `ReportRun`.
2. It then calls `BackgroundTenantScope.Enter(run.InstitutionId)`. Every later query in the job runs
   under the ordinary CT-001 tenant filter, scoped to that institution.
3. `HttpCurrentTenant.InstitutionId` falls back to `BackgroundTenantScope` **only when there is no
   `HttpContext`**. Inside a real request the JWT claim is the sole source, exactly as before.

## Why

A Hangfire worker has no request and no `institution_id` claim. Without a tenant, the query filter
hides every row, so a report job would silently produce empty reports. That is the most dangerous
failure mode: green, and wrong. The alternatives were worse:

- **`IgnoreQueryFilters()` on every report query** would make each query responsible for adding its own
  `InstitutionId` predicate. That is the per-query discipline D4/T-002 exists to eliminate, and one
  missed predicate would leak another school's data into a report.
- **A synthetic `ICurrentTenant` per job via DI** would work, but it needs a custom Hangfire activator and
  duplicates the tenant plumbing. The `AsyncLocal` scope reuses the existing implementation unchanged.

## Scope of the exception

- The cross-tenant read is **one row, the job's own run, by primary key**. Its id came from a
  request that was itself tenant-checked when the run was created.
- The ambient scope cannot affect an HTTP request, because the fallback applies only when `HttpContext` is null.
  It is restored on dispose, so it cannot leak between jobs sharing a thread.

## Consequences

- **Proven by tests, including against a false positive found during CT-402.** The first HTTP test
  queue ran the job inside the request's async context, so it inherited the caller's claim and never
  exercised this path. The queue now suppresses execution-context flow. A test asserts that the job's
  audit rows carry the system actor (`Guid.Empty`) and that a second institution's data never appears
  in the report. Removing `Enter` makes that test fail.
- A real Hangfire server test (`HangfireFailureWiringTests`) runs the failure path with no
  `HttpContext` at all.
- CT-501's recurring jobs (retention sweep, rollover, trial reversion) and CT-202's lock sweep
  (ADR-0005) should adopt the same pattern when they are scheduled: load the work item, enter its scope.
