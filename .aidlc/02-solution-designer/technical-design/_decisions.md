<!-- pipeline-provenance
produced_by: tech-design-agent
plugin_version: 0.3.0 (sdlc-greenfield-v3)
model: claude-sonnet-5
generated: 2026-09-11
phase: 1 (draft worksheet — preliminary stated decisions, pending Decide/Spike answers)
inputs:
  - .aidlc/01-brd-analysis/brd-analyst/final-brd.md
  - .aidlc/01-brd-analysis/brd-analyst/requirements.yaml
  - .aidlc/02-solution-designer/ui-designer/design-tokens.md
  - .aidlc/_config/company.md
  - .aidlc/_config/project.md
-->

# Stated Decisions Register (Phase 1 draft)

Per the router's Step A: these are choices the BRD/config force, that are clear industry best
practice with no real alternative in this context, or that are conventional defaults a competent
architect would pick and that are cheap to change if wrong. Each carries a one-line rationale and
is **`[override if wrong]`**. None of these appear on the worksheet (`_questions.md`) — they are
disclosed here for the architect's review, not asked as questions.

## Step 0 — Archetype (context for every decision below)

**Archetype:** Full-stack, multi-tenant SaaS. Builds its own frontend, backend, database and
identity/authorization end-to-end. Does not connect to any pre-existing institutional system.
Two externally-sourced capabilities are needed but not yet vendor-selected: (1) real-time
messaging/presence transport, (2) live audio/video meetings. Both are addressed as
Decide/Spike items in `_questions.md`, not assumed here, per `company.md` §2's explicit
instruction that messaging/eventing and any non-standardized vendor choice is a Tech Design
Decide/Spike.

All five planner passes apply in full: DB, Backend, Frontend, Integration (thin — vendor
contracts for notification delivery and media/meetings only, no legacy system to connect to),
Security.

---

## D1. Password complexity policy — resolves BR-014 hand-off `L1`
**Decision:** Minimum 10 characters; at least 3 of 4 character classes (upper, lower, digit,
symbol); checked against a common-password/breach-list blocklist at registration and password
change; no forced periodic rotation. Enforced via ASP.NET Core Identity's password validator
pipeline.
**Rationale:** Aligns with current NIST 800-63B guidance (length + blocklist over rotation +
arbitrary complexity theater); conventional, cheap to change via a single validator config.
`[override if wrong]`

## D2. Session / token expiry — resolves BR-015 hand-off `L2`
**Decision:** JWT access token: 30-minute expiry. Refresh token: 14-day sliding expiry, rotated
on use, revocable server-side (denylist on sign-out/password change).
**Rationale:** Standard short-access/long-refresh pattern for SPA + API; balances NFR-017
(session-theft protection) against usability; a config value, cheap to retune.
`[override if wrong]`

## D3. Message length bound — resolves BR-045 hand-off `L3`
**Decision:** 4,000 characters per message.
**Rationale:** Generous for a school-context chat message, keeps storage/row size predictable;
a single constant, trivially changed.
`[override if wrong]`

## D4. RBAC implementation model
**Decision:** Seven fixed roles (BR-007) implemented as ASP.NET Core policy-based authorization
with role + `institution_id` claims embedded in the JWT; every data-access query carries an
EF Core global query filter on `institution_id` (Platform Operator bypasses the filter
explicitly, never by omission).
**Rationale:** The BRD's role set is closed and finite (BR-007–BR-013) — no case for a
general-purpose granular-permission/claims engine at this scope. Institution-scoping as a
query filter, not per-query boilerplate, is the standard way to make NFR-016 ("absolute
isolation") a structural guarantee rather than a per-developer discipline. `[override if wrong]`

## D5. API style
**Decision:** RESTful JSON over ASP.NET Core Web API controllers, URL-versioned (`/api/v1/...`).
**Rationale:** Company standard is ASP.NET Core Web API; REST is the conventional fit, no
consumer requires GraphQL's query flexibility. `[override if wrong]`

## D6. Frontend data/state architecture
**Decision:** TanStack Query (React Query) for server-state/data-fetching and caching; Zustand
for local/UI state (e.g. active room, meeting media state); React Router for routing.
**Rationale:** Conventional, low-ceremony pairing for a React+TypeScript SPA of this shape;
avoids hand-rolled fetch/caching logic; swappable later at low cost. `[override if wrong]`

## D7. Report export formats — resolves NFR-027
**Decision:** PDF (generated server-side, e.g. QuestPDF) and CSV.
**Rationale:** "Commonly used document formats" for offline distribution (BR-052); PDF for
presentation, CSV for re-analysis. Both are cheap, standard .NET-ecosystem choices.
`[override if wrong]`

## D8. Soft-delete / reversible-deactivation pattern
**Decision:** Uniform `IsActive` (boolean) + `DeactivatedAtUtc` (nullable timestamp) columns on
Institution, Student, Teacher, Room and User Account; never a physical delete of a business
record required to stay reversible (BR-005, FR-014, FR-019, FR-004).
**Rationale:** The BRD requires reversibility for institution/student/teacher deactivation
uniformly — one pattern applied consistently avoids inventing per-entity variants.
`[override if wrong]`

## D9. Audit logging implementation — resolves NFR-028–033, BR-059 (`L4`, no separate decision needed)
**Decision:** A dedicated `AuditLog` table (actor user id, institution id, action, entity type,
entity id, before/after snapshot as JSON, timestamp, correlation id) populated via an EF Core
`SaveChanges` interceptor, so audit capture cannot be forgotten by an individual endpoint.
**Rationale:** Structural guarantee beats per-endpoint logging discipline for an org-wide
audit requirement spanning most entities; matches `company.md` §6 structured-logging default.
`[override if wrong]`

## D10. Attendance record concurrency
**Decision:** A unique constraint on `(person_id, attendance_date)`; a repeat submission for
the same person/date is an upsert (update), never a duplicate row (BR-031).
**Rationale:** Directly implements BR-031's stated rule; standard upsert pattern, no
alternative reading of the business rule.

## D11. Report/document storage
**Decision:** Generated report snapshots (FR-055) stored as blobs in Azure Blob Storage, with
a metadata row (`ReportRun`) in SQL Server pointing at the blob.
**Rationale:** Conventional pairing given Azure hosting is already the company default;
avoids storing large binary payloads in SQL Server rows. `[override if wrong]`

## D12. Public institution directory search — resolves FR-005
**Decision:** SQL Server `LIKE`/basic full-text search over institution name and city/region;
no dedicated search engine (e.g. Elasticsearch/Azure AI Search) at this scale.
**Rationale:** Directory is two fields, no ranking/relevance requirement stated; a dedicated
search engine would be over-engineering for this volume. `[override if wrong]`

## D13. Backend layering
**Decision:** Clean/Onion-style layering — `Domain` (entities, business rules),
`Application` (use-case/service layer, DTOs), `Infrastructure` (EF Core, external vendor
clients), `Api` (controllers, auth, mapping) — as separate projects/folders within one
ASP.NET Core solution.
**Rationale:** Standard, well-understood layering for a .NET Web API of this size; keeps
domain logic testable independent of EF Core/HTTP, matching `company.md` §7's unit +
integration testing split. `[override if wrong]`

## D14. Academic year / term modeling
**Decision:** `AcademicYear` and `Term` as first-class entities (ENT-017); a class roster
snapshot (`ClassRosterHistory` or year-scoped `Enrollment` row) is created at rollover
(FR-067/068) rather than mutating the live roster in place, so historical rosters stay
queryable read-only exactly as BR-060 requires.
**Rationale:** The BRD requires historical data to remain intact and read-only after
rollover — an append/snapshot model is the only way to satisfy that without a separate
history/audit table per entity. `[override if wrong]`

---

*These decisions are provisional until Phase 3. If the architect overrides any of them, note
the override against its `D#` id and it will be carried into `Tech_Design_Final.md` verbatim.*
