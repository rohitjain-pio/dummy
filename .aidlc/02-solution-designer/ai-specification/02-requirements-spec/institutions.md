<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
generated: 2026-09-11
-->

# 02 — Requirements Spec — `institutions`

Traces: BRD §6.1 · ENT-001 · ROLE-001 (Platform Operator), ROLE-002 (Institution Administrator).
Design trace: Tech Design §2.2, §3.3, §5.1, §7.

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-001 | FR-001 | Platform Operator registers an institution (registration number, name, contact, full postal address) | `POST /api/v1/institutions` succeeds for role=PlatformOperator with all required fields valid; 403 for any other role; 400 on missing/invalid field (see VAL-001–004) |
| SPEC-FR-002 | FR-002 | Institution Administrator updates their own institution's details | `PUT /api/v1/institutions/{id}` succeeds only when `{id}` = caller's `institution_id` and role ∈ {Administrator, Principal, SchoolInCharge}; 403 for a different institution's admin |
| SPEC-FR-003 | FR-003 | Platform Operator assigns/views/changes an institution's subscription plan + start date | `PUT /api/v1/subscriptions` (institution-scoped) succeeds for PlatformOperator only; BR-064 |
| SPEC-FR-004 | FR-004 | Platform Operator withdraws an institution, preserving history | `POST /api/v1/institutions/{id}/withdraw` sets `IsActive=false`, `DeactivatedAtUtc=now`; no row deleted; reversible via a re-activate path; audit row written (NFR-032) |
| SPEC-FR-005 | FR-005 | Public searchable institution directory — name + city/region only | `GET /api/v1/public/institutions?query=` is anonymous, returns only `name`/`city`/`region` fields, never contact/address; backed by `PublicInstitutionDirectoryProjection` (D12) |

## Business rules

| BRL ref | Statement | Enforcement |
|---|---|---|
| BR-001 | Only Platform Operator may create/withdraw an institution | Authorization policy on `POST /institutions`, `POST /institutions/{id}/withdraw` |
| BR-002 | Institution admins may update their own institution only | Institution-scope check + role check on `PUT /institutions/{id}` |
| BR-003 | Duplicate rejected only when name + contact email + postal code **all three** match | `VAL-002` composite-uniqueness check, not a single-field check |
| BR-004 | Contact email valid; telephone valid; postal code valid 6-digit | `VAL-001/003/004` |
| BR-005 | Institution withdrawal is reversible deactivation, never destructive | D8 soft-delete pattern |
| BR-006 | Every other business record belongs to exactly one institution | Structural — `institution_id` FK + global query filter on every tenant-owned table (T-002) |

## NFR references

SPEC-NFR-016 (tenant isolation — the directory is the one bounded exception), SPEC-NFR-028/032
(audit), SPEC-NFR-006 (list/detail perf on directory search).

## Full detail

Field-level `VAL-` rules: `07-business-logic/institutions.md`. Endpoint contracts: `05-api-contracts/institutions.md`.
Error codes: `08-error-catalogue.md` (`ERR-1xx` range). UI: `11-ui-spec/settings-configuration.md`
(institution profile is edited from the Settings screen, per `ui-design-final.md` §12) and
`11-ui-spec/public-auth.md` (directory search surfaces in the Auth modal's School typeahead).
