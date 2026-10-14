<!-- pipeline-provenance
produced_by: code-review (code-reviewer, Mode B — cross-CT phase-gate pass over the closed build P0–P7)
generated: 2026-09-15
spec_suite_version: 1.0.3  (suite hash 1ee14f1fd6a6, per 03-development/build-log.md — matches C8)
note: Rolling file. Append "## Round N" below; never write a _v2/_final/dated copy (verify-gate.sh C10).
-->

# Code Review: Gate 4, whole product

## Round 1 (2026-09-15)

### Mode, context, and independence

This review used Mode B (cross-CT phase-gate), widened to the whole product. All eight build phases are closed. Nobody had reviewed the code in this repository before. This is the first independent look at the implementation.

`.aidlc/04-code-review/Spec_Audit.md` (Mode D, 2026-09-11) audited the specs and did not read code. This review does not repeat it.

`_config/project.md` §1a sets `team_mode: team`, so a shared session was allowed. I did not use one. I ran in a fresh session with no build context. I read only these inputs:

- the tree (the equivalent of a diff)
- the spec suite
- the config
- `_upstream-feedback.md`
- the ADRs
- the build log

I did not read the conversation that produced the code. `project.md` §5 notes that the reviewer and the author are the same person. That is a question for the operator. This pass cannot fix it.

### What I could not check, and why

| # | Not checked | Why |
|---|---|---|
| 1 | The 116 Playwright tests | I did not run them. They need a Chromium download and a 180s Vite build and preview server, which this session could not run. I read every line of `e2e/a11y.spec.ts`, `e2e/flows.spec.ts` and `e2e/mockApi.ts` instead. **REV-005** covers what these tests cannot prove even when they pass. |
| 2 | Anything against real Azure | This covers ACS (meetings and tokens), Azure SignalR scale-out, Blob storage, Application Insights, and Hangfire on SQL Server storage. No resource exists. U18, U19 and U25 already carry this. Nothing I did changes their status. |
| 3 | Production config binding | `appsettings.Production.*`, Key Vault references and the real `Jwt:SigningKey` path are not in the tree. I checked the pattern: lazy options resolution, no hardcoded key, and the `company.md` §4 message when the key is missing. I did not check a real deployment. |
| 4 | Runtime behaviour of the 7 anonymous endpoints on a live host | I read the controllers, services, repositories and the tenant filter. **REV-001 comes from reading the code. I did not run an exploit.** I am confident in the trace and give the exact calls below. An operator should still confirm with one curl before acting. |
| 5 | The conclusions of the Mode D spec audit | Out of scope here. Already recorded. |
| 6 | SQL Server-only schema behaviour beyond what the model declares | I read the ten migrations. I did not apply them to a clean database. "Migrations apply clean from zero" is unverified. |

### Test suites: re-run, not trusted

Both test counts in the brief are real. I re-ran them:

```
dotnet test tests/SchoolSync.IntegrationTests
  Passed! - Failed: 0, Passed: 358, Skipped: 0, Total: 358, Duration: 5 m 4 s
```

Skipped is 0. So `CapacitySerialisationTests` and `NfrPerformanceTests` ran against real SQL Server (LocalDB). They were not silently skipped.

```
npx vitest run --pool=threads
  Test Files  6 passed (6)       Tests  63 passed (63)
```

The default `forks` pool fails in this sandbox with "Timeout waiting for worker to respond". It then runs 0 tests and still exits 0. The cause is a limit on process forking in this environment, not a defect. But look at the exit code. A CI runner that lost its fork ability would report this build as green with nothing run. Pin `test.pool` in `vite.config.ts`.

### `verify-gate.sh --phase P7`

```
C1  PASS  214 requirement ids found
C2  PASS  no unconfirmed must-haves
C3  PASS  all active requirement ids appear somewhere in the spec suite
C4  SKIP  no 14-test-spec.md   (it is sharded into 14-test-spec/ — script limitation)
C5  FAIL  test report is older than source (TestResults-P0-phase-gate.trx predates CallerClaims.cs)
C6  PASS  every test directory contains at least one test file
C7  PASS  sourcing markers present
C8  PASS  build log records spec-suite hash 1ee14f1fd6a6
C9  PASS  no oversized flat spec file
C9  FAIL  sharded folders missing from shard map: Management x6
C10 PASS  one review file per phase; no CT needed 3 or more attempts
=== summary: 2 FAIL, 0 WARN ===
```

Read both FAILs correctly before you act on them:

- **C9 is a script bug, not a finding.** The repo path has a space (`School Management System`). The check splits the path at the space. "Management" is the six phantom folders it reports. The shard map in `00-spec-index.md` is intact. Do not chase this.
- **C5 is real but small.** The stored TRX file is stale. I re-ran both suites above and they pass at current HEAD, so the substance is fine. The gate record is not honest until the artifact is regenerated.

---

## Verdict

# CHANGES-REQUIRED

I do not approve this gate and I am not recording an approval. A named human closes Gate 4, per the brief. One Critical finding blocks: an unauthenticated caller can get institution-administrator access to any tenant on the platform.

Apart from that, the engineering is unusually disciplined:

- Every concurrency guard that `04-data-model.md` §4 promises exists as a real index in the model.
- ADR-0009 and `CapacitySerialisationTests` are excellent. The test is adversarial, runs on real SQL Server, and would fail against the pre-fix code. The InMemory hosts used elsewhere in this suite cannot do that.
- The token layer has no hardcoded visual values.

That is why the list has seven items and not forty.

---

## Findings

Severity levels:

- **Critical** blocks the gate.
- **Major** must be fixed, or waived in writing by a named owner.
- **Minor** should be scheduled.

Each finding has one label:

- CODE DEFECT: fix the code.
- SPEC DRIFT: the spec moved or is wrong. Route it upstream. Do not force correct code to match it.

Some findings are both, and say so.

| ID | Sev | Class | Where |
|---|---|---|---|
| REV-001 | Critical | CODE DEFECT (follows a wrong spec, see note) | `AuthController.cs:45-68` |
| REV-002 | Major | CODE DEFECT + SPEC DRIFT | whole API, no rate limiting exists |
| REV-003 | Major | CODE DEFECT | `SchoolSyncDbContext.cs` (no `ConfigureTeacher`) |
| REV-004 | Major | CODE DEFECT | whole Application layer |
| REV-005 | Major | TEST INTEGRITY | `SchoolSync.Web/e2e/mockApi.ts`, `src/test/render` |
| REV-006 | Minor | PROCESS | `.aidlc/03-development/test-results/` |
| REV-007 | Minor | CODE DEFECT (class 13) | 3 components |

---

### REV-001: Critical. An anonymous caller can make themselves Administrator of any institution

**CODE DEFECT.** The code correctly follows `05-api-contracts/identity-access.md` API-006 and `02-requirements-spec/identity-access.md` SPEC-FR-006. I am not asking for the code to change to match a different spec. The specified behaviour is a critical authorization hole, and the spec and the code must change together. Routed upstream as **U38**.

**Location:** `src/SchoolSync.Api/Controllers/AuthController.cs:45-68`, with `src/SchoolSync.Application/Auth/AuthService.cs:18-44`.

`POST /api/v1/auth/register` is `[AllowAnonymous]`. It takes `Role` and `InstitutionId` from the request body:

```csharp
public record RegisterBody(string Username, string Email, string Password, string Role, Guid InstitutionId);
```

The only role check excludes `PlatformOperator` (`AuthController.cs:52-54`). The code accepts `Administrator`, `Principal` and `SchoolInCharge`. These are the three members of `Roles.Leadership`.

`AuthService.RegisterAsync` does none of these checks:

- institution-side approval
- email verification
- domain check
- invitation check

It writes `InstitutionId = request.InstitutionId` straight onto the account (`AuthService.cs:30`). It then issues a live session at once (`AuthService.cs:41-43`, "no approval step").

The code builds the tenant claim from this self-declared value (`JwtTokenService.cs:27`). `HttpCurrentTenant` uses the claim to resolve the global query filter (`HttpCurrentTenant.cs:12-21`). The filter works exactly as ADR-0001 describes. The problem is that it treats the attacker as a real member of the tenant. NFR-016 says isolation is absolute. That holds in the structure, and the registration endpoint bypasses it.

**The attacker can get the institution ids without logging in.** `PublicInstitutionDirectoryController.Search` (`InstitutionsController.cs:123-137`) returns `{ id, name, city, region }` for every active institution. It needs no authentication. The `id` was added on purpose at Q-235 so the registration form could send it.

**Failing case: two unauthenticated requests**

```
1)  GET  /api/v1/public/institutions?query=Green
    200 -> [{ "id":"<INST>", "name":"Green Valley School", "city":"Jaipur", "region":"RJ" }]

2)  POST /api/v1/auth/register
    { "username":"attacker", "email":"a@x.test", "password":"Correct-Horse9",
      "role":"Administrator", "institutionId":"<INST>" }
    201 -> { "accessToken":"...", "user":{ "role":"Administrator", "institutionId":"<INST>" } }
```

That token passes `[Authorize(Roles = Roles.Leadership)]` on every leadership endpoint in the product. With it, the attacker can read and write that school's data:

- learner and guardian personal data (`StudentsController`)
- staff records (`TeachersController`)
- attendance and corrections
- timetable
- announcements
- institution configuration
- academic-year rollover
- parent-link code issuance

Expected behaviour: a caller cannot give themselves a privileged role in an institution they have no relationship with.

**Why no test caught it:** `AuthEndpointsTests` covers how roles are parsed (`Registering_with_a_numeric_or_non_exact_role_string_is_rejected`) and the PlatformOperator exclusion. No negative case says "an anonymous caller picks a leadership role", because the spec says that is a feature. The negative-authorization matrix in `09-security-spec.md` §5 only considers authenticated callers with the wrong role. This attack is outside that frame. The frontend limits the dropdown (`screens.test.tsx`, Q-202), but the dropdown is not a security boundary.

**Proposed fix (described, not applied).** This is a decision for the operator and the spec owner, not for me. The smallest change that closes the hole without redesigning onboarding:

- Let anonymous self-registration create only non-privileged roles: `Student`, `Parent`, and maybe `Teacher`.
- Create `Administrator`, `Principal` and `SchoolInCharge` through a logged-in path. Either the PlatformOperator creates the first administrator when the institution is created (that first administrator has to come from somewhere), or an existing leadership user of that institution sends an invitation.

FR-006's "no approval step" can stay for the roles it was written for. Whatever you choose, correct SPEC-FR-006 and API-006 in the same change (**U38**).

---

### REV-002: Major. No rate limiting exists, so ERR-904 can never be returned

**CODE DEFECT + SPEC DRIFT.** The spec half is routed upstream as **U39**.

`08-error-catalogue.md:90` defines:

> `| ERR-904 | 429 | "You're doing that a bit too fast..." | Rate limiting (auth endpoints, per 09-security-spec.md) | NFR-017 |`

There are three facts:

1. **`09-security-spec.md` has no rate-limiting section.** A search of it for `rate limit | throttl | lockout | brute` finds nothing. The catalogue points to an authority that never wrote the rule. (SPEC DRIFT → U39.)
2. **No rate limiter is registered.** `grep -rn "RateLimit\|AddRateLimiter" src` over the whole backend returns zero hits. `UseRateLimiter` is not in the pipeline.
3. **`ErrCatalogue` has no `Err904`.** The 9xx band holds `Err901`, `Err902` and `Err903` only. No code path in the product can return ERR-904.

**What follows, most serious first:**

- `POST /api/v1/auth/login` accepts unlimited password attempts against a known username. BCrypt slows each attempt but does not limit the number. There is also no account lockout: `LoginAsync` (`AuthService.cs:46-57`) has no failed-attempt counter. This goes directly against **NFR-017**.
- `POST /api/v1/auth/register` accepts unlimited account creation. This makes **REV-001** worse. An attacker can script the attack across every institution in the public directory in one pass.
- `POST /api/v1/rooms/{id}/join` accepts unlimited passcode guesses. This is **U16**, now measured. U16 asked whether a limit "belongs in CT-701". CT-701 did not add one, and no other endpoint got one either.

Expected behaviour per NFR-017 and the catalogue: auth endpoints return `429 ERR-904` after a set number of attempts. The threshold must be written in `09-security-spec.md` first. It is not a reviewer's call. Then build it with ASP.NET Core's `AddRateLimiter`, a `CFG-` key for the limit, `ErrCatalogue.Err904`, and negative tests.

---

### REV-003: Major. `Teacher.RowVersion` is not a concurrency token, so teacher edits lose updates silently

**CODE DEFECT.** No upstream routing. `04-data-model.md` §4 is correct and the code does not implement it.

`04-data-model.md` §4, last bullet:

> "**Optimistic concurrency:** all mutable entities carry a `rowversion`/`xmin`-equivalent
> concurrency token (SQL Server `rowversion`) to detect lost-update races on ordinary
> edit-in-place operations (institution profile, **teacher/student edits**, configuration)."

`Teacher.cs:23` declares `public byte[] RowVersion { get; set; } = [];`. But `SchoolSyncDbContext.OnModelCreating` has no `ConfigureTeacher` method. No `IsRowVersion()` call exists for `Teacher` anywhere. Four entities are configured (`SchoolSyncDbContext.cs:121, 155, 172, 293`): Institution, Student, Class and InstitutionConfiguration. Teacher is not one of them.

The generated schema shows the result. In `Migrations/20260911152338_InitialCreate.cs`, the `Teachers` table has:

```csharp
RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
```

The Institutions, Students, Classes and InstitutionConfigurations tables (lines 97, 169, 280, 318) have:

```csharp
RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
```

So on `Teacher` the column is a plain `varbinary(max)`. SQL Server never stamps it. EF never adds it to the `WHERE` clause of an `UPDATE`. `SchoolSyncDbContextModelSnapshot.cs` has only four `IsConcurrencyToken()` entries. The property looks like protection but does nothing.

**Failing case.**

1. Administrator A opens teacher *Anita Verma* and changes `Telephone`.
2. Principal B opens the same record and changes `Address`.
3. A saves, then B saves.
4. B's `UPDATE` sends every mapped column, so A's telephone change is silently overwritten with the value B loaded before A saved.
5. Both callers get `200 OK`.

On `Student`, which is one table over with the same edit shape, B would correctly get a concurrency failure. None of the 358 tests covers this, and none could. `IsRowVersion()` does nothing on the EF Core InMemory provider. So even the four correctly configured entities have no test for it.

**Fix:** add a `ConfigureTeacher` mapping with `e.Property(t => t.RowVersion).IsRowVersion();` and a migration that changes the column to `rowversion`. Then fix REV-004 too. Otherwise the working token will start returning 500 errors.

---

### REV-004: Major. A detected lost-update race returns an unhandled 500, not a catalogued error

**CODE DEFECT**, with a gap in the spec behind it (routed as **U40**).

`grep -rn "DbUpdateConcurrencyException" src tests` returns zero hits in the whole solution.

`04-data-model.md` §4 says the system must detect lost-update races. It does not say what the API returns when it detects one. No `ERR-` in `08-error-catalogue.md` covers a concurrency conflict. The 4xx conflict codes are all tied to one domain case: ERR-203 duplicate account, ERR-303 class-teacher, ERR-307 code consumed. So the build detected the race correctly and had no error to map it to.

**Failing case.** Take `Institution`, `Student`, `Class` or `InstitutionConfiguration`, the four entities where the token is configured. Two leadership users edit the same row and their saves interleave. The second `SaveChangesAsync` finds 0 rows affected, and EF throws `DbUpdateConcurrencyException`. No service catches it. No controller catches it. No exception-handling middleware maps it. The caller gets a raw **500**, and in Development a stack trace. Expected: a catalogued `409` that tells the user the record changed and they should reload. That is the whole point of having the token.

The suite cannot see this for the same reason as REV-003. `IsRowVersion()` does nothing on InMemory, so 357 of the 358 tests can never raise the exception.

**Fix:** add a concurrency-conflict code to `08-error-catalogue.md` (**U40**). Then catch `DbUpdateConcurrencyException` once, in an `IExceptionHandler` or an action filter, and map it there. Use one place, not one per service.

---

### REV-005: Major. Every frontend test checks against a mock that nobody has compared to the API

**TEST INTEGRITY.** This is not a spec violation or a runtime defect. It says what 179 passing tests actually prove. This run has confirmed this failure mode as its most serious one.

`playwright.config.ts` says so directly: *"the SPA built in `e2e` mode ... against a mocked API — every `/api` and `/hubs` request is intercepted in the spec, so no backend is needed."* All 116 Playwright tests, including the axe WCAG 2.1 AA scan across 26 screen-states at 4 breakpoints, run against `e2e/mockApi.ts`. All 63 vitest tests run against the `mockApi` in `src/test/render`. Both mocks are hand-written fixture literals. The same session that wrote the components also wrote the mocks that feed them.

**Nothing ties those mocks to the real controllers.** There is no contract test, no generated client, no OpenAPI schema check and no shared type package. `mockApi.ts` states its response shapes as object literals. If `AttendanceController.ToBody` renames a field tomorrow, all 116 tests still pass and the app breaks in the browser.

I checked three shapes by hand and they currently agree: `GET /classes` (`{id,name,section,classTeacherId}` against `ClassesController.cs:21-27`), the attendance record, and `GET /public/plans`. I am not claiming a live mismatch. I am saying that nothing would tell you if one appeared. This is one level above CT-701's own incident. There, the URLs lacked the `api/v1` prefix, every request got the SPA fallback page, and the test only asserted `200 OK`. That specific problem is now closed: `mockApi.ts:64` routes `**/api/v1/**` and `lib/api.ts:84` fetches `/api/v1${path}`, so a prefix regression would fail loudly.

**What is missing:** no test in this repository runs the React application against the running .NET API. The backend has 358 tests and the frontend has 179. The connection between them has none.

*Credit where it is due:* the existing tests are not empty. `screens.test.tsx` checks payloads, not just status codes (`expect(calls.find(c => c.path === '/auth/register')?.body).toMatchObject({...})`). `a11y.spec.ts` asserts that `results.violations` equals `[]` and prints a readable summary, instead of a bare truthy check. It also checks separately that nothing overflows horizontally. P6's suite had 31 failures that had never been run, so I looked hard for fixtures shaped to pass. I found none. This finding is about the missing connection, not about the tests that exist.

**Fix:** add one thin integration suite that boots the API (`WebApplicationFactory`, following the SQL Server `PerfApiFactory` pattern). It should check the JSON shape of each endpoint the SPA uses, against the same literals `mockApi.ts` serves. That is about one assertion per mocked route. It makes the mock something a test can prove wrong.

---

### REV-006: Minor. Gate artifacts are stale (verify-gate.sh C5)

The stored `TestResults-P0-phase-gate.trx` is older than `CallerClaims.cs`. So the gate's machine evidence is older than the code it certifies. Both suites pass at current HEAD (re-run above), so this is about how fresh the artifact is, not about substance. Still, a gate record that cites a stale TRX is not honest. Regenerate both reports at the gate. Also pin `test.pool` in `vite.config.ts`, because the forks pool exits 0 with zero tests run (see above).

The C9 FAIL that came with it is a script bug caused by the space in the repo path. See the gate section. No action.

---

### REV-007: Minor. Three arbitrary pixel values in an otherwise literal-free token layer

Class 13. `design-tokens.md` requires every visual value to cite a named token. `styles.css` enforces this unusually well: `--color-*: initial` turns off Tailwind's default palette, so nobody can write an off-token colour by accident. Three arbitrary values get through:

- `src/SchoolSync.Web/src/components/ui/index.tsx:247`: `min-w-[640px]`
- `src/SchoolSync.Web/src/pages/app/Schedule.tsx:68`: `min-w-[760px]`
- `src/SchoolSync.Web/src/pages/public/Pricing.tsx:72`: `min-w-[640px]`

`640px` equals `--breakpoint-sm` (40rem). `760px` matches no token. Under the token rule, a one-off value that happens to match a token is still a literal. **Fix:** use `min-w-sm` for the first and third. For Schedule, add a named `--spacing-table-min` token or use the nearest breakpoint.

---

## The six areas the brief flagged: what I found

**1. Test quality and empty tests.** I re-ran both suites and they are real (358/358, 63/63). I found no empty fixtures. The assertions check payloads, not just status. The problem that does exist is structural, not empty tests: **REV-005**. One side risk: vitest's default forks pool exits 0 after running nothing in a restricted environment.

**2. The InMemory blind spot: what else does it hide?** Less than I feared, with one real exception. Every concurrency guard that `04-data-model.md` §4 promises exists as a real index in `SchoolSyncDbContext`:

- attendance `(PersonId, AttendanceDate)` unique (line 196)
- class-teacher filtered unique (165)
- all three timetable conflict uniques (273-275)
- `CorrectionRequest` filtered unique on Pending (219)
- `Message` `ClientMessageId` filtered unique (240)
- `Meeting` one-live-per-room (252)
- `ParentLinkCode.Code` (330)
- institution triple (117)
- student SR and roll (150-151)
- `RoomMembership` composite PK `(RoomId, UserId)`, so a concurrent double join cannot create a duplicate

These indexes back the check-then-act sequences. The uncovered area is optimistic concurrency. `IsRowVersion()` does nothing on InMemory, so REV-003 (missing on Teacher) and REV-004 (no handler at all) both sit in that blind spot, just as U29 did. The next gap after that is the storage-allowance gate that has not been built (FR-073 / U32). U36 already names it as the next aggregate invariant.

**3. Multi-tenancy.** The design is sound. `ApplyTenantQueryFilterConvention` applies the filter by reflection over `ITenantOwned`. It does not rely on each query being written carefully (`SchoolSyncDbContext.cs:88-102`). I checked every `IgnoreQueryFilters()` call site in `src/`:

- `AuthRepository` x3 (ADR-0002, anonymous login by globally unique username or email, limited scope)
- `ParentLinkRepository` (ADR-0004, code redemption)
- `AttendanceRepository` x2 (ADR-0005, lock sweep on Pending only)
- `ConfigurationInfrastructure` x3 (ADR-0008, count only)
- `RolloverInfrastructure` x3 (ADR-0007, job enters scope)
- `DashboardRepository` x3 (ADR-0006)
- `ReportInfrastructure` x2 (ADR-0007)

All of them have an ADR. There is no undocumented seventh exception. `BypassTenantFilter` can be reached only from an authenticated `PlatformOperator` role claim (`HttpCurrentTenant.cs:24-28`). The design-time factory's `=> true` is used only to build the model and is never registered at runtime. On an anonymous request, `InstitutionId` is null and bypass is false. The filter then evaluates to `e.InstitutionId == null` and returns nothing, so it fails closed. The one hole is REV-001. It does not break the filter. It gets a legitimate claim for the filter to accept.

**4. The anonymous surface.** There are exactly 7, and CT-701's count is correct: `auth/register`, `auth/login`, `auth/refresh`, `dashboard/platform/summary`, `public/institutions`, `public/plans`, `public/enquiries`. Six are safe as written.

- `PlatformSummary` returns four integers and no per-institution breakdown (ADR-0006). U24's concern about small-number disclosure stands and I have nothing to add.
- `PublicInstitutionDirectoryController` is a separate controller with no shared base. It returns only `{id, name, city, region}` from a dedicated projection entity. NFR-016 and FR-005 are met by the type itself, not by remembering to leave fields out. That is the right design.
- `auth/register` is REV-001, and the directory's `id` is where an attacker gets the institution ids.

**5. Authorization depth.** The tier axis is built correctly. `RequiresPlanCapabilityAttribute` is an `IAsyncActionFilter`, which runs after authorization. So RBAC is evaluated first, and the plan check is a real second axis (403 then 402, never merged). All four ParentAccess retrofits are present: `attendance/linked`, `parent-links`, `students/linked`, and the Parent branch of `GET /dashboard`. The last one is applied inside the controller because the route is shared across roles, which is the right choice.

Row-level rules are enforced in the services, not by attributes. `InstitutionConfigController` has `[Authorize]` at class level. Writes are gated by `caller.IsLeadership` inside `InstitutionConfigService.cs:61,71`. The attribute alone is not the enforcement, and here the enforcement is real.

**Note the live effect of U28:** with the placeholder tiers, every Entry and Mid school has Parent access switched off. So these four correct gates currently refuse almost every real parent with `402`.

**6. Design tokens.** The U34 and U35 corrections landed and are correct in code. The status pills now use subtle-background and subtle-text pairs (`components/ui/index.tsx:179-182`). The Late pill uses the §1a amber pair. The neutral pair `--color-neutral-subtle-text` was added per §1c. I checked the remaining `color-success` and `color-info` uses for the same wrong-pairing problem:

- `text-success` on `StatTile` (`Attendance.tsx:93`) renders at `text-h2` (24px). It reaches 3.77:1, above WCAG's 3.0:1 threshold for large text.
- The `Pricing.tsx` check icons are non-text UI at 3.77:1, above 3.0:1.
- The `Home.tsx` dot is `aria-hidden` decoration.
- `bg-error text-white` in `toast.tsx` and `VideoCall.tsx` is 4.70:1 and passes.

I found no further wrong pairing. The only token finding is the three pixel values in REV-007. `design-tokens.md` §1b is still wrong on disk and still contradicts §1a. That is U34, unresolved, and it will mislead the next person who reads it.

---

## Already-recorded items: remaining risk only

I do not report these again as findings. I assessed each as asked:

- **U21 (no PDF export).** `PDF` returns `ERR-901`, which says "something about this request wasn't right". But the product's own tech design D7 promises that format. A user who asks for a PDF is told they made a mistake. The technical risk is low and the error wording is poor. NFR-027 is not met.
- **U27 (retention sweep is a scaffold).** I confirmed this by reading `RetentionSweepJob` (`ConfigurationInfrastructure.cs:44-61`). It counts rows and calls `logger.LogWarning`. It anonymises and deletes nothing. **CMP-001, CMP-002 and CMP-003 are not implemented at all, not even in part.** `project.md` §4 commits to 7-year retention, then secure deletion or anonymisation, for learner, guardian and staff personal data. The scaffold is the right call, because guessing an irreversible operation on personal data would be much worse. But this must not reach production before the field-level decision that U27 asks for. **This is the highest remaining risk on this list.**
- **U28 (tier numbers with no source).** The tiers are now enforced and the operator can edit them, so wrong numbers cause wrong behaviour. Entry and Mid schools have Parent access off (see §5 above). These placeholders carry real commercial weight.
- **U29.** Closed properly at CT-701. ADR-0009 and `CapacitySerialisationTests` are the strongest work in this repository. It is a real SQL Server test with 8 concurrent admissions for the last seat. It asserts the invariant, not the status codes. The rest of the suite should be measured against this standard.
- **U30 (enquiries are logged, not delivered), U31, U32 (storage gate not built), U33 (FR-064 has no API).** All confirmed still true. U32 is the next aggregate invariant after U29, per U36.
- **U16.** Still open, and REV-002 now makes it worse. Treat them as one fix.
- **U14.** Still true. Audit snapshots still write enums as ordinals.

---

## Counts

| Severity | Count |
|---|---|
| Critical | 1 |
| Major | 4 |
| Minor | 2 |
| Total | 7 |

By type:

| | Count | IDs |
|---|---|---|
| Code defect only | 3 | REV-003, REV-004 (code half), REV-007 |
| Code defect and spec drift | 2 | REV-001 (code follows a wrong spec), REV-002 |
| Test integrity | 1 | REV-005 |
| Process or artifact | 1 | REV-006 |
| New pure spec drift | 0 | the 27 open items in `_upstream-feedback.md` stand as recorded |

This review raised new upstream entries **U38, U39, U40 and U41**, continuing from U37.

The four Majors U11, U15, U20 and U26 predicted a lot of conformance noise. They produced almost none. Where the spec had moved, the code was consistently the better record, and the build log said so. This is useful to know before the 1.0.4 patch runs.

---

## What unblocks Gate 4

**Blocking (Critical):**

1. **REV-001.** Decide how privileged roles are created. Make the code change. Correct SPEC-FR-006 and API-006 to match (**U38**). Add a negative test: an anonymous register with `role=Administrator` must not return a leadership session.

**Fix, or carry a named written waiver (Major):**

2. **REV-002.** Write the rate limits in `09-security-spec.md` (**U39**). Then build them, with `ErrCatalogue.Err904`. Include **U16**.
3. **REV-003.** Add `ConfigureTeacher`, `IsRowVersion()` and a migration.
4. **REV-004.** Add a concurrency-conflict `ERR-` (**U40**) and one central handler.
5. **REV-005.** Add a contract suite that ties `mockApi.ts` to the real controller shapes.

**Schedule (Minor):** REV-006 (regenerate the gate artifacts and pin the vitest pool), REV-007.

**Also needed before production, already owned:** **U27**, the CMP-001 to CMP-003 retention decision and the real sweep. By the letter of the build plan it is not a Gate 4 blocker. It is still the largest open compliance risk in the product.

---

This review changed no product code and no test. Every fix above is described, not applied. Gate 4 is not approved and no approval has been recorded. That decision belongs to a named human.

---

### Addendum: a C8 WARN caused by this review

I re-ran `verify-gate.sh --phase P7` after this review wrote its four upstream entries. It now reports:

```
C8  WARN  build log records no row against the current spec-suite hash (7034640b28d7)
          — specs may have changed since the last CT was built
```

This comes from this review's own append. It is not a new problem. `_upstream-feedback.md` is inside the hashed spec suite. Adding U38 to U41 moved the hash from `1ee14f1fd6a6` (which C8 matched before this pass) to `7034640b28d7`. The staleness check is working as designed. No CT is stale because of this warning alone. But it will become a real signal once the 1.0.4 patch runs. The API-006 correction that U38 asks for changes a contract that CT-001 and CT-601 were built against. Expect to rebuild and re-review those when U38 is resolved.
