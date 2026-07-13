# Enterprise AI Prompt Monitoring — Flow Diagram (v2, updated against existing extension)

## 0. What already exists today (Chrome/Edge extension, MV3)

Your current working system, mapped from the uploaded code:

```text
STEP 1  manifest.json
        - Declares MAIN-world interceptors (intercept.js, intercept-claude.js)
          and an ISOLATED-world bridge (content.js) per site.
        - host_permissions limited to github.com, claude.ai, localhost:3000.

STEP 2  intercept.js / intercept-claude.js   (MAIN world, runs in the page)
        - Monkey-patches window.fetch (+ navigator.sendBeacon for Copilot).
        - Copilot: detects { content, intent: "conversation" } payloads,
          pulls actor_login out of telemetry events, extracts conversationId
          from the URL path.
        - Claude: matches the completion URL regex
          /organizations/.../chat_conversations/<id>/completion and reads
          bodyJson.prompt.
        - Dispatches a CustomEvent (CopilotPromptCaptured / ClaudePromptCaptured)
          with { timestamp, prompt, currentURL, conversationId, actorLogin }.

STEP 3  content.js   (ISOLATED world)
        - Listens for both CustomEvents, forwards payload to background.js
          via chrome.runtime.sendMessage.

STEP 4  background.js   (service worker)
        - Queues incoming logs in chrome.storage.local.
        - Every 30s (chrome.alarms), flushes the queue to
          http://localhost:3000/api/copilot/log.
        - Only removes queued items after a successful POST.

STEP 5  server.js   (Express backend, local:3000)
        - POST /api/copilot/log accepts a single log or array.
        - Validates shape, hands off to storageService.saveLogs().

STEP 6  storageService.js
        - normalizeLogs() -> logNormalizer.js -> providerUtils.detectProvider()
          (URL-based: github.com -> "GitHub Copilot", claude.ai -> "Claude").
        - Fans out to two sinks in parallel (Promise.allSettled):
            a) local prompts.xlsx (ExcelJS), append-only, write-queued
               to avoid concurrent file corruption.
            b) apiClient.sendLogs() -> external compliance API.
        - Only throws if BOTH sinks fail; one failing doesn't drop the other.

STEP 7  apiClient.js + tokenManager.js
        - tokenManager logs in with email/password, caches the JWT, decodes
          `exp` (or uses server-provided expiresAt) to refresh ~60s early.
        - apiClient POSTs one log at a time (external API has no batch
          endpoint) to /api/ai-provider-conversation-logs, retries once on
          401 after a forced token refresh.
```

```text
+-------------------+     +------------------+     +-------------------+
| Page (MAIN world) |     | Ext. background  |     | Local Node server |
| intercept*.js      | --> | + chrome.storage | --> | server.js         |
| patches fetch/     |     | 30s alarm flush  |     | storageService.js |
| sendBeacon         |     +------------------+     +----+---------+----+
+-------------------+                                     |         |
                                                     Excel |         | apiClient +
                                                  (prompts.xlsx)     | tokenManager
                                                                     v
                                                          External Compliance API
```

**Key limitation driving the change:** this only sees GitHub Copilot and
Claude.ai *in the browser tab*, only while the extension is installed and
enabled, and only sites you've explicitly listed in `manifest.json` /
`host_permissions`. It can't see Copilot in VS Code, Claude Desktop, or any
site not pre-listed, and a user can disable/remove the extension.

## 1. Target architecture — endpoint agent + CDP

Same downstream pipeline (steps 5–7 above are reused almost unchanged),
different capture layer:

```text
+--------------------+
| User Browser       |
| Edge/Chrome/Brave  |
+---------+----------+
          |
          | CDP (Chrome DevTools Protocol) — replaces intercept*.js
          v
+--------------------+
| Endpoint Agent      |   <- new component, replaces extension entirely
| CDP Listener        |
| Prompt Extractor    |      (same job as intercept.js/intercept-claude.js,
| Provider Normalizer |       done from outside the page instead of by
| Local Queue         |       monkey-patching fetch inside it)
+---------+----------+
          |
          | HTTPS
          v
+--------------------+
| Prompt API          |   <- reuse server.js's shape, or extend it
+---------+----------+
          |
          v
+--------------------+
| storageService.js   |   <- reused as-is
| Excel + apiClient +  |
| tokenManager         |
+--------------------+
```

## 2. End-to-end sequence (agent version)

```text
 1. Agent installed via Intune (MSI, runs as Windows Service)
      |
 2. Agent watches for browser processes
    (msedge.exe, chrome.exe, brave.exe, arc.exe)
      |
 3. Browser launched with --remote-debugging-port enabled (via policy)
      |
 4. Agent discovers open tabs via http://127.0.0.1:<port>/json
      |
 5. Agent opens a WebSocket to the tab's devtools endpoint,
    sends Network.enable
      |
 6. CDP streams Network.requestWillBeSent for every outbound request
      |
 7. Agent matches request URL against AI-site registry
    (github.com, claude.ai, chatgpt.com, copilot.microsoft.com, ...
     — today's extension only covers github.com + claude.ai;
     everything else is new coverage)
      |
 8. On match: Network.getRequestPostData pulls the raw POST body
      |
 9. Provider-specific extraction — same logic as intercept.js /
    intercept-claude.js today (Copilot: content+intent=="conversation",
    actor_login from telemetry events; Claude: completion URL regex,
    bodyJson.prompt), just running in the agent instead of the page
      |
10. Normalize into the existing log shape
    { timestamp, user, prompt, url, conversationId, provider }
    (this is exactly logNormalizer.js's output shape today)
      |
11. Local policy pass: strip secrets/tokens, chunk >10KB prompts
    (new — today's extension does no local redaction)
      |
12. Write to local durable queue (SQLite; today's extension uses
    chrome.storage.local + a 30s alarm — same idea, OS-level durability)
      |
13. Background upload worker POSTs to the Prompt API
    (today: http://localhost:3000/api/copilot/log — reusable as-is,
     just needs to run somewhere agents can reach, not just localhost)
      |
14. storageService.saveLogs() — UNCHANGED — writes to prompts.xlsx
    and forwards via apiClient/tokenManager to the external compliance API
      |
15. Data lands on the dashboard / in the external system
```

## 3. Component reuse map

| Existing file | Role today | Fate in agent architecture |
|---|---|---|
| `manifest.json`, `content.js` | Extension plumbing | Removed — no browser extension needed |
| `intercept.js`, `intercept-claude.js` | Per-page fetch/sendBeacon patching | Logic ported into the agent's provider-parsing module (same field extraction, different transport: CDP events instead of monkey-patched fetch) |
| `background.js` | Local queue + 30s flush | Replaced by agent's SQLite queue + upload worker (same pattern, OS-service durability instead of `chrome.storage.local`) |
| `server.js` | Ingest endpoint | Reused, hardened (auth, not localhost-only) as the Prompt API |
| `storageService.js` | Excel + external API fan-out | Reused as-is |
| `logNormalizer.js`, `providerUtils.js` | Shape normalization, provider detection | Reused as-is; provider list grows (Copilot/Claude today -> +ChatGPT, +M365 Copilot, etc.) |
| `apiClient.js`, `tokenManager.js` | External API auth + forwarding | Reused as-is |

## 4. Multi-surface collection (future state, unchanged from before)

```text
Endpoint Agent
     |
     +---- CDP Collector            (Edge/Chrome/Brave/Arc)
     |
     +---- VS Code Collector        (GitHub Copilot extension surface)
     |
     +---- Claude Desktop Collector
     |
     +---- ChatGPT Desktop Collector
```

All collectors emit the same event shape already used by
`logNormalizer.js` today.
