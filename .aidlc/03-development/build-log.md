<!-- pipeline-provenance
produced_by: code-generation (start-development, main conversation loop)
plugin_version: sdlc-greenfield-v3
spec_suite_version_at_phase_start: 1.0.2
suite_hash_at_phase_start: 2555f708e685939b4f0796c2feaed26940de79c8398357ec59787da87cc62351
verify_gate_C8_spec_suite_hash_P0: be02ee939542
-->

# Build Log — School Management System

Append-only. One entry per `CT-`, per `shared-agent-checkpoint`/`code-generation-progress-checkpointer`.

## Phase P0 — Foundation

### CT-001 — EF Core DbContext + all entities in `04-data-model.md` §2; global query filter convention (T-002)

**Status:** DONE
**Specs read:** `04-data-model.md`
**Silence report:** `PublicInstitutionDirectoryProjection` has no column list in §2 (only referenced
in §3/A5). Resolved by cross-reference to Q-H7 ("name and city/region only") and D12/A5, not a
blocking halt — built as `id, institution_id, name, city, region`.

**What was built:**
- Solution scaffold: `SchoolSync.sln`, Clean/Onion layering per D13 —
  `src/SchoolSync.Domain`, `src/SchoolSync.Application`, `src/SchoolSync.Infrastructure`,
  `src/SchoolSync.Api`, `tests/SchoolSync.IntegrationTests`. Target framework `net10.0`.
- 23 entity classes in `SchoolSync.Domain/Entities/` (the data model's ~20 named entities plus
  the `SubjectTeacherClassMapping` join table and the inferred `PublicInstitutionDirectoryProjection`),
  `ITenantOwned`/`ISoftDeletable` marker interfaces in `SchoolSync.Domain/Common/`.
- `SchoolSyncDbContext` (`SchoolSync.Infrastructure/Persistence/`) with every constraint from
  §2 (unique indexes incl. the two BR-003/BR-023 partial-unique indexes, FK relationships,
  enum-to-string conversions, `rowversion` concurrency tokens on the 4 entities named in §4 plus
  `Class`) and the **T-002/D4 global query filter convention**: applied by reflection over every
  `ITenantOwned` entity type in `OnModelCreating`, not hand-declared per entity. Bypass only via
  `ICurrentTenant.BypassTenantFilter` (BR-013's explicit PlatformOperator path — not wired to a
  real caller yet; that's CT-002).
- Initial EF Core migration (`InitialCreate`), a design-time `IDesignTimeDbContextFactory`, and
  an `AddInfrastructure` DI registration extension (deliberately does **not** register a default
  `ICurrentTenant` — a missing registration must fail fast at startup, not silently permit an
  unscoped query; CT-002 registers the real one).
- 4 adversarial tests in `TenantQueryFilterTests` — proving cross-tenant rows are actually
  blocked (not merely that same-tenant rows come back), that a deliberately-bypassed query still
  works (BR-013's escape hatch isn't dead code), and that the convention applies to a second
  entity type (Teacher) as well as Student, i.e. structurally, not per-entity.

**Defect found and fixed during CT-001 (not carried forward as debt):** the first migration
attempt failed against a real LocalDB instance — `SetNull` on both of `UserAccount`'s optional
`Student`/`Teacher` FKs created a multi-cascade-path conflict SQL Server rejects. Changed both to
`Restrict` (soft-delete entities never hard-delete, so no cascade behavior was actually needed).
Re-verified: migration re-generated, re-applied clean from zero against a fresh LocalDB database,
then rolled back and dropped.

**Gate:** ✅ Compiles (`dotnet build`, 0 errors — 4 pre-existing `NU1903` warnings on a transitive
`Microsoft.OpenApi` dependency; `dotnet list package --vulnerable` reports no vulnerable packages
in the resolved graph — flagged for the P7 security-scan gate to re-check, not treated as blocking
now). ✅ Migrations apply clean from zero (verified against a real LocalDB instance, not just
`dotnet build`).
**DoD:** ✅ every tenant-owned table has the filter applied via the shared convention, verified by
4 adversarial tests (0 failures) that prove — not assume — cross-tenant rows are blocked.
**Rollback:** drop the migration; no other code depends on it yet. (Unaffected — no rollback
needed.)

### CT-002 — JWT issuance/validation, password policy, denylist store; UserAccount auth endpoints (API-006–010)

**Status:** DONE
**Specs read:** `05-api-contracts/identity-access.md`, `07-business-logic/identity-access.md`,
`08-error-catalogue.md` (ERR-2xx/901), `09-security-spec.md` §1
**Silence report:** none blocking. Two routine implementation choices not named by the specs:
the refresh-token denylist storage mechanism (built as a DB table, `RefreshToken`, so it survives
across App Service instances — not in-memory/single-node) and the password blocklist's actual
word list (BRL-014 names "a blocklist" without enumerating it; seeded with a small placeholder
set, swappable for a real breached-password source without changing `IPasswordPolicy`'s contract).

**What was built:**
- `RefreshToken` entity + DbContext registration (unique index on `TokenHash`, never the raw
  token persisted — same principle as `PasswordHash`).
- Application layer (`SchoolSync.Application/Auth/`): `IAuthService`/`AuthService`,
  `IAuthRepository`, `IPasswordPolicy`, `IPasswordHasher`, `ITokenService` contracts — Application
  has no EF Core/Infrastructure dependency (Clean/Onion, D13).
- Infrastructure implementations: `PasswordPolicy` (CFG-001/D1: 10 chars, ≥3/4 classes,
  blocklist), `BCryptPasswordHasher`, `JwtTokenService` (CFG-002/D2: 30-min access token with
  `sub`/`role`/`institution_id` claims; 14-day refresh, rotated on use), `AuthRepository`
  (EF-backed; uses `IgnoreQueryFilters()` for the anonymous registration/login path specifically,
  since VAL-006's username/email uniqueness is platform-global and an anonymous caller has no
  tenant scope to filter by at all — a different case from BR-013's authenticated-PlatformOperator
  bypass, not a violation of "one named cross-tenant path"), `HttpCurrentTenant` (the real
  `ICurrentTenant`, CT-001 shipped with none registered on purpose).
- `AuthController` (`/api/v1/auth/{register,login,logout,refresh,me}`), each failure mapped to
  its exact catalogue `ERR-` code and message text, never a re-worded ad hoc message.
- JWT bearer authentication wired into `Program.cs`; signing key sourced from configuration,
  never hardcoded (company.md §4) — local dev value set via `dotnet user-secrets`, never
  committed.
- 11 new tests: 4 adversarial `PasswordPolicyTests` (each rejected case is one condition away
  from passing, not an obviously-bad password) + 7 full-pipeline `AuthEndpointsTests` via
  `WebApplicationFactory<Program>` — register→login→refresh(rotate)→replay-rejected→logout→
  post-logout-refresh-rejected in one continuous adversarial flow, plus duplicate-registration,
  weak-password, generic-login-error (proving unknown-user and wrong-password are byte-for-byte
  identical responses), and the Q-202 PlatformOperator-registration-rejection case.

**Two real defects found and fixed by actually running the tests, not assumed away:**
1. `Program.cs` read `Jwt:SigningKey` eagerly (with a fail-fast throw) inside `AddInfrastructure`,
   called before `builder.Build()` — exactly the point `WebApplicationFactory`'s test
   configuration overrides are injected. Eagerly-read config silently used the pre-override
   snapshot, so every test run failed at startup even with a valid test signing key configured.
   Fixed by moving the read into a `services.AddOptions<JwtBearerOptions>().Configure<IConfiguration>(...)`
   callback, which resolves lazily against the live `IConfiguration`, well after the test host's
   overrides are in place.
2. `AuthController.Logout` read the `sub` claim via `User.FindFirstValue(JwtRegisteredClaimNames.Sub)`
   and always got `null` — ASP.NET Core's default inbound claim-type map silently renames `sub` to
   a legacy XML-namespace URI unless disabled. Fixed with `options.MapInboundClaims = false`, so
   the claims on the validated principal are named exactly as issued (`sub`, `role`,
   `institution_id`, matching `09-security-spec.md` §1 literally).

(A third issue, `UseInternalServiceProvider` conflict between the SqlServer and InMemory EF
providers when the test host layers its own `AddDbContext` over `AddInfrastructure`'s, was a
test-fixture-only issue — fixed in `AuthApiFactory`, not production code.)

**Gate:** ✅ Unit + integration tests green (15/15 — the 4 from CT-001 plus 11 new).
**DoD:** ✅ Register/login/logout/refresh proven end-to-end against CT-001's schema, through the
real HTTP pipeline, not mocked.
**Rollback:** feature-flag the endpoints off; schema unaffected (not exercised — no rollback
needed).

### CT-003 — `SaveChanges` audit interceptor + `AuditLog` table (already in CT-001's schema)

**Status:** DONE
**Specs read:** `07-business-logic/_shared.md` §3, `09-security-spec.md` §8
**Silence report:** none blocking. The governed entity set is directly derivable from the spec's
own citations rather than a guess: NFR-028 names student/staff/class/attendance/announcement
explicitly; BR-044 (message revision/withdrawal) and BR-059 (institution configuration changes)
extend it by citation. Marked `IAuditable`: `Student`, `Teacher`, `Class`, `AttendanceRecord`,
`Announcement`, `Message`, `InstitutionConfiguration`.

**What was built:**
- `IAuditable` marker interface (Domain) — a future CT adding audit coverage for a new entity
  just implements it; the interceptor discovers governed entities this way, never via a
  hand-maintained type list.
- `AuditLoggingInterceptor` (`SaveChangesInterceptor`) — the **only** writer of `AuditLog` rows
  for entity changes anywhere in the codebase. Classifies `Modified` states on soft-deletable
  entities as `Deactivate`/`Reactivate` rather than a generic `Update` (and `Announcement`/
  `Message` withdrawal similarly), per the action vocabulary in `_shared.md` §3. Runs inside the
  same `SaveChanges` call, so the audit row is in the same transaction by construction.
- `IAuditContext`/`HttpAuditContext` — actor user id (from the `sub` claim) and correlation id
  (reuses `HttpContext.TraceIdentifier`, satisfying company.md §6 with no new moving part).
- Registered via `AddInterceptors(sp.GetRequiredService<AuditLoggingInterceptor>())` so it's
  wired into every `SaveChanges` call through the one shared `DbContext`.
- 5 new tests: normal-path Create produces exactly one correctly-shaped row; a soft-delete
  transition is classified `Deactivate`, not `Update`; **the required adversarial pair** —
  `ExecuteUpdate` (a real EF Core 7+ bulk-operation path that bypasses `SaveChanges`/
  `ChangeTracker` entirely) is proven to silently produce zero audit rows for a governed entity
  change, and a companion static-analysis test scans every `.cs` file under `src/` for
  `ExecuteUpdate`/`ExecuteDelete` and fails the build if either ever appears in production code —
  a mechanical guarantee, not a promise, that the demonstrated bypass path stays unused.

**One provider-compatibility issue found and resolved (test-fixture-only, not production code):**
the adversarial bypass test needs `ExecuteUpdate`, which EF Core's InMemory provider does not
implement at all (throws before reaching a database) — tried SQLite next, which doesn't support
SQL Server's database-generated `rowversion` columns several entities rely on (`NOT NULL
constraint failed`) — settled on real LocalDB, consistent with how CT-001/CT-002 already verify
against real SQL Server rather than assuming a lighter provider proves the same thing.

**Gate:** ✅ Adversarial test proves no bypass path (19/19 total tests green: 15 carried forward +
4 new persistence tests — the 5th, the static-analysis check, is listed above as part of the
adversarial pair but is itself the 19th).
**DoD:** ✅ every governed mutation produces exactly one audit row — proven for Create and for a
soft-delete transition, and proven that the one real bypass mechanism in EF Core 7+ is both
demonstrated dangerous and mechanically absent from production code.
**Rollback:** disable interceptor registration; no schema change needed (confirmed — this CT
introduced no migration).

### CT-004 — Soft-delete convention (D8) applied to Institution/Student/Teacher/Room/UserAccount

**Status:** DONE
**Specs read:** `07-business-logic/_shared.md` §2
**Silence report — real gap found and fixed:** `04-data-model.md` §2.8 never lists `IsActive`/
`DeactivatedAtUtc` for `Room`, even though `_shared.md` §2 explicitly names Room as one of the
five soft-deletable entities and CT-001 (reading only `04-data-model.md` per its own `Specs:`
field) built `Room` without them. Resolved by cross-reference — `_shared.md` is the authoritative
cross-cutting policy for this exact pattern — not a new decision: added `IsActive`/
`DeactivatedAtUtc` to `Room`, implementing `ISoftDeletable`.

**What was built:**
- `SoftDeleteExtensions.Deactivate()`/`.Reactivate()` (Domain) — the one canonical state
  transition every future CRUD CT (institutions, students, teachers, rooms, user accounts) must
  call through, rather than five different inline `entity.IsActive = false` implementations that
  could drift out of sync with each other over time.
- Migration adding `Room.IsActive`/`Room.DeactivatedAtUtc`.
- 6 new tests: a `[Theory]` proving `Deactivate()` then `Reactivate()` restores bit-for-bit the
  original state, run against **all five** named entities (not one representative — Room, the
  entity this CT itself had to fix, is exactly the kind of gap a single-entity test would have
  missed), plus a static-analysis test (same pattern as CT-003's `ExecuteUpdate` scan) that fails
  the build if `.Remove()`/`.RemoveRange()` is ever called against any of the five soft-deletable
  `DbSet`s anywhere in `src/`.

**Gate:** ✅ 25/25 tests green — no hard-delete path reachable for the five (mechanically
enforced, not merely asserted).
**DoD:** ✅ Reactivate mirrors Deactivate exactly, proven for all five entities.
**Rollback:** revert the convention; entities unaffected since it was additive (not exercised —
no rollback needed).

## Phase P0 (Foundation) — complete, phase-level verification-gates run

All 4 CTs done (CT-001–CT-004). 25/25 tests green. `dotnet build` clean (0 errors; 4 pre-existing
`NU1903` warnings on a transitive `Microsoft.OpenApi` dependency, `dotnet list package
--vulnerable` reports none — carried to the P7 final-integration gate for re-check, not
blocking here). Migrations apply clean from zero (verified against real LocalDB three times
across the phase, not assumed from a green build). Two spec gaps found and fixed by
cross-reference to a more specific/authoritative spec section, not by invention:
`PublicInstitutionDirectoryProjection`'s schema (CT-001) and `Room`'s soft-delete columns
(CT-004). Three real code defects caught by actually running tests against a real database, not
assumed away: a cascade-path conflict (CT-001), an eager-config-read that defeated test overrides
and a claim-mapping default that broke a claim read (both CT-002).

**Phase-level verification-gates (accumulated suite + lint), run before Gate 3:**
- `dotnet format --verify-no-changes` initially **FAILED** (real whitespace-style violations
  across 3 test files, company.md §5) — fixed with `dotnet format`, re-verified clean, then the
  full 25-test suite + build re-run to confirm the reformat changed nothing behaviorally.
- `dotnet list package --vulnerable`: clean across all 5 projects.
- `bash scripts/verify-gate.sh --root .` — **first run: 1 FAIL (C5, no machine-produced test
  report existed yet — only console `dotnet test` output had been produced across all 4 CTs),
  1 WARN (C8, build log had no row matching the spec-suite hash)**. Both fixed: generated a real
  `TestResults-P0-phase-gate.trx` (the filename must match `TestResults*.trx` — an
  arbitrarily-named `.trx` file does not satisfy the check, learned by trying one first) and
  recorded the C8 hash (`be02ee939542`) in this file's own provenance header. **Final result: `0
  FAIL, 0 WARN` — gate checks satisfied.**

**Phase P0 is ready for Gate 3 (human phase-gate approval).** Per the orchestrator's own rule,
this command does not approve its own work — the operator's explicit approval is the only thing
still outstanding before Phase P1 (Institutions/Identity/Academic Records) can start.

**Gate 3 (Phase P0) approved by Jayesh Idnani, 2026-09-11.**

## Phase P1 — Institutions / Identity / Academic Records

### CT-101 — Institution CRUD (API-001–004b), public directory (API-005)

**Status:** DONE
**Specs read:** `02-requirements-spec/institutions.md`, `05-api-contracts/institutions.md`,
`07-business-logic/institutions.md`, `07-business-logic/configuration-commercial.md` (BRL-074),
`10-process-flows/institutions.md` (PF-001), `08-error-catalogue.md` (1xx), `14-test-spec/institutions.md`
**Silence report:** none blocking. VAL-003's phone-format question (Q-201) already has an
operator-confirmed provisional answer (E.164-tolerant, 7–15 digits, optional `+`) from the ai-spec
stage — not re-litigated here.

**What was built:**
- `IdempotencyRecord` entity (Domain) — API-001's `Idempotency-Key` header handling (a repeated
  key within 24h returns the original `201` body), kept distinct from BRL-003's content-based
  duplicate check.
- 3 `SubscriptionPlan` seed rows (BR-061: Entry/Mid/Bespoke) via EF Core `HasData`, fixed
  well-known GUIDs so the seed is deterministic across environments.
- `IInstitutionService`/`InstitutionService` (Application) — register (auto-assigns the Entry
  plan in the same `SaveChanges` call as the institution row, BRL-074/Q-211), update (403-not-404
  on scope mismatch, BRL-002/E2), withdraw/reactivate (via CT-004's `SoftDeleteExtensions`),
  directory search (shape-limited to name/city/region by the `DirectoryEntry` record itself, never
  by remembering to omit fields per call site).
- `InstitutionRepository` (Infrastructure) — the composite BRL-003 duplicate check runs directly
  against `Institutions` (not `ITenantOwned`, so no filter to bypass) and correctly includes
  withdrawn institutions (E1); the public directory projection is refreshed synchronously on
  create/update/withdraw per Tech Design A5, and is *removed* from the searchable directory (not
  the source `Institution` row) when an institution is withdrawn.
- `InstitutionsController` + a separate, `[AllowAnonymous]`-only `PublicInstitutionDirectoryController`
  (API-005) — kept as two controllers specifically so `[Authorize]` can never apply to the public
  endpoint even by an accidental shared base class.
- 7 tests, one per `TC-101`–`TC-107`, including the 3 the test spec itself marks adversarial:
  TC-102 (differ-by-postal-code-only is NOT a duplicate), TC-103 (same triple as a *withdrawn*
  institution still rejected), TC-105 (directory response never contains `contactEmail`/
  `postalAddress`/`registrationNumber`, checked against the raw response body, not just the
  typed DTO shape), TC-107 (the Entry plan id is the specific well-known seed id, not merely
  non-null).

**One test-infrastructure issue found and fixed (not production code):** EF Core's `HasData`
seed only materializes for the InMemory provider once `Database.EnsureCreated()` is explicitly
called — unlike a real migration-driven database, seeding is not automatic. First test run failed
with "No Entry/Free SubscriptionPlan is seeded"; fixed by calling `EnsureCreatedAsync()` once in
the test fixture's setup.

**Gate:** ✅ TC-101–107 green (32/32 total tests: 25 carried forward + 7 new).
**DoD:** ✅ all 5 endpoints + directory search implemented; BRL-003 duplicate check adversarially
tested; institution creation auto-assigns the Entry/Free plan with no post-creation null window
(TC-107).
**Rollback:** feature-flag institution creation off; existing rows unaffected (not exercised — no
rollback needed).

### CT-102 — Student CRUD (API-011–015), guardian fields

**Status:** DONE (code complete) — **UNVERIFIED: no test execution.** See the phase-wide build
environment note below; this CT's gate has NOT been satisfied by a test run.
**Specs read:** `02-requirements-spec/academic-records.md` (Students, SPEC-FR-011–016),
`05-api-contracts/academic-records.md` (Students section, API-011–015),
`07-business-logic/academic-records.md` (BRL-017–021, VAL-008–011, E3),
`08-error-catalogue.md` (ERR-301), `14-test-spec/academic-records.md` (TC-301, TC-302)
**Silence report:** two field rules trace to questions `00-spec-index.md` §12 still lists as
unresolved — **Q-203** (VAL-010's enrolment age window) and **Q-201** (VAL-003's phone format,
inherited by VAL-011). §13 explicitly authorises building against their stated provisional
choices, so this is not a HALT. Neither is hardened: each is a pair of named constants in one
small type (`StudentFieldPolicy.MinPlausibleAgeYears/MaxPlausibleAgeYears`,
`ContactFormats.MinPhoneDigits/MaxPhoneDigits`) with the question id in the comment, so answering
either is a one-file edit. No `CFG-` key exists for either (12-infrastructure-spec.md lists
CFG-001–008, neither covered), so neither is configuration-bound.

**What was built:**
- `ContactFormats` (Application/Validation) — VAL-001 email via the framework's own RFC-5322
  parser (`MailAddress.TryCreate`, not a hand-rolled regex) and VAL-003 phone. Shared rather than
  restated because VAL-011 defines guardian contact fields *as* "same rules as VAL-001/VAL-003",
  and CT-104's teacher contact fields inherit the same pair.
- `StudentFieldPolicy` — VAL-008–011 on a payload in isolation, mirroring CT-002's split of
  `PasswordPolicy` out of `AuthService`. Age is whole-calendar-years, not a year subtraction.
- `StudentService` — enrol/amend/withdraw/reactivate/link-account/read. BRL-018 checks **both**
  identifiers independently (TC-302's adversarial point); the amend path excludes the student
  itself so a no-op rename cannot collide with its own row.
- `StudentRepository` — every query runs under the CT-001 tenant filter, no `IgnoreQueryFilters()`
  anywhere, so BR-018's "within institution" and BRL-020's access scope are structural. The
  uniqueness checks deliberately include **withdrawn** students, matching the unfiltered
  `(InstitutionId, SrNumber)` / `(InstitutionId, RollNumber)` unique indexes — an implementation
  that skipped inactive rows would accept a payload SQL Server then rejects.
- `Roles` (Api/Authorization) — the leadership set `{Administrator, Principal, SchoolInCharge}`
  (SPEC-FR-032, BR-010) expressed once as compile-time constants, plus the read-only
  leadership+Teacher set API-014/API-028 need. CT-104/105 reuse it.
- `StudentsController` — API-011–015 + the `/reactivate` mirror, matching CT-101's
  withdraw/reactivate pair. Writes are leadership-only, reads add Teacher.
- `ErrCatalogue.Err301` added with the catalogue's wording verbatim.

**BR-021 / BRL-021 (the DoD item), handled structurally:** guardian fields exist only inside
`StudentResult`/the single student response body. There is deliberately **no**
`GET /students/{id}/guardians` — a second endpoint is exactly the "separate, looser rule" BRL-021
forbids. The gate is therefore the same `[Authorize]` as the record itself, not a parallel one.

**Two contract tensions, both resolved without weakening either side:**
1. API-011 lists `classId` with no `?` (every optional field in that same line carries one) and
   SPEC-FR-011 requires the class to exist, but `Student.ClassId` is a nullable column (CT-001:
   "BR-019 gates attendance on this, not enrolment itself"). Resolved as: the **API** requires a
   placement at enrolment, the **schema** continues to tolerate its absence. Both statements stay
   true.
2. API-012 lists `ERR-102`, but within a single institution a leadership caller always has
   permission and the tenant filter makes another institution's student invisible rather than
   forbidden. `ERR-102` here is the **role** denial (a Teacher attempting a write), which — per
   CT-101's precedent with `[Authorize(Roles = ...)]` — is emitted by the framework as a bodyless
   403. Flagged as an observation below rather than diverging from that precedent mid-phase.

**Tests written (26 test methods, ~43 cases once the `[Theory]` rows expand; none executed):** TC-301 (integration) and TC-302 (unit, as the test spec's
Layer column specifies) plus TC-302's mirror, a positive control so a blanket-failure
implementation cannot pass both halves, the withdrawn-student uniqueness case, the
birthday-not-yet-reached age case that a naive year subtraction gets wrong, both Q-201/Q-203
provisional boundaries, anonymous and cross-institution access checks, and the Teacher
read-yes/write-no pair.

**Gate:** ❌ **NOT SATISFIED.** TC-301/TC-302 are written but have never run.
**DoD:** ⚠️ code-complete against every DoD clause (SR/roll uniqueness enforced both ways;
guardian fields access-gated per BR-021) but **unproven** — no assertion in this CT has ever
executed.
**Rollback:** none needed; nothing was deployed.

### Phase P1 build environment — BLOCKED, operator-directed to continue

`dotnet build` fails on this machine: all five projects target `net10.0` and only the .NET 9 SDK
(9.0.317) is installed (`error NETSDK1045`). `bin/Debug/net10.0/` artifacts exist, so a .NET 10
SDK was present when P0 and CT-101 were built; it is no longer. No approved artifact pins a
framework version — the spec suite, `final-tech-design.md` and `project.md` §2 are all silent, so
`net10.0` was a free CT-001 build-time choice recorded only in this log.

Operator (Jayesh Idnani, 2026-09-11) was offered three options — install the .NET 10 SDK, retarget
to net9.0, or build blind — and **chose to build blind and verify later**, with the stated
consequence accepted: every CT from 102 on is UNVERIFIED and **Phase P1 cannot reach Gate 3**
until the suite actually runs green.

**Partial verification obtained anyway, without touching this repo:** the source tree was copied
to a throwaway directory, that copy retargeted to `net9.0` with packages downgraded to 9.0.0, and
**it compiles clean — all five projects, 0 errors, 0 warnings**, including every CT-102 file. So
CT-102 is type-correct and its API usage is valid; what remains unproven is **behaviour**.
Test *execution* could not be obtained: the VSTest host fails to load with
`FileLoadException: ... testhost.dll. Access is denied.` even with full-control ACLs, which is an
application-control/AV restriction on this machine, separate from the SDK problem.

**Also noted, not fixed (outside CT-102's scope):** CT-101's `InstitutionService` never returns
`ValidationFailed` — VAL-001/002/003 are declared for API-001 but no field validation is actually
performed there, so `ErrCatalogue.Err901` is unreachable on that path. CT-102 does implement its
VAL- rules. This is a genuine CT-101 spec-conformance gap for the phase-conformance pass or code
review to rule on; it was not silently repaired here.

### CT-103 — Parent Link Code + link (API-016–019, T-003)

**Status:** DONE (code complete) — **UNVERIFIED: no test execution.** Compiles clean.
**Specs read:** `05-api-contracts/academic-records.md` (API-016–019),
`07-business-logic/academic-records.md` (T-003 state machine, consumption decision table, VAL-012,
E1, BRL-065/066), `10-process-flows/academic-records.md` (PF-005),
`08-error-catalogue.md` (ERR-307–310), `14-test-spec/academic-records.md` (TC-305–309),
`12-infrastructure-spec.md` (CFG-005)
**Silence report:** one genuine silence, resolved conservatively rather than by inventing an
error — **what happens when a Parent already linked to a student redeems a further code for that
same student.** No `ERR-` covers it and `08-error-catalogue.md` is a closed registry, so the code
is still consumed (it was validly presented) and the existing link is returned unchanged, with no
duplicate row — which the composite PK `(ParentUserId, StudentId)` would reject anyway. Flagged
here for review rather than treated as settled.

**What was built:**
- `ParentLinkCodeGenerator` — CFG-005's 8 characters, drawn with `RandomNumberGenerator.GetInt32`
  (a credential, so not `Random`; `GetInt32` is rejection-sampled so the draw stays unbiased where
  a `% alphabet.Length` would skew toward the first letters). Alphabet excludes O/0/I/L/1 because
  PF-005 step 2 has a human reading the code off a message and typing it in.
- `ParentLinkService` — the whole T-003 state machine. States are **derived** from the three
  nullable timestamps (`04-data-model.md` §2.12) rather than stored: `Consumed`/`Revoked` from
  their timestamps, `Expired` from `now >= ExpiresAtUtc`. Deriving `Expired` is what makes expiry
  correct with no sweeper job; the cost is that the derivation must be applied identically
  everywhere, so it lives in two private helpers and nowhere else.
- **Decision-table order is honoured exactly, and it is load-bearing.** Terminal → `ERR-310`;
  then expiry → `ERR-308`; then institution → `ERR-309`. An expired *foreign* code therefore
  returns `ERR-308` and never reaches the institution comparison, because the table's expired row
  carries "—" in the institution-match column. There is a test asserting precisely that ordering.
- The **regenerate** edge: issuing while a code is still `Issued` revokes the previous one, so a
  student never holds two live codes. Revocation is how "old code invalidated" is expressed, which
  also keeps the filtered unique index (unique on `Code` WHERE unconsumed AND unrevoked)
  satisfiable.
- `ParentLinkRepository`, `ParentLinksController` (API-016–019), `ErrCatalogue.Err307–310` with
  catalogue wording verbatim and the catalogue's own status codes (307→409, 308→**410 Gone**,
  309→403, 310→**410 Gone**).
- API-019's two halves: `GET /students/me` (Student, FR-074) and `GET /students/linked` (Parent,
  FR-075/BR-066). These do not collide with CT-102's `GET /students/{id:guid}` — the `:guid`
  route constraint means neither literal can match it.
- A separate `LinkedStudent` shape, deliberately **not** `StudentResult`: BR-066 scopes a Parent
  to linked children, and reusing the staff projection would hand a Parent the guardian contact
  fields that BRL-021 ties to the student record's own (staff) access rule.

**ADR-0004 recorded** — `adr/0004-parent-link-code-consumption-reads-across-tenants-by-design.md`.
ADR-0002 states that `IgnoreQueryFilters()` appears exactly once and that any further use must be
argued against it. CT-103 needs a second: `ERR-309` (institution mismatch) is only distinguishable
from `ERR-310` (not valid) if the foreign student's institution can be read at all — under the
tenant filter the code would simply be invisible, `ERR-309` would be dead code, and TC-308 could
never pass. The exception is bounded to one projected field (the owning institution id), used only
to **refuse** the request; no foreign student data is returned on any path.

**Tests written (20 test methods, 21 cases; none executed):** TC-305/306/307 at the unit layer as the spec's Layer column
specifies, TC-308 and TC-309 end-to-end, plus the CFG-005 boundary proved at 6 days pass / 7 days
fail (so the window is demonstrably 7 days and not merely "some window"), the expiry-before-
institution ordering, both terminal transitions, ERR-307 on revoking a consumed code, the
regenerate-invalidates-the-old-code case, E1 (withdrawal does not auto-revoke), BR-066's
unlinked-student exclusion, and both role gates (a Parent cannot mint a code; leadership cannot
redeem one). A new `TestClock` was added because the 7-day window cannot be tested honestly
against the wall clock — back-dating the stored row proves only the comparison, never the window.

**Gate:** ❌ **NOT SATISFIED.** TC-305–309 are written but have never run.
**DoD:** ⚠️ the full state machine (Issued→Consumed/Revoked/Expired) is implemented and
adversarially covered **in writing**, unproven in execution.
**Rollback:** disable the issuance endpoint (API-016); existing `ParentStudentLink` rows are
unaffected — consumption and linking are independent of issuance.

### CT-104 — Teacher CRUD + assignments (API-020–025)

**Status:** DONE (code complete) — **UNVERIFIED: no test execution.** Compiles clean.
**Specs read:** `05-api-contracts/academic-records.md` (Teachers, API-020–025),
`07-business-logic/academic-records.md` (BRL-022–025, E2), `02-requirements-spec/academic-records.md`
(SPEC-FR-018–023), `08-error-catalogue.md` (ERR-303, ERR-304),
`14-test-spec/academic-records.md` (TC-303, TC-304)
**Silence report:** two, both recorded to `_upstream-feedback.md`.
(1) **No `VAL-` rule exists for teacher fields** (U4) — built to `company.md` §3's org
input-validation floor using the same `ContactFormats` helpers, which is the org baseline being
honoured, not a new business rule. (2) **BRL-023 says the second assignment is rejected "until the
first is released", but no release endpoint is specified** — API-024 gives only `{teacherId}`, so a
**null** teacherId is read as the release. Overwriting an occupied class is deliberately *not*
treated as an implicit release, because that is exactly the case ERR-303's first clause names.

**What was built:**
- `Roles` constants (added in CT-102) reused; **this controller is leadership-only in its
  entirety, including the reads** — API-023 says so explicitly ("no BRD grant to Teacher for a
  peer directory"), which is the deliberate contrast with CT-102's student reads and CT-105's
  class reads, both of which do admit Teacher.
- `TeacherService` — register/amend/remove/reinstate/link-account/read, plus the two assignment
  operations.
- **BRL-023 implemented as its two distinct clauses**, matching ERR-303's two-clause wording:
  clause 1, the target class is already occupied by a different teacher; clause 2, this teacher
  already holds another class. Clause 2 is the half TC-303 targets and the half an implementation
  that only inspected the target class row would miss entirely — TC-303's Class B is empty, so
  clause 2 is the only thing that can reject it.
- Re-asserting the *same* teacher on the *same* class is idempotent, not "a second assignment" —
  the requested post-condition already holds, so there is nothing to reject.
- **BRL-025 guard runs before any state change.** TC-304 asserts both halves: ERR-304 *and* the
  teacher left active. A version that deactivated first and reported the conflict afterwards
  would satisfy the status code while orphaning exactly the assignment BR-025 exists to protect,
  so there is an explicit assertion that `IsActive` is still true after the rejection.
- BRL-024's contrast is covered too: a teacher **may** hold several (subject, class) pairs, so
  API-025 has deliberately no uniqueness guard beyond the composite key — a test asserts three
  mappings succeed, which an over-zealous guard modelled on BRL-023 would wrongly reject.
- `HasActiveAssignmentsAsync` checks both holdings (class-teacher and subject mappings).
  `TimetablePeriod` also carries a `TeacherId`, but timetabling is P4 and BR-025 names only
  "class-teacher/subject assignment" — the check is centralised in the repository so P4 has one
  place to extend rather than a condition inlined in the service.

**Tests written (11 test methods; none executed):** TC-303 and TC-304 (both Integration, as specified), the
subject-assignment variant of BRL-025, a positive control proving removal works when nothing is
held (without it, both guard tests would pass against an implementation where removal is simply
broken), release-then-remove, BRL-023 clause 1, idempotent re-assignment, BRL-024's multiple
holdings, the API-023 leadership-only gate, the validation floor, and cross-institution isolation.

**Gate:** NOT SATISFIED. TC-303/TC-304 written, never run.
**DoD:** code-complete against BR-023/025, adversarially covered in writing, unproven.
**Rollback:** none needed; nothing was deployed.

### CT-105 — Class/Subject CRUD + mappings (API-026–030), Teacher-read on Classes (FR-027)

**Status:** DONE (code complete) — **UNVERIFIED: no test execution.** Compiles clean.
**Specs read:** `05-api-contracts/academic-records.md` (Classes/Subjects, API-026–030),
`07-business-logic/academic-records.md` (BRL-026–029), `02-requirements-spec/academic-records.md`
(SPEC-FR-024–029), `08-error-catalogue.md` (ERR-305, ERR-306), `04-data-model.md` (§2.5)
**Silence report:** two, both in `_upstream-feedback.md`. (1) **U1, the significant one:** API-030
(`class-mappings`) and API-025 (`teacher-assignments`) are the same operation — the data model has
exactly one mapping table, composite PK `(subject_id, teacher_id, class_id)`, and SPEC-FR-029
reads "Map subjects to teachers **and** classes". Built as one behaviour behind two routes, with
API-030 delegating to the same service method rather than duplicating logic or inventing a second
table. (2) **U5:** no `ERR-` covers deleting a subject that still has mappings, and the FK is
`Restrict`; the mapping rows are removed with the subject, since they carry no meaning without it.

**What was built:**
- `ClassService` / `ClassRepository` / `ClassesController` / `SubjectsController`.
- **BRL-029 is expressed as the shape of the routing table, not as per-method checks:** every
  read admits Teacher, every write is leadership-only. "Teacher role: read-only, 403 on write"
  is therefore a structural property rather than something each new action has to remember.
- **CT-105's named gate** — "Contract test confirms Teacher role reaches `GET /classes`" — is
  covered, plus the single-class read, plus a `[Theory]` asserting 403 on POST/PUT/DELETE so the
  read grant cannot silently widen into a write grant.
- BRL-026 enforced as the **pair** (name, section), scoped to the institution: tests cover the
  duplicate, the same name in a different section (allowed), and the same pair in another
  institution (allowed). A name-only check would pass the first and fail the second.
- BRL-028 blocks class deletion while students remain placed, and **counts withdrawn students** —
  a withdrawn student still carries `class_id`, and SPEC-FR-014 requires their history stay
  attributable, so deleting the class from under them would break it. Both the populated and the
  withdrawn-only cases are tested, plus a positive control that an empty class deletes.
- **Class is deliberately a hard delete.** It is not one of the five soft-deletable entities
  (`07-business-logic/_shared.md` §2 names Institution/Student/Teacher/Room/UserAccount and Class
  carries no `IsActive`), so API-027's DELETE removes the row — which is precisely why BRL-028's
  guard is what stands between it and orphaned history. Checked against CT-004's static-analysis
  guard: that test matches `.Remove`/`.RemoveRange` on exactly those five DbSet names, so
  `db.Classes.Remove` and `db.Subjects.Remove` are not violations of D8.

**Tests written (12 test methods, 14 cases; none executed).**
**Gate:** NOT SATISFIED by execution. The gate is a contract test confirming Teacher reaches
`GET /classes`; it is written and compiles, but has never run.
**DoD:** BR-026/028/029 implemented, unproven.
**Rollback:** none needed; nothing was deployed.

---

## Phase P1 — phase-conformance pass (no tests run; this asks a different question)

**Question asked:** did the files, endpoints, entities, artifacts, DoD items and ADRs that
`15-task-breakdown.md` §4 promised for P1 actually get built?

**Endpoints — all 20 API- numbers in the P1 range are present.** Enumerated mechanically from the
`[Http*]` attributes across the controllers and mapped against the contract:

| CT | Promised | Present |
|---|---|---|
| CT-101 | API-001–004b, 005 | yes (P1's earlier task) |
| CT-102 | API-011, 012, 013, 014 (3 routes), 015 | yes, plus the `/students/{id}/reactivate` mirror |
| CT-103 | API-016, 017, 018, 019 (2 routes) | yes |
| CT-104 | API-020, 021, 022, 023 (2 routes), 024, 025 | yes, plus `/teachers/{id}/reinstate` and SPEC-FR-023's `link-account` |
| CT-105 | API-026, 027 (2), 028 (2), 029 (3), 030 | yes |

**Entities:** no new entity types were required — CT-001 had already created all 23, including
`ParentLinkCode`, `ParentStudentLink`, `Subject` and `SubjectTeacherClassMapping`, with the
indexes BR-018/BR-023/BR-026 and T-003's filtered-unique-code constraint need. **No migration was
added in P1 after CT-101's, and none was needed.**

**ADRs:** 1 recorded (ADR-0004, CT-103). ADR-0002 required it explicitly.

**CFG- bindings:** CFG-005 (8 chars / 7 days) is bound, as named constants in
`ParentLinkCodeGenerator` and `ParentLinkService` — consistent with CT-002's treatment of CFG-001
in `PasswordPolicy`, not `IConfiguration`.

**Provisional (Q-) handling:** Q-201 and Q-203 are both still open in `00-spec-index.md` §12. §13
authorises building against their stated provisional choices, and neither is hardened: each is a
pair of named constants in one small type, carrying its question id. **No Q-GEN provisional is
hardened outside a feature flag**, so nothing here is a BLOCKED finding under the gated-scope rule.

**Two conformance findings, neither repaired here:**
1. **U7 (Major) — CT-101 has an unreachable error path.** `InstitutionService.RegisterAsync`
   performs no field validation, so `ValidationFailed` is never returned and `ERR-901` cannot
   occur on API-001, despite API-001 listing it and BRL-004/VAL-001–003 declaring the rules.
   Deliberately left for the gate rather than silently fixed inside another CT.
2. **CT-101's four source files had never been format-checked** (CT-101 closed no phase gate).
   `dotnet format` found real violations in them as well as in CT-102–105's files. All fixed —
   whitespace only, no behavioural change.

## Phase P1 — verification-gates(PHASE): what actually ran, and what did not

Reported as-is rather than summarised as a pass.

| Gate | Result |
|---|---|
| Compile (all 5 projects) | **PASS** — 0 errors, 0 warnings. Obtained on a throwaway copy retargeted to `net9.0` with packages at 9.0.0, because the repo's `net10.0` target cannot build with the only installed SDK (9.0.317). Every CT-102–105 file is included. |
| `dotnet format --verify-no-changes` | **PASS** — clean, after one fix pass that corrected real violations in CT-101's files and in CT-102–105's. |
| xUnit analyzers | **PASS** — one xUnit2012 warning was raised and fixed; the build is warning-free. |
| **Full test suite** | **DID NOT RUN.** The VSTest host fails to load — `FileLoadException: ... testhost.dll. Access is denied.` — with full-control ACLs on the file and with the sandbox disabled, so this is an application-control/AV restriction on this machine, *separate* from the SDK problem. **69 test methods across P1 (about 90 cases once `[Theory]` rows expand) have therefore never executed, including every TC- this phase is gated on (TC-301 through TC-309).** |
| Dependency scan | did not run (`dotnet list package --vulnerable` needs a successful restore against the real target) |
| Migration-applies-from-zero | did not run (no SDK, no LocalDB attempt); no migration was added in P1 |

**Phase P1 is NOT eligible for Gate 3.** Every CT is code-complete and type-correct; not one
assertion has been executed. The operator directed this explicitly (see the build environment note
above) with that consequence stated and accepted. What unblocks it is a .NET 10 SDK plus a working
test host — then `dotnet test` decides whether CT-102–105 actually behave as written, and the 69 test
methods become evidence instead of intent.

## Phase P1 — verification-gates(PHASE), re-run on the real target (2026-09-14)

No new code was written in this pass; it only asks whether CT-101–105 behave as written.
Environment: `.NET SDK 10.0.303` now installed (alongside 9.0.314). No retarget, no test-stack change.

| Gate | Result |
|---|---|
| Compile (`dotnet build SchoolSync.slnx`, net10.0, in-repo) | **PASS** — 0 errors. 4 `NU1903` warnings, all the same `Microsoft.OpenApi` 2.0.0 advisory (see dependency scan). |
| **Full test suite** (`dotnet test`) | **PASS — 122 executed, 122 passed, 0 failed, 0 skipped, 14 s.** Machine report: `test-results/TestResults-P1-phase-gate.trx`. The VSTest adapter-load failure reproduced on 2026-09-14 **did not recur** — the in-repo xunit 2.9.3 stack ran under VSTest unchanged. Every P1 test class appears in the TRX: StudentFieldPolicy (26), StudentsEndpoints (10), StudentService (8), ParentLinkService (14), ParentLinkEndpoints (7), TeachersEndpoints (11), ClassesEndpoints (14), InstitutionsEndpoints (7), plus all P0 classes. |
| `dotnet format --verify-no-changes` | **PASS** — exit 0, no changes. |
| Dependency scan (`dotnet list package --vulnerable --include-transitive`) | **FINDING — 1 High.** `Microsoft.OpenApi` 2.0.0 (GHSA-v5pm-xwqc-g5wc), transitive via `Microsoft.AspNetCore.OpenApi` 10.0.3, in `SchoolSync.Api` and `SchoolSync.IntegrationTests`. **This corrects P0's "clean" record**, which ran without `--include-transitive`; the NU1903 warning was already present in P0. Not fixed here — a package bump is outside a verification pass and is left for the operator. |
| Migration-applies-from-zero | **PASS** — against a fresh LocalDB database: `database update` applied cleanly, `database update 0` reverted cleanly, database dropped. `migrations has-pending-model-changes`: none. |
| `verify-gate.sh --root .` | **1 FAIL (C5), 1 WARN (C8)** — both examined, see below. Not reported as passed. |

**Per-CT gates now satisfied by execution:** CT-102 (TC-301/302), CT-103 (TC-305–309), CT-104
(TC-303/304), CT-105 (Teacher reaches `GET /classes`). CT-101's 32/32 claim now has a machine
report behind it. CT-102–105 status: DONE (verified).

**verify-gate.sh findings, investigated rather than silenced:**
- **C5 FAIL** — the script takes the *first* `TestResults*.trx` it finds (`head -n1`), which is
  P0's 2026-09-11 report, and compares that to the newest source. The P1 report (14:46:26) is newer
  than every `.cs` file in the tree, including `obj/` outputs (newest 14:45:46). The check fails on
  the wrong file; P0's report was left in place as P0's evidence rather than deleted to make the
  script pass.
- **C8 WARN** — current top-level spec hash `9ab858a8549a`; the build log records `be02ee939542`.
  Recomputed with `_upstream-feedback.md` excluded, the hash is exactly `be02ee939542`. **No spec
  content changed**; the difference is the feedback file code-generation itself created during P1,
  which the script's `cat *.md` includes. No CT is STALE.

**Observation, not a defect:** the migrations folder holds a single `20260911152338_InitialCreate`
that already contains `IdempotencyRecords` and `Room.IsActive/DeactivatedAtUtc`, whereas the CT-004
entry describes a separate Room migration. The migrations were evidently consolidated at some point
without a log entry. Model and snapshot agree, so nothing is broken — recorded so a reviewer is not
surprised.

**Still outstanding before Gate 3-P1 (not this command's to close):** upstream feedback U1–U7 (two
Major: U1, U7), owned by Jayesh Idnani and unresolved; the High transitive vulnerability above; the
C5/C8 script results need an owned waiver or a script fix.

## Phase P1 — pre-gate resolution pass (2026-09-14)

Decisions by **Rashmi Mehta**, 2026-09-14: U1 keep both routes as alias · U2–U6 accept as built,
correct the spec · U7 fix now in P1 · vulnerability bump and re-verify · verify-gate.sh C5/C8 to
be carried as a named waiver, not gamed.

### CT-101 (reopened for U7) — API-001 field validation

**Specs read:** `07-business-logic/institutions.md` (BRL-004, VAL-001–004), `05-api-contracts/institutions.md`
(API-001), `04-data-model.md` §2.1, `10-process-flows/institutions.md` (PF-001 step 2).
**Built:** `SchoolSync.Application/Institutions/InstitutionFieldPolicy.cs` — ENT-001 required
fields, VAL-001 via `ContactFormats.IsValidEmail`, VAL-003 via `ContactFormats.IsValidPhone`
(provisional Q-201, unchanged), VAL-004 exactly 6 ASCII digits. Called first in
`InstitutionService.RegisterAsync`, before the BRL-003 duplicate check, returning
`ValidationFailed` → the controller's existing `ERR-901` mapping, which is now reachable.
**Tests:** +10 cases in `InstitutionsEndpointsTests` — 8 one-field-invalid rows each asserting
`400 ERR-901` and that nothing reached the directory, plus 2 boundary positive controls (7-digit
phone, separators, `000000`) so a blanket-reject implementation cannot pass.
**Scope held:** API-002 (update) was **not** given validation — its contract allows partial fields
and lists no `ERR-901`, which conflicts with BRL-004. Raised as U9 rather than decided here.

### Dependency fix

`Microsoft.AspNetCore.OpenApi` 10.0.3 → **10.0.12** (aligns with the other ASP.NET Core 10.0.12
packages), which resolves `Microsoft.OpenApi` 2.0.0 → **2.12.0**. `dotnet list package --vulnerable
--include-transitive`: **no vulnerable packages in any of the 5 projects.** The 4 `NU1903` build
warnings are gone.

### Spec suite 1.0.3 (ai-spec agent, narrow patch)

U1–U6 applied as text corrections to `05-api-contracts/academic-records.md` (API-012, 018, 025,
029, 030), `02-requirements-spec/academic-records.md` (SPEC-FR-017), `07-business-logic/academic-records.md`
(VAL-011 widened to teacher contact fields; subject-delete cascade; Parent Link decision-table
row 5) and `00-spec-index.md` (version bump). No new IDs. Spot-checked by the orchestrator.
**Staleness:** every changed ID describes behaviour already built and tested in CT-102–105; no
code change was required. The re-run below post-dates the spec edits (TRX 15:01:45 vs last spec
edit 15:00:31), so it is evidence against 1.0.3.
**Suite hash, 1.0.3 (top-level `*.md` excluding `_upstream-feedback.md`):** `de275235b49a`.

### verification-gates(PHASE), re-run

| Gate | Result |
|---|---|
| Build | **PASS** — 0 errors, **0 warnings** |
| Tests | **PASS — 132 executed / 132 passed / 0 failed** (122 + 10 new). `test-results/TestResults-P1-phase-gate.trx` regenerated |
| Format | **PASS** |
| Dependency scan (transitive) | **PASS** — clean |
| Migration-from-zero | not re-run: no entity, configuration or migration changed in this pass; last PASS earlier 2026-09-14 |
| `verify-gate.sh` | **1 FAIL (C5), 1 WARN (C8)** — same script causes as recorded above (C5 reads P0's TRX; C8 hashes `_upstream-feedback.md`, which changes whenever feedback is recorded). To be carried as a named waiver at Gate 3-P1. |

**New findings raised (in `_upstream-feedback.md`):** U8 **Major** — security-spec N1 expects a
`403 ERR-102` body for a Teacher write while API-012 (1.0.3) and the code give a bodyless 403, and
no test asserts either; U9 Minor — BRL-004 vs API-002 update validation; U10 Minor — API-059 cites
PF-008 for rollover (should be PF-012).

**Gate 3 (Phase P1) approved by Rashmi Mehta, 2026-09-14.** Recorded with it: a named waiver
(owner Rashmi Mehta) for `verify-gate.sh` C5 FAIL and C8 WARN — script artifacts investigated
above, not passes. U8 (Major), U9, U10 carried forward **open**, filed against Rashmi Mehta as
owner because none was assigned at approval.

## Phase P2 — Attendance

**Started 2026-09-14** at Rashmi Mehta's instruction, spec suite **1.0.3**, with U8 still open.

**Pre-build decisions** (halt-and-ask, all by **Rashmi Mehta**, 2026-09-14; to be recorded as Q-213–Q-217):
- **Q-213** CorrectionRequest storage: a **new table** (the data model had only `correction_status`). Built in CT-202.
- **Q-214** Day 30: **still correctable**. The lock applies at `> 30` days (CFG-004, TC-408); TC-410's "reaches day 30" wording needs correcting to day 31.
- **Q-215** Time zone: `InstitutionConfiguration.TimeZone` is an **IANA id**. With no configuration row, or an unresolvable id, fall back to **`Asia/Kolkata`**, proposed as **CFG-009** (not yet in `12-infrastructure-spec.md`).
- **Q-216** Lock sweep: **logic now, schedule in P5**. `RunLockSweepAsync` plus a lock re-check on approve/reject in CT-202; the daily Hangfire registration is CT-501.
- **Q-217** (raised during CT-201): API-031 and API-014 both claimed `GET /classes/{id}/roster`, and the host rejected the route as ambiguous at runtime. API-031 **moved to `GET /api/v1/attendance/roster?classId=&date=`**; API-014 unchanged.

### CT-201 — Roster fetch + bulk upsert (API-031/032), NotYetMarked default

**Status:** DONE — verified by execution.
**Specs read:** `02-requirements-spec/attendance.md`, `05-api-contracts/attendance.md`,
`07-business-logic/attendance.md`, `04-data-model.md` §2.6, `10-process-flows/attendance.md` (PF-006),
`08-error-catalogue.md` (ERR-401–403), `14-test-spec/attendance.md` (TC-401–404).
**Silence report:** the four blocking gaps above were asked, not assumed. Resolved from the specs
without asking:
- **Teacher "own class"** = class teacher OR any subject mapping for the class (PF-006 pre-condition, verbatim).
- **Past-date bulk write by a Teacher** → `ERR-402`, per the catalogue's own trigger text. Leadership applies directly on days 1–30 (Q-205).
- **Same-day overwrite of another user's outcome** by a Teacher → 403 `ERR-102`. SPEC-FR-033 names the *original recording teacher*.
- **`RecordedByUserId`** keeps the original recorder; the corrector is carried by the audit row (BRL-035).
- **Staff entries** need no `classId`; student entries require one and must be on that class's active roster (VAL-015).

Invented, needs spec confirmation (to feedback): the in-service scope denials return a 403 **with**
an `ERR-102` body, following InstitutionsController's BRL-002 precedent. That is the opposite
choice from the bodyless framework 403 on role gates, so it adds to U8.
**Built:** `Application/Attendance/{AttendanceCalendar, AttendanceContracts, AttendanceService}.cs`,
`Infrastructure/Attendance/AttendanceRepository.cs`, `Api/Controllers/AttendanceController.cs`,
`ErrCatalogue.Err401–403` (catalogue wording verbatim), DI registration. No schema change. One
`SaveChanges` per request, so a roster is one transaction and the CT-003 interceptor writes one
audit row per changed record.
**Tests (30 cases, all run):** TC-401 (30-student roster, raw payload contains no "Absent"), TC-402
(30 rows plus 30 audit rows attributed to the teacher), TC-403 at the service layer over the real
repository (same record id, one stored row), TC-404. Also covered:
- the local-midnight case: 00:30 IST while UTC is still yesterday; a UTC implementation returns `ERR-403`
- teacher past-day write → `ERR-402`; leadership at day 1 and day 30 → 200; day 31 → `ERR-401`
- BRL-033 staff attendance, with a positive control
- an unassigned teacher (roster and mark) and a subject-mapped teacher
- original-recorder overwrite, denied for someone else's outcome and allowed for their own
- 4 × ERR-901 cases with nothing written
- a cross-institution class → 404, and the Student/Parent role gates
- time-zone fallback and CFG-004 boundary arithmetic

**Test-fixture issues (not product code):** (1) `DbSet.FindAsync` in a seed helper is hidden by
the tenant filter outside a request; switched to `IgnoreQueryFilters()`. (2) The route ambiguity
above surfaced only at runtime, as `AmbiguousMatchException`; it did not show at compile time.
**Gate:** ✅ TC-401–404 green; full suite **162/162**.
**DoD:** ✅ upsert proven non-duplicating (D10); default proven NotYetMarked, not Absent.
**Rollback:** feature-flag `PUT /attendance`; the read path is unaffected.

### CT-202 — Correction/approval state machine (API-033/034), 30-day lock

**Status:** DONE — verified by execution.
**Specs read:** `07-business-logic/attendance.md` (state machine, guards, decision table, E1–E3),
`10-process-flows/attendance.md` (PF-007), `05-api-contracts/attendance.md` (API-033/034),
`04-data-model.md` §2.6, `08-error-catalogue.md` (ERR-401/402), `14-test-spec/attendance.md` (TC-405–412),
`12-infrastructure-spec.md` (CFG-004).
**Silence report:** Q-213/214/216 answered before the build (above). Resolved without asking, each
to go to feedback for confirmation:
- **E3 and "approve an already-resolved request" have no `ERR-`.** They fall back to `ERR-901`, per the catalogue's own rule, rather than minting a code.
- **A leadership direct write while a Teacher's request is pending** closes that request as **`Superseded`**. Otherwise a later approval would overwrite leadership's newer outcome with a stale one. Covered by a test.
- **API-033 on day 0:** the original recorder, or leadership, gets `AppliedDirectly`, per the decision table's day-0 rows. A non-recorder Teacher gets 403.
- **Reject requires a reason** (PF-007 step 4); API-033's `reason` is required.

**Built:**
- `Domain/Entities/CorrectionRequest.cs`, a new table per Q-213. Statuses: Pending, Approved, Rejected, AutoRejected, AppliedDirectly, Superseded. It is `IAuditable`, so every transition is audited (BRL-035).
- Migration **`20260914095538_AddCorrectionRequest`**: new table only. Cascade from the record, Restrict from Institution (avoids a second cascade path), and a filtered unique index on `AttendanceRecordId WHERE Status='Pending'` (E3 enforced in the database as well).
- `AttendanceService.RequestCorrectionAsync / ApproveCorrectionAsync / RejectCorrectionAsync / RunLockSweepAsync`.
- API-033/034 endpoints.
- **ADR-0005**, covering the sweep's cross-tenant read.
- Q-212's provisional "both" is implemented: approve/reject re-check the lock at the moment of action and auto-reject a request found past it.

**Tests (21 cases, all run):** TC-405 (day-0 self-correction, both via bulk and via API-033),
TC-406, TC-407 (HTTP), TC-408 (Teacher and Principal, no request row created), TC-409 (days 1, 29
and 30), TC-410 (the sweep leaves a request alone at day 30 and auto-rejects at day 31, record
outcome unchanged), TC-411, TC-412 (HTTP). Also covered:
- the sweep reaching another institution's request
- approving after the lock without a sweep → `ERR-401`, and the request auto-rejected
- reject needs a reason and leaves the record unchanged
- a resolved request can't be approved
- supersede
- a teacher can't correct staff records or approve
- the 423 wire format
- a cross-institution record → 404

**Mutation check (run, then reverted):** changing `IsLocked` from `> 30` to `>= 30` failed **5
tests** across the unit, state-machine and HTTP layers. The boundary is genuinely guarded.
Original restored, rebuilt, and suite re-run green.
**Gate:** ✅ TC-405–412 green; full suite **183/183**.
**DoD:** ✅ day 0, 29, 30 and 31 boundaries tested. ⚠️ **"daily lock-sweep job (CT-501)
integrated": partially.** The sweep logic is built and tested; the daily schedule is CT-501 (P5)
per Q-216. That is a recorded split, not a silent one.
**Rollback:** disable API-033/034 and revert to direct-edit-only; drop the migration if the table
must go. Record any such rollback as a deviation.

### CT-203 — Attendance removal (API-035), retrieval (API-036)

**Status:** DONE — verified by execution.
**Specs read:** `05-api-contracts/attendance.md` (API-035/036), `02-requirements-spec/attendance.md`
(SPEC-FR-036/037), `07-business-logic/attendance.md` (Locked is terminal), `05-api-contracts/academic-records.md`
(API-019 pattern for `/me` and `/linked`).
**Silence report:** resolved from the specs, to feedback for confirmation:
- **A locked entry can't be removed** (`ERR-401`). "Locked (terminal — no further mutation by anyone)" covers removal.
- **A Teacher calling API-036 must name a class they teach.** "Own class" leaves no institution-wide grant, and staff records never appear to a Teacher.
- **`classId` filters on the student's *current* placement.** The record has no class column, and §2.6's index is `class_id_via_student`.
- **No response shape is specified.** Built as `{id, personId, personType, attendanceDate, outcome, recordedByUserId, recordedAtUtc, correctionStatus, locked}`, with `locked` derived at read time.

**Built:** `RemoveAsync`, `SearchAsync`, `ForOwnStudentAsync`, `ForLinkedStudentsAsync`, plus
repository methods and four endpoints (`DELETE /attendance/{id}`, `GET /attendance`, `GET /attendance/me`,
`GET /attendance/linked`). No schema change.
**Real defect found by running the tests, and fixed:** the first version left the correction
requests to the FK **cascade**. The test showed they survived on the InMemory provider. More
importantly, on SQL Server a database-side cascade deletes them where the CT-003 interceptor never
sees them, so the request history would vanish **with no audit row**, the same bypass class CT-003
guards against. `RemoveRecordAsync` now loads and removes the requests explicitly; each deletion is
audited, and a test asserts the `CorrectionRequest` Delete audit row. The cascade stays in the
schema as a backstop.
**Finding, not fixed (P0 code, outside CT-203):** `AuditLoggingInterceptor.Serialize` writes enum
properties as **ordinals** (`"Outcome":1`), not names. Reordering or inserting an enum member
would silently change what historical audit rows mean. To feedback.
**Tests (11 cases, all run):**
- removal forbidden for Teacher (even the recorder), Student and Parent, with the row still present
- leadership removal → 204: row gone, requests gone, both deletions audited with actor and before-image
- a locked entry → 423 `ERR-401`; an unknown id → 404
- leadership institutional list plus person, class, date and other-day filters
- Teacher own-class only: no staff rows, no class omitted, no other class, and a personId from another class returns nothing
- Student `/me`, Parent `/linked` (and an unlinked parent), and a cross-institution list and delete

**Gate:** ✅ contract tests green; full suite **194/194**.
**DoD:** ✅ leadership-only removal enforced (adversarial, 3 roles).
**Rollback:** none needed.

## Phase P2 — phase-conformance (no tests run)

| Promised by 15-task-breakdown.md §4 | Present |
|---|---|
| CT-201 API-031, API-032 | yes. API-031 at `GET /attendance/roster` (Q-217) |
| CT-202 API-033, API-034 (approve + reject) | yes |
| CT-203 API-035, API-036 (+ `/me`, `/linked` per the contract) | yes |
| TC-401 – TC-412 | each referenced by at least one test in `tests/.../Attendance/` |
| Entities | `CorrectionRequest` added (Q-213), migration `20260914095538_AddCorrectionRequest` |
| ADRs | ADR-0005 (lock sweep's cross-tenant read) |
| CFG bindings | CFG-004 (30, fixed constant) bound in `AttendanceCalendar`; proposed CFG-009 (`Asia/Kolkata`) bound the same way |
| Gated scope | no Q-GEN provisional hardened outside a flag. Q-212's provisional "both" is implemented as stated |

**Partial DoD, recorded:** CT-202's "daily lock-sweep job (CT-501) integrated". The logic is built
and tested; the schedule is CT-501 (Q-216).
**Spec drift, recorded:** the five decisions above are built but not yet written back into the
suite (U11).

## Phase P2 — verification-gates(PHASE)

| Gate | Result |
|---|---|
| Build | **PASS**: 0 errors, 0 warnings |
| Tests | **PASS: 194 executed / 194 passed / 0 failed** (P1 closed at 132, +62 in P2). Report: `test-results/TestResults-P2-phase-gate.trx` (15:36:44, newer than every source file) |
| Mutation spot-check | `IsLocked` `>30`→`>=30` failed 5 tests; reverted and re-verified |
| Format | **FAILED first**: real whitespace violations in the new P2 test files. Fixed with `dotnet format`, then build and all 194 tests re-run. **PASS** |
| Dependency scan (transitive) | **PASS**: no vulnerable packages |
| Migration-from-zero | **PASS** on a fresh LocalDB: both migrations applied, filtered index `IX_CorrectionRequests_AttendanceRecordId` present with `([Status]='Pending')`, both reverted, database dropped (0 left over) |
| `verify-gate.sh` | **1 FAIL (C5), 1 WARN (C8)**, the same script causes as waived at Gate 3-P1. C5 reads P0's TRX. C8's hash includes `_upstream-feedback.md`; excluding it reproduces `de275235b49a`, P1's recorded spec hash, so **no spec content changed during P2**. Not reported as passed. |

**Phase P2 is ready to present at Gate 3.** This command does not approve it.

**Gate 3 (Phase P2) approved by Rashmi Mehta, 2026-09-14.** Recorded with it: a named waiver
(owner Rashmi Mehta) for `verify-gate.sh` C5 FAIL and C8 WARN (script artifacts, investigated
above, not passes). U11 (Major), U12, U13 and U14 are filed against Rashmi Mehta as owner, because
none was assigned at approval. They are carried forward **open**, with U8–U10.

## Phase P3 — Communication

**Started 2026-09-14** at Rashmi Mehta's instruction, spec suite **1.0.3**, with U11 (the spec
has not caught up with P2) and U8 open.

**Pre-build decisions** (halt-and-ask, **Rashmi Mehta**, 2026-09-14):
- **Q-218** Meeting UI: **API now, UI in CT-603.** CT-304 builds API-047 and TC-507. The UI states and the browser tests TC-508/509 move to CT-603 (P6); no frontend exists yet.
- **Q-219** Room membership: a **`RoomMembership` table**. It is created by a correct-passcode join and removed only by an explicit leave or room removal. A disconnect only changes presence to offline, so auto-reconnect needs no passcode (NFR-012).
- **Q-220** Meetings: `Meeting` becomes **many per room** (the schema had it one-to-one). API-047 reuses the room's live meeting or starts one and returns `groupCallId = Meeting.Id`. The meeting ends on room removal or when the last member leaves. A new ACS identity is created per token request, with none persisted.
- **Q-221** Azure dependencies: **config-gated.** SignalR runs in-process, with Azure SignalR added only when `AZURE_SIGNALR_CONNECTION_STRING` is set. ACS token issuance sits behind an interface: the real client when `ACS_CONNECTION_STRING` is set, otherwise `503 ERR-903`. Tests use fakes and a real SignalR client against the test host.

**Task-breakdown gap, not asked (placement only):** API-046 (message revise/withdraw) is in no CT's
scope, but its gate test TC-510 is CT-303's. It is built in CT-303.

### CT-301 — Announcement CRUD + withdraw narrowing (API-037–040)

**Status:** DONE — verified by execution.
**Specs read:** `05-api-contracts/communication.md`, `07-business-logic/communication.md`
(BRL-036–039, VAL-016/017, E1), `02-requirements-spec/communication.md`, `04-data-model.md` §2.7,
`08-error-catalogue.md` (ERR-501), `14-test-spec/communication.md` (TC-505).
**Silence report:** resolved from the specs, to feedback:
- **§2.7 has no creation-date column, but BRL-039 names `CreatedAtUtc` as immutable.** Added it (migration `20260914111207_AddAnnouncementCreatedAt`).
- **"Current" announcements (FR-042)** = not withdrawn. The list is not filtered by effective date, because VAL-017 permits future dates and no rule hides them.
- **A withdrawn announcement returns 404 on the detail route too.**
- **Withdrawing twice** is a no-op.
- **The withdraw permission check runs before the existence check**, so a Teacher always gets `ERR-501`.
- **Amending a withdrawn announcement is not restricted**, because no rule restricts it.

**Built:** `AnnouncementService`, `AnnouncementRepository` and `AnnouncementsController`. Also a
shared `IIdempotencyStore` over CT-101's `IdempotencyRecord` table: keys are namespaced by endpoint
and institution, and an expired key's row is reused instead of colliding on the primary key.
`CallerClaims`, `ErrCatalogue.Err501–505/903`, DI registration. Withdraw uses `[Authorize]` only, so
BRL-038's denial carries the ERR-501 body TC-505 requires.
**Tests (13 cases, all run):** TC-505 (the Teacher's own announcement; still listed afterwards).
Also covered:
- leadership withdraw: removed from the list, detail returns 404
- BRL-039: a principal's amendment keeps the teacher as author and keeps the creation date
- Student/Parent can read but not post or amend
- another institution: list, detail and withdraw all 404
- 4 × VAL cases
- the 200/201-character title boundary
- a future effective date is accepted
- an Idempotency-Key replay creates one row, and the same key from another institution does not replay

**Gate:** ✅ TC-505 green; full suite **207/207**.
**DoD:** ✅ BR-038 narrowing adversarially tested.

### CT-302 — Room CRUD + join (API-041–045), passcode hashing

**Status:** DONE — verified by execution.
**Specs read:** `05-api-contracts/communication.md` (Rooms), `07-business-logic/communication.md`
(BRL-040–042, room-membership state machine, VAL-018/019, E2/E3), `04-data-model.md` §2.8,
`08-error-catalogue.md` (ERR-502/503), `14-test-spec/communication.md` (TC-501/502), `07-business-logic/_shared.md` §2 (D8).
**Silence report:** Q-219 was answered before the build. Resolved without asking, to feedback:
- **The creator is the room's first member.** They set the passcode, so a separate join would verify nothing.
- **API-043 returns `{roomId, hubPath}`.** The contract's "session token for the SignalR hub group" is superseded by Q-219's database membership.
- **Joining re-verifies the passcode even for an existing member.**
- **API-044 for a non-member** returns 403 `ERR-102`. `ERR-505`'s wording is meeting-specific, and the contract names no error.
- **API-045 soft-deletes the room** (D8 lists Room). Its memberships are removed, message history stays (BR-043), and a removed room returns 404 for join and history.
- **Security observation:** the contracts specify no rate limit on API-043, so a passcode can be brute-forced. `ERR-904` rate limiting is scoped to auth endpoints only. To feedback.

**Built:** `Domain/Entities/RoomMembership.cs` (tenant-owned, PK `(RoomId, UserId)`),
`RoomService` / `RoomRepository` / `RoomsController`, Idempotency-Key on `POST /rooms`.
Passcodes are hashed with the existing BCrypt `IPasswordHasher`. `RoomResult` has no passcode
member, so the list and create responses cannot carry one. Migration **`20260914111747_AddRoomMembership`**.
**Tests (14 cases, all run):** TC-501, adversarial: neither the create 201 nor the list contains
`passcode` as a field name, the plaintext, or the stored hash. TC-502, adversarial: an off-by-one
passcode gets ERR-502 with no secret echoed back, no membership row, and 403 on history. Also covered:
- the positive join opens history; a withdrawn message is blanked with author and position kept; a re-join doesn't duplicate membership
- the hash is BCrypt-verifiable and is not the plaintext
- the creator reads history without joining
- BRL-042: a non-creator Teacher gets ERR-503; creator and leadership both remove → soft-deleted, memberships gone, former member gets 404
- cross-institution: list, join and delete
- anonymous → 401
- 4 × VAL cases

**Gate:** ✅ TC-501/502 green; full suite **221/221**.
**DoD:** ✅ passcode never in any response after creation, and not in the creation response either.

### CT-303 — SignalR hub (`/hubs/collaboration`): message send, presence, typing, join/leave (+ API-046)

**Status:** DONE — verified by execution.
**Specs read:** `06-event-contracts.md` §1, `07-business-logic/communication.md` (BRL-043–045, membership
state machine, VAL-020, E3), `05-api-contracts/communication.md` (API-046, real-time, idempotency),
`12-infrastructure-spec.md` (CFG-003, CFG-SEC-03, INF-003), `14-test-spec/communication.md` (TC-503/504/506/510).
**Silence report:** Q-219/221 answered before the build. Resolved without asking, to feedback:
- **API-046 was in no CT's scope**; built here next to its gate test TC-510.
- **EVT-004 "joined"** fires when membership is created (REST API-043, through `IHubContext`). **"left"** fires on an explicit `LeaveRoom`. **EVT-002 online/offline** follows each user's connection count per room. Under Q-219, a reconnect is not a re-join.
- **EVT-006 `HistoryResync`** is sent to the caller on every `JoinRoom`, with `messages: []`. The contract says history is re-fetched over REST, never replayed.
- **`MessageSent` also carries `roomId` and `clientMessageId`**, so the sender can reconcile its optimistic message. That is additive to EVT-001's payload.
- **§2.8 has no withdraw or revise timestamps.** Added `WithdrawnAtUtc` (named in BRL-044), `RevisedAtUtc` (so a reader can see edited text, NFR-030) and `ClientMessageId` with a unique filtered index per `(room, author)` (EVT-001 idempotency).
- **Revising a withdrawn message** returns `ERR-901`.
- **No hub event is defined for revise or withdraw**, so other clients see those changes on their next history fetch. None was invented. To feedback.
- **Hub refusals** are `HubException` carrying the ERR- code: ERR-102 for not a member or not joined, ERR-504 for length.
- **The JWT is accepted as `?access_token=` only for `/hubs/*`.** A test proves a REST call with the query token gets 401.

**Known limitation, recorded:** presence counts are per app instance, so with Azure SignalR scale-out
a user with tabs on two instances can flicker "offline". The specs name no shared presence store.
**Built:** `Api/Hubs/CollaborationHub.cs` (`JoinRoom`, `SendMessage`, `Typing`, `SetMediaState`,
`LeaveRoom`, `OnDisconnectedAsync`), `RoomPresenceTracker`, `HubRoomEventPublisher`,
`MessagingService` / `MessagingRepository`, API-046 on `RoomsController`, `AddSignalR` plus
`AddAzureSignalR` only when `AZURE_SIGNALR_CONNECTION_STRING` is set (Q-221), the JwtBearer
`OnMessageReceived` hub-token hook. Packages: `Microsoft.Azure.SignalR` 1.33.1 (Api) and
`Microsoft.AspNetCore.SignalR.Client` 10.0.12 (tests); the transitive dependency scan is still
clean. Migration **`20260914113210_AddMessageRevisionAndClientId`**.
**Tests (19 cases, all run):**
- **Unit layer:** TC-504 (4,001 rejected and not stored, 4,000 accepted); TC-510 (the row and author are kept, content is stored but rendered blank); author-only revise/withdraw; a withdrawn message can't be revised; `clientMessageId` de-duplication per author; no sending after leaving; empty content rejected.
- **Hub layer:** real SignalR clients over **WebSockets** through the test server, token on the URL as a browser sends it.
  - TC-503: delivered under 1 s, measured and asserted.
  - TC-506: a message sent while disconnected shows up in history after `HistoryResync` on reconnect, with no passcode.
  - A non-member can't join the group and receives nothing; another institution is refused.
  - `JoinRoom` is required before sending; ERR-504 over the hub.
  - A retried send is acknowledged as a duplicate and delivered once.
  - Presence, typing, joined and left events all arrive; after leaving, the passcode is needed again.
  - A disconnect shows offline with no "left".
  - After room removal, sending is refused on the live connection.
  - API-046 over REST: author-only, ERR-504, and a withdraw that is blanked in history.
  - The query-string token is rejected for REST.

**Mutation checks (each mutant built, then was reverted):** withdrawn content not blanked on read →
**3 tests failed** (unit, REST, hub). Per-send membership re-check removed → **1 test failed**; the
hub's own DB check keeps hub-level tests green, by design. Restored, rebuilt, **240/240**.
**Gate:** ✅ TC-503, TC-510 green (plus TC-504, TC-506); full suite **240/240**.
**DoD:** ✅ EVT-001 delivery measured under 1 s against the in-process transport. **Caveat:** this
proves the application path, not NFR-008 under production load through Azure SignalR. That belongs
to CT-701's NFR performance checks.
**Rollback:** disable the hub. No REST-only fallback was built this release, per the task
breakdown: a real regression if exercised.

### CT-304 — ACS Calling token issuance (API-047); meeting UI deferred to CT-603

**Status:** DONE (API half) — verified by execution. **UI half moved to CT-603 per Q-218.**
**Specs read:** `05-api-contracts/communication.md` (API-047), `10-process-flows/communication.md` (PF-009),
`07-business-logic/communication.md` (BRL-046), `04-data-model.md` §2.8 (Meeting),
`12-infrastructure-spec.md` (CFG-SEC-04), `final-tech-design.md` §6/§13.1, `14-test-spec/communication.md` (TC-507).
**Silence report:** Q-218/220/221 were answered before the build. Resolved without asking, to feedback:
- **The response adds `groupCallId`** to the contract's `{acsToken, expiresAtUtc}`. Members need a shared call id; this follows from Q-220.
- **The membership check runs before any ACS call**, so a non-member causes no identity or token to be minted and no meeting to start.
- **An unconfigured ACS** returns 503 `ERR-903` and starts no meeting.
- **Two members requesting at the same instant** both create a meeting. The filtered unique index lets one insert win, and the loser discards its insert and joins the winner. Only SQL Server enforces that index, so this path is **not exercised by the InMemory test provider**. Recorded as untested.
- **Token lifetime** is the ACS SDK default. The specs say "short-lived" with no number.

**Built:** `MeetingService`; `MeetingRepository`; `AcsMeetingTokenIssuer`, which uses
`Azure.Communication.Identity` 1.3.1 with `CreateUserAndTokenAsync` and VoIP scope, registered
lazily when `ACS_CONNECTION_STRING` is set, otherwise `UnconfiguredMeetingTokenIssuer`; API-047 on
`RoomsController`. `Meeting` is now many-per-room, with a unique index on `RoomId WHERE EndedAtUtc IS NULL`.
The live meeting ends on room removal (`RoomService`) and when the last member leaves
(`MessagingService.LeaveAsync`). Migration **`20260914113858_MeetingManyPerRoom`**. The transitive
dependency scan is still clean.
**Tests (6 cases, all run):**
- TC-507, adversarial: a leadership non-member gets 403 `ERR-505`, no `acsToken` in the body, the issuer called 0 times, and no Meeting row
- two members get distinct tokens with the same `groupCallId`, matching the one live Meeting row
- no ACS configured → 503 `ERR-903`, no meeting
- room removal ends the meeting, then the token request → 404
- another institution → 404, issuer never called
- service layer: the meeting survives one leave and ends on the last leave

**Mutation check:** removing the membership check made TC-507 fail. Reverted and rebuilt.
**Gate:** ✅ TC-507 green. ⚠️ **TC-508/509 not built here**: they are browser tests of the meeting UI and move to CT-603 (Q-218).
**DoD:** ⚠️ **"Connecting/reconnecting/error states built per UI-014"** also moves to CT-603 (Q-218). A recorded split, not a silent one.
**Rollback:** feature-flag meetings off; chat is unaffected. (No flag exists yet: API-047 already returns 503 when ACS is unconfigured, which acts as the off switch.)

## Phase P3 — phase-conformance (no tests run)

| Promised by 15-task-breakdown.md §4 | Present |
|---|---|
| CT-301 API-037–040 | yes: POST, PUT, withdraw, list, detail |
| CT-302 API-041–045 | yes: create, list, join, history, remove |
| CT-303 hub: message send, presence, typing, join/leave | yes: `JoinRoom`, `SendMessage`, `Typing`, `LeaveRoom`, disconnect handling, `SetMediaState` (EVT-005). **Plus API-046** (in no CT's scope; built here) |
| CT-304 API-047 + meeting UI wiring | API-047 yes. **Meeting UI → CT-603 (Q-218)** |
| TC-501–510 | TC-501–507 and TC-510 have tests. **TC-508/509 → CT-603 (Q-218)** |
| Entities / migrations | `RoomMembership`; `Announcement.CreatedAtUtc`; Message revision/withdrawal/client-id; `Meeting` many-per-room. 4 migrations: `…111207`, `…111747`, `…113210`, `…113858` |
| CFG bindings | CFG-003 (4,000) bound in `MessagingService`; CFG-SEC-03/04 read lazily (Q-221) |
| Packages | `Microsoft.Azure.SignalR` 1.33.1, `Azure.Communication.Identity` 1.3.1, `Microsoft.AspNetCore.SignalR.Client` 10.0.12 (tests) |

**Recorded deferrals:** CT-304's UI half and TC-508/509 → CT-603. **Recorded untested path:**
the concurrent live-meeting race (U19). **Spec drift:** U15.

## Phase P3 — verification-gates(PHASE)

| Gate | Result |
|---|---|
| Build | **PASS**: 0 errors, 0 warnings |
| Tests | **PASS: 246 executed / 246 passed / 0 failed** (P2 closed at 194, +52). Report: `test-results/TestResults-P3-phase-gate.trx`. Includes real SignalR clients over WebSockets |
| Mutation spot-checks | withdrawn content not blanked → 3 failed; per-send membership re-check removed → 1 failed; meeting-token membership check removed → TC-507 failed. Every mutant built; all reverted and re-verified |
| Format | **FAILED first** (whitespace in `RoomService.cs` and test files). Fixed with `dotnet format`, then build and all 246 tests re-run. **PASS** |
| Dependency scan (transitive) | **PASS**: clean, including the 3 new packages |
| Model vs migrations | no pending model changes |
| Migration-from-zero | **PASS** on fresh LocalDB: all **6** migrations applied. Filtered indexes verified in SQL Server: `IX_Meetings_RoomId` unique `([EndedAtUtc] IS NULL)`, `IX_Messages_RoomId_AuthorId_ClientMessageId` unique `([ClientMessageId] IS NOT NULL)`. All 6 reverted, database dropped, 0 left over |
| `verify-gate.sh` | **1 FAIL (C5), 1 WARN (C8)**. C5: same cause as before (reads P0's TRX). C8: see the correction below |

**Correction to the Gate 3-P2 record:** P2's verification said the spec hash excluding
`_upstream-feedback.md` was `de275235b49a` and that no spec content had changed. That was true at
the 15:37 check. At 15:39, before Gate 3-P2 was approved, `_questions.md` was appended with the
Q-213–Q-217 record, which changed the hash. A file-by-file check now shows **only `_questions.md`
and `_upstream-feedback.md` have been modified since the 1.0.3 patch (15:00:31). No normative spec
file has changed since 1.0.3.** Current hashes: `94791d1f6758` excluding the feedback file only;
`cd58a470c6b8` excluding both record files.

**Phase P3 is ready to present at Gate 3.** This command does not approve it.

**Gate 3 (Phase P3) approved by Rashmi Mehta, 2026-09-14.** An earlier reply, "ZAPPROVED", was not
treated as approval; it was confirmed as "APPROVED" first. Recorded with the approval:
- CT-301, CT-302 and CT-303 are accepted, plus CT-304's API half.
- CT-304's meeting UI (UI-014 states) and TC-508/509 carry to **CT-603** (Q-218).
- A named waiver (owner Rashmi Mehta) covers `verify-gate.sh` C5 FAIL and C8 WARN.
- U15 (Major) and U16–U19 are filed against Rashmi Mehta, because none was assigned. All stay **open**.

## Phase P4 — Operations

**Started 2026-09-14** at Rashmi Mehta's instruction, spec suite **1.0.3**, with three Majors open (U8, U11, U15).

**Pre-build decisions** (halt-and-ask, **Rashmi Mehta**, 2026-09-14):
- **Q-222** Report infrastructure: **Hangfire now** with SQL Server storage (in-memory storage in tests). Hangfire's retry policy applies, and exhausted retries mark the run `Failed` with an audit row. Blob Storage is used when `BLOB_STORAGE_CONNECTION_STRING` is set; otherwise a local-disk store for dev and tests only.
- **Q-223** Metrics:
  - **Attendance %** = (Present + Late) ÷ (Present + Late + Absent); Leave is excluded.
  - **Teacher compliance** = % of (own class × Mon–Fri day in range) with any record.
  - **Timeliness** = % of the teacher's records marked on the attendance date, in institution time.
  - **Course analytics** = average attendance % of the classes mapped to each subject.
- **Q-224** Dashboard: **traceable data only**, computed live with no A2 cache.
  - **Leadership:** totals, present today, average, a 7-day trend, per class, gender split, recent and upcoming announcements.
  - **Teacher:** the same scoped to own classes, with no teacher totals.
  - **Student/Parent:** own or each linked child's attendance % and today's status, plus announcements.
- **Q-225** Timetable: an **`IsPublished` flag per period**. API-054's `{id}` is a period id, and non-leadership callers see only published periods. `TimeSlot` is a validated 24h `HH:mm` start time. Exam schedules are not modelled this release.

### CT-401 — Dashboard endpoint + role variants (API-048/049)

**Status:** DONE — verified by execution.
**Specs read:** `05-api-contracts/operations.md`, `11-ui-spec/operations.md` §UI-015,
`02-requirements-spec/operations.md` (FR-052/053/056, BR-048–050), `07-business-logic/operations.md` (BRL-048–050).
**Silence report:** Q-223/224 were answered before the build. Resolved without asking:
- **PlatformOperator has no dashboard variant** → 403 `ERR-102` (API-048's role list).
- **Days with no countable records** are omitted from the trend, not reported as 0%.
- **"Recent" announcements** have an effective date ≤ today; **"upcoming"** have one > today. Withdrawn ones are excluded.

**Built:** `AttendanceMetrics` (the single Q-223 formula), `DashboardService`, `DashboardRepository`,
`DashboardController`. Serialisation uses `WhenWritingNull`, so a variant's hidden members are
**absent** from the JSON rather than zero. **ADR-0006** covers API-049's cross-tenant count, which
is aggregate-only. No schema change.
**Tests (9 cases, all run):**
- **Leadership:** Late counts as present and Leave doesn't; a 7-point trend; days with no data omitted; per class; gender split; recent vs upcoming.
- **Teacher, adversarial:** 2 students, not 3; class B absent; no teacher keys.
- **Student, adversarial:** none of the 9 institution-wide keys appears in the raw JSON; own % = 50.
- **Parent:** two linked children only; a Leave-only child has no percentage.
- **Other checks:** another institution's leadership sees none of this data; PlatformOperator → 403, anonymous → 401; the platform summary is anonymous, counts across 2 institutions, and has exactly 4 fields with no names or ids; 2 formula unit tests.
- TC-609's dashboard half: no "satisfaction" or "completion" anywhere in any variant.

**Mutation checks (each built, then was reverted):** the Teacher variant unscoped → 1 test failed.
Leave counted in the denominator → 4 tests failed.
**Gate:** ✅ contract tests per role variant green; full Operations suite 9/9.
**DoD:** ✅ Student/Parent variants never expose institution-wide totals (BR-048), asserted on the raw payload.

### CT-402 — Report generation job + types (API-050–052)

**Status:** DONE — verified by execution. **PDF export deferred (Q-226).**
**Specs read:** `07-business-logic/operations.md` (BRL-051/052/090/091, VAL-021/022, E1), `06-event-contracts.md` §2,
`11-ui-spec/operations.md` §UI-017, `05-api-contracts/operations.md` (API-050–052, report types, idempotency),
`10-process-flows/operations.md` (PF-010), `final-tech-design.md` (A1, D7, D11, T-005), `14-test-spec/operations.md` (TC-601–604, 609).
**Silence report:** Q-222/223 were answered before the build. Also:
- **Q-226** (asked, **Rashmi Mehta**): **CSV only for now**. PDF export is deferred as a **recorded D7 / NFR-027 deviation**; a `PDF` request is refused with `ERR-901` rather than served as CSV. (The choice offered was PDFsharp (MIT), QuestPDF (revenue-threshold licence), or CSV only.)
- **Resolved without asking, to feedback:**
  - **`ReportRun` (§2.9) had no lifecycle.** Added `Status`, `Format`, `FromDate`/`ToDate`, `ScopeClassId`, `ScopeTeacherUserId`, `RequestedAtUtc` and `FailureReason`. `GeneratedAtUtc` and `BlobUri` are now nullable, since there is no file while Pending. `ReportType` is an enum stored as a string.
  - **Request shape:** `{reportType, dateRange:{from,to}, scope:{classId|null}, format}`.
  - **A Teacher** may request only for a class they teach (no whole-school scope). Their Teacher Performance report covers **themselves only**, and they see only runs they requested.
  - **Downloading a Pending or Failed run** returns 409 `ERR-901`; no catalogue code exists for "not ready".
  - **"Surfaced to ops alerting"** is implemented as an `Error` log plus the audited status change. No alerting channel is otherwise specified.

**Built:**
- **Application:** `ReportContracts`, `ReportService`, `ReportGenerator` (four types; every metric goes through `AttendanceMetrics`; CSV with a UTF-8 BOM and RFC-4180 escaping).
- **Infrastructure** (`ReportInfrastructure.cs`): `ReportRunRepository`; `ReportDataSource` (no member can return non-attendance metrics); `ReportJobRunner`; `ReportFailureRecorder`.
- **The Hangfire job:** `ReportJob` with `[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = Fail)]`, plus `ReportRunFailureFilter`, which marks the run Failed only when Hangfire actually applies FailedState.
- **Job queues:** `HangfireReportJobQueue`, and `DisabledReportJobQueue` for when Hangfire is off.
- **File stores:** `BlobReportFileStore` (when `BLOB_STORAGE_CONNECTION_STRING` is set); `LocalDiskReportFileStore`, which refuses paths outside its root.
- **ADR-0007 tenant scope:** `BackgroundTenantScope`, and `HttpCurrentTenant` falls back to it only when there is no HttpContext.
- **Api:** `ReportsController` with Idempotency-Key; `HangfireSetup` with SQL Server storage and a conditional in-process server, all read lazily from `Hangfire:Enabled`.
- **Packages** (transitive scan still clean): `Hangfire.Core`/`SqlServer`/`AspNetCore` 1.8.25, `Azure.Storage.Blobs` 12.29.2, `Hangfire.InMemory` 1.0.0 (tests).
- **Migration:** **`20260914130535_ReportRunLifecycle`**.

**Test infrastructure change:** both shared HTTP test factories set `Hangfire:Enabled=false`.
Hangfire's storage and filters are process-static, and parallel test hosts would share them. Before
this, CT-101's factory hosts were starting a Hangfire server that retried against a non-existent
LocalDB database in the background. That was checked: no `SchoolSync` database or Hangfire schema
was created.
**Defect in my own test, found and fixed:** the first synchronous HTTP job queue ran inside the
request's async context, so the job inherited the caller's tenant claim and **did not exercise the
background-scope path it claimed to test**. The audit rows showed the principal as actor. The queue
now suppresses execution-context flow. A new test asserts the system actor (`Guid.Empty`) and that
another institution's data never appears in the report.
**Tests (19 cases, all run):**
- **Generator, unit, with a spy data source:**
  - TC-601: Student Performance reads only `Students` + `StudentAttendance`; 66.7% with Leave excluded; blank % for no data; CSV comma escaping; out-of-scope student absent.
  - TC-602: Teacher Performance reads no subjects or classes; compliance 40% (2 of 5 weekdays), timeliness 50%.
  - A record made after local midnight counts as not on time.
  - Class and course aggregates.
  - E1: an empty report is header only.
  - TC-609: no Satisfaction/Completion type or column.
- **HTTP:**
  - 202 → Ready → CSV download with the computed row.
  - TC-603: a Teacher is denied another class, whole school, and Teacher Performance for a class; a positive control passes.
  - A Teacher's history is scoped; leadership sees all.
  - 3 × ERR-901 cases: PDF (Q-226), from > to, an untraced type.
  - An idempotency replay yields one run; Student and Parent are denied.
  - A Pending run can't be downloaded → 409.
  - **TC-604**: every attempt fails (4 = 1 try + 3 retries) → `Failed` with a reason and a system-actor audit row.
  - ADR-0007: a no-context job stays in its own institution.
- **Hangfire, isolated, non-parallel:** a real `BackgroundJobServer` on in-memory storage runs an always-throwing report-run job through 1 retry, and only then does the filter mark the run `Failed`. A reflection test checks the production job's retry policy and marker attribute.

**Mutation check:** removing `BackgroundTenantScope.Enter` from the job → 2 tests failed. Reverted.
**Gate:** ✅ TC-601, 602, 609 green (plus 603, 604).
**DoD:** ✅ Student/Teacher performance types read only `AttendanceRecord` (spy-verified); no Satisfaction/Completion field anywhere.
**Deviation:** ⚠️ PDF export not built (Q-226).

### CT-403 — Scheduling CRUD + conflict validation (API-053–055)

**Status:** DONE — verified by execution.
**Specs read:** `07-business-logic/operations.md` (BRL-053–056, decision table, VAL-023, E2/Q-208), `04-data-model.md` §2.9,
`05-api-contracts/operations.md` (API-053–055), `10-process-flows/operations.md` (PF-011), `08-error-catalogue.md` (ERR-701–703),
`14-test-spec/operations.md` (TC-605–608).
**Silence report:** Q-225 was answered before the build. Resolved without asking, to feedback:
- **One save can trip up to three ERR- codes**, but the catalogue body is a single `{code,message}`. A conflict returns **409 `{errors:[{code,message},…]}`**, each entry a verbatim catalogue body, in teacher → class → room order.
- **Room is matched case- and whitespace-insensitively**, matching SQL Server's default collation, so the application check and the unique-index backstop agree. Room stays free text (Q-208).
- **Class, subject and teacher must exist** in the institution (the PF-011 pre-condition); otherwise ERR-901.
- **An edit keeps the period's publish state.** Publishing twice is a no-op.
- **No delete endpoint is specified** for a period, so none was built. Exam schedules (FR-061) are not modelled (Q-225). To feedback.
- **`TimetablePeriod` is now `IAuditable`**, because NFR-028 names schedule changes.

**Built:** `TimetableService` (all three conflicts in one pass, never stopping at the first),
`TimetableRepository`, `TimetableController`, `ErrCatalogue.Err701–703`, `TimetablePeriod.IsPublished`/`PublishedAtUtc`.
Migration **`…_TimetablePublishState`** adds only those two columns.
**Tests (17 cases, all run):**
- **Unit, over InMemory (no index enforcement, so every rejection is the application's decision table):**
  - TC-605 (teacher), TC-606 (class), TC-607 (room, matched as `"  lab 1 "` vs `"Lab 1"`).
  - **TC-608**: teacher AND room reports both, and nothing is saved.
  - A different slot or day saves; an edit doesn't conflict with itself.
  - 4 × `HH:mm` validation cases (`9am`, `9:00`, `24:00`, empty); unknown references rejected.
- **HTTP:**
  - One 409 carrying `["ERR-701","ERR-703"]`.
  - Teacher/Student/Parent can't create or publish.
  - Drafts are hidden from a Teacher and a Student until published, then visible by class and by teacher; the other draft stays hidden.
  - Create and publish are audited with the actor.

**Mutation check:** stopping at the first conflict → **2 tests failed** (unit + HTTP). Reverted.
**Gate:** ✅ TC-605–608 green.
**DoD:** ✅ all 3 conflict dimensions checked together.
**Not tested here:** the SQL Server unique indexes as a second line of defence. They are present from InitialCreate and are verified by the migration-from-zero index listing below.

## Phase P4 — phase-conformance (no tests run)

| Promised by 15-task-breakdown.md §4 | Present |
|---|---|
| CT-401 API-048/049 | yes: `GET /dashboard` (4 role variants), `GET /platform/summary` |
| CT-402 API-050–052 | yes: POST (202), list, detail, download. **PDF format not built (Q-226)** |
| CT-403 API-053–055 | yes: POST, PUT, publish, GET by class/teacher |
| TC-601–609 | each referenced by at least one test |
| Entities / migrations | `ReportRun` lifecycle (`20260914130535_ReportRunLifecycle`); `TimetablePeriod` publish state (`20260914131138_TimetablePublishState`) |
| ADRs | ADR-0006 (platform summary cross-tenant count), ADR-0007 (background-job tenant scope) |
| CFG bindings | CFG-SEC-05 (Blob) read lazily; `Hangfire:Enabled` (default true) |
| Packages | Hangfire.Core/SqlServer/AspNetCore 1.8.25, Azure.Storage.Blobs 12.29.2, Hangfire.InMemory 1.0.0 (tests) |

**Recorded deviation:** PDF export (D7 / NFR-027), U21.
**Recorded scope move:** Hangfire was installed in P4 (Q-222); CT-501 now adds only its recurring jobs.
**Spec drift:** U20.

## Phase P4 — verification-gates(PHASE)

| Gate | Result |
|---|---|
| Build | **PASS**: 0 errors, 0 warnings. Three warnings raised during CT-402 were fixed rather than suppressed: an unused parameter, a member hiding `ControllerBase.Request`, and an obsolete Hangfire constructor. |
| Tests | **PASS: 291 / 291 on two consecutive full runs** (P3 closed at 246, +45). Report: `test-results/TestResults-P4-phase-gate.trx`, newer than every source file. |
| First full run | **FAILED 2/291.** Both Hangfire wiring tests hit `ObjectDisposedException: LoggerFactory`. **Root cause:** `AuthApiFactory` (CT-002), a third independent test factory, lacked `Hangfire:Enabled=false`. Its hosts started a real Hangfire server, which retried against a non-existent LocalDB database and bound Hangfire's process-global log provider to a host that was later disposed. Found with a throwaway diagnostic, since deleted. **Fixed:** the flag was added to `AuthApiFactory`, and the isolated Hangfire tests now install their own log provider. |
| Mutation spot-checks | Teacher dashboard unscoped → 1 failed. Leave in the denominator → 4. Job tenant scope removed → 2. Stop at first schedule conflict → 2. Every mutant built, and all were reverted. |
| Format | **PASS** after one `dotnet format` pass (test-file reflow), then build and tests re-run. |
| Dependency scan (transitive) | **PASS**: clean, including the 5 new packages. |
| Model vs migrations | no pending changes |
| Migration-from-zero | **PASS** on a fresh LocalDB: all **8** migrations applied. `TimetablePeriods` has the three unique conflict indexes (teacher, class, room + institution; `TimeSlot` is `nvarchar(450)`, so it can be indexed). All reverted, 0 databases left over. |
| `verify-gate.sh` | **1 FAIL (C5), 1 WARN (C8)**, the same causes as P1–P3. Normative spec files modified since 1.0.3: **0**. |

**Test-infrastructure lesson, recorded:** `Hangfire:Enabled` defaults to **true**, so production can
never silently stop running jobs. The cost is that every test host must switch it off; three separate
factories needed the flag. A future factory that forgets it will reproduce this failure.

**Phase P4 is ready to present at Gate 3.** This command does not approve it.

**Gate 3 (Phase P4) approved by Rashmi Mehta, 2026-09-14.** Recorded with the approval:
- CT-401, CT-402 (CSV only; the PDF deviation stays open as U21) and CT-403 are accepted.
- A named waiver (owner Rashmi Mehta) covers `verify-gate.sh` C5 FAIL and C8 WARN.
- U20 and U21 (Major) and U22–U25 are filed against Rashmi Mehta, because none was assigned. All stay **open**.

## Phase P5 — Configuration & Commercial

**Started 2026-09-14** at Rashmi Mehta's instruction, spec suite **1.0.3**, with five Majors open (U8, U11, U15, U20, U21).

**Pre-build decisions** (halt-and-ask, **Rashmi Mehta**, 2026-09-14):
- **Q-227** Plan limits: the seeded numbers had **no source anywhere** in the BRD, tech design or specs (CT-001 placeholders). The **PlatformOperator can edit each plan's learner capacity, storage allowance and capabilities**, with the seeds as provisional defaults. Learner capacity is enforced on enrolment and reactivation; the **storage gate is deferred** (no metering source).
- **Q-228** Parent gate: BRL-063's ParentAccess check covers **every Parent-role endpoint**: link-code redemption, linked students, linked attendance and the Parent dashboard. Staff can still issue codes. This **retrofits the approved P1–P4 endpoints**.
- **Q-229** Rollover: a **`Class.NextClassId`** successor (null = final year).
  - Rollover creates the next `AcademicYear` if it's missing.
  - It writes an `Enrollment` for each active student; hold-backs keep their class.
  - It updates `Student.ClassId` as the student's current placement.
  - Final-year students get no new enrolment and stay active.
- **Q-230** Trials: started by an **authenticated leader of that institution (or a PlatformOperator), never anonymously for an arbitrary id**, once per institution.
  - A trial grants **Mid** for 30 days (CFG-006) and adds trial fields on `Institution`.
  - A daily job reverts expired trials to Entry, with no billing call.
  - An operator plan change ends the trial.

### CT-501 — Institution configuration CRUD (API-056–058) + daily retention/lock-sweep job scaffold

**Status:** DONE — verified by execution. **Retention sweep is a scaffold (ADR-0008).**
**Specs read:** `05-api-contracts/configuration-commercial.md` (API-056–058), `07-business-logic/_shared.md` §4,
`07-business-logic/configuration-commercial.md` (BRL-057–059, VAL-024/025), `04-data-model.md` §2.11.
**Silence report:** resolved without asking, to feedback:
- **GET with no saved row** returns defaults (`isDefault: true`; time zone = the CFG-009 fallback). The first PUT creates the row.
- **VAL-024 accepts only IANA ids.** A Windows id such as "India Standard Time" is refused even though this Windows host could resolve it.
- **VAL-025** accepts `en` only (Q-209 provisional, unchanged).
- **The contract gives no types for academic settings.** `courseCategories` and `assessmentTypes` are string arrays; `gradingScale` and `reportCardFormat` are strings. Notification preferences are `{email, sms, inPlatform}` booleans stored as JSON.
- **FR-064's account-management actions** (e.g. forcing a password reset, Q-207) have no API in the contracts, so none was built.
- **Retention sweep (CMP-001)** is a **scaffold only**: it counts records past 7 years across tenants and logs them, and **anonymises and deletes nothing**. The anonymise/delete field rules are unspecified and irreversible (ADR-0008).

**Built:**
- `InstitutionConfigService`, `InstitutionConfigRepository` and `InstitutionConfigController`.
- **Recurring jobs:** `RecurringJobs.Register` schedules two daily jobs (**`attendance-lock-sweep`**, closing Q-216's split from CT-202, and **`retention-sweep`**) when the in-process server starts. The job classes are `AttendanceLockSweepJob` and `RetentionSweepJob`.
- **ADR-0008.**
- No schema change.

**Tests (12 cases, all run):**
- **BRL-059, adversarial:** 4 saves (one sending `audit:false` and `recordForAudit:false`) produce 4 audit rows with the principal as actor.
- **Immediate effect:** settings are visible to Teacher, Student and Parent. Setting New York makes marking the 14th a future date (ERR-403) straight away.
- **Validation and scope:** Teacher/Student/Parent writes → 403, no row created; VAL-024/025 × 3; another institution still sees defaults.
- **Jobs:** both recurring jobs registered daily. The retention scaffold counts 2 of 4 across institutions, and every row and name is unchanged afterwards.

**Mutation checks (each built, then was reverted):** configuration made non-auditable → the audit
test failed. Windows ids accepted → the VAL-024 test failed.
**Gate:** — (no TC named).
**DoD:** ✅ audit-on-save unconditional (BR-059), adversarially proven.
**Deviation:** ⚠️ retention anonymise/delete not implemented (ADR-0008).

### CT-502 — Academic year rollover (API-059/060)

**Status:** DONE — verified by execution.
**Specs read:** `10-process-flows/configuration-commercial.md` (PF-012), `07-business-logic/configuration-commercial.md`
(BRL-060/067, VAL-026), `04-data-model.md` §2.11 (Enrollment append-only, D14), `05-api-contracts/configuration-commercial.md`
(API-059/060), `14-test-spec/configuration-commercial.md` (TC-701–703).
**Silence report:** Q-229 was answered before the build. Resolved without asking, to feedback:
- **No contract creates an AcademicYear or sets a successor class.** Built minimal `POST`/`GET /api/v1/academic-years` and `PUT /api/v1/classes/{id}/next-class` (leadership).
- **Rollover state** is `RolloverRequestedAtUtc`/`RolledOverAtUtc`/`NextAcademicYearId` on `AcademicYear`. A year rolls over **exactly once**: a second API-059 returns 409 `ERR-901`, and a second job run is a no-op.
- **The next year** is created if none starts the day after this year ends. Its label is derived (`2026-27` → `2027-28`) and it spans the same length.
- **History snapshot:** students enrolled before any Enrollment rows existed (P1–P4 never wrote them) get the closing year's placement recorded first, so the closed year stays queryable.
- **Hold-backs** are a new `RolloverHoldBack` table. **After** a rollover, a hold-back **appends** a new next-year enrolment in the prior class and never edits the promotion row.
- **The scheduled trigger** is a daily `academic-year-rollover` job. It rolls every year that has ended in its institution's calendar, following ADR-0007 (enumerate by id, run inside each year's scope).
- **P1 retrofits to `ClassRepository`** (API-027 delete):
  - a class with Enrollment history is refused with `ERR-306`, where it previously failed on the foreign key
  - deleting a class that is another class's successor clears that link, so the predecessor becomes a final year

**Real defect found by a test and fixed:** a hold-back applied after rollover judged "already held
back" by ordering Enrollment rows by `CreatedAtUtc`. Rows written in the same instant tie, so it
could pick the promotion row and append a duplicate correction. It now uses `Student.ClassId`, the
single source of current placement.
**Built:** `Class.NextClassId`; AcademicYear rollover fields; `RolloverHoldBack`; `RolloverService`
(append-only: no code path edits or removes an Enrollment); `RolloverRepository`; `RolloverJobRunner`
and `RolloverJob` (ADR-0007); `HangfireRolloverQueue` / `DisabledRolloverQueue`; `YearEndRolloverJob`;
`AcademicYearsController`. Migration **`20260914135942_AcademicYearRollover`**.
**Tests (15 cases, all run, job outside any request context):**
- **TC-701, adversarial:** the seeded prior-year row is unchanged field by field. Promotions go to the successor class; a history snapshot exists for students with no prior row and no duplicate for those who had one; the next year is created (`2027-28`, starting 2027-04-01); current placement moves; withdrawn students are not rolled.
- **Final-year class:** no new enrolment; the student stays active.
- **TC-702:** held back before the rollover → same class.
- **Hold-back after:** 2 rows, the promotion unchanged plus the appended correction; repeating it appends nothing.
- **TC-703, adversarial:** a list with a foreign or withdrawn student is rejected whole, with nothing recorded.
- **Idempotency:** a second request → 409, and a second job run leaves the enrolment count unchanged.
- **Other checks:** Teacher/Student → 403; the two class retrofits; the year-end job rolls only ended years; 4 × label derivation cases.

**Mutation check:** making the rollover touch an existing current-year enrolment → TC-701 failed. Reverted.
**Gate:** ✅ TC-701–703 green.
**DoD:** ✅ append-only `Enrollment` proven (no historical mutation).

### CT-503 — Subscription/tier model + capacity gate (API-064, BRL-073/063) + ParentAccess retrofit

**Status:** DONE — verified by execution.
**Specs read:** `07-business-logic/configuration-commercial.md` (BRL-061–064/073, decision table, role×tier cross-reference, E1/Q-210),
`05-api-contracts/configuration-commercial.md` (API-061/062/064), `09-security-spec.md` §2 axis 2 + N8, `07-business-logic/_shared.md` §1,
`04-data-model.md` §2.10, `08-error-catalogue.md` (ERR-801/802), `14-test-spec/configuration-commercial.md` (TC-706/707).
**Silence report:** Q-227/228 were answered before the build. Resolved without asking, to feedback:
- **API-061/062 were in no CT's scope.** Built here: public plans (structural fields only; `learnerCapacity: null` = unlimited) and enquiries (validated, Idempotency-Key).
- **Enquiry "notification" (T-004):** no email channel is bound, so the enquiry is **logged at Warning** and **not delivered**.
- **API-064's `startDate`** is stored as `Institution.SubscriptionStartDate` (§2.1 had no column). `Institution` and `SubscriptionPlan` are now **`IAuditable`** (NFR-032).
- **Security:** the subscription endpoints **re-read the caller's role from their account row**, as `_shared.md` §1 requires for subscription changes, rather than trusting the JWT. A token claiming PlatformOperator for a non-PlatformOperator account gets 403.
- **Capacity counts active learners only.** Enrolment and reactivation are gated. Q-210's provisional grandfathering is what results: an institution already over a lowered limit keeps its learners but can add none.
- **Only ParentAccess is gated.** `AdvancedAnalytics` and `CustomReporting` have **no feature mapped** to them anywhere in the specs, so nothing is gated on them.
- **Known limitation:** the capacity check-then-insert is not serialised. Two concurrent enrolments at limit − 1 could both pass. Recorded for CT-701 (a serializable transaction or a row lock).

**Built:**
- `IPlanEntitlements` / `PlanEntitlements`: the effective plan, trial-aware for CT-504.
- `SubscriptionService`, `SubscriptionRepository`, `LoggingEnquiryNotifier`, `SubscriptionsController` (API-061/062/064 plus `PUT /subscription-plans/{id}`), `ErrCatalogue.Err801/802`.
- `RequiresPlanCapabilityAttribute`, an action filter. It runs after authorization, so RBAC is always evaluated first.
- **P1–P4 retrofits (Q-228):**
  - the ParentAccess gate on `POST /parent-links`, `GET /students/linked`, `GET /attendance/linked`, and the Parent variant of `GET /dashboard` (checked in the controller, since the route is shared)
  - the learner-capacity gate in `StudentService.EnrolAsync`/`ReactivateAsync` (402 `ERR-801`)
- **Test retrofits:** `StudentServiceTests` gets an unlimited-plan fake. The P1/P2/P4 Parent tests move their institution to Bespoke (the only seeded plan with ParentAccess) through a new `SetPlanAsync` helper.
- Migration **`20260914140924_SubscriptionAndTrialFields`**.

**Tests (14 cases, all run):**
- **TC-706, adversarial:** capacity 2 → the 2nd learner is created, the 3rd gets 402 `ERR-801`, and exactly 2 are active. Reactivation at capacity is refused.
- **TC-707, adversarial:** a Parent on Entry gets 402 `ERR-802` on all 4 Parent endpoints.
- **Axis independence (DoD):** Entry → 402, Mid → 402, Bespoke → 200, and a Teacher on Bespoke → 403.
- **Other checks:**
  - staff issue codes on Entry
  - API-064 assignment changes the gates immediately and is audited with the operator as actor
  - Administrator and forged operator tokens → 403, with the plan unchanged
  - Q-227: an unknown capability is rejected; a capacity of 1 and Entry + ParentAccess take effect immediately
  - public plans have exactly the structural fields
  - enquiry validation and idempotency

**Mutation checks (each built, then was reverted):**
- tier filter removed from `/attendance/linked` → 1 failed (precisely that endpoint's case)
- enrolment capacity gate removed → 2 failed
- operator role trusted from the token → 1 failed (the forged-token case)

**Gate:** ✅ TC-706, 707 green; full suite **318/318** before these 14 were added.
**DoD:** ✅ RBAC and tier axes proven independently enforced.

### CT-504 — Trial lifecycle (API-063, reversion job)

**Status:** DONE — verified by execution.
**Specs read:** `10-process-flows/configuration-commercial.md` (PF-013), `07-business-logic/configuration-commercial.md` (BRL-070/071, E2),
`05-api-contracts/configuration-commercial.md` (API-063), `12-infrastructure-spec.md` (CFG-006), `final-tech-design.md` A4,
`14-test-spec/configuration-commercial.md` (TC-704/705).
**Silence report:** Q-230 was answered before the build. Resolved without asking, to feedback:
- **A trial can start only from the Entry plan** (a paid school starting one would be a downgrade), and only once. A repeat attempt gets 409 `ERR-901`.
- **A4 "reuses the rollover scheduler class"** is read as the same Hangfire recurring scheduler: `trial-reversion` is registered daily beside `academic-year-rollover`.
- **API-063's body is `{institutionId}` only.** A payment field sent by a client (e.g. `cardNumber`) is ignored, not stored.
- **Entitlements drop at expiry even before the nightly job runs:** `PlanEntitlements` only honours an unexpired trial. The job then makes the stored state match.
- **The trial plan's ids** are constants in the Application layer (which can't reference the DbContext seeds). A test asserts they equal the seeded rows.

**Built:** `TrialService`; `TrialReversionJob` (plan → Entry, trial closed, `TrialStartedAtUtc` kept, audited as the system actor); `TrialsController` (Idempotency-Key); the `trial-reversion` recurring registration. A purchase through API-064 closes a running trial (PF-013 3a, via `SubscriptionService.EndTrial`). No schema change beyond CT-503's migration.
**Tests (9 cases, all run):**
- **Trial start:** a 30-day Mid trial expires at exactly +30 days. Its capacity applies (a 2nd learner is admitted where Entry refused one), while the stored subscription stays Entry.
- **TC-704:** a purchase on day 10 closes the trial; at day 35, reversion does nothing and the plan stays Bespoke.
- **TC-705:** at 30 days minus 1 second, nothing happens; at exactly 30 days the school reverts to Entry, the trial is cleared, and an audit row records the system actor.
- **TC-705, structural, adversarial:** no billing, payment or charge dependency exists on the trial service or reversion job constructors; no Billing/Payment/Charge/Invoice type exists in any product assembly; and their non-comment source contains no such term.
- **Q-230 rules:**
  - an expired trial's entitlements are gone before the job runs
  - a second trial, or one from a paid plan → 409
  - anonymous → 401; another school's leadership → 403; a Teacher → 403; a real PlatformOperator → 201
- **Other checks:** scheduled daily; the plan constants match the seeds.

**Mutation checks (each built, then was reverted):**
- **A**, a billing dependency added to the reversion job → the structural test failed.
- **B**, the "plan → Entry" assignment removed → **survived, as an equivalent mutant**. A trial can only start from Entry and a purchase ends the trial, so that assignment is always a no-op at reversion time. The line is kept because FR-071 states it literally.
- **C**, the trial not cleared → TC-705 failed.

**Test false positive found and fixed:** the billing scan matched "charge" inside the BR-007 role name `SchoolInCharge`. Only that identifier is stripped; the pattern was not weakened.
**Gate:** ✅ TC-704, 705 green.
**DoD:** ✅ no billing call exists on the auto-reversion path, proven structurally and behaviourally.

## Phase P5 — phase-conformance (no tests run)

| Promised by 15-task-breakdown.md §4 | Present |
|---|---|
| CT-501 API-056–058 + retention/lock-sweep job scaffold | yes: GET/PUT config, notifications, academic; daily `attendance-lock-sweep` and `retention-sweep` (scaffold). **Q-216 closed.** |
| CT-502 API-059/060 | yes, plus `POST`/`GET /academic-years` and `PUT /classes/{id}/next-class`, which rollover needs (silences); daily `academic-year-rollover` |
| CT-503 API-064, BRL-073/063 | yes, plus the API-061/062 public endpoints (unassigned in §4), `PUT /subscription-plans/{id}` (Q-227), and the ParentAccess retrofit on 4 P1–P4 endpoints |
| CT-504 API-063 + reversion job | yes: `POST /trials`, daily `trial-reversion` |
| TC-701–707 | each referenced by at least one test |
| Migrations | `20260914135942_AcademicYearRollover`, `20260914140924_SubscriptionAndTrialFields` |
| ADRs | ADR-0008 (retention scaffold, count-only cross-tenant) |
| CFG bindings | CFG-006 (30), CFG-007 (7 years) bound as named constants; VAL-025 `en` (Q-209) |

**Recorded deviations:** retention anonymise/delete not implemented (U27); storage gate not built
(U32); AdvancedAnalytics/CustomReporting gate nothing (U31); enquiries logged, not delivered (U30).
**Recorded retrofits to approved code:** ParentAccess gate (P1 CT-103, P2 CT-203, P4 CT-401); the
capacity gate in P1 CT-102 `StudentService`; P1 CT-105 class delete. Six P1/P2/P4 tests were moved
to a ParentAccess plan, and `StudentServiceTests` got an unlimited-plan fake.
**Spec drift:** U26.

## Phase P5 — verification-gates(PHASE)

| Gate | Result |
|---|---|
| Build | **PASS**: 0 errors, 0 warnings |
| Tests | **PASS: 341 / 341 on two consecutive full runs** (P4 closed at 291, +50). Report: `test-results/TestResults-P5-phase-gate.trx`, newer than every source file. |
| Mutation spot-checks | Config not audited → 1. Windows time-zone id accepted → 1. Rollover touches history → TC-701. Tier filter removed from `/attendance/linked` → 1. Capacity gate removed → 2. Operator role trusted from token → 1. Billing dependency added → the structural test failed. Trial not cleared → TC-705. **"Plan → Entry" removed survived as an explained equivalent mutant** (invariants make it a no-op). Every mutant built, and all were reverted. |
| Real defect found by tests | Rollover hold-back after the fact tie-broke on `CreatedAtUtc` and could append a duplicate correction. Fixed to use `Student.ClassId`. |
| Format | **PASS** after one `dotnet format` reflow; tests re-run on the formatted code. |
| Dependency scan (transitive) | **PASS**: clean |
| Model vs migrations | no pending changes |
| Migration-from-zero | **PASS** on a fresh LocalDB: all **10** migrations applied and reverted. Foreign keys verified: `FK_Classes_Classes_NextClassId` NO_ACTION (the application clears predecessors); `FK_RolloverHoldBacks_AcademicYears` CASCADE; `FK_RolloverHoldBacks_Students` NO_ACTION. Institution trial and subscription columns present. 0 databases left over. (A first listing query failed on a catalog collation mismatch in string concatenation. The migration itself had passed; the listing was re-run correctly on a second fresh database.) |
| `verify-gate.sh` | **1 FAIL (C5), 1 WARN (C8)**, same causes as P1–P4. Normative spec files modified since 1.0.3: **0**. |

**Phase P5 is ready to present at Gate 3.** This command does not approve it.

**Gate 3 (Phase P5) approved by Rashmi Mehta, 2026-09-14.** Recorded with the approval:
- CT-501 (retention sweep is a scaffold, U27), CT-502, CT-503 and CT-504 are accepted, including the retrofits into approved P1–P4 code.
- A named waiver (owner Rashmi Mehta) covers `verify-gate.sh` C5 FAIL and C8 WARN.
- U26, U27 and U28 (Major) and U29–U33 are filed against Rashmi Mehta, because none was assigned. All stay **open**.

---

# Phase P6 — UI / cross-cutting hardening

## P6 — reconciliation of pre-existing, unlogged work (2026-09-15)

**This section records work this command did not perform.** A session on 2026-09-14 (20:38–21:20)
wrote a complete React/Vite/TypeScript SPA at `src/SchoolSync.Web` and ended before checkpointing
anything: `build-log.md` had no P6 section and `aidlc-state.json`'s `build_phase_status.P6.cts` was
empty. Because this command resumes from the build log alone, a normal resume would have concluded
CT-601 was unbuilt and overwritten it. Operator (Rashmi Mehta, 2026-09-15) directed **reconcile
first**: audit and verify what exists before writing anything.

**Nothing in the existing tree has been modified at the time of writing this section.**

### What exists

`src/SchoolSync.Web` — React 19.3 / Vite 8.3 / TypeScript 5.9, Tailwind 4.3 with the design tokens
bound in `src/styles.css`, Radix primitives, TanStack Query, react-router, zustand, ACS Calling
behind an adapter. 53 source and e2e files. Routes exist for **all 20 screens UI-001–UI-020**.

Tokens are bound correctly: `styles.css` sets `--color-*: initial` to switch Tailwind's default
palette off entirely, so an off-token colour cannot be written by accident. This is a stronger
guarantee than the ui-spec asked for and it holds — the a11y failures below are **token-pairing**
defects, not literal-value defects.

### What was actually verified (first time these gates have ever run)

| Check | Result |
|---|---|
| `npm run typecheck` (`tsc -b`) | **PASS** — 0 errors |
| `npm run lint` (eslint) | **PASS** — 0 problems |
| `npm test` (vitest) | **PASS — 63 / 63** across 6 test files |
| `npx playwright test` (e2e + a11y, chromium) | **FAIL — 85 passed, 31 failed** |

**The CT-601–604 gate is `a11y scan AA`. It fails.** No CT in P6 can be recorded done.

### The 31 failures, root-caused

**29 are a11y (`e2e/a11y.spec.ts`), and reduce to four distinct defects:**

| # | axe rule | Where | Verdict |
|---|---|---|---|
| 1 | `color-contrast` on `.bg-success` / `.px-2\.5` | `StatusPill` — `Present: bg-success text-white` (**3.77:1**) and `Leave: bg-info text-white` (**4.10:1**) | **Real.** Both below AA 4.5:1. Root cause is **`design-tokens.md` §1b, which asserts these pass and is arithmetically wrong** → filed as **U34 (Major)**. Also hits the Fill-attendance selected radio (`.bg-success.px-2\.5.py-0\.5 \| .border-success`). |
| 2 | `color-contrast` on `.bg-border-subtle` | neutral `Badge` and the attendance "Locked" pill — `text-secondary` on `border-subtle` = **4.47:1** | **Real.** No neutral pair is defined in `design-tokens.md` §1; the build improvised one that misses AA → filed as **U35**. |
| 3 | `scrollable-region-focusable` on `.overflow-x-auto` | UI-004 Pricing at bp-sm (the horizontally scrollable comparison table) | **Real, code-only.** A scrollable region must be keyboard-reachable. NFR-025's "tables scroll inside their own container" is satisfied; making that container focusable is ordinary implementation, no spec question. |
| 4 | — | — | Screens affected: UI-004 (bp-sm), UI-008, UI-009 ×2, UI-010 ×2, UI-012, UI-015 (parent), at bp-sm/md/lg/xl. |

**2 are `e2e/flows.spec.ts`:**

| Test | Verdict |
|---|---|
| UI-006 profile menu — `aria-expanded` on the trigger | **Real, minor, code-only.** Radix `DropdownMenu.Root` is modal by default, which `aria-hidden`s the rest of the page — including the trigger — while the menu is open, so the trigger leaves the accessibility tree. A profile disclosure should be `modal={false}`. |
| UI-005 login — "Signed in as jane" | **Test defect, not a product defect.** Strict-mode violation: the locator matches *both* the `aria-live` announcement and the toast. The product is correct to have both; the test locator must be scoped. |

### Undocumented decisions found in the code — Q-231 … Q-237

The interrupted session made **seven decisions that are cited in code comments but recorded
nowhere** — not in `_questions.md` (which ends at Q-230), not in the build log, not in state. Only
**Q-233 carries an attribution** (`Rashmi Mehta`, in `Pricing.tsx`). The other six are unattributed.

| Q | Subject, as the code states it |
|---|---|
| Q-231 | Path routes (not hash), served by the API's SPA fallback; ui-spec route names kept. Every API call is a same-origin relative path. |
| Q-232 | Access token held **in memory only** — no persist middleware, no web storage; session restored per page load from the httpOnly refresh cookie (NFR-017). |
| Q-233 | Pricing shows **no price**; every plan's CTA is "Contact sales". *(Rashmi Mehta)* |
| Q-234 | ACS Calling behind an adapter; a fake adapter compiled into the `e2e` build mode only, never production, to drive UI-014's states for TC-508/509. |
| Q-235 | Register submits the id of the school chosen from the public directory. |
| Q-236 | Where the API exposes no data, the screen omits the element rather than inventing it — per-class attendance sparkline, meeting participant avatars, Reports performance charts, Teachers staff-by-subject chart, Settings school profile / FR-064 account actions, "Forgot password?". Filed as gaps. |
| Q-237 | With no prices shown, the monthly/yearly toggle renders nothing and is omitted. |

**These must be written into `_questions.md` with named owners before Gate 3-P6.** Q-232 and Q-236
in particular are substantive: one is a security posture, the other omits UI the ui-spec describes.

### Binding UI-beat constraints — checked, both honoured

- **Reports "Satisfaction"/"Completion" gauges: ABSENT.** Confirmed — `Reports.tsx` renders no gauge.
- **Pricing feature copy not built as requirements.** Confirmed — Q-233 removes prices entirely.

### Carried in from P3 (Q-218)

CT-304's meeting UI states (UI-014 connecting / reconnecting / error) and **TC-508/509 exist** and
are exercised by `flows.spec.ts` against the Q-234 fake ACS adapter — that test **passes**. They
are not among the 31 failures.

### Status of each P6 CT after reconciliation

| CT | Code | Gate (`a11y scan AA`) | Status |
|---|---|---|---|
| CT-601 Public + Auth modal + App shell (UI-001–006) | present | **FAIL** (UI-004 scroll region; UI-006 profile menu) | **NOT done** |
| CT-602 Academic records + Attendance (UI-007–010) | present | **FAIL** (contrast, UI-008/009/010) | **NOT done** |
| CT-603 Communication (UI-011–014) | present, incl. the CT-304 carry-in | **FAIL** (contrast, UI-012) | **NOT done** |
| CT-604 Operations + Settings + utility (UI-015–020) | present | **FAIL** (contrast, UI-015 parent) | **NOT done** |

**No CT is recorded done. Phase P6 is not presentable at Gate 3.**

### Halt — specs do not answer this

U34 and U35 require changing `design-tokens.md`, which is a **Gate-2-approved artifact**. Per this
command's own rule (no architectural decisions, no silent choices), the build stopped here and put
the decision to the operator rather than picking a replacement colour pairing itself.

## P6 — fixes applied after the halt (2026-09-15)

Operator (Rashmi Mehta, 2026-09-15) answered the halt. Decisions and what was done:

**U34 — apply §1a's own light-bg/dark-text pattern to all four status pills.** `design-tokens.md`
§1b was rewritten: the old text and its wrong arithmetic are quoted and marked superseded, the real
measured values are tabled, and the correction is recorded. Three new tokens
(`color-error-subtle-text` rose-700, `color-info-subtle-bg` sky-50, `color-info-subtle-text`
sky-700) were added and bound in `styles.css`. `color-success`/`color-error`/`color-info` keep
their values — they stay correct for non-text uses (presence dot, "Live" marker, borders, icons).
`StatusPill`, the Fill-attendance selected radio and the `Badge` now cite only the new pairs.

**U35 — neutral pair added** as `design-tokens.md` §1c: `color-border-subtle` +
`color-neutral-subtle-text` (= `color-text-primary`), **15.4:1**. Applied to the neutral `Badge`
and the attendance "Locked" pill.

**Code-only fixes**, no spec question:
- `DataTable`, Schedule and Pricing scroll containers are now `tabIndex={0} role="region"` with a
  label and a visible focus ring (axe `scrollable-region-focusable`, WCAG 2.1 AA).
- `DropdownMenu.Root modal={false}` on the profile menu — a modal Radix menu `aria-hidden`s the
  rest of the page including its own trigger, removing the disclosure from the accessibility tree.
- **A real NFR-025 defect, found only once the contrast failures stopped masking it:** the charts'
  screen-reader-only `<table>` carried `sr-only` on the `<table>` itself. Table layout is auto-width
  and ignores the 1px box, so it stayed content-width and pushed document `scrollWidth` **2px past
  the viewport** on UI-010 at bp-lg/bp-xl. `sr-only` moved to a block `<div>` wrapper.

**Two test defects fixed (the product was correct in both):**
- `flows.spec.ts` UI-005 matched both the `aria-live` announcement and the toast — scoped to the toast.
- `a11y.spec.ts` UI-009 used `getByRole('table', { name: 'Students' })`; Playwright matches
  accessible names as a **substring** by default, so the chart's sr-only "Active students by class"
  table also matched. Pinned with `exact: true`. *(This accounts for 8 of the original 31 failures.)*

**Playwright output moved:** the junit report now lands in `.aidlc/03-development/test-results/`
beside the backend TRX, and traces go to `.playwright-artifacts/`. The old `test-results/` name
collided with `verify-gate.sh` C6 and failed the gate on an output directory rather than on
anything real.

## Phase P6 — phase-conformance (no tests run)

| Promised by 15-task-breakdown.md §4 | Present |
|---|---|
| CT-601 UI-001–006 (public marketing, auth modal, app shell) | yes — Home/About/Contact/Pricing, AuthModal, AppShell + PublicLayout |
| CT-602 UI-007–010 (classes, teachers, students, attendance) | yes |
| CT-603 UI-011–014 (announcements, meetings, chat, video call) | yes, **including the CT-304 carry-in**: UI-014 connecting/reconnecting/error states and TC-508/509 |
| CT-604 UI-015–020 (dashboard, schedule, reports, settings, unauthorized, not found) | yes |
| **DoD** CT-601 nav model matches `09-security-spec.md` §2 exactly | `navConfig.test.ts` — per-role nav sets; no Payment, no Leave, no role switcher; chat/video inherit Meetings access |
| **DoD** CT-602 role-gated icon actions match BR-029/BR-020 | `screens.test.tsx` — Teacher sees no add/view/edit/delete on Classes; leadership delete requires a destructive confirmation naming the class; BRL-034 no pre-selected outcome |
| **DoD** CT-603 reconnecting/error states present | `flows.spec.ts` drives connecting → connected → reconnecting → leave against the Q-234 fake ACS adapter |
| **DoD** CT-604 gauges absent; role-gated Settings verified | `screens.test.tsx` asserts the rendered DOM never matches /satisfaction\|completion/i and offers CSV only; non-leadership Settings has no configuration sections **in the DOM**, not merely hidden |
| Binding UI-beat constraints | both honoured — Reports gauges absent, Pricing shows no prices (Q-233) |
| Decisions | Q-231–Q-237 written into `_questions.md`, attributed to Rashmi Mehta |
| ADRs | none — no architecturally significant decision arose beyond Q-231–237 |

**Recorded deviations:** Q-236 — wherever the API exposes no data the screen omits the element
rather than inventing it (Classes sparkline, Meetings avatars, Reports performance charts, Teachers
staff-by-subject, Settings school profile / FR-064 actions, "Forgot password?"). Overlaps U33.

## Phase P6 — verification-gates(PHASE)

| Gate | Result |
|---|---|
| Frontend typecheck (`tsc -b`) | **PASS** — 0 errors |
| Frontend lint (eslint) | **PASS** — 0 problems |
| Frontend unit tests (vitest) | **PASS — 63 / 63**, 6 files |
| **Frontend e2e + a11y (Playwright, chromium)** | **PASS — 116 / 116 on two consecutive full runs** (was 85/31 at reconciliation). Report: `test-results/TestResults-P6-e2e-junit.xml` |
| **a11y scan AA — the CT-601–604 gate** | **PASS** — axe-core wcag2a/2aa/21a/21aa clean on all 26 screen states × 4 breakpoints (bp-sm/md/lg/xl), plus the no-horizontal-overflow assertion (NFR-025) |
| Backend build | **PASS** — 0 errors, 0 warnings |
| Backend tests | **PASS — 348 / 348**. Report: `test-results/TestResults-P6-phase-gate.trx`. P5 closed at 341; the +7 were written by the same unlogged 2026-09-14 session and are recorded here for the first time. |
| Mutation spot-checks | **3 built, all caught, all reverted.** (A) Present pill back to solid+white → a11y failed. (B) `sr-only` back onto the `<table>` → the overflow assertion failed. (C) `modal={false}` removed → the profile-menu disclosure test failed. |
| `dotnet format` | **PASS** |
| Backend dependency scan (transitive) | **PASS** — clean, all 5 projects |
| Frontend dependency audit (`npm audit`) | **PASS** — 0 vulnerabilities, with and without dev dependencies |
| Spec-suite hash at build | `e5c6b3d825a5` |

**Phase P6 is ready to present at Gate 3. This command does not approve it.**

**Gate 3 (Phase P6) approved by Rashmi Mehta, 2026-09-15.** Recorded with the approval:
- CT-601, CT-602, CT-603 and CT-604 are accepted, and the CT-304 carry-in from P3 (UI-014
  connecting/reconnecting/error states, TC-508, TC-509) is **closed**.
- A named waiver (owner Rashmi Mehta) covers `verify-gate.sh` **C5 FAIL** — the script reads
  `TestResults-P0-phase-gate.trx` via `head -n1`. Both P6 reports were independently verified
  newer than all their source. **C6 and C8 needed no waiver — a first for this run.**
- `design-tokens.md`, a Gate-2-approved artifact, was **amended on substance** under this gate by
  named operator decision (§1b corrected, §1c added). Recorded in
  `gates.gate-2-solution.artifact_hash_history`. Gate 2 is **not** reopened.
- **Q-232** (access token in memory only) and **Q-236** (omit UI wherever the API exposes no data,
  six screens, overlaps U33) were flagged as substantive at approval.
- U34 and U35 are **resolved**. U8–U33 stay **open** — 26 items, 8 Major.

---

# Phase P7 — Final integration

## CT-701 — full-suite security scan, dependency/CVE audit, NFR performance checks

**Status:** DONE — verified by execution. **Spec-suite hash at build:** `1ee14f1fd6a6`.

### The measurement problem, and what was built to solve it

CT-701's DoD is *"every NFR numeric target measured at least once against a realistic dataset"*.
The obstacle is that **every existing test host in this suite runs on the EF Core InMemory
provider** — no query planner, no indexes, no I/O. A p95 measured there says nothing about
NFR-006/007/022, and 14-test-spec §6's method column ("load test, realistic dataset size per
institution") can only honestly be read as a real database.

So `PerfApiFactory` was added: the **only** test host in the product that runs on real SQL Server.
It migrates a throwaway LocalDB from zero — migrate, never `EnsureCreated`, so the measurements run
against the schema that actually ships, indexes and all — and drops it afterwards.

`RealisticDataset` is sized from the product's own tier definitions rather than for convenience:
**1,000 learners** (the Mid-tier ceiling, `04-data-model.md` §2.10), 40 classes, 60 teachers each
with a linked account and a real class-teacher assignment, **30,000 attendance rows** (30 school
days), 200 announcements, a room with 500 messages.

**A vacuous first version, caught and fixed.** The first run reported a p95 of **0.5ms for a
1,000-row list** — not credible, and not real: the URLs were missing the `api/v1` prefix, so every
request was being served `index.html` by the SPA fallback and every assertion (`200 OK`) passed.
Every measurement now asserts the **payload** — exact row counts, the requested id, a non-empty
dashboard body — so a test that is not reaching the API cannot report a time. Mutation-verified:
pointing one URL back at the fallback fails the test.

### NFR measurements — all against the realistic dataset, on real SQL Server

| NFR | Target (14-test-spec §6) | **Measured** | Verdict |
|---|---|---|---|
| **NFR-006** list | p95 < 2000ms | `GET /students`, 1,000 rows: p50 **27.5ms**, **p95 62.0ms**, max 88.1ms | PASS |
| **NFR-006** detail | p95 < 2000ms | `GET /students/{id}`: p50 1.5ms, **p95 2.3ms** | PASS |
| **NFR-006** roster | p95 < 2000ms | `GET /classes/{id}/roster`: p50 3.0ms, **p95 4.8ms** | PASS |
| **NFR-007** dashboard | p95 < 3000ms | **cold 1440.4ms**; warm p50 133.3ms, **p95 232.4ms** | PASS — *cold is the tightest margin in the product, at 48% of budget* |
| **NFR-008** chat fan-out | p95 < 1000ms | 25 real SignalR connections in one room, send to **all 25** received: p50 4.6ms, **p95 107.9ms** | PASS |
| **NFR-022** attendance peak | no degradation at peak | baseline single class 488.5ms; **39 classes concurrently**, each marked by **its own class teacher**: wall clock 346.3ms, per-request p50 180.6ms, **p95 249.0ms**. All 1,000 registers verified landed. | PASS |
| **NFR-020** concurrent institutions | no leakage or contention at N=50 | **50 institutions queried concurrently in 172.3ms**; every tenant's student set asserted **set-equal to its own** — no extra rows (leakage) and none missing (contention) | PASS |

**NFR-022 read explicitly:** "no degradation" is taken as *under peak, a bulk mark still meets the
NFR-006 interactive ceiling*. A stricter multiple-of-baseline rule would measure this machine's core
count rather than the product. Stated so the next reader can disagree with the reading rather than
having to reverse-engineer it.

### NFR numeric targets NOT measured — recorded, not assumed

| NFR | Why not measured |
|---|---|
| **NFR-010** 99.5% availability | An SLO observed over a production window, not a test. `12-infrastructure-spec.md` owns it; no environment exists to measure it in. |
| **NFR-013** daily backup + recovery objective | Same — a deployed-infrastructure property. No backup schedule is provisioned anywhere yet. |
| **NFR-023** storage within tier allowances | **Not measurable: the storage gate is not built** (U32), because no storage-metering source exists (Q-227). |
| **NFR-033** retention period | **Not measurable: the retention sweep is a scaffold** (U27, ADR-0008) — it counts, and deletes nothing. |
| **NFR-021** capacity floor/ceiling | Enforced and tested as a rule (CT-503, and now CT-701's race test), but "under 200 to unlimited" is a tier definition, not a latency target. |

### Full-surface security sweep — `FullSurfaceSecurityTests`

Each phase gate checked the endpoints *that phase* added. This asks the whole-surface question, by
reflection over the shipped assembly so it cannot rot:

| Check | Result |
|---|---|
| Every endpoint explicitly `[Authorize]` or `[AllowAnonymous]` (NFR-014/015) | **PASS — 88 endpoints swept, 0 unprotected** |
| The anonymous surface is exactly the approved set | **PASS — exactly 7**: Login, Register, Refresh, `PlatformSummary` (ADR-0006 aggregates), `PublicInstitutionDirectory.Search` (NFR-016's single bounded exception), `Subscriptions.Plans`, `Subscriptions.Enquire`. Pinned, so widening the public surface now fails a test. |
| Every audited entity still carries `IAuditable` (09-security-spec) | **PASS** — 12: Announcement, AttendanceRecord, Class, CorrectionRequest, Institution, InstitutionConfiguration, Message, ReportRun, Student, SubscriptionPlan, Teacher, TimetablePeriod |
| No literal secret in shipped configuration (12-infrastructure-spec CFG-) | **PASS** — 2 config files swept |

### U29 closed — and it was real

`15-task-breakdown.md` assigns U29 to CT-701, and U29 names its own remedy ("a serializable
transaction or row lock"), so no new decision was invented.

**The defect was exploitable.** Measured on real SQL Server, Entry plan, 199 of 200 seats taken, 8
concurrent admissions: **all 8 succeeded — 207 active learners against a capacity of 200**, with no
error raised to anyone. A silent seven-seat giveaway across a commercial boundary, repeatable on
demand. It survived five phase gates because the InMemory provider has no locking, so the race
cannot occur — or be observed — there.

**Fixed:** the capacity check and the write it guards now run in one `Serializable` transaction
(`ISerializableTransaction`), on both seat-consuming paths — `EnrolAsync` and `ReactivateAsync`.
**After: 1 created, 7 x `ERR-801`, exactly 200 active.**

Two things the fix surfaced, both recorded rather than papered over:

- **A deadlock appeared on the first real run**, exactly as SERIALIZABLE implies (two range S locks,
  each then wanting X to insert; error 1205). A deadlock victim is not a business answer, so it is
  retried — 5 attempts, linear backoff, change tracker cleared between. My first version claimed
  "the execution strategy owns the retry"; that was **wrong** — the default strategy does not retry
  — and the code was corrected to do it explicitly.
- **`IsRelational()` is the wrong guard, and caused a 22-test regression.** It returns *true* on the
  InMemory test hosts, because they call `AddDbContext` a second time to swap providers and the
  SqlServer extension stays in the options. `IsRelational()` describes the options; `IsSqlServer()`
  describes the provider in use. Guard corrected; 358/358 green.

**ADR-0009** records the general rule: an invariant over an *aggregate* is serialised, not
constrained — `04-data-model.md` §4 resolves all four of its races at a unique index, and
`COUNT(...) <= capacity` cannot be expressed as one. The next such rule is the unbuilt storage gate.

### New upstream feedback

**U36 (Major)** — `04-data-model.md` §4 has no category for aggregate invariants, which is *why*
U29 existed; needs a fifth bullet so the next one is not written as another check-then-act.
**U37 (Minor)** — no decision exists on connection-level `EnableRetryOnFailure`; deliberately not
enabled as a side effect of a bug fix, since it changes retry behaviour for every query in the
product.

## Phase P7 — phase-conformance (no tests run)

| Promised by 15-task-breakdown.md §4 | Present |
|---|---|
| CT-701 full-suite security scan | yes — `FullSurfaceSecurityTests`, 4 sweeps, 88 endpoints |
| CT-701 dependency/CVE audit | yes — backend transitive scan across all 5 projects, plus `npm audit` on the frontend |
| CT-701 NFR performance checks | yes — 5 scenarios measured, 4 recorded as not-measurable with reasons |
| **Gate:** all gates green, `verify-gate.sh` PASS | **1 FAIL (C5)** — see below. Everything else green. |
| **DoD:** every NFR numeric target measured at least once against a realistic dataset | met for all 5 measurable; the other 4 recorded as **not measured, with the reason**, never assumed met |
| ADRs | ADR-0009 |
| U29 (assigned to CT-701 by the task breakdown) | closed, mutation-proven |

## Phase P7 — verification-gates(FINAL)

| Gate | Result |
|---|---|
| Backend build | **PASS** — 0 errors, 0 warnings |
| Backend tests | **PASS — 358 / 358 on two consecutive full runs**. Report: `test-results/TestResults-P7-final.trx` |
| Frontend typecheck / lint / unit | **PASS** / **PASS** / **PASS 63/63** |
| Frontend e2e + a11y (axe WCAG 2.1 AA) | **PASS — 116/116** |
| **NFR performance checks** | **PASS** — 5 of 5 measurable targets met, on real SQL Server against the realistic dataset |
| **Full-surface security scan** | **PASS** — 88 endpoints, 0 unprotected; anonymous surface pinned at 7 |
| Backend dependency/CVE audit (transitive) | **PASS** — clean, all 5 projects |
| Frontend dependency audit | **PASS** — 0 vulnerabilities, with and without dev dependencies |
| `dotnet format` | **PASS** |
| Migration-from-zero | **PASS** — all 10 migrations applied to a fresh LocalDB; temporary database dropped, **0 left over** |
| Mutation spot-checks | **3 built, 3 caught, all reverted** — (A) SPA-fallback URL, the NFR-006 payload assertion failed; (B) serialisation removed, 8 created / 207 active; (C) the 22-test `IsRelational()` regression, found by running the suite rather than by reasoning about it |
| `verify-gate.sh` | **1 FAIL (C5), 0 WARN** |

### `verify-gate.sh` C5 — a tooling defect, stated precisely

CT-701's gate is `verify-gate.sh` **PASS**, so C5 is reported as a FAIL rather than quietly
inheriting the P1-P6 waiver. This time the script's own code was read, not just its message:

    report="$(find "$ROOT" -type f \( -name 'TestResults*.trx' -o ... \) | head -n1)"

It takes **the first report the filesystem happens to return, not the newest**, then compares that
one against the newest source file. With seven `TestResults*.trx` in the directory it picks P0's,
from 2026-09-11. The fix belongs in the plugin, not here — and deleting or renaming the older
reports to satisfy it would mean destroying the evidence pinned in the P0-P6 gate records.

Independently verified instead: `TestResults-P7-final.trx` postdates every `.cs` file in the repo,
and the frontend junit report postdates every frontend source file. **Both reports are current; the
script is reading the wrong file.** This needs a named waiver at the gate.

**C4 also SKIPs** (`no 14-test-spec.md`) for a related reason — the test spec is a *sharded folder*,
`14-test-spec/`, and the script looks for a flat file. Worth knowing, because C4 is the check that
would otherwise confirm every `TC-` exists as a real test.

**Phase P7 is ready to present at Gate 3. This command does not approve it.**

**Gate 3 (Phase P7) approved by Rashmi Mehta, 2026-09-15.** Recorded with the approval:
- CT-701 is accepted. **All eight build phases P0–P7 are now closed, and Stage 3 (development) is
  complete.**
- A named waiver (owner Rashmi Mehta) covers `verify-gate.sh` **C5 FAIL** — a confirmed plugin
  defect (`find ... | head -n1` takes the first report, not the newest). Both P7 reports were
  independently verified newer than all their source.
- **Recorded as a known gap, not a pass:** C4 SKIPs because `14-test-spec` is a sharded folder and
  the script looks for a flat file. C4 is the check that would confirm every `TC-` exists as a real
  test, so that assurance is **absent**.
- U29 is closed and was real. U36 (Major) and U37 are new and open.
- 27 upstream items remain open, 9 Major.

**Next: Stage 4 — independent code review. No code in this repository has ever been reviewed.**

---

# Phase P8 — Gate 4 fix round (spec 1.0.5, hash 80fe55d28923)

## CT-801 — privileged-role provisioning by leadership code (U38 / REV-001, Critical)

### Step 0 — TC-209 observed RED against the pre-fix implementation

CT-801's gate requires this, and it is the reason the step exists: *a regression test that has
never been seen failing proves nothing*. This run has already produced one suite that passed
vacuously, so the red observation is recorded here as evidence rather than asserted afterwards.

**The Critical is no longer read-derived. It is executed and proven.** TC-209 drives the exact
two-call sequence REV-001 used — read an institution `id` from the anonymous public directory
(API-005), then POST it to `/api/v1/auth/register` with a privileged role:

| Attack input | Pre-fix result |
|---|---|
| `"role": "Administrator"` | **201 Created**, live `accessToken`, decoded claims `role=Administrator`, target `institution_id` |
| `"role": "SchoolInCharge"` | **201 Created** |
| `"role": "Teacher"` | **201 Created** |
| `"role": "Principal"` | **201 Created** |
| `"staffCode": ""` + `"role":"Administrator"` | **201 Created** — the empty-code fallback is real |
| `"staffCode": "   "` + `"role":"Administrator"` | **201 Created** |
| `"role": 99`, `2`, `"0"`, `"ADMINISTRATOR"` | 400 — the enum parse already rejected these |

The response body for a successful attack is a complete session:
`{"accessToken":"eyJ…","user":{"role":"Administrator","institutionId":"…"}}`.

**A fixture defect was found and fixed while establishing this, and it is worth recording because
it is the failure mode this project keeps hitting.** The first red run failed *every* case —
including the positive control — which is the signature of a test failing for the wrong reason. It
was: API-005 reads a **projection table** refreshed on institution create/update/withdraw (Tech
Design A5), not the `Institution` table, and the baseline seeder writes only the latter, so the
directory was empty. Had the reason not been read, TC-209 would have been "red then green" while
proving nothing at all. The fixture now seeds the projection.

**Only steps (1) and (3c) were red.** Steps (2) and (4) — no `UserAccount` row, and no access to
learner PII — could not even be reached, because the attack succeeded outright at step (1).

### Step 1 — the fix, and TC-209 RED → GREEN

**The Critical is closed in code.** TC-209 and its siblings: **15/15 green**, after being observed
red above. Full backend suite **373 / 373 on two consecutive sequential runs** (was 358; +15).

| Built | Where |
|---|---|
| `LeadershipCode` entity — lifecycle copied from `ParentLinkCode`, **storage not** | `Domain/Entities/LeadershipCode.cs` |
| `code_hash` (BCrypt) + `code_lookup` (`char(43)`), `Status` with `Expired` derived at read time | same, + `SchoolSyncDbContext.ConfigureLeadershipCode` |
| Filtered unique index on `CodeLookup` while `Status='Issued'` | migration `20260915132144_LeadershipCodeProvisioning` |
| `ILeadershipCodeLookup` / `LeadershipCodeLookup` — **CFG-SEC-06, fail-closed at construction** | `Application/Common`, `Infrastructure/Auth` |
| **API-006 rewritten to BRL-075's two branches** | `AuthService.RegisterAsync` |
| ERR-206/207/208 (410/410/409) | `Errors/ErrorResponse.cs`, `AuthController` |
| `UserAccount.provisioned_by_code_id` + filtered index (Q-241) | `UserAccount`, same migration |

**Three things are worth recording because they were judgement calls, not transcription:**

1. **The enforcement moved into the service, and the old comment is why.** `AuthService` carried:
   *"enforced at the DTO/controller boundary, not here, since Role is a closed enum and this
   service trusts its caller already filtered the offered choices"*. **That assumption was the
   Critical.** The caller is an anonymous HTTP request; it filters nothing. VAL-007's allowlist now
   lives in the service, where the account is actually created.

2. **The controller's role parse became LENIENT on purpose.** It used to reject any
   non-letter-initial / undefined / PlatformOperator role at the boundary — and then hand every
   *other* role straight through to account creation. Now an unparseable role becomes `null` and
   the decision moves to the service, because on the code branch a conflicting body role must be
   **ignored, not rejected** (E4) — rejecting would tell a prober which institution a code belongs
   to.

3. **`TryConsumeCodeAsync` is conditional on the row still being `Issued`**, with the
   `DbUpdateConcurrencyException` treated as a normal lost race returning ERR-206. An
   unconditional load-mutate-save would let two concurrent redemptions both create an account from
   one code — the same check-then-act shape as U29, which is still fresh enough to recognise.

**Three existing tests were changed, and the change is disclosed rather than buried:**
`AuthEndpointsTests` registered as `Teacher` purely to obtain a session. `Teacher` is no longer
self-selectable (VAL-007), so those three now register as `Student`. Their subject is session
mechanics — register/login/refresh/logout — not roles, and the role assertion still round-trips.
**No test was weakened to accommodate the fix**; TC-209 is strictly stronger than anything that
existed before it.

**A flake investigated and dismissed, honestly:** one intermediate run showed `Nfr022` failing.
Re-run three times in isolation it passed at p95 229–251ms against a 2000ms target — nowhere near
the threshold. Cause: two full suites launched back-to-back in one shell command, the second
overlapping the first's LocalDB teardown. **My invocation pattern, not a product flake.** Confirmed
by two strictly sequential full runs, both 373/373.

### CT-801 is NOT done — what remains

| Remaining | Status |
|---|---|
| API-065/066/067/068 — issue (PlatformOperator), issue (leadership), revoke, list | **not built** |
| API-001 returns the first Administrator code (the bootstrap) | **not built** |
| UI-005's two-mode Register panel | **not built** |
| TC-210, TC-211, TC-212, TC-215, TC-216, TC-217 | **not written** |
| ADR-0010 — the anonymous-redemption `IgnoreQueryFilters()` exception | **not written** |
| CT-802 (rate limiting) | **not started** |

**The vulnerability itself is closed and proven.** What remains is the machinery around it — issuing
codes, revoking a leaked one (the only containment control, U46), the bootstrap path, and the UI.
Until API-065/066 exist, **no privileged account can be created at all**, which is safe but not yet
usable.
