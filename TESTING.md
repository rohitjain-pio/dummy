# Manual UI testing guide

How to get demo data into the database and exercise SchoolSync end-to-end through the real
frontend, backed by the real API and a real SQL Server LocalDB database (nothing mocked).

## 1. One-time local setup

The API needs two secrets that are deliberately **not** checked into the repo. Set them once
per machine with `dotnet user-secrets` (run from `src/SchoolSync.Api`):

```
cd "src/SchoolSync.Api"
dotnet user-secrets set "Jwt:SigningKey" "local-dev-signing-key-change-me-please-32chars+"
dotnet user-secrets set "Security:LeadershipCodeLookupKey" "local-dev-leadership-lookup-key-32B-minimum!!"
```

Both must be at least 32 characters. Without them the API throws on startup by design (fail
closed — see `LeadershipCodeLookup.cs` / `DependencyInjection.cs`).

You also need SQL Server LocalDB installed (ships with Visual Studio / SQL Server Express
LocalDB). The API's default connection string points at
`Server=(localdb)\MSSQLLocalDB;Database=SchoolSync;...` — no extra setup needed if LocalDB is
present.

## 2. Seed the database

A standalone seeding tool lives at `tools/DbSeeder`. It applies EF Core migrations (creating the
`SchoolSync` database if needed) and inserts one demo institution with one account per role:

```
dotnet run --project tools/DbSeeder
```

It's safe to re-run — if the demo institution already exists it just reprints the credentials
instead of duplicating data.

### Seeded login credentials

| Role          | Username   | Password       | Notes                                   |
|---------------|-----------|----------------|------------------------------------------|
| Administrator | `admin1`   | `Admin@12345`  | Full CRUD across the institution         |
| Teacher       | `teacher1` | `Teacher@12345`| Linked to teacher "Anita Sharma"         |
| Student       | `student1` | `Student@12345`| Linked to student "Riya Verma", Grade 5-A|
| Parent        | `parent1`  | `Parent@12345` | Linked to "Riya Verma" via parent link   |

Institution: **Sunrise Public School** (Bengaluru), on the Bespoke subscription plan, so every
gated feature (advanced analytics, parent access, custom reporting) is unlocked for testing.

> The system actually supports 7 roles (`PlatformOperator`, `Administrator`, `Principal`,
> `SchoolInCharge`, `Teacher`, `Student`, `Parent`). The seeder covers the 4 most commonly tested
> ones. `Principal`/`SchoolInCharge` behave like `Administrator` (see
> `src/SchoolSync.Api/Authorization/Roles.cs`); `PlatformOperator` is a cross-tenant role that is
> intentionally never created via self-registration and isn't seeded here.

## 3. Run the app

Two processes, in two terminals:

```
# Terminal 1 — API (must run on :7001 to match the frontend's dev proxy)
dotnet run --project src/SchoolSync.Api --urls "https://localhost:7001"

# Terminal 2 — frontend
cd src/SchoolSync.Web
npm run dev
```

Open the URL Vite prints (typically `http://localhost:5173`, or the next free port if that one's
taken).

The first time you hit `https://localhost:7001` directly your browser/OS may warn about the
untrusted local dev HTTPS certificate — trust it once with `dotnet dev-certs https --trust`, or
just proceed past the vite dev server which talks to it over the local proxy.

## 4. What to test, per role

Log in at the app's login screen with each of the 4 credentials above (one browser profile /
incognito window per role avoids token collisions if you want to compare roles side by side).

### Administrator (`admin1`)
- Dashboard loads with institution-wide stats.
- Manage classes, teachers, students, subjects (create/edit/deactivate).
- Institution settings / configuration screens.
- Reports: generate and download a report.
- Announcements: create one and confirm it's visible to other roles.

### Teacher (`teacher1`)
- Dashboard shows classes/subjects assigned (none yet — this seed doesn't create a subject
  mapping; create one as Administrator first if you want to test attendance marking).
- Mark attendance for a class.
- View/respond to attendance correction requests.
- Messaging/rooms: send a message, join a room.

### Student (`student1`)
- Dashboard shows own attendance/timetable.
- View own profile, announcements.
- Messaging (if enabled for students).

### Parent (`parent1`)
- Dashboard shows the linked child (Riya Verma)'s attendance and announcements.
- Confirm the parent cannot see any other student's data (multi-tenant/ownership check).

### Cross-cutting checks worth doing regardless of role
- Log out / log in again (refresh token cookie `ss_refresh` should silently renew the session).
- Real-time features: open two browser windows as different roles and confirm SignalR-backed
  updates (chat, presence) appear live.
- Role-based access: try navigating directly to a URL/route that belongs to another role and
  confirm you're blocked, not just hidden from the nav.

## 5. Automated tests (for reference, not a substitute for the above)

- `cd src/SchoolSync.Web && npm run test` — frontend unit tests (Vitest).
- `cd src/SchoolSync.Web && npm run e2e` — Playwright e2e; **note this suite mocks every API
  call** (see `src/SchoolSync.Web/e2e/mockApi.ts`), so it never touches the real backend/DB and
  doesn't exercise anything seeded above. It's a UI-behavior smoke test only.
- `dotnet test` (from repo root, or `tests/SchoolSync.IntegrationTests`) — backend integration
  tests against an in-memory EF Core provider, also independent of the seeded LocalDB data.

Manual testing per the steps above is currently the only way to exercise the real, seeded data
through the real UI.
