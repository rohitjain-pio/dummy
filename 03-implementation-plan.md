# Enterprise AI Prompt Monitoring — Implementation Plan (v2)

## 1. Scope of V1

| Area | In Scope | Out of Scope (V1) | Status vs. existing extension |
|---|---|---|---|
| Browsers | Edge, Chrome, Brave, Arc (Chromium-based) | Firefox, Safari | Extension today only runs on whatever Chromium browser has it installed |
| Providers | GitHub Copilot, Claude.ai (parity with today), + expansion | ChatGPT, M365 Copilot (stubbed, not built) | Extension already covers Copilot + Claude |
| Surfaces | Browser traffic via CDP | VS Code Copilot, Claude Desktop, ChatGPT Desktop | Extension can never reach these regardless |
| Deployment | Intune-managed Windows devices | macOS, unmanaged/BYOD devices | Extension today is manually/CI installed, not policy-managed |
| Backend | Reuse `server.js`, `storageService.js`, `logNormalizer.js`, `providerUtils.js`, `apiClient.js`, `tokenManager.js` | Full backend rewrite | Already built and working |
| Governance | Local secret stripping, chunking, basic classification | Advanced DLP, ML-based content risk scoring | New — extension does no local redaction today |

## 2. Component-Level Plan

### 2.1 Endpoint Agent (new)
- **Stack:** .NET 8 Worker Service, replacing the extension's
  `background.js` + `content.js` + `intercept*.js` trio.
- **Install/run-as:** Windows Service under `LocalService` or
  `NetworkService`.
- **Responsibilities:**
  - Discover browser processes (`Process.GetProcesses()` / WMI) — same
    idea as watching for `github.com`/`claude.ai` tabs, but at the process
    level instead of `host_permissions`.
  - Connect to each browser's CDP endpoint (`127.0.0.1:<port>/json`).
  - Subscribe to `Network.requestWillBeSent`; on match, call
    `Network.getRequestPostData`.
  - Run the ported provider-extraction logic from `intercept.js` /
    `intercept-claude.js` (same field mapping: `prompt`, `currentURL`,
    `conversationId`, `actorLogin`/`user`).
  - Run local policy engine, write to SQLite queue.
  - Background worker drains the queue to the Prompt API.
- **Config:** AI-site registry (today hardcoded to 2 domains in
  `manifest.json`'s `host_permissions`) delivered as a signed, versioned
  config file so it can grow without redeploying the agent.

### 2.2 Local Policy Engine (new)
- Regex/keyword-based secret detection (`password=`, `token=`,
  connection-string patterns) before upload — nothing like this exists in
  the extension today; prompts currently go straight from `intercept.js`
  to Excel/external API unfiltered.
- Chunking logic for prompts over 10 KB.
- Lightweight classifiers producing boolean flags (`containsCode`,
  `containsPII`).

### 2.3 Local Queue (adapted from `background.js`)
- SQLite table `PromptQueue(Id, UserId, Prompt, Timestamp, Status)`,
  replacing `chrome.storage.local`'s `queuedLogs` array.
- Same flush pattern as today's `chrome.alarms` 30-second timer, but
  service-managed and durable across reboots, not just browser sessions.
- Statuses: `Pending`, `Uploading`, `Sent`, `Failed`.

### 2.4 Prompt API (reuse + harden `server.js`)
- Current `POST /api/copilot/log` on `localhost:3000` works but has no
  auth and assumes a single local device. Harden:
  - Real hostname + TLS.
  - Device/agent authentication (cert-based or managed identity token) —
    today literally any local process can POST to this endpoint.
  - Schema validation already exists (`hasInvalidLogShape`) — extend for
    the new local-policy fields (`containsCode`, `containsPII`, chunk
    metadata).

### 2.5 Backend Processing (reuse `storageService.js` unchanged)
- Keep the `Promise.allSettled` dual-sink pattern (Excel + external API);
  it degrades gracefully today and there's no reason to change it.
- Decide capacity plan: `prompts.xlsx` as a single append-only file
  behind a write queue will not hold up across many concurrent agents —
  either keep Excel as a periodic export/audit artifact rather than the
  live sink, or point `_saveToExcelInternal` at a proper store.
- `apiClient.js` / `tokenManager.js` reused as-is; confirm the external
  API's rate limits tolerate one-row-at-a-time posting (`sendOne` has no
  batch support today) at expected agent-fleet volume.

### 2.6 Dashboard (new)
- Views: usage by tool/user/department, flagged (PII/secret) events,
  trend over time. Nothing like this exists today — the only visibility
  into captured data is the raw `prompts.xlsx` or the external system.
- Access control: compliance/security admins vs. read-only viewers.

## 3. Deployment Plan
1. Build agent as `PromptMonitorAgent.msi`.
2. Wrap as an Intune Win32 app (`.intunewin`).
3. Define Intune assignment rings: **Test (IT) → Pilot (department) →
   Org-wide**.
4. Configure detection rules (service installed, version check) for
   Intune compliance reporting.
5. Run pilot side-by-side with the existing extension; diff outputs for
   parity before uninstalling the extension fleet-wide.
6. Define uninstall/rollback path in case of issues.

## 4. Testing Strategy
| Test Type | Focus |
|---|---|
| Unit | Provider extraction parity vs. `intercept.js`/`intercept-claude.js` fixtures; policy engine rules |
| Integration | Agent ↔ CDP ↔ Prompt API end-to-end on each supported browser |
| Parity | Same live sessions captured by both extension and agent; diff results |
| Resilience | Backend-down scenarios — confirm queue persists, drains on reconnect |
| Performance | Agent CPU/memory footprint under normal browsing load |
| Security | Secrets never leave device unmasked; TLS enforced to Prompt API; Prompt API rejects unauthenticated posts |
| Pilot (real users) | False-positive/negative rate on PII/code classification |

## 5. Risks & Mitigations
| Risk | Mitigation |
|---|---|
| CDP only covers browser traffic, not desktop/IDE AI tools | Roadmap dedicated collectors (Phase 2+) — same gap the extension has today |
| Enabling remote debugging ports can be a security exposure if misconfigured | Restrict to loopback only, managed via policy, agent-only access — needs explicit IT sign-off (see discussion doc) |
| Users bypass by disabling debugging flags or using unmanaged browsers | Combine with Intune app compliance checks; alert on missing agent heartbeat |
| Sensitive data captured in prompts | Local-first policy engine strips before any network transmission (new capability vs. today) |
| Backend outage causing data loss | Local SQLite queue guarantees durability until upload succeeds |
| `prompts.xlsx` single-file sink doesn't scale to fleet volume | Treat Excel as secondary/audit sink; confirm external API/DB is the primary store before fleet rollout |
| Prompt API currently has no auth | Must be fixed before any multi-device rollout — today it would accept logs from any local process |
| Employee privacy/trust concerns | Clear internal policy communication, scope limited to company-managed devices, transparent data-retention rules |

## 6. Success Criteria for V1
- Agent deployed to pilot ring with <2% CPU overhead at idle.
- 100% parity with the existing extension's capture of Copilot and
  Claude.ai prompts across Edge, Chrome, Brave, and Arc.
- Zero data loss during simulated backend outages (queue drains fully on
  reconnect).
- Dashboard reflects usage within an agreed SLA.
- No plaintext secrets/tokens found in any stored event during security
  review.
- Prompt API rejects unauthenticated requests.

## 7. Immediate Next Steps
1. Decide the Excel-vs-primary-store question for `storageService.js`
   before scaling past one device.
2. Add authentication to `server.js`'s ingest endpoint.
3. Port `intercept.js` / `intercept-claude.js` extraction logic into a
   provider-parsing module, with fixture-based tests for parity.
4. Prototype the CDP listener against one browser (Edge) end-to-end,
   feeding into the existing (hardened) Prompt API.
5. Schedule the IT discussion (see companion document) before enabling
   remote debugging via policy or requesting Intune packaging support.
