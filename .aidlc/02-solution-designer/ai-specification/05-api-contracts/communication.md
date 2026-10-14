<!-- pipeline-provenance
produced_by: ai-spec-agent (contract-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch). **Written back from the P3 build
  (U15):** API-043 returns `{roomId, hubPath}` and membership is a row, not a token (Q-219);
  API-047 adds `groupCallId` (Q-220); API-044/045/046 gain the error and lifecycle rules CT-302/303
  resolved from silence; API-046's CT placement recorded. **Added (Q-240 / U39, closes U16):**
  `ERR-904` on API-043. The U15 items describe behaviour already built and tested — the code is
  the source of truth for them; the rate limit is new and not yet built.
-->

# 05 — API Contracts — `communication`

## Announcements

**API-037** `POST /api/v1/announcements` — Auth: leadership + Teacher. `{title, content,
effectiveDate}`. Success `201`.
**API-038** `PUT /api/v1/announcements/{id}` — Auth: leadership + Teacher (BR-037).
**API-039** `POST /api/v1/announcements/{id}/withdraw` — Auth: leadership **only** (BRL-038).
Errors: `ERR-501` (403).
**API-040** `GET /api/v1/announcements` · `GET /api/v1/announcements/{id}` — Auth: any
authenticated user of the institution (BR-036).

## Rooms

**API-041** `POST /api/v1/rooms` — Auth: any authenticated user. `{name, description?, passcode}`.
Success `201` (passcode never echoed back after this call).
**API-042** `GET /api/v1/rooms` — Auth: any authenticated user. Response never includes passcode
field (FR-043).
**API-043** `POST /api/v1/rooms/{id}/join` — `{passcode}`. Success `200` → **`{roomId, hubPath}`**.
Errors: `ERR-502`, **`ERR-904`** (429 — rate-limited per `09-security-spec.md` §10, `CFG-013`).

> **Response corrected at 1.0.4 (Q-219, U15).** This read "session token for the SignalR hub
> group". **There is no session token.** Membership is a **`RoomMembership` row** (§2.8a), because
> a token cannot be revoked when the room is removed and cannot survive a reconnect. The hub
> re-checks the row, so the client needs only the path. **Also corrected: the room's *creator* is
> its first member** (they set the passcode, so a separate join would verify nothing), joining
> **re-verifies the passcode even for an existing member**, and a disconnect changes presence only
> — it does not end membership, so auto-reconnect needs no passcode (NFR-012).
>
> **`ERR-904` added at 1.0.4 (Q-240, U39 — closes U16):** this endpoint accepted unlimited
> passcode guesses. CT-302 flagged it at build time and the contract had no limit to point at.

**API-044** `GET /api/v1/rooms/{id}/messages` — Auth: room member. History for on-join/reconnect
load (NFR-012). **A non-member gets `403 ERR-102`** (*written back 1.0.4*: `ERR-505`'s wording is
meeting-specific and the contract named no error). A withdrawn message is returned with **content
blanked, author and position kept** (BRL-044). **The room's creator reads history without
joining.**
**API-045** `DELETE /api/v1/rooms/{id}` — Auth: creator or leadership. Errors: `ERR-503`.
**Soft-delete** (D8 lists Room): memberships are removed, **message history is retained**
(BR-043), any live meeting is ended, and a removed room returns 404 for join and history.
**API-046** `PUT /api/v1/rooms/{id}/messages/{messageId}` (revise) · `DELETE .../{messageId}`
(withdraw, soft) — Auth: message author only. Errors: `ERR-504` (length), `ERR-102` (not author),
`ERR-901` (revising an already-withdrawn message).

> **Task placement recorded at 1.0.4 (U15):** API-046 was in **no `CT-`'s scope** in
> `15-task-breakdown.md` §4, yet its gate test TC-510 was CT-303's. It was built in **CT-303**,
> next to its test. `15-task-breakdown.md` now says so. **No hub event is defined for revise or
> withdraw** — other clients see those changes on their next history fetch, and none was invented.

## Meetings

**API-047** `POST /api/v1/rooms/{id}/meetings/token` — Auth: room member (BRL-046). Success
`200` → **`{acsToken, expiresAtUtc, groupCallId}`** — short-lived ACS Calling token; API never
relays media itself (Tech Design §6). Errors: `ERR-505`, `ERR-903` (503 when ACS is unconfigured —
no meeting is started).

> **`groupCallId` added at 1.0.4 (Q-220, U15).** Members need a shared call id to join the same
> call; `groupCallId = Meeting.Id`. A room now has **many** meetings with at most one live
> (§2.8), so this endpoint **reuses the room's live meeting or starts one**. The meeting ends on
> room removal or when the last member leaves. **The membership check runs before any ACS call**,
> so a non-member causes no identity to be minted, no token issued and no meeting started.
> Token lifetime is the ACS SDK default — the specs say "short-lived" and name no number.

## Real-time (SignalR hub, not REST — see `06-event-contracts.md` for the event catalogue)

`/hubs/collaboration` — group = room id, joined only after API-043 succeeds. All chat/presence/
typing/join-leave/media-state traffic flows here, never as REST polling.

## Idempotency

`POST /rooms` and `POST /announcements` accept `Idempotency-Key`. Message send (`EVT-001`) is a
SignalR call, not a REST POST — its own retry/de-duplication is a client-side concern (a client
that doesn't receive an ack re-sends with the same client-generated message id, which the server
treats as a no-op if that id was already persisted).
