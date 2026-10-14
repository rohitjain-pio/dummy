<!-- pipeline-provenance
produced_by: ai-spec-agent (contract-writer)
generated: 2026-09-11
trigger: ASYNC (03-system-design.md §1)
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch). **Written back from the P3/P4/P5
  builds (U15, U20, U26):** EVT-001's broadcast payload adds `roomId` and `clientMessageId`;
  EVT-002 and EVT-004 separated onto their two real axes (connections vs membership, Q-219);
  EVT-006 clarified; §2's job table updated to what was actually built. **The code is the source
  of truth for all of it and no code change is implied.**
-->

# 06 — Event Contracts

Triggered because real-time transport (SignalR, T-001) and background jobs (Hangfire, T-005)
both exist. Two event families: **hub events** (client ⇄ server, live) and **job events**
(server-internal, scheduled/enqueued — no client subscribes directly, but their side effects
appear as REST-visible state changes).

## 1. SignalR hub events — `/hubs/collaboration` (group = room id, joined only post `API-043`)

| EVT | Direction | Payload | Trigger | Trace |
|---|---|---|---|---|
| EVT-001 | client→server→broadcast | `{roomId, clientMessageId, content}` → `{messageId, **roomId**, **clientMessageId**, authorId, content, sentAtUtc}` | Send message (FR-045) | BR-043, NFR-008 (<1s delivery) |
| EVT-002 | server→broadcast | `{roomId, userId, status: "online"\|"offline"}` | **Connection count per room** — not join/leave | FR-047 |
| EVT-003 | server→broadcast | `{roomId, userId, isTyping: bool}` | Typing start/stop | FR-047 |
| EVT-004 | server→broadcast | `{roomId, userId, event: "joined"\|"left"}` | **`RoomMembership` created / explicitly removed** — not connection state | FR-050 |
| EVT-005 | server→broadcast | `{roomId, userId, mic: bool, camera: bool}` | Mic/camera toggle during a meeting | FR-049 |
| EVT-006 | server→client | `{roomId, messages: [...]}` | Reconnect — history re-fetch is REST (`API-044`), **not** replayed over the hub; this event only signals "reconnected, now re-fetch" to the client state machine | NFR-012 |

**EVT-002 vs EVT-004 — the distinction, written back at 1.0.4 (Q-219, U15).** The original table
gave both "join/leave room" as their trigger, which reads as if they were the same event twice.
They are two different axes and the `RoomMembership` table (§2.8a) is what separates them:

- **EVT-004 (`joined`/`left`) follows *membership*.** `joined` fires when the row is created —
  by the **REST** call API-043, published through `IHubContext`, not from a hub method. `left`
  fires only on an **explicit** `LeaveRoom`. **A disconnect never emits `left`.**
- **EVT-002 (`online`/`offline`) follows *connections*.** It tracks each user's live connection
  count for the room. A reconnect is not a re-join (Q-219), so a user whose network drops goes
  `offline` and then `online` again while remaining a member throughout.

**Known limitation, recorded not hidden:** presence counts are **per app instance**. Under Azure
SignalR scale-out a user with tabs on two instances can flicker `offline`. The specs name no
shared presence store and none was built. Same class of caveat as `09-security-spec.md` §10's
per-instance rate limiter, and it becomes real at the same moment: more than one instance.

**EVT-006 `HistoryResync`, clarified 1.0.4:** sent **to the caller on every `JoinRoom`**, with
`messages: []`. It signals "re-fetch over REST"; history is never replayed over the hub.

**Hub refusals** are a `HubException` carrying the `ERR-` code — `ERR-102` for not a member or not
joined, `ERR-504` for length. **The JWT is accepted as a `?access_token=` query parameter for
`/hubs/*` only**; a REST call presenting it that way gets 401.

**Idempotency/ordering:** `EVT-001` carries a client-generated `clientMessageId` so a client that
retries an unacknowledged send does not produce a duplicate visible message (server treats a
repeated `clientMessageId` as a no-op, returning the original `messageId`). Events are delivered
per-connection in SignalR's own order guarantee within a group; no cross-event ordering contract
is asserted beyond that.

**Authorization:** every hub method re-validates the caller is a current group member (joined via
`API-043`) before accepting `EVT-001`/`EVT-003`/`EVT-005` — group membership is necessary but the
hub does not trust a stale client-held token indefinitely; membership is re-checked against the
current DB state at connect time, not cached for the connection's lifetime beyond that.

## 2. Background job events (Hangfire, T-005 — internal, no external subscriber)

| Job | Trigger | Effect (REST-visible) | Trace |
|---|---|---|---|
| Report generation | `API-050` enqueue | `ReportRun` row transitions `Pending → Ready`; blob written | FR-054/055, A1 |
| Notification dispatch | Announcement publish, attendance events, per institution's `notification_preferences` | **NOT BUILT** — no email/SMS channel is bound. API-062 enquiries are **logged at Warning, not delivered** (U30, open). Recorded at 1.0.4 so the table is not read as describing a working feature | FR-065, T-004 |
| Academic year rollover | Daily `academic-year-rollover` job (rolls every year that has ended in its institution's calendar) + `API-059` on-demand | New `Enrollment` rows **appended**; prior rows never touched. Runs **exactly once** per year. Enters each tenant's scope by id per ADR-0007 | FR-067/068, BR-060, Q-229 |
| Trial-to-entry reversion | Daily `trial-reversion` job | `Institution.trial_plan_id` cleared and plan set to Entry, `trial_started_at_utc` retained, audited as the **system actor**. **Entitlement already drops at expiry before the job runs** — the job makes stored state match. No billing call exists on this path | FR-071, A4, Q-230 |
| **Attendance lock sweep** | Daily `attendance-lock-sweep` | `Pending` correction requests past the `CFG-004` lock → `AutoRejected` (§2.6a). *Added at 1.0.4 (Q-216, U11) — it was missing from this inventory entirely* | FR-035, Q-206 |
| Retention/deletion sweep | Daily `retention-sweep` | **SCAFFOLD ONLY — counts rows past 7 years and logs them; anonymises and deletes nothing** (ADR-0008). **CMP-001/002/003 are unimplemented, not partially implemented (U27, open — the largest unaddressed compliance exposure in the product).** Recorded at 1.0.4 because this row previously read as if the sweep worked | `_shared.md` §4, CMP-001–003 |

**Failure handling:** every job is retried per Hangfire's built-in retry policy (exponential
backoff, capped attempts); a job that exhausts retries is surfaced to the `AuditLog`/ops alerting
channel (`12-infrastructure-spec.md`), never silently dropped — this satisfies NFR-011's "no
submitted data is lost" extended to the async path (a report request that never completes must be
visibly failed, not silently forgotten).

## 3. What is explicitly NOT an event in this suite

- The public site's "Live" platform-stats badge (`ui-design-final.md` §1.1) is **decorative**, not
  backed by a real socket — no `EVT-` exists for it, and `11-ui-spec.md` must not imply one.
- The notification bell (`ui-design-final.md` §2) has no backing event/inbox model this release —
  flagged there as a UI-only decorative affordance, not built as a real-time feature here.
