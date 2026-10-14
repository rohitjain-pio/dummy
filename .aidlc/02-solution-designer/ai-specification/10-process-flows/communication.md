<!-- pipeline-provenance
produced_by: ai-spec-agent (process-flow-writer)
generated: 2026-09-11
-->

# 10 — Process Flows — `communication`

## PF-008 — Room creation, join, and messaging

**Trigger:** A user wants to collaborate with others in a dedicated space.
**Actors:** any authenticated user (creator + joiners).
**Pre-conditions:** authenticated session.
**Post-conditions:** room exists; joined members can exchange messages in real time.

**Steps:**
1. Creator submits name/description/passcode (API-041) — passcode hashed at rest.
2. Creator shares the passcode out-of-band (never shown in the room list).
3. A prospective participant browses rooms (API-042 — no passcode visible) and submits the
   passcode to join (API-043).
4. On success, client connects to `/hubs/collaboration`, joins the room's SignalR group.
5. Client loads message history (API-044) and renders it.
6. Participant sends messages (EVT-001), sees presence/typing/join-leave (EVT-002/003/004).

**Exception flows:**
- Wrong passcode at step 3 → `ERR-502`, no group join, no history exposed.
- Disconnection mid-session → SignalR auto-reconnect (NFR-012); on reconnect, client re-fetches
  history via API-044 rather than trusting a hub-replayed backlog (EVT-006 signals only "re-fetch
  now").

**Exercised by:** `TC-501`–`TC-506`.

## PF-009 — Live meeting join

**Trigger:** A room participant starts or joins a meeting tied to their room.
**Actors:** room members.
**Pre-conditions:** caller is a current member of the room (BRL-046).
**Post-conditions:** participant is in an active ACS Calling session with mic/camera controls.

**Steps:**
1. Participant requests a meeting token (API-047).
2. System issues a short-lived ACS token scoped to this meeting/participant — API never relays
   media itself (Tech Design §6).
3. Browser SDK establishes the media session directly with ACS.
4. Participant toggles mic/camera (EVT-005 broadcasts the state to other participants).
5. Participant leaves (EVT-004 `left`); other participants' tiles update.

**Exception flows:**
- Caller not a room member → `ERR-505`, no token issued.
- Camera/mic permission denied by the browser → inline "how to enable" message on that
  participant's own tile, never a silent failure (`ui-design-final.md` §9.3,
  Tech Design §13.1 risk 4).
- Media session drops → "reconnecting" state shown per tile, not vanished (NFR-012, Tech Design
  §13.1 risk 5 — **not yet confirmed** for the ACS media session itself; build defensively).

**Exercised by:** `TC-507`–`TC-510` (including the permission-denied and reconnect states as
required designed behaviour, not merely happy-path join).
