# ADR-0002 — Anonymous auth queries use `IgnoreQueryFilters()`, not `ICurrentTenant.BypassTenantFilter`

**Status:** Accepted
**Date:** 2026-09-11
**Context:** CT-002

## Decision

`AuthRepository`'s username/email-uniqueness check and the login/refresh account lookups call
`.IgnoreQueryFilters()` directly on the `UserAccount`/`RefreshToken` queries, rather than routing
through `ICurrentTenant.BypassTenantFilter` (BR-013's PlatformOperator escape hatch, ADR-0001).

## Why

VAL-006 (`07-business-logic/identity-access.md`) makes username/email uniqueness
**platform-global**, not institution-scoped — but an anonymous caller (registering, logging in)
has no `institution_id` claim at all, so there is no tenant to bypass *from*. This is a different
case from BR-013 (an authenticated PlatformOperator deliberately crossing tenant boundaries), not
a second instance of it — using `ICurrentTenant.BypassTenantFilter` here would either require
fabricating a fake "is PlatformOperator" claim for an anonymous request, or weakening
`BypassTenantFilter`'s meaning to include "no one is signed in," both worse than the direct,
narrowly-scoped `IgnoreQueryFilters()` call this uses instead.

## Consequences

- This is the **only** place `IgnoreQueryFilters()` is called directly instead of relying on the
  `ICurrentTenant` convention — any other appearance of it in a future CT should be treated as
  suspicious and reviewed against this ADR before being accepted as another legitimate instance.
- If a future stage needs the same "no tenant scope exists yet" pattern elsewhere (e.g. the public
  institution directory search, FR-005), it should cite this ADR rather than reinvent the
  reasoning.
