<!-- pipeline-provenance
produced_by: ai-spec-agent (infra-config-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). §7 gains CFG-009 (time-zone fallback, written back from
  CT-201 — U11), CFG-010 (sign-up code, Q-238 / U38) and CFG-011/012/013 (rate limits, Q-240 /
  U39 — the thresholds `09-security-spec.md` §10 binds). §8 gains the attendance lock sweep and
  the Hangfire placement correction (U11, U20). §9's deferrals are unchanged and all still open.
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, authorised by Rashmi Mehta, closing
  the Mode D Round-2 audit against 1.0.4). §6 gains **CFG-SEC-06** (the leadership-code lookup
  key — U45a: 1.0.4 named a key *class* and bound no secret), and **CFG-012's unit is corrected
  from "accounts" to "requests"** (m3 — §10.2 requires the limiter to run before any account
  insert, so it cannot count accounts). §7's other rows, §8 and §9 are unchanged.
-->

# 12 — Infrastructure Spec

**Never shards** — global by definition.

## 1. Infrastructure inventory (`INF-`)

| INF ref | Component | Note |
|---|---|---|
| INF-001 | Azure App Service | Hosts ASP.NET Core Web API + Hangfire in-process |
| INF-002 | Azure SQL Server (Azure SQL) | Shared DB, `institution_id` discriminator |
| INF-003 | Azure SignalR Service | Real-time backplane (T-001), "Default" mode for scale-out |
| INF-004 | Azure Communication Services | Email + SMS (T-004), Calling SDK (T-006) |
| INF-005 | Azure Blob Storage | Report snapshots (D11) |
| INF-006 | Static hosting or same App Service | React SPA delivery (Tech Design §2.1 notes this as a deployment-topology detail, out of scope for code-level design) |

## 2. Environments

| Environment | Purpose | Vendor mode |
|---|---|---|
| Local dev | Developer machines | `.env`/user-secrets (company.md §4); sandbox ACS/SignalR |
| CI | Automated test runs | Ephemeral, per-worker DB/fixtures (company.md §7 determinism rule) |
| Staging | Pre-production validation | Sandbox vendor credentials (company.md §8 "live vendors are sandbox-only in non-production") |
| Production | Live institutions | Production vendor credentials, production data |

## 3. CI/CD stages (inherited from `company.md` §5/§7)

1. Restore/build (`dotnet build`, `npm ci && npm run build`)
2. Lint (`dotnet format` + Roslyn analyzers; ESLint + Prettier) — must be clean, no reviewer
   hand-flags style
3. Unit tests (xUnit, Jest/RTL)
4. Integration tests (xUnit, API/data-access boundary)
5. E2E tests (Playwright, user-facing flows)
6. Dependency/CVE scan (`dotnet list package --vulnerable` / `npm audit`) — critical/high blocks
   merge unless explicitly waived and recorded
7. Secret scan (gitleaks or platform-native)
8. Deploy to staging → smoke test → manual/gated promote to production

## 4. SLO/SLI per NFR (code-enabling only — the numeric SLO itself is an ops deliverable, per
Tech Design §15)

| NFR | SLI | Target | Owner |
|---|---|---|---|
| NFR-010 | App Service uptime during institutional working day | 99.5% | Delivery/DevOps lead (Jayesh Idnani, `project.md` §5 — no separate DevOps role recorded) |
| NFR-006/007/008 | p95 latency (list/detail, dashboard, chat) | <2s / <3s / <1s | Same |
| NFR-013 | Backup recency + documented RTO/RPO | Daily backup, RTO/RPO to be set at the ops layer | Same |

## 5. Backup / DR

Daily backup with a documented recovery objective (NFR-013) — the specific backup/restore
mechanism and tested RTO/RPO figure is an **operations deliverable**, not decided here (Tech
Design §15, carried forward unchanged). Azure SQL's built-in point-in-time restore is the natural
default; the exact retention window is an ops decision.

## 6. `CFG-` matrix — secret references only, never values

| CFG ref | Purpose | Source (env var name pattern) |
|---|---|---|
| CFG-SEC-01 | SQL connection string | `SQLCONNSTR_Default` (or platform-native secret ref) |
| CFG-SEC-02 | JWT signing key | `JWT_SIGNING_KEY` |
| CFG-SEC-03 | Azure SignalR connection string | `AZURE_SIGNALR_CONNECTION_STRING` |
| CFG-SEC-04 | Azure Communication Services connection string | `ACS_CONNECTION_STRING` |
| CFG-SEC-05 | Azure Blob Storage connection string | `BLOB_STORAGE_CONNECTION_STRING` |
| **CFG-SEC-06** | **Leadership/staff sign-up code lookup key** — the HMAC-SHA256 key behind `LeadershipCode.code_lookup` (`04-data-model.md` §2.12b) | `LEADERSHIP_CODE_LOOKUP_KEY` — **32 random bytes, Base64-encoded** |

**`CFG-SEC-06` added at 1.0.5 (U45a).** 1.0.4 specified the lookup as an HMAC "under the
JWT-signing-key-class secret" — a *class* of secret, not a secret, with no `CFG-` anywhere and no
entry in `09-security-spec.md` §7's inventory. **It is a secret of its own and must not be the JWT
signing key.** Sharing them couples two unrelated rotation schedules: rotating the signing key —
routine, and mandatory after a suspected token leak — would silently make every outstanding
leadership code fail with `ERR-206`, with nothing in any message connecting the two events.
**Rotating `CFG-SEC-06` invalidates every outstanding `Issued` code and has a mandatory
revoke-then-reissue procedure** — the full rule, and the cheaper drain-and-rotate alternative, are
in `04-data-model.md` §2.12b(c). Generating it is a **CT-801 environment prerequisite**: the
endpoint cannot function without it, and there is deliberately no fallback-to-a-default behaviour
(a defaulted HMAC key is no key at all).

**No value is ever recorded in this suite** — per `company.md` §4, secrets are sourced from the
CI/CD platform's own secret store (non-local) or a gitignored `.env`/.NET user-secrets (local).

## 7. Fixed platform constants (`CFG-` — non-secret, cross-referenced from `constitution.md` §3)

| CFG ref | Constant | Value | Source |
|---|---|---|---|
| CFG-001 | Password policy | min 10 chars, ≥3 of 4 classes, blocklist, no forced rotation | D1, BR-014 |
| CFG-002 | JWT access / refresh expiry | 30 min / 14-day sliding | D2, BR-015 |
| CFG-003 | Message length bound | 4,000 characters | D3, BR-045 |
| CFG-004 | Attendance correction lock | 30 calendar days | FR-035 |
| CFG-005 | Parent Link Code length / expiry | 8 chars / 7 days | T-003, ASM-003 |
| CFG-006 | Trial duration | 30 days | FR-070 |
| CFG-007 | Retention: student/guardian/staff PII | 7 years post-departure/termination | CMP-001 |
| CFG-008 | Subscription-end export grace period | 90 days | CMP-002 |
| CFG-009 | **Institution time-zone fallback** | `Asia/Kolkata` | *Added 1.0.4 (Q-215, built in CT-201, U11).* `InstitutionConfiguration.time_zone` is an **IANA id** (VAL-024 — a Windows id such as "India Standard Time" is refused even on a Windows host). This value applies when no configuration row exists **or** the stored id is unresolvable. It is load-bearing: the attendance-day boundary (`07-business-logic/attendance.md`) is midnight in this zone, so a UTC fallback would push a teacher marking at 11:58pm local into the wrong day. It was proposed by the build and used by CT-201/CT-501 but never recorded here |
| CFG-010 | **Leadership/staff sign-up code length / expiry** | 8 chars / 7 days | *Added 1.0.4 (Q-238, U38).* Mirrors `CFG-005` deliberately — same mechanism, same scale (ASM-006) |
| CFG-011 | **Login rate limit** | 10 attempts per (username, IP) **and** 50 per IP, 5-minute sliding window | *Added 1.0.4 (Q-240, U39).* `09-security-spec.md` §10, RL-1. NFR-017. **Default, operator-editable — ASM-007** |
| CFG-012 | **Registration rate limit** | **5 requests to API-006 per IP, counted regardless of outcome**, 60-minute fixed window | *Added 1.0.4 (Q-240, U39); unit corrected 1.0.5 (m3).* §10, RL-2. **It counts requests, not accounts** — `09-security-spec.md` §10.2(1) requires the limiter to run *before* any uniqueness query or account insert, so at the moment it decides there is no account to count; §10.2(3) requires successful requests to count too. 1.0.4 bound it as "5 accounts per IP", which the limiter cannot implement. The bound that stops anonymous account creation being scripted across the public directory. **ASM-007** |
| CFG-013 | **Room-passcode attempt limit** | 5 attempts per (user, room), 15-minute sliding window | *Added 1.0.4 (Q-240, U39 — closes U16).* §10, RL-3. **ASM-007** |

**`CFG-011`–`CFG-013` must be bound from configuration, not compiled in.** A literal threshold in
a controller or in middleware is a class-11 review finding. The three of them are the operator's
tuning surface for a school behind a single NAT gateway, and TC-213/214/511 each assert that
lowering the value moves the trip point — a test that would pass against a hardcoded constant is
not testing the requirement.

## 8. Background jobs (Hangfire, T-005) — restated here as the ops-facing job inventory

| Job | Schedule | Failure handling |
|---|---|---|
| Report generation | On-demand enqueue | Retry per Hangfire policy; `Failed` status surfaced, never silently dropped |
| Notification dispatch | On-demand (announcement publish, attendance events) | Same |
| Academic year rollover | Scheduled (institution year-end) + on-demand | Same |
| Trial-to-entry reversion | Scheduled (trial expiry check) | Same |
| Retention/deletion sweep | Daily | Failure alerts ops — a missed sweep is a compliance risk (CMP-001/002), escalated, not silently retried indefinitely without visibility. **Built as a count-and-log scaffold only — it anonymises and deletes nothing (ADR-0008, U27, still open.)** |
| **Attendance lock sweep** | **Daily** (`attendance-lock-sweep`) | *Written back at 1.0.4 (Q-216, U11).* Auto-rejects `CorrectionRequest` rows whose record has passed the `CFG-004` lock (§2.6a `AutoRejected`). The **logic** was built in CT-202 and the **daily registration** in CT-501 — a recorded split, not a silent one. Approve/reject additionally re-check the lock at the moment of action, so a missed sweep cannot let a stale approval through |

**Job registration (written back at 1.0.4 — Q-222, U20):** Hangfire with SQL Server storage was
installed in **P4/CT-402**, not CT-501 as `15-task-breakdown.md` §4 planned; CT-501 added only the
recurring registrations. `Hangfire:Enabled` defaults to **true** so production can never silently
stop running jobs — the cost is that every test host must switch it off. Report artifacts go to
Blob Storage when `CFG-SEC-05` is set, otherwise a local-disk store for dev and tests only.

## 9. Deferred to Operations (carried unchanged from Tech Design §15 — named owner, not designed here)

| Item | Owner |
|---|---|
| Environment topology (App Service plan sizing, slots) | Delivery/DevOps lead |
| Autoscale rules | Same |
| DR / RTO / RPO specific numbers | Same |
| Monitoring/alerting/tracing platform (Application Insights candidate) | Same |
| IaC tooling | Same |
| Cloud networking (VNets, private endpoints) | Same |
