# ADR-0006 — The public platform summary counts across tenants

**Status:** Accepted
**Date:** 2026-09-14
**Context:** CT-401 (Phase P4)

## Decision

`DashboardRepository.CountAcrossInstitutionsAsync` calls `.IgnoreQueryFilters()` on `Students`,
`Teachers` and `Classes`. It is the source for API-049 `GET /api/v1/platform/summary`. ADR-0002
requires every further use of `IgnoreQueryFilters()` to be argued on its own terms; this ADR is
that argument.

## Why

API-049 is **anonymous** and **platform-wide by definition**. BR-049 and SPEC-FR-053 describe
"platform-wide public summary (institutions/students/teachers/classes)". An anonymous caller has no
`institution_id`, so under the tenant filter every count would be zero, which is the wrong answer
rather than a safe one. The case matches ADR-0002 (no tenant exists) and ADR-0005 (no caller).

## Scope of the exception

- **Only `COUNT` aggregates cross the boundary.** No row is materialised, and no name, id or
  per-institution figure is selected. BRL-049: "never per-institution breakdown".
- The response shape is exactly four integers. A test asserts that it contains no institution name
  or id, and that the counts include a second institution's rows (so the cross-tenant path is
  proven real, not assumed).
- `Institutions` is not tenant-owned, so no filter is bypassed for that count.
- The method name carries `AcrossInstitutions`, so a reader sees the exception at every call site.

## Consequences

- Five direct uses of `IgnoreQueryFilters()` are now argued: ADR-0002 (anonymous auth), ADR-0004
  (parent link code), ADR-0005 (lock sweep), this ADR, plus the test-only seed helpers.
- Small-number disclosure: with very few institutions, public totals reveal something about each
  one, e.g. a single-institution platform publishes that institution's headcount. BR-049 accepts
  aggregate public figures, and this ADR does not add suppression. To feedback for a decision if
  it matters at launch.
