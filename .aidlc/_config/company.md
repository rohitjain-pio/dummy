# Company Standards — Programmers.ai

> **Org constitution.** This governs *how* we build, regardless of stack. The BRD, Tech Design, AI
> Spec and Code Review agents all read it, and Code Review cites it **by section number** when it
> raises a standards finding.
>
> Project-specific facts — this app's archetype, its vendors, its compliance regime — belong
> in `project.md`, never here.

---

## 1. Identity & scope

- **Organization:** Programmers.ai
- **What this governs:** org-wide engineering standards across all projects — approved technology, security baseline, secrets handling, the coding/commit/PR constitution, observability defaults, testing standards, data-handling rules.
- **Precedence:** this file is the default and the floor. A project overrides a default only via an **explicit, recorded deviation** in its `project.md` §8. Silent deviation is a review finding. On security and compliance a project may be **stricter, never looser**.

## 2. Approved technology

> The governed competency set, not a per-project mandate. A project picks its slice in
> `project.md` §2. Anything genuinely outside this set is a Tech Design **Decide/Spike** —
> never introduced silently.

- **Cloud / hosting:** Microsoft Azure (App Service / Azure SQL / Azure-native services).
- **Languages / runtimes:** C# / .NET (backend); TypeScript (frontend).
- **Backend frameworks:** ASP.NET Core (Web API).
- **Frontend frameworks:** React (TypeScript).
- **Datastores** (relational / document / cache / vector): SQL Server as the primary relational datastore. Document/cache/vector: none standardized — a project proposes one as a Decide/Spike if a genuine need arises.
- **Messaging / eventing:** none standardized at org level — each project's Tech Design proposes what real-time/eventing mechanism it needs (e.g. SignalR, a message broker) as a Decide/Spike.
- **AI / LLM providers:** n/a — no org-approved LLM provider; a project introducing an AI/LLM feature must propose one explicitly as a Decide/Spike, including what data may be sent to it.
- **Radar discipline:** anything not listed is "Hold" — propose it as a Decide/Spike.

## 3. Security baseline

- **AuthN / AuthZ:** JWT-based authentication (OAuth2/OIDC-compatible bearer tokens) with a role-based access control (RBAC) model. Deny-by-default: every endpoint requires an authenticated, authorized principal unless explicitly marked public.
- **Transport:** TLS 1.2+ everywhere; no plaintext internal hops.
- **Data at rest:** Encrypted at rest using the hosting platform's managed encryption (e.g. Azure Transparent Data Encryption for SQL Server, Azure Storage Service Encryption). No customer-managed key program required by default.
- **Input handling:** validate and encode all external input; parameterized queries / ORM bindings only — no string-built SQL. *(Change this only with a recorded reason.)*
- **Endpoints:** deny-by-default; no anonymous endpoint without an explicit recorded reason.
- **Dependencies:** dependency versions kept current; a CVE scan (e.g. `dotnet list package --vulnerable`, `npm audit`, or an equivalent CI-integrated scanner) runs in CI, and a critical/high CVE at release blocks merge unless explicitly waived and recorded.
- **Never log secrets, PII, or regulated personal data.** See §6 and §8.

## 4. Secrets management

- **Source of truth:** environment variables, sourced from the CI/CD platform's own secret store (e.g. GitHub Actions secrets / Azure DevOps variable groups) in non-local environments. No dedicated secrets-manager service (e.g. Key Vault) is mandated at org level; a project may adopt one and record it in `project.md` §8 if its compliance regime requires it.
- **Local dev:** environment variables via a gitignored `.env` file (backend: `.NET` user-secrets is also acceptable).
- **In code:** read secrets from the configured provider (environment) at runtime; never hardcode. This includes LLM / API keys.
- **Detection:** secret-scanning enabled in CI (e.g. `gitleaks` or the CI platform's built-in secret scanning) and, where available, as a pre-commit hook. A committed secret is a critical finding.

## 5. Coding / commit / PR constitution

- **Style is enforced by linters and formatters, not by review prose.** CI must show them clean; reviewers never hand-flag mechanical style. Tool per language:

  | Language | Lint / format |
  |---|---|
  | C# / .NET | `dotnet format` + built-in Roslyn analyzers |
  | TypeScript / React | ESLint + Prettier |

- **Naming & structure:** follow the idioms of the surrounding code and the conventions in the Tech Design; respect module boundaries and dependency direction.
- **Errors & logging:** structured logging (§6); user-facing wording matches the error catalogue; no swallowed exceptions.
- **Commits:** Conventional Commits (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`, etc.). Small, atomic, message explains *why*.
- **Pull requests:** at least one reviewer approval required; CI must be green before merge; main/release branches are protected (no direct pushes).
- **ADRs:** record architecturally significant decisions as ADRs in `.aidlc/03-development/adr/` during the build.
- **No TODO/HACK left at a phase gate** without a tracked work item.

## 6. Observability defaults

- **Logging:** structured (JSON) logs, with a correlation/trace ID generated at the request boundary and propagated through all downstream calls and log lines for that request.
- **Metrics & tracing:** no dedicated APM/tracing platform mandated at org level; a project may adopt one (e.g. Azure Application Insights) and record it in `project.md` if its scale warrants it.
- **Health:** each service exposes a liveness endpoint (process is up) and a readiness endpoint (dependencies — DB, downstream services — are reachable).
- **Log hygiene:** never log secrets or regulated personal data (§3, §8).
- *(Deployment, scaling, DR and IaC are operations concerns — out of scope for these code-defining standards. The Tech Design defers them with a named owner.)*

## 7. Testing standards

- **Required layers:** unit tests for business logic; integration tests for API/data-access boundaries; contract tests for public APIs where consumed by another team/service; end-to-end (E2E) tests for user-facing flows where there is a UI. Tool per language:

  | Language / area | Test tooling |
  |---|---|
  | C# / .NET (unit + integration) | xUnit |
  | React / TypeScript (unit) | Jest / React Testing Library |
  | E2E (browser flows) | Playwright |

- **Coverage policy:** every requirement traces to ≥1 test (`FR-` → `SPEC-FR-` → `TC-`). Coverage is judged by **lens completeness** — happy path, error paths, edge cases, security, idempotency — not by a single percentage. Org-wide % gate: none — lens completeness is the standard, not a numeric threshold.
- **No vacuous tests:** a test must assert its real behaviour, not merely exercise a mock.
- **Adversarial fixtures:** for every branch a test claims to cover, at least one case must fail against the pre-fix code. A suite that only agrees with the implementation proves nothing. *(This is the org rule that catches the single most common failure mode of AI-written tests.)*
- **Determinism & isolation:** deterministic and isolated; parallel runs use per-worker DBs/fixtures.
- **A red suite blocks a phase gate.** Never weaken a test to make it pass.

## 8. Data handling & compliance

- **Default posture:** per-client; absent a declared regime in a project's `project.md` §4, all client data (learner, guardian and staff personal data included) is treated as confidential by default and handled under the strictest reasonable interpretation until a regime is explicitly named.
- **Synthetic data only** in tests, fixtures, logs and tickets — never real regulated data.
- **Live vendors are sandbox-only in non-production.** Vendor + sandbox details per project → `project.md` §7.
- **LLM / AI data handling:** n/a for this org at present — no approved LLM provider (§2); any project introducing an AI/LLM feature must record explicitly what data may be sent to it and under what agreement, as a recorded project-level deviation (`project.md` §8).
- **Data minimization & retention:** collect only what is needed; follow the project's stated retention rules.

## 9. Domain conventions & terminology

- **Org-wide glossary / naming conventions:** none standardized beyond plain, unambiguous English business terms — one term per concept, consistently applied. Project-specific glossary lives in `project.md` §3.
- **Consistency:** the same term for the same concept across BRD, design, specs, code and tickets.

---

### Maintenance
Owned by Programmers.ai engineering leadership. Changes here affect every project that re-runs `setup-org-config`; version this file with the plugin and announce material changes.
