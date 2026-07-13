# Enterprise AI Prompt Monitoring — Development Path (v2)

## Guiding Principle

You already have a working, tested capture-and-forward pipeline in the
extension (interception -> normalization -> Excel + external API). The
agent migration is **not a rewrite** — it's swapping the capture layer
(browser extension -> endpoint agent + CDP) while carrying the backend
(`server.js`, `storageService.js`, `logNormalizer.js`, `providerUtils.js`,
`apiClient.js`, `tokenManager.js`) forward largely untouched. Sequence the
work so the backend risk is retired first (it's mostly done) and the new
agent risk (CDP, Windows Service, Intune packaging) gets the most runway.

## Milestone 0 — Backend Hardening (Week 1-2)
- Move `server.js` off `localhost`-only assumptions: bind to a real host,
  add TLS, add agent authentication (today it has none — any local process
  can POST to it).
- Add device/user identity fields to the ingest schema so multi-device
  telemetry doesn't collide (today's flow is single-machine, single-browser
  session oriented).
- Confirm `tokenManager`'s email/password login flow is acceptable for a
  service account long-term, or plan a move to a managed identity / client
  credential flow (see IT discussion doc).
- Load-test `storageService.saveLogs()` — the current `prompts.xlsx`
  single-file sink with a write queue will not scale past a handful of
  concurrent devices; decide now whether Excel stays as a secondary/audit
  sink only, with the external API/DB as primary.

## Milestone 1 — Provider Extraction Port (Week 2-4)
- Port the exact extraction logic from `intercept.js` (Copilot:
  `content` + `intent === "conversation"`, `actor_login` from telemetry
  events, conversationId from URL path or `responseMessageID`) and
  `intercept-claude.js` (completion URL regex, `bodyJson.prompt`) into a
  provider-parsing module the agent can call after it has a captured
  request body from CDP.
- Unit tests should reuse the same fixture payloads that validated the
  extension, so parity is provable, not assumed.
- Confirm `providerUtils.detectProvider()`'s URL-substring approach still
  works when the URL comes from a CDP `requestWillBeSent` event rather than
  `resource.toString()` in a page-context `fetch` call (should be
  equivalent, but verify redirects/absolute vs relative URLs).

## Milestone 2 — CDP Endpoint Agent Core (Week 3-6)
- .NET 8 Worker Service skeleton, installable as a Windows Service.
- Process watcher for `msedge.exe`, `chrome.exe`, `brave.exe`, `arc.exe`.
- Policy-driven enablement of `--remote-debugging-port` (this needs an IT
  decision — see the discussion doc — since it's a bigger footprint change
  than shipping a browser extension).
- WebSocket client for CDP: `Network.enable`, `requestWillBeSent`,
  `getRequestPostData`.
- AI-site registry, expanded beyond today's two entries (github.com,
  claude.ai) to whatever else IT wants covered (ChatGPT, M365 Copilot —
  `providerUtils.js` already has a placeholder comment for M365).

## Milestone 3 — Local Governance Layer (Week 5-7)
- New capability, not present in the extension today: secret/token/
  connection-string stripping before anything leaves the device.
- Prompt chunking for payloads >10 KB.
- Local classification flags (`containsCode`, `containsPII`).

## Milestone 4 — Local Queue + Upload Worker (Week 6-8)
- SQLite-backed queue replacing `chrome.storage.local` +
  `chrome.alarms` 30s flush. Same pattern (queue, periodic flush, only
  clear on confirmed delivery) but OS-level durability across reboots,
  not just browser-session durability.
- Talks to the hardened Prompt API from Milestone 0.

## Milestone 5 — Deployment & Packaging (Week 8-10)
- Package agent as an Intune Win32 app (.intunewin).
- Silent install/uninstall, auto-update channel, service recovery config.
- Pilot rollout to a small device ring; monitor CPU/memory footprint,
  since a persistent CDP WebSocket per tab is a different resource profile
  than a content script.

## Milestone 6 — Dashboard & Reporting (Week 9-11)
- Build the reporting layer the extension never had — today's only "view"
  of the data is `prompts.xlsx` and whatever the external compliance API
  does with it. Add usage-by-tool/user/department, flagged-event views.

## Milestone 7 — Pilot & Cutover (Week 11-13)
- Run the agent and the extension side-by-side on a pilot ring; diff
  captured prompts to confirm parity before decommissioning the extension.
- Security review of the agent (service account privileges, signed
  binaries, tamper resistance).
- Decommission the extension once parity is confirmed and IT sign-off
  (see discussion doc) is complete.

## Milestone 8 — General Availability (Week 13+)
- Org-wide Intune rollout.
- Runbook for support/ops (service won't start, queue backlog, token/cert
  rotation, CDP connection drops on browser update).

## Post-GA Roadmap — Multi-Surface Expansion
| Phase | Collector | Notes |
|---|---|---|
| Phase 2 | VS Code Collector | GitHub Copilot extension/telemetry surface — not reachable via CDP |
| Phase 3 | Claude Desktop Collector | Requires its own local capture approach |
| Phase 4 | ChatGPT Desktop Collector | Same as above |
| Phase 5 | M365 Copilot | Placeholder already exists in `providerUtils.js` |
| Phase 6 | Mobile/BYOD coverage | Different threat model, likely MDM-policy based |

## Team & Skillset Needs
- .NET engineer for the Windows Service/agent (new).
- Backend engineer to harden and scale the existing Node/Express service
  (extension of current work, not new).
- DevOps/Intune admin for packaging and deployment (new).
- Security/compliance stakeholder to sign off on remote-debugging policy,
  data retention, and the service-account credential model (new — see
  discussion doc).
- Dashboard/frontend engineer for reporting (new).
