# ADR-0004 — Parent Link Code consumption reads across tenants, by design

**Status:** Accepted
**Date:** 2026-09-11
**Context:** CT-103

## Decision

`ParentLinkRepository.FindCodeForConsumptionAsync` calls `.IgnoreQueryFilters()` when resolving a
submitted parent link code and its student's owning institution. This is the **second** direct use
of `IgnoreQueryFilters()` in the codebase, and ADR-0002 requires any such appearance to be
justified against it rather than accepted silently. This ADR is that justification.

## Why

The decision table in `07-business-logic/academic-records.md` distinguishes two failures that a
tenant-filtered lookup physically cannot tell apart:

| code status | expired? | institution match? | Result |
|---|---|---|---|
| Issued | N | N | Rejected `ERR-309` "institution mismatch" |
| Revoked/Expired/Consumed | — | — | Rejected `ERR-310` "code not valid" |

Under the global tenant filter, a code belonging to another institution's student is simply not
visible, so the only answer the code could ever produce is "no such code" — `ERR-310`. `ERR-309`
would be dead, unreachable code, and `TC-308` ("Code issued by Institution A; Parent registered
under Institution B submits it → `ERR-309`") could never pass. T-003 names this guard explicitly
as the thing that stops a code leaked outside its institution being redeemed cross-tenant, so the
distinction is a deliberate requirement, not an accident of wording.

This is therefore a different case from ADR-0002's. There, no tenant existed yet (an anonymous
caller has no `institution_id` claim to be scoped by). Here a tenant exists and is known — the
boundary is crossed *deliberately, to enforce the boundary itself*.

## Scope of the exception

- **Exactly one field crosses the boundary**: the student's `institution_id`, projected in the
  query. The foreign `Student` entity is never returned, never materialised into a response, and
  never reaches the caller on any path.
- It is used **only to refuse the request**. On a mismatch the method's result is discarded and
  `ERR-309` is returned; on a match the institutions are equal, so nothing cross-tenant remains.
- The `ParentLinkCode` row returned is not itself tenant-owned (`04-data-model.md` §2.12), so no
  filter is being bypassed for it — only for the `Student` join it needs to reach the institution
  id.

## Consequences

- There are now **two** legitimate direct uses of `IgnoreQueryFilters()` — ADR-0002's and this
  one. A third should still be treated as suspicious and argued on its own terms; this ADR does
  not open the door to a general pattern.
- `ERR-309` deliberately tells an unauthenticated-to-this-institution caller that the code is real
  but belongs to a different school, where `ERR-310` would reveal nothing. That disclosure is the
  spec's own choice (the decision table gives the two outcomes distinct codes and distinct
  wording), not this ADR's, and it is bounded: the caller learns only that the code exists
  somewhere, never whose it is, which student it names, or which institution issued it.
- The expiry check runs **before** the institution check, per the table's own row order, so an
  expired foreign code returns `ERR-308` and never reaches the cross-tenant comparison at all.
