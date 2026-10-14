# ADR-0001 — Tenant query filter applied structurally via a marker interface, not per-entity

**Status:** Accepted
**Date:** 2026-09-11
**Context:** CT-001

## Decision

Every entity implementing `ITenantOwned` automatically gets EF Core's global query filter
applied, via reflection over `modelBuilder.Model.GetEntityTypes()` in
`SchoolSyncDbContext.OnModelCreating` — not by hand-declaring `HasQueryFilter(...)` once per
entity type.

## Why

Tech Design (T-002/D4) mandates "EF Core global query filters" as the enforcement mechanism but
does not mandate *how* ~15 tenant-owned entity types each get the filter wired up. Hand-declaring
it per entity is the same logic copy-pasted 15+ times, and it is exactly the kind of place a
future CT adding entity #16 forgets the one line and silently ships an unscoped table.

## Consequences

- Adding a new tenant-owned entity to the model requires only implementing `ITenantOwned` — no
  `OnModelCreating` edit.
- The one, single-purpose exception (`ICurrentTenant.BypassTenantFilter`, BR-013's
  PlatformOperator path) is easy to audit because it is the only escape hatch that exists.
- `AuthRepository` uses `IgnoreQueryFilters()` directly for the anonymous registration/login path
  (see ADR-0002) — a narrower, query-level exception, not a change to this convention.
