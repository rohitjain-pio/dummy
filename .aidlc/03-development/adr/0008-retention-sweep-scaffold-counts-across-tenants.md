# ADR-0008 — The retention sweep scaffold counts across tenants, and acts on nothing

**Status:** Accepted
**Date:** 2026-09-14
**Context:** CT-501 (Phase P5), `07-business-logic/_shared.md` §4, CMP-001/CFG-007

## Decision

`RetentionSweepJob` (daily, Hangfire) uses `IgnoreQueryFilters()` to **count** Student, Teacher and
UserAccount rows whose `DeactivatedAtUtc` is more than 7 years ago, across every institution. It
logs the counts at Warning. **It anonymises and deletes nothing.**

## Why cross-tenant

Retention is a platform obligation that runs with no caller and no tenant, the same situation as
ADR-0005's lock sweep. A tenant-filtered count would always be zero.

## Why a scaffold and not the real sweep

The task row says "job scaffold". The specs do not say **which fields are anonymised and which rows
are deleted**. `_shared.md` §4 says "secure delete or anonymise personal fields" without choosing,
and audit-log anonymisation ("strip `actor_user_id`/PII from snapshots") conflicts with CT-003's
immutable audit trail. Anonymisation and deletion are irreversible and legally significant (CMP-001).
Guessing them would be precisely the silent, irreversible decision this build must not make.

## Scope of the exception

- `COUNT` only. No row is materialised, returned or modified. A test asserts that every row, and
  every name, is unchanged after a run.
- When the real sweep is specified, it must follow ADR-0007: enumerate the affected institutions,
  then act inside each one's `BackgroundTenantScope`, so the destructive part runs under the ordinary
  filter.

## Consequences

- CMP-001 is **not yet met**. Records past retention are only reported. Raised as an owned feedback
  item with the field-level anonymise/delete decision it needs.
