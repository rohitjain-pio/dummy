# Topics to Discuss with IT — Endpoint Agent Migration

This is meant as an agenda for a working session with IT/security/compliance
before building or piloting the endpoint-agent version of the AI prompt
monitoring tool. Grouped by theme, with why each item matters and what
decision or input is needed from IT.

## 1. Remote Debugging Port Policy
- **What:** The agent needs Chromium browsers launched with
  `--remote-debugging-port` enabled to use CDP. This is normally an
  opt-in developer flag, not something enabled by default.
- **Ask IT:** Can this be pushed via existing browser management policy
  (Edge/Chrome ADMX templates, e.g. `RemoteDebuggingAllowed` or equivalent)
  across managed devices? Is there an existing security objection to
  enabling debugging ports fleet-wide?
- **Why it matters:** This is the single biggest architectural change
  from "install a browser extension" to "change a security-relevant
  browser flag org-wide." IT/security needs to own this decision, not
  just be informed of it.
- **Mitigation to propose:** Port bound to loopback only (127.0.0.1),
  no external exposure; only the local agent (running as a trusted
  service) connects to it.

## 2. Endpoint Agent Deployment & Lifecycle
- Confirm Intune is the right channel (already assumed in the plan) and
  get an owner for packaging the `.intunewin` app.
- Assignment rings: who is "Test," who is "Pilot," what's the timeline to
  "Org-wide"?
- Update/versioning strategy — does IT want the agent to self-update, or
  strictly via Intune-pushed versions?
- Uninstall/rollback process if the pilot surfaces problems.
- Service account: what should the Windows Service run as
  (`LocalService`/`NetworkService`) and does that clear endpoint
  protection / EDR allow-listing?

## 3. Interaction with Existing Security Tooling
- Will EDR/antivirus flag a service that opens WebSocket connections to
  browser debugging ports? Needs an allow-list entry likely.
- Does the org already have a CASB/DLP tool that overlaps with this
  scope? Worth checking for duplication before building custom tooling.
- Any existing policy against enabling remote debugging that this would
  need an exception to?

## 4. Data Scope & Retention
- Confirm exactly which sites/tools are in scope (today: GitHub Copilot,
  Claude.ai — expansion candidates: ChatGPT, M365 Copilot, others).
- How long are captured prompts retained? In Excel, in the external
  compliance API/DB, and in the local SQLite queue on-device.
- Who has access to raw prompt content vs. aggregated/classified metadata
  only?
- Should certain fields (full prompt text) be masked/redacted by default
  and only unmasked under a documented access process?

## 5. Sensitive Data Handling
- The local policy engine will attempt to strip secrets/tokens/connection
  strings before upload — what patterns/rules does IT/security want
  included beyond the basics (`password=`, `token=`, connection strings)?
- What should happen to a prompt flagged as containing PII or secrets —
  dropped, redacted-then-sent, or sent with a flag for review?
- Any regulatory constraints (industry-specific, regional data residency)
  that affect where the backend/database can live?

## 6. Backend Hosting & Auth
- Current backend (`server.js`) has **no authentication** on its ingest
  endpoint and assumes `localhost`. Before any multi-device rollout it
  needs: a real hostname, TLS, and device/agent authentication (cert-based
  or managed identity). Who owns provisioning that (internal PKI? Azure AD
  app registration?).
- `tokenManager.js` currently uses an email/password login for the
  external API. Is a service-account email/password acceptable long-term,
  or should this move to client-credentials/OAuth2 or a managed identity?
  Who owns rotating that credential if it stays password-based?
- Where does the backend get hosted — existing internal infra, or new
  Azure resources (App Service, Azure SQL/Cosmos DB) as sketched in the
  architecture? Whose budget/subscription?

## 7. Storage Scaling
- Current storage sink is a single `prompts.xlsx` file written via a
  serialized write queue. This works for a single-device pilot but will
  not scale to a fleet. Does IT want:
  - Excel kept only as a periodic export/audit artifact, with a real
    database as the primary store, or
  - A different reporting format entirely?
- Confirm expected fleet size and expected prompt volume/device/day to
  size the database and API correctly.

## 8. Employee Communication & Trust
- What is the internal policy language communicated to employees about
  this monitoring (scope: company-managed devices only, purpose:
  compliance/governance, not performance monitoring)?
- Does this require sign-off from HR/Legal/Works Council (if applicable
  in relevant regions) before piloting?
- Should there be a visible indicator on managed devices that the agent
  is running (transparency), or is this expected to run silently like
  other endpoint security agents?

## 9. Pilot Plan & Success Criteria
- Agree on a pilot device ring and duration.
- Agree on parity criteria vs. the existing extension (same prompts
  captured, same accuracy) before decommissioning the extension.
- Agree on rollback triggers (performance issues, false-positive rate,
  security concerns) that would pause or stop the rollout.

## 10. Future Surface Coverage (set expectations early)
- CDP-based capture only ever covers Chromium browsers. IT should know
  up front that VS Code Copilot, Claude Desktop, and ChatGPT Desktop are
  explicitly out of scope for V1 and need separate collectors later —
  avoid this being a surprise gap discovered post-launch.

## Suggested Meeting Agenda (60-90 min)
1. Walkthrough of current extension vs. proposed agent architecture (10 min)
2. Remote debugging policy decision (15 min) — likely the crux of the discussion
3. Deployment/Intune ownership and timeline (10 min)
4. Backend hosting, auth, and storage scaling decisions (15 min)
5. Data retention, sensitive-data handling, and employee communication (15 min)
6. Pilot scope, ring, and success criteria (10 min)
7. Open questions / next steps and owners (5 min)
